using GtaJournal.Core.Navigation;
using GtaJournal.Data;
using GtaJournal.Data.Migrations;
using GtaJournal.Data.Repositories;
using GtaJournal.Services;
using GtaJournal.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;

namespace GtaJournal;

public partial class App : Application
{
    private MainWindow? _window;

    public App()
    {
        InitializeComponent();
        UnhandledException += (_, e) =>
        {
            ErrorLog.Write(e.Exception);
            // До появления окна показать ошибку негде — падаем с сообщением, а не висим без окна.
            if (_window is null)
            {
                ErrorLog.ShowFatal(e.Exception);
                return;
            }
            e.Handled = true;
            GetService<MessageService>().Error("Непредвиденная ошибка", e.Exception.Message);
        };
    }

    public static new App Current => (App)Application.Current;

    public static IServiceProvider Services { get; private set; } = null!;

    public static string DatabasePath => Database.DefaultPath;

    public static T GetService<T>() where T : notnull => Services.GetRequiredService<T>();

    public MainWindow? Window => _window;

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            await StartAsync();
        }
        catch (Exception ex)
        {
            ErrorLog.Write(ex);
            ErrorLog.ShowFatal(ex);
            Exit();
        }
    }

    private async Task StartAsync()
    {
        var database = new Database(DatabasePath);
        new MigrationRunner(database).ApplyPending();
        Services = ConfigureServices(database);

        var accounts = GetService<AccountService>();
        await accounts.InitializeAsync();

        _window = new MainWindow();
        ConfigureNavigation(accounts);

        GetService<NotificationService>().Register();
        GetService<NotificationService>().Invoked += () => _window.DispatcherQueue.TryEnqueue(_window.ShowAndActivate);

        _window.Activate();

        var navigation = GetService<NavigationService>();
        navigation.Navigate(accounts.Client?.HasSession == true ? Routes.Dashboard : Routes.Login, new LoginRequest(accounts.ActiveAccount?.Id));

        GetService<StatusService>().Start();

#if !DEBUG
        _ = CheckUpdatesPeriodicallyAsync();
#endif
    }

    /// <summary>Проверка обновлений вскоре после запуска и далее раз в 6 часов (приложение подолгу живёт в трее).</summary>
    private static async Task CheckUpdatesPeriodicallyAsync()
    {
        var updates = GetService<UpdateService>();
        await Task.Delay(TimeSpan.FromSeconds(5));
        while (true)
        {
            await updates.CheckAsync(interactive: false);
            await Task.Delay(TimeSpan.FromHours(6));
        }
    }

    private static IServiceProvider ConfigureServices(Database database)
    {
        var services = new ServiceCollection();

        // Данные
        services.AddSingleton(database);
        services.AddSingleton<AccountRepository>();
        services.AddSingleton<SettingsRepository>();
        services.AddSingleton<UnitTagRepository>();
        services.AddSingleton<UserAvatarRepository>();

        // Сервисы
        services.AddSingleton<SettingsService>();
        services.AddSingleton<TagService>();
        services.AddSingleton<AccountService>();
        services.AddSingleton<QueryCache>();
        services.AddSingleton<StatusService>();
        services.AddSingleton<SoundService>();
        services.AddSingleton<NotificationService>();
        services.AddSingleton<MessageService>();
        services.AddSingleton<DialogService>();
        services.AddSingleton<AvatarService>();
        services.AddSingleton<UpdateService>();
        services.AddSingleton(AppRoutes.Create());
        services.AddSingleton<NavigationService>();
        services.AddSingleton<ViewModelContext>();

        // ViewModel'и: новая на каждый переход
        services.AddTransient<ShellViewModel>();
        services.AddTransient<LoginViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<TopViewModel>();
        services.AddTransient<StatsViewModel>();
        services.AddTransient<UserStatsViewModel>();
        services.AddTransient<AddUserViewModel>();
        services.AddTransient<EditUserViewModel>();
        services.AddTransient<AccountsViewModel>();
        services.AddTransient<SettingsViewModel>();

        return services.BuildServiceProvider();
    }

    private void ConfigureNavigation(AccountService accounts)
    {
        var navigation = GetService<NavigationService>();
        navigation.AttachRoot(_window!.RootFrame, Routes.Shell);

        // Нет активного аккаунта или его сессии — на вход.
        navigation.Guards.Add((route, _) =>
            route.RequiresSession && accounts.Client?.HasSession != true
                ? (Routes.Login, new LoginRequest(accounts.ActiveAccount?.Id))
                : null);

        // Админские страницы — только администраторам (если профиль уже известен).
        navigation.Guards.Add((route, _) =>
            route.RequiresAdmin && accounts.Profile is { IsAdmin: false } ? (Routes.Dashboard, null) : null);

        accounts.SessionExpired += account =>
            navigation.Navigate(Routes.Login, new LoginRequest(account.Id, "Сессия истекла — войдите снова"));
    }

    /// <summary>Повторный запуск exe: показываем уже открытое окно.</summary>
    internal static void OnRedirectedActivation()
    {
        if (Current._window is { } window)
            window.DispatcherQueue.TryEnqueue(window.ShowAndActivate);
    }

    /// <summary>Полный выход (из трея или если сворачивание в трей выключено).</summary>
    public void ExitApplication()
    {
        GetService<StatusService>().Dispose();
        GetService<NotificationService>().Unregister();
        GetService<SoundService>().Dispose();
        _window?.CloseForExit();
        Exit();
    }
}
