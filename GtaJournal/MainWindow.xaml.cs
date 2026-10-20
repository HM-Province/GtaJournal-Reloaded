using System.Runtime.InteropServices;
using GtaJournal.Models;
using GtaJournal.Services;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using WinRT.Interop;

namespace GtaJournal;

public sealed partial class MainWindow : Window
{
    private const int DefaultWidth = 1180, DefaultHeight = 760, MinWidth = 970, MinHeight = 600;

    private readonly SettingsService _settings = App.GetService<SettingsService>();
    private readonly AccountService _accounts = App.GetService<AccountService>();
    private readonly StatusService _status = App.GetService<StatusService>();
    private readonly SoundService _sounds = App.GetService<SoundService>();
    private readonly MessageService _messages = App.GetService<MessageService>();
    private readonly TrayIcon _tray;
    private bool _exiting;
    private bool _trayHintShown;

    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        AppWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
        AppWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;

        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
        AppWindow.SetIcon(iconPath);

        var scale = GetDpiForWindow(WindowNative.GetWindowHandle(this)) / 96.0;
        AppWindow.Resize(new((int)(DefaultWidth * scale), (int)(DefaultHeight * scale)));
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.PreferredMinimumWidth = (int)(MinWidth * scale);
            presenter.PreferredMinimumHeight = (int)(MinHeight * scale);
        }
        CenterOnScreen();

        _messages.Attach(DispatcherQueue);
        MessagesHost.ItemsSource = _messages.Items;

        RootGrid.Loaded += (_, _) => App.GetService<DialogService>().Attach(RootGrid.XamlRoot);
        // Звук клика по любому месту окна, как в старой версии.
        RootGrid.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler((_, _) => _sounds.PlayClick()), handledEventsToo: true);

        _tray = new TrayIcon(iconPath, "GTA Journal");
        _tray.Activated += ShowAndActivate;
        _tray.MenuProvider = BuildTrayMenu;
        _tray.Show();
        _accounts.PropertyChanged += (_, _) => UpdateTrayTooltip();

        AppWindow.Closing += OnClosing;
    }

    public Frame RootFrame => ContentFrame;

    public void ShowAndActivate()
    {
        AppWindow.Show();
        if (AppWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Minimized } p)
            p.Restore();
        Activate();
        SetForegroundWindow(WindowNative.GetWindowHandle(this));
    }

    /// <summary>Закрытие окна при выходе из приложения.</summary>
    public void CloseForExit()
    {
        _exiting = true;
        _tray.Dispose();
        Close();
    }

    private void OnClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_exiting) return;
        if (_settings.Current.MinimizeToTray)
        {
            // Окно прячется в трей, наблюдение за игрой продолжается.
            args.Cancel = true;
            AppWindow.Hide();
            if (!_trayHintShown)
            {
                _trayHintShown = true;
                App.GetService<NotificationService>().Show("GTA Journal", "Приложение свёрнуто в трей.");
            }
            return;
        }
        args.Cancel = true;
        App.Current.ExitApplication();
    }

    private IReadOnlyList<TrayMenuItem> BuildTrayMenu()
    {
        var status = _accounts.Profile?.Status;
        var canChange = _accounts.Profile is not null;
        return
        [
            new("Открыть GTA Journal", ShowAndActivate),
            TrayMenuItem.Separator,
            new("Онлайн", () => SetStatusFromTray(JournalStatus.Online), IsChecked: status == JournalStatus.Online, IsEnabled: canChange),
            new("АФК", () => SetStatusFromTray(JournalStatus.Afk), IsChecked: status == JournalStatus.Afk, IsEnabled: canChange),
            new("Оффлайн", () => SetStatusFromTray(JournalStatus.Offline), IsChecked: status == JournalStatus.Offline, IsEnabled: canChange),
            TrayMenuItem.Separator,
            new("Выход", () => App.Current.ExitApplication()),
        ];
    }

    private async void SetStatusFromTray(JournalStatus status)
    {
        try
        {
            if (await _status.SetStatusAsync(status))
                _messages.Success("Статус изменён", status.Title());
        }
        catch (Services.Journal.SessionExpiredException)
        {
            _accounts.NotifySessionExpired();
            ShowAndActivate();
        }
        catch (Exception ex)
        {
            _messages.Error("Не удалось изменить статус", ex.Message);
        }
    }

    private void UpdateTrayTooltip()
    {
        var account = _accounts.ActiveAccount?.Title;
        var status = _accounts.Profile?.Status.Title();
        _tray.SetTooltip(account is null ? "GTA Journal" : status is null ? $"GTA Journal — {account}" : $"GTA Journal — {account}: {status}");
    }

    private void OnMessageClosed(InfoBar sender, object args)
    {
        if (sender.DataContext is AppMessage message) _messages.Dismiss(message);
    }

    private void CenterOnScreen()
    {
        var area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
        AppWindow.Move(new(area.X + (area.Width - AppWindow.Size.Width) / 2, area.Y + (area.Height - AppWindow.Size.Height) / 2));
    }

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hwnd);
}
