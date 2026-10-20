using System.Diagnostics;
using GtaJournal.Models;
using GtaJournal.Services.Journal;
using Microsoft.UI.Dispatching;

namespace GtaJournal.Services;

/// <summary>
/// Управление собственным статусом и наблюдение за процессом игры:
/// если процесс закрыт, а мы «Онлайн»/«АФК», — переводит в «Оффлайн», играет звук и шлёт уведомление.
/// </summary>
public sealed class StatusService : IDisposable
{
    private readonly AccountService _accounts;
    private readonly SettingsService _settings;
    private readonly SoundService _sounds;
    private readonly NotificationService _notifications;
    private readonly MessageService _messages;
    private DispatcherQueueTimer? _timer;
    private bool _checking;

    public StatusService(AccountService accounts, SettingsService settings, SoundService sounds,
        NotificationService notifications, MessageService messages)
    {
        _accounts = accounts;
        _settings = settings;
        _sounds = sounds;
        _notifications = notifications;
        _messages = messages;
        _settings.Changed += (_, _) => ApplyInterval();
    }

    /// <summary>Статус снят автоматически, потому что игра закрыта.</summary>
    public event Action? ForcedOffline;

    /// <summary>Запуск наблюдения. Вызывать из UI-потока.</summary>
    public void Start()
    {
        _timer ??= DispatcherQueue.GetForCurrentThread().CreateTimer();
        _timer.Tick += async (_, _) => await CheckAsync();
        ApplyInterval();
        _timer.Start();
    }

    public bool IsProcessRunning()
    {
        var name = NormalizeProcessName(_settings.Current.ProcessName);
        if (name.Length == 0) return true;
        var processes = Process.GetProcessesByName(name);
        foreach (var p in processes) p.Dispose();
        return processes.Length > 0;
    }

    /// <summary>Сменить свой статус. «Онлайн»/«АФК» недоступны без запущенной игры.</summary>
    public async Task<bool> SetStatusAsync(JournalStatus status)
    {
        if (_accounts.Client is not { } client) return false;

        if (status != JournalStatus.Offline && !await Task.Run(IsProcessRunning))
        {
            _sounds.PlayAlert();
            _notifications.Show("ЖА недоступен", $"Запустите {DisplayProcessName}.");
            _messages.Error("Статус не изменён", $"Процесс {DisplayProcessName} не запущен.");
            return false;
        }

        await client.EditStatusAsync(status);
        _accounts.SetProfileStatus(status);
        return true;
    }

    /// <summary>Проверить процесс прямо сейчас (например, после загрузки дашборда).</summary>
    public async Task CheckAsync()
    {
        if (_checking) return;
        _checking = true;
        try
        {
            if (_accounts.Client is not { } client || _accounts.Profile is not { } profile || profile.Status == JournalStatus.Offline)
                return;
            if (await Task.Run(IsProcessRunning))
                return;

            await client.EditStatusAsync(JournalStatus.Offline);
            _accounts.SetProfileStatus(JournalStatus.Offline);
            ForcedOffline?.Invoke();
            _sounds.PlayAlert();
            _notifications.Show("Вы оффлайн", $"Процесс {DisplayProcessName} завершён.");
            _messages.Warning("Вы оффлайн", $"Процесс {DisplayProcessName} завершён.");
        }
        catch (SessionExpiredException)
        {
            _accounts.NotifySessionExpired();
        }
        catch (JournalException ex)
        {
            _messages.Error("Не удалось снять статус", ex.Message);
        }
        finally
        {
            _checking = false;
        }
    }

    public string DisplayProcessName => string.IsNullOrWhiteSpace(_settings.Current.ProcessName) ? "игра" : _settings.Current.ProcessName;

    public static string NormalizeProcessName(string? name)
    {
        var trimmed = (name ?? "").Trim();
        return trimmed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? trimmed[..^4] : trimmed;
    }

    private void ApplyInterval()
    {
        if (_timer is not null)
            _timer.Interval = TimeSpan.FromSeconds(_settings.Current.PollIntervalSeconds);
    }

    public void Dispose() => _timer?.Stop();
}
