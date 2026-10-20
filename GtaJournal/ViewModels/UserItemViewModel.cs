using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GtaJournal.Models;
using GtaJournal.Services;
using Microsoft.UI.Xaml.Media;

namespace GtaJournal.ViewModels;

/// <summary>Действия над пользователем из списка — реализует страница-владелец.</summary>
public interface IUserActions
{
    bool CanManageUsers { get; }
    void CopyMention(UserItemViewModel user);
    Task SetUserStatusAsync(UserItemViewModel user, JournalStatus status);
    Task OpenUserStatsAsync(UserItemViewModel user);
    Task EditUserAsync(UserItemViewModel user);
    Task DeleteUserAsync(UserItemViewModel user);
}

public sealed partial class UserItemViewModel : ObservableObject
{
    private readonly IUserActions _actions;

    public UserItemViewModel(JournalUser user, UnitTag? unit, IUserActions actions, AvatarService avatars)
    {
        Model = user;
        Unit = unit;
        _actions = actions;
        TagBrush = new SolidColorBrush(unit?.Color ?? UnitTag.ParseColor(null));
        _ = LoadAvatarAsync(avatars);
    }

    private async Task LoadAvatarAsync(AvatarService avatars) => Avatar = await avatars.LoadAsync(Model.AvatarUrl);

    public JournalUser Model { get; }
    public UnitTag? Unit { get; }

    public string Username => Model.Username;
    /// <summary>Тег показываем, только если система тегов включена и тег есть.</summary>
    public string? TagText => Unit is not null ? Model.Tag : null;
    public bool HasTag => TagText is not null;
    public string? Note => Model.Note;
    public bool HasNote => !string.IsNullOrEmpty(Model.Note);
    public bool IsAdmin => Model.IsAdmin;
    public string Initials => Model.Username.Length > 0 ? Model.Username[..1] : "?";
    public Brush TagBrush { get; }
    public Brush StatusBrush => Helpers.StatusVisuals.Brush(Model.Status);

    [ObservableProperty] public partial ImageSource? Avatar { get; private set; }

    /// <summary>Смена статуса и удаление — только по id из ссылки в списке.</summary>
    public bool CanManage => _actions.CanManageUsers && Model.Id is not null;

    /// <summary>Изменение и онлайн доступны и для администраторов: у них в списке нет ссылки, id находится по нику.</summary>
    public bool CanEdit => _actions.CanManageUsers;

    public bool Matches(string filter) =>
        filter.Length == 0
        || Model.Username.Contains(filter, StringComparison.OrdinalIgnoreCase)
        || (Model.Tag?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false)
        || (Model.Note?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false);

    [RelayCommand] private void CopyMention() => _actions.CopyMention(this);
    [RelayCommand] private Task SetOnline() => _actions.SetUserStatusAsync(this, JournalStatus.Online);
    [RelayCommand] private Task SetAfk() => _actions.SetUserStatusAsync(this, JournalStatus.Afk);
    [RelayCommand] private Task SetOffline() => _actions.SetUserStatusAsync(this, JournalStatus.Offline);
    [RelayCommand] private Task OpenStats() => _actions.OpenUserStatsAsync(this);
    [RelayCommand] private Task Edit() => _actions.EditUserAsync(this);
    [RelayCommand] private Task Delete() => _actions.DeleteUserAsync(this);
}
