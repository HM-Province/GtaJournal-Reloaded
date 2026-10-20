using GtaJournal.Core.Navigation;
using GtaJournal.ViewModels;

namespace GtaJournal.Views;

public sealed partial class AddUserPage : RoutedPage
{
    public AddUserPage()
    {
        ViewModel = App.GetService<AddUserViewModel>();
        InitializeComponent();
    }

    public AddUserViewModel ViewModel { get; }

    protected override object? NavigationTarget => ViewModel;
}
