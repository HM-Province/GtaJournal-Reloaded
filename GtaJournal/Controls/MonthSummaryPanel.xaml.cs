using GtaJournal.Helpers;
using GtaJournal.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace GtaJournal.Controls;

/// <summary>Итоги месяца: всего онлайна и количество дней по статусам (цвета совпадают с календарём).</summary>
public sealed partial class MonthSummaryPanel : UserControl
{
    public static readonly DependencyProperty SummaryProperty = DependencyProperty.Register(
        nameof(Summary), typeof(CalendarSummary), typeof(MonthSummaryPanel),
        new PropertyMetadata(null, (d, _) => ((MonthSummaryPanel)d).Bindings.Update()));

    public MonthSummaryPanel() => InitializeComponent();

    public CalendarSummary? Summary
    {
        get => (CalendarSummary?)GetValue(SummaryProperty);
        set => SetValue(SummaryProperty, value);
    }

    public string TotalText => Summary is { } s ? OnlineTime.Format(s.Total) : "—";
    public string SuccessText => Summary is { } s ? Days(s.SuccessDays) : "—";
    public string NormalText => Summary is { } s ? Days(s.NormalDays) : "—";
    public string MissedText => Summary is { } s ? Days(s.MissedDays) : "—";

    public Brush SuccessBrush { get; } = new SolidColorBrush(StatusVisuals.Online);
    public Brush NormalBrush { get; } = new SolidColorBrush(StatusVisuals.Normal);
    public Brush MissedBrush { get; } = new SolidColorBrush(StatusVisuals.Offline);

    private static string Days(int n)
    {
        var mod100 = n % 100;
        var mod10 = n % 10;
        var word = mod100 is >= 11 and <= 14 ? "дней" : mod10 switch { 1 => "день", 2 or 3 or 4 => "дня", _ => "дней" };
        return $"{n} {word}";
    }
}
