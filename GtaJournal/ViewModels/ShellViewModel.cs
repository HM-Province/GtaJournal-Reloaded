using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GtaJournal.Core.Navigation;
using GtaJournal.Helpers;
using GtaJournal.Models;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace GtaJournal.ViewModels;

public sealed partial class ShellViewModel : ViewModelBase
{
    public ShellViewModel(ViewModelContext context) : base(context)
    {
        Accounts.PropertyChanged += OnAccountsPropertyChanged;
    }

    public string AccountTitle => Accounts.Profile?.Username ?? Accounts.ActiveAccount?.Title ?? "Нет аккаунта";
    public string AccountSubtitle => Accounts.Profile?.Status.Title() ?? (Accounts.ActiveAccount is null ? "Добавьте аккаунт" : "Не загружен");
    [ObservableProperty] public partial ImageSource? Avatar { get; set; }
    private string? _avatarUrl;

    private async Task LoadAvatarAsync()
    {
        var url = Accounts.Profile?.AvatarUrl;
        if (url == _avatarUrl) return;
        _avatarUrl = url;
        Avatar = await Context.Avatars.LoadAsync(url);
    }
    public Brush StatusBrush => StatusVisuals.Brush(Accounts.Profile?.Status ?? JournalStatus.Offline);
    public bool IsAdmin => Accounts.IsAdmin;

    public event Action? AdminChanged;

    private void OnAccountsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(AccountTitle));
        OnPropertyChanged(nameof(AccountSubtitle));
        _ = LoadAvatarAsync();
        OnPropertyChanged(nameof(StatusBrush));
        if (e.PropertyName is nameof(Accounts.Profile) or nameof(Accounts.IsAdmin))
        {
            OnPropertyChanged(nameof(IsAdmin));
            AdminChanged?.Invoke();
        }
    }

    public IReadOnlyList<Models.Account> AllAccounts => Accounts.Accounts;
    public long? ActiveAccountId => Accounts.ActiveAccount?.Id;

    public async Task SwitchAccountAsync(long id)
    {
        await RunAsync(() => Accounts.ActivateAsync(id), "Не удалось переключить аккаунт");
        Navigation.Navigate(Routes.Dashboard, clearHistory: true);
    }

    [RelayCommand]
    private void AddAccount() => Navigation.Navigate(Routes.Login, new LoginRequest());

    [RelayCommand]
    private void ManageAccounts() => Navigation.Navigate(Routes.Accounts);

    [RelayCommand]
    private async Task LogoutAsync()
    {
        if (!await Dialogs.ConfirmAsync("Выход", $"Выйти из аккаунта «{AccountTitle}»? Аккаунт останется в списке.", "Выйти"))
            return;
        await RunAsync(Accounts.LogoutAsync, "Не удалось выйти");
    }

    public void Detach() => Accounts.PropertyChanged -= OnAccountsPropertyChanged;
}
