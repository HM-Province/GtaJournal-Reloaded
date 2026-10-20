using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GtaJournal.Models;
using GtaJournal.Services;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI;
using Windows.UI;

namespace GtaJournal.ViewModels;

public sealed partial class TopRow(int place, string username, string? tag, string time, string lastUpdate, Brush placeBrush, Brush placeForeground, Brush tagBrush)
    : ObservableObject
{
    public int Place { get; } = place;
    public string Username { get; } = username;
    public string? Tag { get; } = tag;
    public string Time { get; } = time;
    public string LastUpdate { get; } = lastUpdate;
    public Brush PlaceBrush { get; } = placeBrush;
    public Brush PlaceForeground { get; } = placeForeground;
    public Brush TagBrush { get; } = tagBrush;

    public string PlaceText => Place.ToString();
    public bool HasTag => Tag is not null;

    [ObservableProperty] public partial ImageSource? Avatar { get; set; }

    public async Task LoadAvatarAsync(AvatarService avatars) => Avatar = await avatars.LoadForAsync(Username);
}

public sealed partial class TopViewModel(ViewModelContext context) : ViewModelBase(context)
{
    private static readonly Color Gold = Color.FromArgb(255, 0xE8, 0xB3, 0x0E);
    private static readonly Color Silver = Color.FromArgb(255, 0x9C, 0xA3, 0xAF);
    private static readonly Color Bronze = Color.FromArgb(255, 0xC0, 0x7A, 0x45);

    public ObservableCollection<TopRow> Rows { get; } = [];

    [ObservableProperty] public partial bool IsEmpty { get; set; }

    public override Task OnNavigatedToAsync(object? parameter) => RefreshAsync();

    [RelayCommand]
    private Task RefreshAsync() => QueryAsync(QueryKeys.Top,c => c.GetTopAsync(), Apply, "Не удалось загрузить топ");

    private void Apply(IReadOnlyList<TopEntry> top)
    {
        Rows.Clear();
        foreach (var entry in top.Take(10))
        {
            var unit = Context.Tags.Match(entry.Tag);
            var medal = entry.Place switch { 1 => Gold, 2 => Silver, 3 => Bronze, _ => (Color?)null };
            var row = new TopRow(
                entry.Place,
                entry.Username,
                unit is null ? null : entry.Tag,
                entry.Duration is null ? entry.RawTime : OnlineTime.Format(entry.Duration),
                entry.LastUpdate,
                medal is { } m ? new SolidColorBrush(m) : (Brush)Microsoft.UI.Xaml.Application.Current.Resources["SubtleFillColorSecondaryBrush"],
                medal is not null ? new SolidColorBrush(Colors.White) : (Brush)Microsoft.UI.Xaml.Application.Current.Resources["TextFillColorPrimaryBrush"],
                new SolidColorBrush(unit?.Color ?? Colors.Transparent));
            Rows.Add(row);
            _ = row.LoadAvatarAsync(Context.Avatars);
        }
        IsEmpty = Rows.Count == 0;
    }
}
