using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;

namespace GtaJournal.Services;

/// <summary>Системные toast-уведомления Windows.</summary>
public sealed class NotificationService(SettingsService settings)
{
    private bool _registered;

    /// <summary>Пользователь кликнул по уведомлению.</summary>
    public event Action? Invoked;

    public void Register()
    {
        try
        {
            AppNotificationManager.Default.NotificationInvoked += (_, _) => Invoked?.Invoke();
            AppNotificationManager.Default.Register();
            _registered = true;
        }
        catch (Exception)
        {
            // Уведомления недоступны (политика, старая ОС) — работаем без них.
        }
    }

    public void Unregister()
    {
        if (!_registered) return;
        try { AppNotificationManager.Default.Unregister(); } catch (Exception) { }
    }

    public void Show(string title, string body)
    {
        if (!_registered || !settings.Current.Notifications) return;
        try
        {
            var notification = new AppNotificationBuilder()
                .AddText(title)
                .AddText(body)
                .BuildNotification();
            AppNotificationManager.Default.Show(notification);
        }
        catch (Exception)
        {
        }
    }
}
