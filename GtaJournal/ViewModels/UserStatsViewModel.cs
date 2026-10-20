using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GtaJournal.Core.Navigation;
using GtaJournal.Models;
using GtaJournal.Services;

namespace GtaJournal.ViewModels;

public sealed partial class UserStatsViewModel(ViewModelContext context) : ViewModelBase(context)
{
    private bool _suppressReload;

    public ObservableCollection<UserOption> Users { get; } = [];
    public IReadOnlyList<MonthOption> Months { get; } = MonthOption.All();

    [ObservableProperty] public partial UserOption? SelectedUser { get; set; }
    [ObservableProperty] public partial MonthOption? SelectedMonth { get; set; }
    [ObservableProperty] public partial CalendarData? Calendar { get; set; }
    [ObservableProperty] public partial CalendarSummary? Summary { get; set; }
    [ObservableProperty] public partial string? Bank { get; set; }
    [ObservableProperty] public partial string? FirstHalf { get; set; }
    [ObservableProperty] public partial string? SecondHalf { get; set; }
    [ObservableProperty] public partial bool IsLoaded { get; set; }

    public bool HasUser => SelectedUser is not null && Calendar is not null;
    public bool ShowPlaceholder => IsLoaded && !HasUser;
    public string UserName => SelectedUser?.Name ?? "";
    public string UserInitials => UserName.Length > 0 ? UserName[..1] : "?";
    public string MonthTitle => Calendar?.Month.Title ?? "";

    public override async Task OnNavigatedToAsync(object? parameter)
    {
        _suppressReload = true;
        SelectedMonth = Months[0];
        _suppressReload = false;
        await LoadAsync(parameter as long?);

        // Открыли из меню без пользователя — показываем свой онлайн.
        if (SelectedUser is null && Users.FirstOrDefault(u => u.Name == Accounts.Profile?.Username) is { } self)
            await LoadAsync(self.Id);
    }

    partial void OnSelectedUserChanged(UserOption? value)
    {
        NotifyState();
        if (!_suppressReload && value is not null) _ = LoadAsync(value.Id);
    }

    partial void OnSelectedMonthChanged(MonthOption? value)
    {
        if (!_suppressReload && SelectedUser is not null) _ = LoadAsync(SelectedUser.Id);
    }

    partial void OnCalendarChanged(CalendarData? value) => NotifyState();
    partial void OnIsLoadedChanged(bool value) => NotifyState();

    [RelayCommand]
    private Task RefreshAsync() => LoadAsync(SelectedUser?.Id);

    [RelayCommand]
    private void EditUser()
    {
        if (SelectedUser is not null) Navigation.Navigate(Routes.EditUser, SelectedUser.Id);
    }

    private async Task LoadAsync(long? userId)
    {
        var month = SelectedMonth is null || SelectedMonth == Months[0] ? null : SelectedMonth;
        await QueryAsync(QueryKeys.UserStatistics(userId, month),c => c.GetUserStatisticsAsync(userId, month),
            data => Apply(data, userId), "Не удалось загрузить онлайн пользователя");
        IsLoaded = true;
    }

    private void Apply(UserStatisticsData data, long? userId)
    {
        _suppressReload = true;
        try
        {
            if (!Users.SequenceEqual(data.Users))
            {
                Users.Clear();
                foreach (var u in data.Users) Users.Add(u);
            }
            SelectedUser = Users.FirstOrDefault(u => u.Id == (data.SelectedUserId ?? userId));
        }
        finally
        {
            _suppressReload = false;
        }

        Bank = data.Bank;
        FirstHalf = data.FirstHalf;
        SecondHalf = data.SecondHalf;
        Calendar = data.Calendar;
        Summary = data.Calendar?.Summary;
        IsLoaded = true;
    }

    private void NotifyState()
    {
        OnPropertyChanged(nameof(HasUser));
        OnPropertyChanged(nameof(ShowPlaceholder));
        OnPropertyChanged(nameof(UserName));
        OnPropertyChanged(nameof(UserInitials));
        OnPropertyChanged(nameof(MonthTitle));
    }
}
