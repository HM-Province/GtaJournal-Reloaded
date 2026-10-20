using GtaJournal.Core.Navigation;
using GtaJournal.ViewModels;

namespace GtaJournal.Views;

public sealed partial class UserStatsPage : RoutedPage
{
    public UserStatsPage()
    {
        ViewModel = App.GetService<UserStatsViewModel>();
        InitializeComponent();
    }

    public UserStatsViewModel ViewModel { get; }

    protected override object? NavigationTarget => ViewModel;
}
