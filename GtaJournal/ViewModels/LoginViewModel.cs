using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GtaJournal.Core.Navigation;
using GtaJournal.Models;
using GtaJournal.Services;
using GtaJournal.Services.Journal;
using GtaJournal.Services.Security;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using QRCoder;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Storage.Streams;

namespace GtaJournal.ViewModels;

/// <summary>Параметр маршрута входа: какой аккаунт перелогинить и почему.</summary>
public sealed record LoginRequest(long? AccountId = null, string? Reason = null);

public enum LoginStep { Credentials, Totp, Setup }

public sealed partial class LoginViewModel(ViewModelContext context) : ViewModelBase(context)
{
    private Account? _draft;
    private JournalClient? _client;
    private string? _pending;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCredentialsStep), nameof(IsTotpStep), nameof(IsSetupStep), nameof(SubmitText), nameof(ShowCodeInput))]
    public partial LoginStep Step { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SubmitCommand))]
    public partial string Login { get; set; } = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SubmitCommand))]
    public partial string Password { get; set; } = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SubmitCommand))]
    public partial string Code { get; set; } = "";

    [ObservableProperty] public partial string? ErrorText { get; set; }
    [ObservableProperty] public partial string? InfoText { get; set; }
    [ObservableProperty] public partial bool IsReLogin { get; set; }
    [ObservableProperty] public partial bool CanCancel { get; set; }

    // ---- первичная настройка 2FA ----
    [ObservableProperty] public partial ImageSource? QrImage { get; set; }
    [ObservableProperty] public partial string? SetupSecret { get; set; }

    /// <summary>Выбор пользователя: хранить секрет в приложении (удобство) или нет (безопасность).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowCodeInput))]
    [NotifyCanExecuteChangedFor(nameof(SubmitCommand))]
    public partial bool StoreSecret { get; set; } = true;

    // ---- уже настроенная 2FA: можно сохранить секрет для автоподстановки ----
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SubmitCommand))]
    public partial string ManualSecret { get; set; } = "";

    public bool IsCredentialsStep => Step == LoginStep.Credentials;
    public bool IsTotpStep => Step == LoginStep.Totp;
    public bool IsSetupStep => Step == LoginStep.Setup;
    public bool ShowCodeInput => Step == LoginStep.Totp || (Step == LoginStep.Setup && !StoreSecret);
    public string SubmitText => Step == LoginStep.Credentials ? "Войти" : "Подтвердить";

    public override Task OnNavigatedToAsync(object? parameter)
    {
        var request = parameter as LoginRequest ?? new LoginRequest(Accounts.ActiveAccount?.Id);
        var account = request.AccountId is { } id ? Accounts.Accounts.FirstOrDefault(a => a.Id == id) : null;

        IsReLogin = account is not null;
        Login = account?.Login ?? "";
        InfoText = request.Reason;
        CanCancel = Accounts.Accounts.Count > 0 && Accounts.Client?.HasSession == true && request.Reason is null;
        return Task.CompletedTask;
    }

    public override void OnNavigatedFrom()
    {
        _client?.Dispose();
        _client = null;
    }

    private bool CanSubmit() => Step switch
    {
        LoginStep.Credentials => !string.IsNullOrWhiteSpace(Login) && Password.Length > 0,
        LoginStep.Totp => Code.Trim().Length >= 6 || IsManualSecretValid,
        LoginStep.Setup => StoreSecret || Code.Trim().Length >= 6,
        _ => false,
    };

    private bool IsManualSecretValid => !string.IsNullOrWhiteSpace(ManualSecret) && Totp.IsValidSecret(ManualSecret);

    [RelayCommand(CanExecute = nameof(CanSubmit))]
    private async Task SubmitAsync()
    {
        ErrorText = null;
        await RunAsync(Step switch
        {
            LoginStep.Credentials => SubmitCredentialsAsync,
            LoginStep.Totp => SubmitTotpAsync,
            _ => SubmitSetupAsync,
        }, "Ошибка входа");
    }

    private async Task SubmitCredentialsAsync()
    {
        _client?.Dispose();
        _draft = Accounts.FindOrCreateDraft(Login.Trim());
        _client = Accounts.CreateLoginClient(_draft);

        var result = await _client.LoginAsync(Login.Trim(), Password);
        switch (result.Outcome)
        {
            case LoginOutcome.Success:
                await CompleteAsync(secretToStore: null, forgetSecret: false);
                break;

            case LoginOutcome.TotpRequired:
                _pending = result.Pending;
                // Секрет сохранён — подставляем код сами.
                if (AccountService.GetTotpSecret(_draft) is { } secret)
                {
                    if (await _client.LoginTotpAsync(_pending!, Totp.Generate(secret)))
                    {
                        await CompleteAsync(secretToStore: null, forgetSecret: false);
                        return;
                    }
                    ErrorText = "Сохранённый секрет не подошёл — введите код из приложения";
                }
                Step = LoginStep.Totp;
                InfoText = "Введите код из приложения-аутентификатора";
                break;

            case LoginOutcome.TotpSetupRequired:
                _pending = result.Pending;
                await LoadSetupAsync();
                break;

            default:
                ErrorText = result.Message ?? "Произошла ошибка";
                break;
        }
    }

    private async Task LoadSetupAsync()
    {
        var uri = await _client!.TotpSetupAsync(_pending!);
        var parameters = TotpParameters.TryParseUri(uri);
        if (uri is null || parameters is null)
        {
            ErrorText = "Не удалось получить QR для настройки 2FA. Попробуйте войти ещё раз.";
            return;
        }

        SetupSecret = parameters.Secret;
        QrImage = await RenderQrAsync(uri);
        InfoText = "Для аккаунта требуется настроить двухфакторную аутентификацию";
        Step = LoginStep.Setup;
    }

    private async Task SubmitTotpAsync()
    {
        var code = Code.Trim();
        if (code.Length == 0 && IsManualSecretValid)
            code = Totp.Generate(ManualSecret);

        if (!await _client!.LoginTotpAsync(_pending!, code))
        {
            ErrorText = "Неверный или просроченный код";
            return;
        }
        await CompleteAsync(IsManualSecretValid ? ManualSecret : null, forgetSecret: false);
    }

    private async Task SubmitSetupAsync()
    {
        var code = StoreSecret ? Totp.Generate(SetupSecret!) : Code.Trim();
        if (!await _client!.LoginTotpAsync(_pending!, code))
        {
            ErrorText = "Неверный или просроченный код";
            return;
        }
        await CompleteAsync(StoreSecret ? SetupSecret : null, forgetSecret: !StoreSecret);
    }

    private async Task CompleteAsync(string? secretToStore, bool forgetSecret)
    {
        var client = _client!;
        _client = null;
        await Accounts.CompleteLoginAsync(_draft!, client, secretToStore, forgetSecret);
        Password = "";
        Messages.Success("Вход выполнен", _draft!.Login);
        Navigation.Navigate(Routes.Dashboard, clearHistory: true);
    }

    [RelayCommand]
    private void Restart()
    {
        _client?.Dispose();
        _client = null;
        _pending = null;
        Code = "";
        ManualSecret = "";
        QrImage = null;
        SetupSecret = null;
        ErrorText = null;
        InfoText = null;
        Step = LoginStep.Credentials;
    }

    [RelayCommand]
    private void CopySecret()
    {
        if (SetupSecret is null) return;
        DialogService.CopyToClipboard(SetupSecret);
        Messages.Info("Секрет скопирован");
    }

    [RelayCommand]
    private void Cancel() => Navigation.Navigate(Routes.Dashboard, clearHistory: true);

    [RelayCommand]
    private void OpenSettings() => Navigation.Navigate(Routes.Settings);

    private static async Task<ImageSource> RenderQrAsync(string text)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.M);
        var png = new PngByteQRCode(data).GetGraphic(8);

        using var stream = new InMemoryRandomAccessStream();
        await stream.WriteAsync(png.AsBuffer());
        stream.Seek(0);
        var bitmap = new BitmapImage();
        await bitmap.SetSourceAsync(stream);
        return bitmap;
    }
}
