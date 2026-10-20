using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GtaJournal.Models;
using GtaJournal.Services;
using Microsoft.UI.Xaml.Media;

namespace GtaJournal.ViewModels;

/// <summary>Редактируемая строка тега подразделения.</summary>
public sealed partial class UnitTagEditor : ObservableObject
{
    [ObservableProperty, NotifyPropertyChangedFor(nameof(Preview))]
    public partial string Tag { get; set; } = "";

    [ObservableProperty] public partial string Title { get; set; } = "";

    [ObservableProperty, NotifyPropertyChangedFor(nameof(Preview))]
    public partial string Color { get; set; } = "#F97316";

    public Brush Preview => new SolidColorBrush(UnitTag.ParseColor(Color));
}

public sealed partial class SettingsViewModel(ViewModelContext context) : ViewModelBase(context)
{
    private SettingsService SettingsStore => Context.Settings;

    [ObservableProperty] public partial string ProcessName { get; set; } = "";
    [ObservableProperty] public partial double PollInterval { get; set; } = 10;
    [ObservableProperty] public partial string BaseUrl { get; set; } = "";
    [ObservableProperty] public partial bool ClickSound { get; set; }
    [ObservableProperty] public partial bool AlertSound { get; set; }
    [ObservableProperty] public partial bool Notifications { get; set; }
    [ObservableProperty] public partial bool MinimizeToTray { get; set; }
    [ObservableProperty] public partial bool UseTags { get; set; }

    public ObservableCollection<UnitTagEditor> Tags { get; } = [];
    public ObservableCollection<string> ProcessSuggestions { get; } = [];

    public int MinPoll => AppSettings.MinPollInterval;
    public int MaxPoll => AppSettings.MaxPollInterval;

    public override Task OnNavigatedToAsync(object? parameter)
    {
        var s = SettingsStore.Current;
        ProcessName = s.ProcessName;
        PollInterval = s.PollIntervalSeconds;
        BaseUrl = s.BaseUrl;
        ClickSound = s.ClickSound;
        AlertSound = s.AlertSound;
        Notifications = s.Notifications;
        MinimizeToTray = s.MinimizeToTray;
        UseTags = s.UseTags;

        Tags.Clear();
        foreach (var t in Context.Tags.Tags)
            Tags.Add(new UnitTagEditor { Tag = t.Tag, Title = t.Title, Color = t.ColorHex });
        return Task.CompletedTask;
    }

    /// <summary>Подсказки для имени процесса из запущенных программ.</summary>
    public void UpdateProcessSuggestions(string query)
    {
        var names = Process.GetProcesses()
            .Select(p => { using (p) return p.ProcessName; })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(n => n.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.OrdinalIgnoreCase)
            .Take(15)
            .Select(n => n + ".exe");
        ProcessSuggestions.Clear();
        foreach (var n in names) ProcessSuggestions.Add(n);
    }

    [RelayCommand]
    private void AddTag() => Tags.Add(new UnitTagEditor { Tag = "", Title = "", Color = "#F97316" });

    [RelayCommand]
    private void RemoveTag(UnitTagEditor? tag)
    {
        if (tag is not null) Tags.Remove(tag);
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        var url = BaseUrl.Trim().TrimEnd('/');
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            Messages.Error("Некорректный адрес", "Укажите полный адрес сайта, например https://status-journal.com");
            return;
        }
        if (string.IsNullOrWhiteSpace(ProcessName))
        {
            Messages.Error("Не указан процесс", "Укажите имя процесса игры, например Multi Theft Auto.exe");
            return;
        }

        var tags = Tags
            .Where(t => !string.IsNullOrWhiteSpace(t.Tag))
            .Select(t => new UnitTag(0, t.Tag.Trim(), string.IsNullOrWhiteSpace(t.Title) ? t.Tag.Trim() : t.Title.Trim(), NormalizeColor(t.Color)))
            .DistinctBy(t => t.Tag, StringComparer.OrdinalIgnoreCase)
            .ToList();

        await RunAsync(async () =>
        {
            await Context.Tags.SaveAsync(tags);
            await SettingsStore.UpdateAsync(s => s with
            {
                ProcessName = ProcessName.Trim(),
                PollIntervalSeconds = (int)Math.Clamp(double.IsNaN(PollInterval) ? 10 : PollInterval, MinPoll, MaxPoll),
                BaseUrl = url,
                ClickSound = ClickSound,
                AlertSound = AlertSound,
                Notifications = Notifications,
                MinimizeToTray = MinimizeToTray,
                UseTags = UseTags,
            });
            Messages.Success("Настройки сохранены");
        }, "Не удалось сохранить настройки");
    }

    private static string NormalizeColor(string color)
    {
        var c = UnitTag.ParseColor(color);
        return $"#{c.R:X2}{c.G:X2}{c.B:X2}";
    }
}
