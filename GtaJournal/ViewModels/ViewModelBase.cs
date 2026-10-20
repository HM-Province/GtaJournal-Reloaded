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
    AvatarService Avatars);

public abstract partial class ViewModelBase(ViewModelContext context) : ObservableObject, INavigationAware
{
    protected ViewModelContext Context { get; } = context;
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
}
