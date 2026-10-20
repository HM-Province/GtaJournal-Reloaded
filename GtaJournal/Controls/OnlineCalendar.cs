using GtaJournal.Helpers;
using GtaJournal.Models;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace GtaJournal.Controls;

/// <summary>Календарь онлайна за месяц: 7 дней недели + недельный итог. Сегодняшний день подсвечивается.</summary>
public sealed partial class OnlineCalendar : UserControl
{
    private static readonly string[] Headers = ["Пн", "Вт", "Ср", "Чт", "Пт", "Сб", "Вс", "Неделя"];

    public static readonly DependencyProperty CalendarProperty = DependencyProperty.Register(
        nameof(Calendar), typeof(CalendarData), typeof(OnlineCalendar),
        new PropertyMetadata(null, (d, _) => ((OnlineCalendar)d).Rebuild()));

    private readonly Grid _grid = new() { ColumnSpacing = 6, RowSpacing = 6 };

    public OnlineCalendar()
    {
        for (var i = 0; i < Headers.Length; i++)
        {
            _grid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(i == Headers.Length - 1 ? 1.15 : 1, GridUnitType.Star),
                MinWidth = 86,
            });
        }
        Content = new ScrollViewer
        {
            Content = _grid,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollMode = ScrollMode.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollMode = ScrollMode.Disabled,
        };
        ActualThemeChanged += (_, _) => Rebuild();
    }

    public CalendarData? Calendar
    {
        get => (CalendarData?)GetValue(CalendarProperty);
        set => SetValue(CalendarProperty, value);
    }

    private void Rebuild()
    {
        _grid.Children.Clear();
        _grid.RowDefinitions.Clear();

        _grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (var c = 0; c < Headers.Length; c++)
        {
            var header = new TextBlock
            {
                Text = Headers[c],
                Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(10, 0, 0, 2),
                Foreground = Resource<Brush>("TextFillColorSecondaryBrush"),
            };
            Grid.SetColumn(header, c);
            _grid.Children.Add(header);
        }

        if (Calendar is not { } calendar) return;

        var now = DateTime.Now;
        var today = calendar.Month.Year == now.Year && calendar.Month.Month == now.Month ? now.Day : (int?)null;

        for (var r = 0; r < calendar.Weeks.Count; r++)
        {
            _grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var week = calendar.Weeks[r];
            for (var c = 0; c < week.Count && c < Headers.Length; c++)
            {
                var isWeekTotal = c == Headers.Length - 1;
                var cell = isWeekTotal ? CreateWeekCell(week[c]) : CreateDayCell(week[c], week[c].Day == today);
                Grid.SetRow(cell, r + 1);
                Grid.SetColumn(cell, c);
                _grid.Children.Add(cell);
            }
        }
    }

    private FrameworkElement CreateDayCell(CalendarCell cell, bool isToday)
    {
        if (cell.Day is null)
            return new Border { MinHeight = 64 };

        var accent = StatusVisuals.CalendarColor(cell.Kind);
        var root = new Border
        {
            MinHeight = 64,
            Padding = new Thickness(10, 8, 10, 8),
            CornerRadius = new CornerRadius(6),
            Background = accent is { } a
                ? new SolidColorBrush(Color.FromArgb(ActualTheme == ElementTheme.Light ? (byte)0x26 : (byte)0x33, a.R, a.G, a.B))
                : Resource<Brush>("SubtleFillColorSecondaryBrush"),
            BorderThickness = new Thickness(isToday ? 2 : 1),
            BorderBrush = isToday ? Resource<Brush>("AccentFillColorDefaultBrush") : Resource<Brush>("CardStrokeColorDefaultBrush"),
        };

        var content = new Grid { RowSpacing = 4 };
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var header = new Grid();
        header.Children.Add(new TextBlock
        {
            Text = cell.Day.ToString(),
            FontWeight = isToday ? FontWeights.Bold : FontWeights.SemiBold,
            Foreground = isToday ? Resource<Brush>("AccentTextFillColorPrimaryBrush") : Resource<Brush>("TextFillColorPrimaryBrush"),
        });
        if (accent is { } dotColor)
        {
            header.Children.Add(new Microsoft.UI.Xaml.Shapes.Ellipse
            {
                Width = 8,
                Height = 8,
                Fill = new SolidColorBrush(dotColor),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
            });
        }
        content.Children.Add(header);

        if (cell.Duration is { } duration)
        {
            var time = new TextBlock
            {
                Text = OnlineTime.Format(duration),
                Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                Foreground = Resource<Brush>("TextFillColorSecondaryBrush"),
                VerticalAlignment = VerticalAlignment.Bottom,
                TextTrimming = TextTrimming.CharacterEllipsis,
                IsTextSelectionEnabled = true,
            };
            Grid.SetRow(time, 1);
            content.Children.Add(time);
        }

        root.Child = content;
        return root;
    }

    private FrameworkElement CreateWeekCell(CalendarCell cell) => new Border
    {
        MinHeight = 64,
        Padding = new Thickness(10, 8, 10, 8),
        CornerRadius = new CornerRadius(6),
        Background = Resource<Brush>("LayerFillColorDefaultBrush"),
        BorderThickness = new Thickness(1),
        BorderBrush = Resource<Brush>("CardStrokeColorDefaultBrush"),
        Child = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = 2,
            Children =
            {
                new TextBlock
                {
                    Text = "Итого",
                    Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                    Foreground = Resource<Brush>("TextFillColorTertiaryBrush"),
                },
                new TextBlock
                {
                    Text = cell.Duration is null ? "—" : OnlineTime.Format(cell.Duration),
                    FontWeight = FontWeights.SemiBold,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                },
            },
        },
    };

    private static T Resource<T>(string key) where T : class => (T)Application.Current.Resources[key];
}
