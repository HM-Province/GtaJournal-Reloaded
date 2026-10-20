using GtaJournal.Core.Navigation;
using GtaJournal.ViewModels;

namespace GtaJournal.Views;

public sealed partial class EditUserPage : RoutedPage
{
    public EditUserPage()
    {
        ViewModel = App.GetService<EditUserViewModel>();
        InitializeComponent();
    }

    public EditUserViewModel ViewModel { get; }

    protected override object? NavigationTarget => ViewModel;
}
