using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace GtaJournal.Core.Navigation;

/// <summary>
/// Базовая страница: передаёт параметр маршрута во ViewModel (если она <see cref="INavigationAware"/>).
/// В наследнике достаточно переопределить <see cref="NavigationTarget"/>.
/// </summary>
public abstract partial class RoutedPage : Page
{
    protected virtual object? NavigationTarget => null;

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        var parameter = e.Parameter is RouteArgs args ? args.Parameter : e.Parameter;
        if (NavigationTarget is INavigationAware aware)
            await aware.OnNavigatedToAsync(parameter);
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        (NavigationTarget as INavigationAware)?.OnNavigatedFrom();
    }
}
