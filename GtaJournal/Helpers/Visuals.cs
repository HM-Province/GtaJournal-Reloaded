using GtaJournal.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace GtaJournal.Helpers;

public static class StatusVisuals
{
    public static readonly Color Online = Color.FromArgb(255, 0x22, 0xA0, 0x55);
    public static readonly Color Afk = Color.FromArgb(255, 0xE0, 0x9A, 0x0B);
    public static readonly Color Offline = Color.FromArgb(255, 0xDC, 0x3C, 0x3C);
    public static readonly Color Normal = Color.FromArgb(255, 0x63, 0x66, 0xF1);
    public static readonly Color Inactive = Color.FromArgb(255, 0x8A, 0x8F, 0x98);

    public static Brush Brush(JournalStatus status) => new SolidColorBrush(status switch
    {
        JournalStatus.Online => Online,
        JournalStatus.Afk => Afk,
        _ => Offline,
    });

    /// <summary>Цвет дня календаря: норма выполнена / был онлайн / не было онлайна.</summary>
    public static Color? CalendarColor(CalendarCellKind kind) => kind switch
    {
        CalendarCellKind.Error => Offline,
        CalendarCellKind.Success => Online,
        CalendarCellKind.Normal => Normal,
        CalendarCellKind.Inactive => Inactive,
        _ => null,
    };
}

public static class MoscowTime
{
    public static readonly TimeZoneInfo Zone = Find();

    private static TimeZoneInfo Find()
    {
        foreach (var id in new[] { "Russian Standard Time", "Europe/Moscow" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
        }
        return TimeZoneInfo.CreateCustomTimeZone("MSK", TimeSpan.FromHours(3), "MSK", "MSK");
    }
}

/// <summary>Функции для x:Bind.</summary>
public static class Bind
{
    public static Visibility Visible(bool value) => value ? Visibility.Visible : Visibility.Collapsed;
    public static Visibility Collapsed(bool value) => value ? Visibility.Collapsed : Visibility.Visible;
    public static Visibility VisibleIfText(string? value) => string.IsNullOrEmpty(value) ? Visibility.Collapsed : Visibility.Visible;
    public static Visibility VisibleIfNotNull(object? value) => value is null ? Visibility.Collapsed : Visibility.Visible;
    public static bool Not(bool value) => !value;
}
