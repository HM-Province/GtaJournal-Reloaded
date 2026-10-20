using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GtaJournal.Models;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI;
using Windows.UI;

namespace GtaJournal.ViewModels;

public sealed record TopRow(int Place, string Username, string? Tag, string Time, string LastUpdate, Brush PlaceBrush, Brush PlaceForeground, Brush TagBrush)
{
    public string PlaceText => Place.ToString();
    public bool HasTag => Tag is not null;
}

public sealed partial class TopViewModel(ViewModelContext context) : ViewModelBase(context)
{
    private static readonly Color Gold = Color.FromArgb(255, 0xE8, 0xB3, 0x0E);
    private static readonly Color Silver = Color.FromArgb(255, 0x9C, 0xA3, 0xAF);
    private static readonly Color Bronze = Color.FromArgb(255, 0xC0, 0x7A, 0x45);
    private int _loadVersion;

    public ObservableCollection<TopRow> Rows { get; } = [];

    [ObservableProperty] public partial bool IsEmpty { get; set; }

    public override Task OnNavigatedToAsync(object? parameter) => RefreshAsync();

    [RelayCommand]
    private async Task RefreshAsync()
    {
        var version = ++_loadVersion;
        await RunAsync(async () =>
        {
            var top = await Client.GetTopAsync();
            if (version != _loadVersion) return;

            Rows.Clear();
            foreach (var entry in top.Take(10))
            {
                var unit = Context.Tags.Match(entry.Tag);
                var medal = entry.Place switch { 1 => Gold, 2 => Silver, 3 => Bronze, _ => (Color?)null };
                Rows.Add(new TopRow(
                    entry.Place,
                    entry.Username,
                    unit is null ? null : entry.Tag,
                    entry.Duration is null ? entry.RawTime : OnlineTime.Format(entry.Duration),
                    entry.LastUpdate,
                    medal is { } m ? new SolidColorBrush(m) : (Brush)Microsoft.UI.Xaml.Application.Current.Resources["SubtleFillColorSecondaryBrush"],
                    medal is not null ? new SolidColorBrush(Colors.White) : (Brush)Microsoft.UI.Xaml.Application.Current.Resources["TextFillColorPrimaryBrush"],
                    new SolidColorBrush(unit?.Color ?? Colors.Transparent)));
            }
            IsEmpty = Rows.Count == 0;
        }, "Не удалось загрузить топ");
    }
}
