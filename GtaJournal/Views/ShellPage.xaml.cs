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

    private void OnAccountButtonClick(object sender, RoutedEventArgs e)
    {
        var flyout = new MenuFlyout { Placement = Microsoft.UI.Xaml.Controls.Primitives.FlyoutPlacementMode.TopEdgeAlignedLeft };

        foreach (var account in ViewModel.AllAccounts)
        {
            var id = account.Id;
            var item = new ToggleMenuFlyoutItem
            {
                Text = account.Title == account.Login ? account.Login : $"{account.Title} ({account.Login})",
                IsChecked = id == ViewModel.ActiveAccountId,
            };
            item.Click += async (_, _) =>
            {
                if (id != ViewModel.ActiveAccountId) await ViewModel.SwitchAccountAsync(id);
                else item.IsChecked = true;
            };
            flyout.Items.Add(item);
        }

        if (ViewModel.AllAccounts.Count > 0)
            flyout.Items.Add(new MenuFlyoutSeparator());

        flyout.Items.Add(new MenuFlyoutItem { Text = "Добавить аккаунт", Icon = new FontIcon { Glyph = "" }, Command = ViewModel.AddAccountCommand });
        flyout.Items.Add(new MenuFlyoutItem { Text = "Управление аккаунтами", Icon = new FontIcon { Glyph = "" }, Command = ViewModel.ManageAccountsCommand });
        if (ViewModel.ActiveAccountId is not null)
        {
            flyout.Items.Add(new MenuFlyoutSeparator());
            flyout.Items.Add(new MenuFlyoutItem { Text = "Выйти из аккаунта", Icon = new FontIcon { Glyph = "\uE7E8" }, Command = ViewModel.LogoutCommand });
        }

        flyout.ShowAt(AccountButton);
    }
}
