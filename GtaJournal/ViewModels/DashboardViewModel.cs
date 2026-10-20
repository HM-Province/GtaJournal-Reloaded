using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GtaJournal.Core.Navigation;
using GtaJournal.Helpers;
using GtaJournal.Models;
using GtaJournal.Services;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace GtaJournal.ViewModels;

public sealed record UnitStat(string Title, int Online, int Total, Brush Brush)
{
    public string Text => $"{Online} онлайн из {Total}";
    public double Share => Total == 0 ? 0 : Online * 100.0 / Total;
}

public sealed partial class DashboardViewModel : ViewModelBase, IUserActions
{
    private readonly StatusService _status;
    private List<UserItemViewModel> _online = [], _afk = [], _offline = [];

    public DashboardViewModel(ViewModelContext context, StatusService status) : base(context)
    {
        _status = status;
    }

    public ObservableCollection<UserItemViewModel> OnlineUsers { get; } = [];
    public ObservableCollection<UserItemViewModel> AfkUsers { get; } = [];
    public ObservableCollection<UserItemViewModel> OfflineUsers { get; } = [];
    public ObservableCollection<UnitStat> UnitStats { get; } = [];

    [ObservableProperty] public partial bool IsLoaded { get; set; }
    [ObservableProperty] public partial string SearchText { get; set; } = "";
    [ObservableProperty] public partial string LastUpdateText { get; set; } = "Никогда";
    [ObservableProperty] public partial string OnlineCountText { get; set; } = "0";
    [ObservableProperty] public partial string AfkCountText { get; set; } = "0";
    [ObservableProperty] public partial string OfflineCountText { get; set; } = "0";
    [ObservableProperty] public partial string TotalText { get; set; } = "";

    /// <summary>Первая загрузка: данных ещё нет — показываем заглушки вместо пустых списков.</summary>
    public bool IsFirstLoad => !IsLoaded && _online.Count + _afk.Count + _offline.Count == 0;
    [ObservableProperty] public partial bool ShowUnitStats { get; set; }

    // ---- Профиль ----
    public string ProfileName => Accounts.Profile?.Username ?? Accounts.ActiveAccount?.Title ?? "—";
    public string? ProfileTag => Context.Tags.Match(Accounts.Profile?.Tag) is not null ? Accounts.Profile?.Tag : null;
    [ObservableProperty] public partial ImageSource? ProfileAvatar { get; set; }
    public JournalStatus ProfileStatus => Accounts.Profile?.Status ?? JournalStatus.Offline;
    public string ProfileStatusTitle => Accounts.Profile is null ? "Загрузка…" : ProfileStatus switch
    {
        JournalStatus.Online => "Вы онлайн",
        JournalStatus.Afk => "Вы отошли (АФК)",
        _ => "Вы оффлайн",
    };

    // Состояние сегментного переключателя статуса
    public bool IsOnline => Accounts.Profile is not null && ProfileStatus == JournalStatus.Online;
    public bool IsAfk => Accounts.Profile is not null && ProfileStatus == JournalStatus.Afk;
    public bool IsOffline => Accounts.Profile is not null && ProfileStatus == JournalStatus.Offline;
    public Brush ProfileStatusBrush => StatusVisuals.Brush(ProfileStatus);

    public bool OnlineEmpty => !IsFirstLoad && OnlineUsers.Count == 0;
    public bool AfkEmpty => !IsFirstLoad && AfkUsers.Count == 0;
    public bool OfflineEmpty => !IsFirstLoad && OfflineUsers.Count == 0;

    /// <summary>Пусто из-за поиска, а не потому что никого нет.</summary>
    public bool IsFiltering => SearchText.Trim().Length > 0;
    public string OnlineEmptyText => IsFiltering ? "Никого не найдено" : "Все уснули :(";
    public string AfkEmptyText => IsFiltering ? "Никого не найдено" : "Нет АФКшеров ╰(*°▽°*)╯";
    public string OfflineEmptyText => IsFiltering ? "Никого не найдено" : "Все онлайн O_O";

    private const string SearchGlyph = "\uE721";
    public string OnlineEmptyGlyph => IsFiltering ? SearchGlyph : "\uE708";
    public string AfkEmptyGlyph => IsFiltering ? SearchGlyph : "\uE76E";
    public string OfflineEmptyGlyph => IsFiltering ? SearchGlyph : "\uE9D9";

    public bool CanManageUsers => Accounts.IsAdmin;

    public override async Task OnNavigatedToAsync(object? parameter)
    {
        Accounts.PropertyChanged += OnAccountsChanged;
        _status.ForcedOffline += OnForcedOffline;
        await RefreshAsync();
    }

    public override void OnNavigatedFrom()
    {
        Accounts.PropertyChanged -= OnAccountsChanged;
        _status.ForcedOffline -= OnForcedOffline;
    }

