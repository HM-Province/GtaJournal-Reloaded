using System.Globalization;
using GtaJournal.Data.Repositories;

namespace GtaJournal.Services;

public enum AppTheme
{
    System,
    Light,
    Dark,
}

/// <summary>Снимок настроек приложения (иммутабельный, меняется целиком через <see cref="SettingsService.SaveAsync"/>).</summary>
public sealed record AppSettings
{
    public string ProcessName { get; init; } = "Multi Theft Auto.exe";
    public int PollIntervalSeconds { get; init; } = 10;
    public string BaseUrl { get; init; } = "https://status-journal.com";
    public bool ClickSound { get; init; } = true;
    public bool AlertSound { get; init; } = true;
    public bool Notifications { get; init; } = true;
    public bool MinimizeToTray { get; init; } = true;
    public bool UseTags { get; init; }
    public AppTheme Theme { get; init; } = AppTheme.System;
    public long? ActiveAccountId { get; init; }

    public Uri BaseUri => new(BaseUrl.TrimEnd('/') + "/");

    public const int MinPollInterval = 1;
    public const int MaxPollInterval = 300;
}

public sealed class SettingsService
{
    private const string ProcessNameKey = "process_name";
    private const string PollIntervalKey = "poll_interval_seconds";
    private const string BaseUrlKey = "base_url";
    private const string ClickSoundKey = "click_sound";
    private const string AlertSoundKey = "alert_sound";
    private const string NotificationsKey = "notifications";
    private const string MinimizeToTrayKey = "minimize_to_tray";
    private const string ActiveAccountKey = "active_account_id";
    private const string UseTagsKey = "use_tags";
    private const string ThemeKey = "theme";

    private readonly SettingsRepository _repository;

    public SettingsService(SettingsRepository repository)
    {
        _repository = repository;
        var values = repository.GetAll();
        var defaults = new AppSettings();
        Current = new AppSettings
        {
            ProcessName = values.GetValueOrDefault(ProcessNameKey) ?? defaults.ProcessName,
            PollIntervalSeconds = int.TryParse(values.GetValueOrDefault(PollIntervalKey), out var interval)
                ? Math.Clamp(interval, AppSettings.MinPollInterval, AppSettings.MaxPollInterval)
                : defaults.PollIntervalSeconds,
            BaseUrl = values.GetValueOrDefault(BaseUrlKey) ?? defaults.BaseUrl,
            ClickSound = ReadBool(values, ClickSoundKey, defaults.ClickSound),
            AlertSound = ReadBool(values, AlertSoundKey, defaults.AlertSound),
            Notifications = ReadBool(values, NotificationsKey, defaults.Notifications),
            MinimizeToTray = ReadBool(values, MinimizeToTrayKey, defaults.MinimizeToTray),
            UseTags = ReadBool(values, UseTagsKey, defaults.UseTags),
            Theme = Enum.TryParse<AppTheme>(values.GetValueOrDefault(ThemeKey), ignoreCase: true, out var theme) && Enum.IsDefined(theme)
                ? theme
                : defaults.Theme,
            ActiveAccountId = long.TryParse(values.GetValueOrDefault(ActiveAccountKey), out var id) && id > 0 ? id : null,
        };
    }

    public AppSettings Current { get; private set; }

    /// <summary>Срабатывает после сохранения: (старые, новые).</summary>
    public event Action<AppSettings, AppSettings>? Changed;

    public async Task SaveAsync(AppSettings settings)
    {
        var old = Current;
        await _repository.SetManyAsync(new Dictionary<string, string>
        {
            [ProcessNameKey] = settings.ProcessName,
            [PollIntervalKey] = settings.PollIntervalSeconds.ToString(CultureInfo.InvariantCulture),
            [BaseUrlKey] = settings.BaseUrl,
            [ClickSoundKey] = settings.ClickSound ? "1" : "0",
            [AlertSoundKey] = settings.AlertSound ? "1" : "0",
            [NotificationsKey] = settings.Notifications ? "1" : "0",
            [MinimizeToTrayKey] = settings.MinimizeToTray ? "1" : "0",
            [UseTagsKey] = settings.UseTags ? "1" : "0",
            [ThemeKey] = settings.Theme.ToString().ToLowerInvariant(),
            [ActiveAccountKey] = settings.ActiveAccountId?.ToString(CultureInfo.InvariantCulture) ?? "0",
        });
        Current = settings;
        Changed?.Invoke(old, settings);
    }

    public Task UpdateAsync(Func<AppSettings, AppSettings> change) => SaveAsync(change(Current));

    private static bool ReadBool(Dictionary<string, string> values, string key, bool fallback) =>
        values.TryGetValue(key, out var v) ? v == "1" : fallback;
}
