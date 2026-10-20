using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using GtaJournal.Core.Navigation;
using GtaJournal.Models;
using GtaJournal.Services;
using GtaJournal.Services.Security;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace GtaJournal.ViewModels;

public sealed partial class AccountItemViewModel(Account account, bool isActive, AccountsViewModel owner)
{
    public Account Model { get; } = account;
    public string Title => Model.Title;
    public string Login => Model.Login;

    public bool IsActive { get; } = isActive;
    public bool HasSession => !string.IsNullOrEmpty(Model.CookiesJson) && Model.CookiesJson != "[]";
    public bool HasTotpSecret => Model.HasTotpSecret;
    public string SessionText => HasSession ? "Вход выполнен" : "Нужен вход";
    public string TotpText => HasTotpSecret ? "Коды 2FA автоматически" : "Коды 2FA вручную";
    public string LastLoginText => Model.LastLoginAt is { } d ? $"Последний вход: {d.LocalDateTime:dd.MM.yyyy HH:mm}" : "Ещё не входили";

    [RelayCommand] private Task Activate() => owner.ActivateAsync(this);
    [RelayCommand] private void Relogin() => owner.Relogin(this);
    [RelayCommand] private Task SetSecret() => owner.SetSecretAsync(this);
    [RelayCommand] private Task ForgetSecret() => owner.ForgetSecretAsync(this);
    [RelayCommand] private Task Remove() => owner.RemoveAsync(this);
}

public sealed partial class AccountsViewModel(ViewModelContext context) : ViewModelBase(context)
{
    public ObservableCollection<AccountItemViewModel> Items { get; } = [];

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty] public partial bool IsEmpty { get; set; }

    public override Task OnNavigatedToAsync(object? parameter)
    {
        Accounts.AccountsChanged += Rebuild;
        Rebuild();
        return Task.CompletedTask;
    }

    public override void OnNavigatedFrom() => Accounts.AccountsChanged -= Rebuild;

    private void Rebuild()
    {
        Items.Clear();
        foreach (var account in Accounts.Accounts)
            Items.Add(new AccountItemViewModel(account, account.Id == Accounts.ActiveAccount?.Id, this));
        IsEmpty = Items.Count == 0;
    }

    [RelayCommand]
    private void Add() => Navigation.Navigate(Routes.Login, new LoginRequest());

    internal async Task ActivateAsync(AccountItemViewModel item)
    {
        await RunAsync(() => Accounts.ActivateAsync(item.Model.Id));
        Rebuild();
        Navigation.Navigate(item.HasSession ? Routes.Dashboard : Routes.Login, item.HasSession ? null : new LoginRequest(item.Model.Id));
    }

    internal void Relogin(AccountItemViewModel item) => Navigation.Navigate(Routes.Login, new LoginRequest(item.Model.Id));

    internal async Task SetSecretAsync(AccountItemViewModel item)
    {
        var secret = await Dialogs.PromptAsync("Автоматические коды 2FA",
            "Секрет из приложения-аутентификатора",
            "K3UT YJCQ 3KUH …", "Сохранить");
        if (secret is null) return;
        if (!Totp.IsValidSecret(secret))
        {
            Messages.Error("Некорректный секрет");
            return;
        }
        await RunAsync(() => Accounts.SetTotpSecretAsync(item.Model.Id, secret));
        Messages.Success("Секрет сохранён");
    }

    internal async Task ForgetSecretAsync(AccountItemViewModel item)
    {
        if (!await Dialogs.ConfirmAsync("Отключить автоматические коды",
                "При входе нужно будет вводить код вручную.", "Отключить", destructive: true))
            return;
        await RunAsync(() => Accounts.SetTotpSecretAsync(item.Model.Id, null));
    }

    internal async Task RemoveAsync(AccountItemViewModel item)
    {
        if (!await Dialogs.ConfirmAsync("Удалить аккаунт",
                $"Удалить «{item.Title}» из списка?", "Удалить", destructive: true))
            return;
        await RunAsync(() => Accounts.RemoveAsync(item.Model.Id));
        if (Accounts.ActiveAccount is null)
            Navigation.Navigate(Routes.Login, new LoginRequest());
    }
}
