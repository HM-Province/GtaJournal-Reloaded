using CommunityToolkit.Mvvm.ComponentModel;
using GtaJournal.Data.Repositories;
using GtaJournal.Models;
using GtaJournal.Services.Http;
using GtaJournal.Services.Journal;
using GtaJournal.Services.Security;

namespace GtaJournal.Services;

/// <summary>
/// Аккаунты и активная сессия. В каждый момент работает один аккаунт; остальные хранят свои cookie
/// и User-Agent и активируются переключением.
/// </summary>
public sealed partial class AccountService : ObservableObject
{
    private readonly AccountRepository _repository;
    private readonly SettingsService _settings;
    private List<Account> _accounts = [];

    public AccountService(AccountRepository repository, SettingsService settings)
    {
        _repository = repository;
        _settings = settings;
        _settings.Changed += OnSettingsChanged;
    }

    public IReadOnlyList<Account> Accounts => _accounts;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActiveAccount))]
    public partial Account? ActiveAccount { get; private set; }

    public JournalClient? Client { get; private set; }

    /// <summary>Профиль активного аккаунта из последней загрузки дашборда.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAdmin))]
    public partial JournalProfile? Profile { get; private set; }

    public bool IsAdmin => Profile?.IsAdmin == true;
    public bool HasActiveAccount => ActiveAccount is not null;

    public event Action? AccountsChanged;

    /// <summary>Сессия активного аккаунта больше не действует.</summary>
    public event Action<Account>? SessionExpired;

    public async Task InitializeAsync()
    {
        _accounts = await _repository.GetAllAsync();
        var active = _accounts.FirstOrDefault(a => a.Id == _settings.Current.ActiveAccountId) ?? _accounts.FirstOrDefault();
        SetActive(active);
        AccountsChanged?.Invoke();
    }

    public async Task ActivateAsync(long accountId)
    {
        if (ActiveAccount?.Id == accountId) return;
        var account = _accounts.FirstOrDefault(a => a.Id == accountId) ?? throw new InvalidOperationException("Аккаунт не найден");
        SetActive(account);
        await _settings.UpdateAsync(s => s with { ActiveAccountId = accountId });
    }

    /// <summary>Клиент для входа: новая сессия (без cookie), но с User-Agent аккаунта.</summary>
    public JournalClient CreateLoginClient(Account account) =>
        new(_settings.Current.BaseUri, account.UserAgent, account.ClientHints, cookiesJson: null);

    /// <summary>Заготовка нового аккаунта с уникальным браузерным отпечатком.</summary>
    public Account FindOrCreateDraft(string login)
    {
        var existing = _accounts.FirstOrDefault(a => string.Equals(a.Login, login, StringComparison.OrdinalIgnoreCase));
        if (existing is not null) return existing;

        var identity = UserAgentGenerator.Generate();
        return new Account
        {
            Login = login,
            UserAgent = identity.UserAgent,
            ClientHints = identity.ClientHints,
            CreatedAt = DateTimeOffset.Now,
        };
    }

    /// <summary>Сохраняет аккаунт после успешного входа и делает его активным.</summary>
    public async Task CompleteLoginAsync(Account account, JournalClient loginClient, string? totpSecretToStore, bool forgetSecret)
    {
        account.CookiesJson = CookieStore.Save(GetCookies(loginClient));
        account.LastLoginAt = DateTimeOffset.Now;
        if (totpSecretToStore is not null)
            account.ProtectedTotpSecret = SecretProtector.Protect(Totp.NormalizeSecret(totpSecretToStore));
        else if (forgetSecret)
            account.ProtectedTotpSecret = null;

        if (account.Id == 0)
        {
            await _repository.InsertAsync(account);
            _accounts.Add(account);
        }
        else
        {
            await _repository.UpdateAsync(account);
        }

        loginClient.Dispose();
        SetActive(account, force: true);
        await _settings.UpdateAsync(s => s with { ActiveAccountId = account.Id });
        AccountsChanged?.Invoke();
    }

    /// <summary>Обновляет профиль и кэш имени/аватарки аккаунта.</summary>
    public async Task SetProfileAsync(JournalProfile profile)
    {
        Profile = profile;
        if (ActiveAccount is { } account && (account.DisplayName != profile.Username || account.AvatarUrl != profile.AvatarUrl))
        {
            account.DisplayName = profile.Username;
            account.AvatarUrl = profile.AvatarUrl;
            await _repository.UpdateAsync(account);
            AccountsChanged?.Invoke();
        }
    }

    public void SetProfileStatus(JournalStatus status)
    {
        if (Profile is { } p && p.Status != status)
            Profile = p with { Status = status };
    }

    public async Task LogoutAsync()
    {
        if (ActiveAccount is not { } account || Client is null) return;
        await Client.LogoutAsync();
        account.CookiesJson = null;
        await _repository.UpdateCookiesAsync(account.Id, null);
        Profile = null;
        SessionExpired?.Invoke(account);
    }

    public async Task RemoveAsync(long accountId)
    {
        var account = _accounts.FirstOrDefault(a => a.Id == accountId);
        if (account is null) return;
        await _repository.DeleteAsync(accountId);
        _accounts.Remove(account);
        if (ActiveAccount?.Id == accountId)
        {
            var next = _accounts.FirstOrDefault();
            SetActive(next);
            await _settings.UpdateAsync(s => s with { ActiveAccountId = next?.Id });
        }
        AccountsChanged?.Invoke();
    }

    public async Task SetTotpSecretAsync(long accountId, string? secret)
    {
        var account = _accounts.First(a => a.Id == accountId);
        account.ProtectedTotpSecret = secret is null ? null : SecretProtector.Protect(Totp.NormalizeSecret(secret));
        await _repository.UpdateAsync(account);
        AccountsChanged?.Invoke();
    }

    public static string? GetTotpSecret(Account account) => SecretProtector.TryUnprotect(account.ProtectedTotpSecret);

    /// <summary>Вызывается при любом SessionExpiredException.</summary>
    public void NotifySessionExpired()
    {
        if (ActiveAccount is not { } account) return;
        Profile = null;
        SessionExpired?.Invoke(account);
    }

    private void SetActive(Account? account, bool force = false)
    {
        if (!force && ReferenceEquals(account, ActiveAccount) && Client is not null) return;
        Client?.Dispose();
        Client = account is null ? null : CreateSessionClient(account);
        Profile = null;
        // Принудительно уведомляем, даже если объект тот же: клиент пересоздан.
        ActiveAccount = null;
        ActiveAccount = account;
    }

    private JournalClient CreateSessionClient(Account account) =>
        new(_settings.Current.BaseUri, account.UserAgent, account.ClientHints, account.CookiesJson, async json =>
        {
            account.CookiesJson = json;
            await _repository.UpdateCookiesAsync(account.Id, json);
        });

    private void OnSettingsChanged(AppSettings old, AppSettings now)
    {
        if (old.BaseUrl != now.BaseUrl && ActiveAccount is { } account)
            SetActive(account, force: true);
    }

    private static System.Net.CookieContainer GetCookies(JournalClient client) => client.Cookies;
}
