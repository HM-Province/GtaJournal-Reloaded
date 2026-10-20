using CommunityToolkit.Mvvm.ComponentModel;
using GtaJournal.Core.Navigation;
using GtaJournal.Services;
using GtaJournal.Services.Journal;

namespace GtaJournal.ViewModels;

/// <summary>Общие зависимости ViewModel'ей — чтобы не раздувать конструкторы.</summary>
public sealed record ViewModelContext(
    AccountService Accounts,
    NavigationService Navigation,
    MessageService Messages,
    DialogService Dialogs,
    SettingsService Settings,
    TagService Tags,
    AvatarService Avatars,
    UpdateService Updates,
    QueryCache Cache);

public abstract partial class ViewModelBase(ViewModelContext context) : ObservableObject, INavigationAware
{
    private int _queryVersion;
    private string? _shownQuery;

    protected ViewModelContext Context { get; } = context;
    protected QueryCache Cache => Context.Cache;
    protected AccountService Accounts => Context.Accounts;
    protected NavigationService Navigation => Context.Navigation;
    protected MessageService Messages => Context.Messages;
    protected DialogService Dialogs => Context.Dialogs;

    /// <summary>Клиент активного аккаунта (гарды маршрутов гарантируют его наличие).</summary>
    protected JournalClient Client => Accounts.Client ?? throw new SessionExpiredException();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    public partial bool IsBusy { get; set; }

    public bool IsNotBusy => !IsBusy;

    public virtual Task OnNavigatedToAsync(object? parameter) => Task.CompletedTask;

    public virtual void OnNavigatedFrom() { }

    /// <summary>
    /// Выполняет действие с индикатором загрузки и единообразной обработкой ошибок:
    /// истёкшая сессия → экран входа, сетевые ошибки → сообщение.
    /// </summary>
    protected async Task<bool> RunAsync(Func<Task> action, string errorTitle = "Ошибка", bool showBusy = true)
    {
        if (showBusy) IsBusy = true;
        try
        {
            await action();
            return true;
        }
        catch (SessionExpiredException)
        {
            Accounts.NotifySessionExpired();
        }
        catch (JournalException ex)
        {
            Messages.Error(errorTitle, ex.Message);
        }
        catch (Exception ex)
        {
            Messages.Error(errorTitle, ex.Message);
        }
        finally
        {
            if (showBusy) IsBusy = false;
        }
        return false;
    }

    /// <summary>
    /// Загрузка данных страницы через <see cref="QueryCache"/>: если по ключу уже есть данные, а на экране
    /// сейчас другие — сразу показывает кэш, затем в фоне берёт свежие с сайта и показывает их.
    /// Ответ устаревшего запроса (пока грузили, запросили другой ключ) отбрасывается.
    /// </summary>
    /// <param name="apply">Показывает данные — и из кэша, и свежие.</param>
    /// <param name="onFresh">Дополнительно только для свежих данных (побочные эффекты).</param>
    protected async Task<bool> QueryAsync<T>(string key, Func<JournalClient, Task<T>> fetch, Action<T> apply,
        string errorTitle, Func<T, Task>? onFresh = null)
    {
        var version = ++_queryVersion;
        if (key != _shownQuery && Cache.TryGet<T>(key, out var cached))
        {
            apply(cached);
            _shownQuery = key;
        }

        return await RunAsync(async () =>
        {
            var data = await Cache.FetchAsync(key, fetch);
            if (version != _queryVersion) return;
            apply(data);
            _shownQuery = key;
            if (onFresh is not null) await onFresh(data);
        }, errorTitle);
    }
}
