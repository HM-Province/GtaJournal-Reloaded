using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GtaJournal.Core.Navigation;
using GtaJournal.Models;

namespace GtaJournal.ViewModels;

public sealed partial class EditUserViewModel(ViewModelContext context) : ViewModelBase(context)
{
    private long _userId;

    public IReadOnlyList<string> StatusOptions { get; } = ["Оффлайн", "Онлайн", "АФК"];
    private static readonly JournalStatus[] StatusValues = [JournalStatus.Offline, JournalStatus.Online, JournalStatus.Afk];

    [ObservableProperty] public partial bool IsLoaded { get; set; }

    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial string Nickname { get; set; } = "";

    [ObservableProperty] public partial string Password { get; set; } = "";

    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial string Bank { get; set; } = "";

    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial double Rank { get; set; } = 1;

    [ObservableProperty] public partial int StatusIndex { get; set; }
    [ObservableProperty] public partial bool IsAdmin { get; set; }
    [ObservableProperty] public partial bool UpdateAvatar { get; set; }

    public const int MinRank = 1, MaxRank = 100;
    public int MinRankValue => MinRank;
    public int MaxRankValue => MaxRank;

    public bool UseTags => Context.Tags.Enabled;
    public string NicknamePlaceholder => UseTags ? "[ТЕГ] Ivan_Ivanov" : "Ivan_Ivanov";

    public override async Task OnNavigatedToAsync(object? parameter)
    {
        if (parameter is not long id)
        {
            Navigation.Navigate(Routes.Dashboard, clearHistory: true);
            return;
        }
        _userId = id;

        await RunAsync(async () =>
        {
            var user = await Client.GetUserAsync(id);
            if (user is null)
            {
                Messages.Warning("Нет доступа", "Изменять пользователей могут только администраторы");
                Navigation.Navigate(Routes.Dashboard, clearHistory: true);
                return;
            }

            Nickname = user.Login;
            Bank = user.Bank;
            Rank = double.TryParse(user.Role, out var r) ? r : 1;
            IsAdmin = user.IsAdmin;
            StatusIndex = Array.IndexOf(StatusValues, user.Status);
            IsLoaded = true;
            SaveCommand.NotifyCanExecuteChanged();
        }, "Не удалось загрузить пользователя");
    }

    private bool CanSave() =>
        IsLoaded
        && Models.Nickname.IsValid(Nickname, requireTag: UseTags)
        && !double.IsNaN(Rank) && Rank is >= MinRank and <= MaxRank
        && Bank.Trim().Length > 0;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        var user = new EditableUser(Nickname.Trim(), Bank.Trim(), ((int)Rank).ToString(), IsAdmin,
            StatusValues[Math.Clamp(StatusIndex, 0, StatusValues.Length - 1)]);

        await RunAsync(async () =>
        {
            var result = await Client.UpdateUserAsync(_userId, user, string.IsNullOrEmpty(Password) ? null : Password, UpdateAvatar);
            if (result.IsSuccess)
            {
                Messages.Success("Изменения сохранены", user.Login);
                Navigation.Navigate(Routes.Dashboard, clearHistory: true);
            }
            else
            {
                Messages.Error("Ошибка", result.Text ?? "Неизвестная ошибка");
            }
        }, "Не удалось сохранить пользователя");
    }
}
