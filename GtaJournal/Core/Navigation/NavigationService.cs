using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;

namespace GtaJournal.Core.Navigation;

/// <summary>Аргумент, который получает страница: ключ маршрута и параметр.</summary>
public sealed record RouteArgs(string Key, object? Parameter);

/// <summary>Страница/VM, которой нужно знать о переходах.</summary>
public interface INavigationAware
{
    Task OnNavigatedToAsync(object? parameter);
    void OnNavigatedFrom() { }
}

/// <summary>Решение гарда: null — пускать, иначе — куда перенаправить.</summary>
public delegate (string Key, object? Parameter)? RouteGuard(RouteDefinition route, object? parameter);

/// <summary>
/// Навигация по ключам маршрутов. Знает про два фрейма: корневой (окно) и фрейм оболочки.
/// Переход на Shell-маршрут сам поднимет оболочку, если она ещё не показана.
/// </summary>
public sealed class NavigationService(RouteRegistry registry)
{
    private Frame? _rootFrame;
    private Frame? _shellFrame;
    private string? _shellRouteKey;

    public RouteRegistry Registry => registry;

    /// <summary>Гарды выполняются по порядку; первый, вернувший перенаправление, побеждает.</summary>
    public List<RouteGuard> Guards { get; } = [];

    public RouteDefinition? Current { get; private set; }

    public event Action<RouteDefinition>? Navigated;

    public bool CanGoBack => _shellFrame?.CanGoBack == true;

    public void AttachRoot(Frame frame, string shellRouteKey)
    {
        _rootFrame = frame;
        _shellRouteKey = shellRouteKey;
    }

    /// <summary>Вызывается оболочкой из конструктора.</summary>
    public void AttachShell(Frame frame)
    {
        if (_shellFrame is not null)
            _shellFrame.Navigated -= OnShellNavigated;
        _shellFrame = frame;
        _shellFrame.Navigated += OnShellNavigated;
    }

    public bool Navigate(string key, object? parameter = null, bool clearHistory = false)
    {
        var route = registry.Get(key);

        foreach (var guard in Guards)
        {
            if (guard(route, parameter) is { } redirect)
            {
                if (string.Equals(redirect.Key, key, StringComparison.OrdinalIgnoreCase)) break;
                return Navigate(redirect.Key, redirect.Parameter, clearHistory: true);
            }
        }

        var args = new RouteArgs(key, parameter);
        if (route.Host == RouteHost.Root)
        {
            _shellFrame = null;
            var ok = Root.Navigate(route.PageType, args, new DrillInNavigationTransitionInfo());
            Root.BackStack.Clear();
            if (ok) SetCurrent(route);
            return ok;
        }

        EnsureShell();
        var shell = _shellFrame ?? throw new InvalidOperationException("Оболочка не зарегистрировала свой фрейм");
        var result = shell.Navigate(route.PageType, args, new EntranceNavigationTransitionInfo());
        if (clearHistory) shell.BackStack.Clear();
        return result;
    }

    public void GoBack()
    {
        if (_shellFrame?.CanGoBack == true)
            _shellFrame.GoBack();
    }

    /// <summary>Повторно открыть текущую страницу (например, после смены аккаунта).</summary>
    public void Reload()
    {
        if (Current is { } current)
            Navigate(current.Key, null, clearHistory: true);
    }

    private Frame Root => _rootFrame ?? throw new InvalidOperationException("Корневой фрейм не подключён");

    private void EnsureShell()
    {
        if (_shellFrame is not null && Root.Content?.GetType() == registry.Get(_shellRouteKey!).PageType)
            return;
        Root.Navigate(registry.Get(_shellRouteKey!).PageType, new RouteArgs(_shellRouteKey!, null), new DrillInNavigationTransitionInfo());
        Root.BackStack.Clear();
    }

    private void OnShellNavigated(object sender, Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        var route = e.Parameter is RouteArgs args ? registry.Get(args.Key) : registry.FindByPage(e.SourcePageType);
        if (route is not null) SetCurrent(route);
    }

    private void SetCurrent(RouteDefinition route)
    {
        Current = route;
        Navigated?.Invoke(route);
    }
}
