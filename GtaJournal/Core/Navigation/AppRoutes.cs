using GtaJournal.Views;

namespace GtaJournal.Core.Navigation;

/// <summary>Ключи маршрутов.</summary>
public static class Routes
{
    public const string Shell = "shell";
    public const string Login = "login";
    public const string Dashboard = "dashboard";
    public const string AddUser = "users/add";
    public const string EditUser = "users/edit";
    public const string UserStats = "users/stats";
    public const string Top = "top";
    public const string Stats = "stats";
    public const string Accounts = "accounts";
    public const string Settings = "settings";
}

/// <summary>
/// Все страницы приложения. Чтобы добавить страницу: создать Page (наследник RoutedPage) + ViewModel,
/// зарегистрировать VM в DI (App.ConfigureServices) и добавить строку сюда — пункт меню появится сам.
/// </summary>
public static class AppRoutes
{
    public static RouteRegistry Create() => new RouteRegistry()
        .Add<ShellPage>(Routes.Shell, r => r with { Host = RouteHost.Root, RequiresSession = false })
        .Add<LoginPage>(Routes.Login, r => r with { Host = RouteHost.Root, RequiresSession = false })

        .Add<DashboardPage>(Routes.Dashboard, r => r with
        {
            Title = "Панель управления", Glyph = "", Menu = MenuPlacement.Main, Order = 0,
        })
        .Add<AddUserPage>(Routes.AddUser, r => r with
        {
            Title = "Добавить пользователя", Glyph = "", Menu = MenuPlacement.Main, Order = 10, RequiresAdmin = true,
        })
        .Add<UserStatsPage>(Routes.UserStats, r => r with
        {
            Title = "Онлайн пользователей", Glyph = "", Menu = MenuPlacement.Main, Order = 15, RequiresAdmin = true,
        })
        .Add<EditUserPage>(Routes.EditUser, r => r with
        {
            Title = "Изменить пользователя", RequiresAdmin = true, MenuParent = Routes.Dashboard,
        })
        .Add<TopPage>(Routes.Top, r => r with
        {
            Title = "Топ-10", Glyph = "", Menu = MenuPlacement.Main, Order = 20,
        })
        .Add<StatsPage>(Routes.Stats, r => r with
        {
            Title = "Статистика", Glyph = "", Menu = MenuPlacement.Main, Order = 30,
        })

        .Add<AccountsPage>(Routes.Accounts, r => r with
        {
            Title = "Аккаунты", Glyph = "", Menu = MenuPlacement.Footer, Order = 0, RequiresSession = false,
        })
        .Add<SettingsPage>(Routes.Settings, r => r with
        {
            Title = "Настройки", Glyph = "", Menu = MenuPlacement.Footer, Order = 10, RequiresSession = false,
        });
}
