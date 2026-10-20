using GtaJournal.Core.Navigation;
using GtaJournal.ViewModels;

namespace GtaJournal.Views;

public sealed partial class TopPage : RoutedPage
{
    public TopPage()
    {
        ViewModel = App.GetService<TopViewModel>();
        InitializeComponent();
    }

    public TopViewModel ViewModel { get; }

    protected override object? NavigationTarget => ViewModel;
}
