using System.Diagnostics.CodeAnalysis;
using Microsoft.UI.Xaml.Controls;

namespace GtaJournal.Core.Navigation;

/// <summary>Где отображается страница.</summary>
public enum RouteHost
{
    /// <summary>Во весь размер окна (например, вход).</summary>
    Root,
    /// <summary>Внутри оболочки с боковым меню.</summary>
    Shell,
}

/// <summary>Куда поместить пункт в боковом меню.</summary>
public enum MenuPlacement
{
    Hidden,
    Main,
    Footer,
}

public sealed record RouteDefinition
{
    public required string Key { get; init; }

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
    public required Type PageType { get; init; }

    public RouteHost Host { get; init; } = RouteHost.Shell;
    public string? Title { get; init; }
    /// <summary>Глиф Segoe Fluent Icons.</summary>
    public string? Glyph { get; init; }
    public MenuPlacement Menu { get; init; } = MenuPlacement.Hidden;
    public int Order { get; init; }
    /// <summary>Нужен активный аккаунт с сессией.</summary>
    public bool RequiresSession { get; init; } = true;
    /// <summary>Только для администраторов журнала.</summary>
    public bool RequiresAdmin { get; init; }
    /// <summary>Ключ пункта меню, который подсвечивать (для скрытых страниц, например «изменить пользователя»).</summary>
    public string? MenuParent { get; init; }
}

/// <summary>
/// Реестр всех страниц приложения. Новая страница = одна регистрация в <see cref="AppRoutes"/>:
/// меню, гарды и навигация подхватят её автоматически.
/// </summary>
public sealed class RouteRegistry
{
    private readonly Dictionary<string, RouteDefinition> _routes = new(StringComparer.OrdinalIgnoreCase);

    public RouteRegistry Add<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] TPage>(
        string key, Func<RouteDefinition, RouteDefinition>? configure = null) where TPage : Page
    {
        var route = new RouteDefinition { Key = key, PageType = typeof(TPage) };
        route = configure?.Invoke(route) ?? route;
        if (!_routes.TryAdd(key, route))
            throw new InvalidOperationException($"Маршрут '{key}' уже зарегистрирован");
        return this;
    }

    public RouteDefinition Get(string key) =>
        _routes.TryGetValue(key, out var route) ? route : throw new KeyNotFoundException($"Маршрут '{key}' не зарегистрирован");

    public RouteDefinition? FindByPage(Type pageType) => _routes.Values.FirstOrDefault(r => r.PageType == pageType);

    public IEnumerable<RouteDefinition> Menu(MenuPlacement placement) =>
        _routes.Values.Where(r => r.Menu == placement).OrderBy(r => r.Order);
}
