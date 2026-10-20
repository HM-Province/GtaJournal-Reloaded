using GtaJournal.Core.Navigation;
using GtaJournal.ViewModels;

namespace GtaJournal.Views;

public sealed partial class AccountsPage : RoutedPage
{
    public AccountsPage()
    {
        ViewModel = App.GetService<AccountsViewModel>();
        InitializeComponent();
    }

    public AccountsViewModel ViewModel { get; }

    protected override object? NavigationTarget => ViewModel;
}