    private void OnForcedOffline()
    {
        if (!IsBusy) _ = RefreshAsync();
    }

    private void OnAccountsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AccountService.Profile))
            NotifyProfileChanged();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsLoaded = false;
        NotifyProfileChanged();
        await QueryAsync(QueryKeys.Dashboard,c => c.GetDashboardAsync(), Apply, "Не удалось загрузить панель", async data =>
        {
            await Accounts.SetProfileAsync(data.Profile);
            IsLoaded = true;
            await Context.Avatars.RememberAsync(data.Online.Concat(data.Afk).Concat(data.Offline)
                .Select(u => (u.Username, u.AvatarUrl))
                .Append((data.Profile.Username, data.Profile.AvatarUrl)));
            // Как и раньше: если игра закрыта, а статус не «Оффлайн» — снимаем.
            await _status.CheckAsync();
        });
        IsLoaded = true;
        NotifyProfileChanged();
        NotifyEmptyStates();
    }

    /// <summary>Показывает списки — и из кэша, и свежие. Профиль берётся только из свежих данных.</summary>
    private void Apply(DashboardData data)
    {
        _online = data.Online.Select(Wrap).ToList();
        _afk = data.Afk.Select(Wrap).ToList();
        _offline = data.Offline.Select(Wrap).ToList();

        TotalText = $"Всего в журнале: {_online.Count + _afk.Count + _offline.Count}";
        BuildUnitStats();

        var updatedAt = Cache.UpdatedAt(QueryKeys.Dashboard) ?? DateTimeOffset.Now;
        var moscow = TimeZoneInfo.ConvertTime(updatedAt, MoscowTime.Zone);
        LastUpdateText = $"Обновлено в {moscow:HH:mm:ss} МСК";
        ApplyFilter();
    }

    [RelayCommand(CanExecute = nameof(CanSetStatus))]
    private Task SetOnlineAsync() => SetStatusAsync(JournalStatus.Online);

    [RelayCommand(CanExecute = nameof(CanSetStatus))]
    private Task SetAfkAsync() => SetStatusAsync(JournalStatus.Afk);

    [RelayCommand(CanExecute = nameof(CanSetStatus))]
    private Task SetOfflineAsync() => SetStatusAsync(JournalStatus.Offline);

    private bool CanSetStatus() => IsLoaded && Accounts.Profile is not null;

    private async Task SetStatusAsync(JournalStatus status)
    {
        var changed = false;
        if (status != ProfileStatus)
            await RunAsync(async () => changed = await _status.SetStatusAsync(status), "Не удалось изменить статус");
        // Переключатель уже «нажался» — возвращаем его к реальному статусу, если смена не удалась.
        NotifyStatusToggles();
        if (changed) await RefreshAsync();
    }

    private void NotifyStatusToggles()
    {
        OnPropertyChanged(nameof(IsOnline));
        OnPropertyChanged(nameof(IsAfk));
        OnPropertyChanged(nameof(IsOffline));
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnIsLoadedChanged(bool value)
    {
        SetOnlineCommand.NotifyCanExecuteChanged();
        SetAfkCommand.NotifyCanExecuteChanged();
        SetOfflineCommand.NotifyCanExecuteChanged();
        NotifyEmptyStates();
    }

    private void ApplyFilter()
    {
        var filter = SearchText.Trim();
        Fill(OnlineUsers, _online, filter);
        Fill(AfkUsers, _afk, filter);
        Fill(OfflineUsers, _offline, filter);

        string Count(int shown, int total) => filter.Length == 0 ? total.ToString() : $"{shown} из {total}";
        OnlineCountText = Count(OnlineUsers.Count, _online.Count);
        AfkCountText = Count(AfkUsers.Count, _afk.Count);
        OfflineCountText = Count(OfflineUsers.Count, _offline.Count);
        NotifyEmptyStates();
    }

    private static void Fill(ObservableCollection<UserItemViewModel> target, List<UserItemViewModel> source, string filter)
    {
        target.Clear();
        foreach (var user in source.Where(u => u.Matches(filter)))
            target.Add(user);
    }

    private void BuildUnitStats()
    {
        UnitStats.Clear();
        if (Context.Tags.Enabled)
        {
            var all = _online.Concat(_afk).Concat(_offline).ToList();
            foreach (var unit in Context.Tags.Tags)
            {
                var total = all.Count(u => u.Unit?.Id == unit.Id);
                var online = _online.Count(u => u.Unit?.Id == unit.Id);
                UnitStats.Add(new UnitStat(unit.Title, online, total, new SolidColorBrush(unit.Color)));
            }
        }
        ShowUnitStats = UnitStats.Any(s => s.Total > 0);
    }

    private UserItemViewModel Wrap(JournalUser user) => new(user, Context.Tags.Match(user.Tag), this, Context.Avatars);

    private void NotifyProfileChanged()
    {
        OnPropertyChanged(nameof(ProfileName));
        OnPropertyChanged(nameof(ProfileTag));
        _ = LoadProfileAvatarAsync();
        OnPropertyChanged(nameof(ProfileStatus));
        OnPropertyChanged(nameof(ProfileStatusTitle));
        OnPropertyChanged(nameof(ProfileStatusBrush));
        OnPropertyChanged(nameof(CanManageUsers));
        NotifyStatusToggles();
        SetOnlineCommand.NotifyCanExecuteChanged();
        SetAfkCommand.NotifyCanExecuteChanged();
        SetOfflineCommand.NotifyCanExecuteChanged();
    }

    private string? _profileAvatarUrl;

    private async Task LoadProfileAvatarAsync()
    {
        var url = Accounts.Profile?.AvatarUrl;
        if (url == _profileAvatarUrl) return;
        _profileAvatarUrl = url;
        ProfileAvatar = await Context.Avatars.LoadAsync(url);
    }

    private void NotifyEmptyStates()
    {
        OnPropertyChanged(nameof(OnlineEmpty));
        OnPropertyChanged(nameof(AfkEmpty));
        OnPropertyChanged(nameof(OfflineEmpty));
        OnPropertyChanged(nameof(IsFirstLoad));
        OnPropertyChanged(nameof(OnlineEmptyText));
        OnPropertyChanged(nameof(AfkEmptyText));
        OnPropertyChanged(nameof(OfflineEmptyText));
        OnPropertyChanged(nameof(OnlineEmptyGlyph));
        OnPropertyChanged(nameof(AfkEmptyGlyph));
        OnPropertyChanged(nameof(OfflineEmptyGlyph));
    }

    // ---- IUserActions ----

    public void CopyMention(UserItemViewModel user)
    {
        Services.DialogService.CopyToClipboard(user.Model.Mention);
        Messages.Info("Скопировано", "Упоминание пользователя скопировано");
    }

    public async Task SetUserStatusAsync(UserItemViewModel user, JournalStatus status)
    {
        await RunAsync(async () =>
        {
            var result = await Client.SetUserStatusAsync(user.Model.Id!.Value, status);
            // Сайт отвечает на смену только статуса кодами 0/1/3 — все означают, что статус применён.
            if (result.Res is 0 or 1 or 3)
                Messages.Success("Статус изменён", $"Пользователь {user.Username}");
            else
                Messages.Error("Не удалось изменить статус", $"Пользователь {user.Username}");
        }, "Не удалось изменить статус", showBusy: false);
        await RefreshAsync();
    }

    public async Task OpenUserStatsAsync(UserItemViewModel user)
    {
        if (await ResolveUserIdAsync(user) is { } id) Navigation.Navigate(Routes.UserStats, id);
    }

    public async Task EditUserAsync(UserItemViewModel user)
    {
        if (await ResolveUserIdAsync(user) is { } id) Navigation.Navigate(Routes.EditUser, id);
    }

    /// <summary>
    /// У администраторов сайт не выводит в списке ссылку на профиль, поэтому id
    /// ищется по нику в списке пользователей страницы статистики.
    /// </summary>
    private async Task<long?> ResolveUserIdAsync(UserItemViewModel user)
    {
        if (user.Model.Id is { } known) return known;

        long? id = null;
        await RunAsync(async () =>
        {
            long? Find(UserStatisticsData stats) =>
                stats.Users.FirstOrDefault(u => string.Equals(Nickname.Split(u.Name).Name, user.Username, StringComparison.OrdinalIgnoreCase))?.Id;

            // Список пользователей меняется редко — сначала ищем в кэше, не нашли — запрашиваем свежий.
            var key = QueryKeys.UserStatistics(null, null);
            if (Cache.TryGet<UserStatisticsData>(key, out var cached)) id = Find(cached);
            id ??= Find(await Cache.FetchAsync(key, c => c.GetUserStatisticsAsync(null, null)));
            if (id is null) Messages.Warning("Пользователь не найден", user.Username);
        }, "Не удалось найти пользователя");
        return id;
    }

    public async Task DeleteUserAsync(UserItemViewModel user)
    {
        if (!await Dialogs.ConfirmAsync("Удаление пользователя",
                $"Вы действительно хотите удалить пользователя «{user.Username}»?", "Удалить", "Не удалять", destructive: true))
            return;

        var deleted = false;
        await RunAsync(async () =>
        {
            var result = await Client.DeleteUserAsync(user.Model.Id!.Value);
            if (result.IsSuccess) deleted = true;
            else Messages.Error("Ошибка", result.Text ?? "Неизвестная ошибка");
        }, "Не удалось удалить пользователя");

        if (deleted)
        {
            Messages.Success("Пользователь удалён", user.Username);
            await RefreshAsync();
        }
    }
}
