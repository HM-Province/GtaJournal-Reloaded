using System.Security.Cryptography;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GtaJournal.Core.Navigation;
using GtaJournal.Models;

namespace GtaJournal.ViewModels;

public sealed partial class AddUserViewModel(ViewModelContext context) : ViewModelBase(context)
{
    private const string PasswordAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789@#$%^&*()_+-=";

    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(AddCommand), nameof(AddAndContinueCommand))]
    public partial string Nickname { get; set; } = "";

    [ObservableProperty] public partial string Password { get; set; } = "";

    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(AddCommand), nameof(AddAndContinueCommand))]
    public partial string Bank { get; set; } = "";

    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(AddCommand), nameof(AddAndContinueCommand))]
    public partial string VkLink { get; set; } = "";

    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(AddCommand), nameof(AddAndContinueCommand))]
    public partial double Rank { get; set; } = 1;

    public const int MinRank = 1, MaxRank = 100;
    public int MinRankValue => MinRank;
    public int MaxRankValue => MaxRank;

    public bool UseTags => Context.Tags.Enabled;
    public string NicknamePlaceholder => UseTags ? "[ТЕГ] Ivan_Ivanov" : "Ivan_Ivanov";
    public string NicknameDescription => UseTags ? "Тег подразделения и Имя_Фамилия латиницей" : "Имя_Фамилия латиницей";
    public string RankDescription => $"От {MinRank} до {MaxRank}";

    public override async Task OnNavigatedToAsync(object? parameter)
    {
        await RunAsync(async () =>
        {
            if (!await Client.CanAddUsersAsync())
            {
                Messages.Warning("Нет доступа", "Добавлять пользователей могут только администраторы");
                Navigation.Navigate(Routes.Dashboard, clearHistory: true);
            }
        });
    }

    private bool CanAdd() =>
        Models.Nickname.IsValid(Nickname, requireTag: UseTags)
        && !double.IsNaN(Rank) && Rank is >= MinRank and <= MaxRank
        && Bank.Trim().Length > 0
        && VkLinkRegex().IsMatch(VkLink.Trim());

    [RelayCommand(CanExecute = nameof(CanAdd))]
    private Task AddAsync() => CreateAsync(stayOnPage: false);

    [RelayCommand(CanExecute = nameof(CanAdd))]
    private Task AddAndContinueAsync() => CreateAsync(stayOnPage: true);

    private async Task CreateAsync(bool stayOnPage)
    {
        var user = new NewUser(
            Nickname.Trim(),
            string.IsNullOrEmpty(Password) ? RandomPassword(10) : Password,
            Bank.Trim(),
            VkLink.Trim(),
            ((int)Rank).ToString());

        var created = await RunAsync(async () =>
        {
            var result = await Client.AddUserAsync(user);
            if (!result.IsSuccess)
                throw new InvalidOperationException(result.Text ?? "Неизвестная ошибка");
        }, "Не удалось добавить пользователя");
        if (!created) return;

        await Dialogs.ShowUserCreatedAsync(user);

        if (stayOnPage)
        {
            Nickname = Password = Bank = VkLink = "";
            Rank = 1;
        }
        else
        {
            Navigation.Navigate(Routes.Dashboard, clearHistory: true);
        }
    }

    [RelayCommand]
    private void GeneratePassword() => Password = RandomPassword(10);

    [RelayCommand]
    private void Cancel()
    {
        if (Navigation.CanGoBack) Navigation.GoBack();
        else Navigation.Navigate(Routes.Dashboard, clearHistory: true);
    }

    private static string RandomPassword(int length) =>
        new(Enumerable.Range(0, length).Select(_ => PasswordAlphabet[RandomNumberGenerator.GetInt32(PasswordAlphabet.Length)]).ToArray());

    [GeneratedRegex(@"^https://(m\.)?vk\.(com|ru)/.+", RegexOptions.IgnoreCase)]
    private static partial Regex VkLinkRegex();
}
