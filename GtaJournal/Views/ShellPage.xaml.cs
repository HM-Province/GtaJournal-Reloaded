using GtaJournal.Core.Navigation;
using GtaJournal.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GtaJournal.Views;

/// <summary>Оболочка: боковое меню (строится из <see cref="RouteRegistry"/>) + фрейм страниц.</summary>
public sealed partial class ShellPage : RoutedPage
{
    private readonly NavigationService _navigation = App.GetService<NavigationService>();
    private readonly Dictionary<string, NavigationViewItem> _items = new(StringComparer.OrdinalIgnoreCase);

    public ShellPage()
    {
        ViewModel = App.GetService<ShellViewModel>();
        InitializeComponent();

        _navigation.AttachShell(ContentFrame);
        _navigation.Navigated += OnNavigated;
        ViewModel.AdminChanged += UpdateAdminItems;
        Unloaded += (_, _) =>
        {
            _navigation.Navigated -= OnNavigated;
            ViewModel.AdminChanged -= UpdateAdminItems;
            ViewModel.Detach();
        };

        BuildMenu();
    }

    public ShellViewModel ViewModel { get; }

    protected override object? NavigationTarget => ViewModel;

    private void BuildMenu()
    {
        foreach (var route in _navigation.Registry.Menu(MenuPlacement.Main))
            NavView.MenuItems.Add(CreateItem(route));
        foreach (var route in _navigation.Registry.Menu(MenuPlacement.Footer))
            NavView.FooterMenuItems.Add(CreateItem(route));
        UpdateAdminItems();
    }

    private NavigationViewItem CreateItem(RouteDefinition route)
    {
        var item = new NavigationViewItem
        {
            Content = route.Title,
            Tag = route.Key,
            Icon = route.Glyph is null ? null : new FontIcon { Glyph = route.Glyph },
        };
        ToolTipService.SetToolTip(item, route.Title);
        _items[route.Key] = item;
        return item;
    }

    private void UpdateAdminItems()
    {
        foreach (var (key, item) in _items)
        {
            var route = _navigation.Registry.Get(key);
            item.Visibility = !route.RequiresAdmin || ViewModel.IsAdmin ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void OnItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.InvokedItemContainer?.Tag is string key && !string.Equals(key, _navigation.Current?.Key, StringComparison.OrdinalIgnoreCase))
            _navigation.Navigate(key);
    }

    private void OnBackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args) => _navigation.GoBack();

    private void OnNavigated(RouteDefinition route)
    {
        NavView.IsBackEnabled = _navigation.CanGoBack;
        var key = route.MenuParent ?? route.Key;
        NavView.SelectedItem = _items.GetValueOrDefault(key);
    }

    private void OnAccountFlyoutOpening(object sender, object e) => ViewModel.RefreshMenuAccounts();

    private async void OnAccountItemClick(object sender, RoutedEventArgs e)
    {
        AccountFlyout.Hide();
        if (sender is FrameworkElement { Tag: AccountMenuItem item } && item.Id != ViewModel.ActiveAccountId)
            await ViewModel.SwitchAccountAsync(item.Id);
    }

    private void OnAccountMenuActionClick(object sender, RoutedEventArgs e) => AccountFlyout.Hide();
}
