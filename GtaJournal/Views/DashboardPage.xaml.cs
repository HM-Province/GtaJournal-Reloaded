using GtaJournal.Core.Navigation;
using GtaJournal.Helpers;
using GtaJournal.ViewModels;
using Microsoft.UI.Xaml.Media;

namespace GtaJournal.Views;

public sealed partial class DashboardPage : RoutedPage
{
    public DashboardPage()
    {
        ViewModel = App.GetService<DashboardViewModel>();
        InitializeComponent();
    }

    public DashboardViewModel ViewModel { get; }

    protected override object? NavigationTarget => ViewModel;

    public Brush OnlineBrush { get; } = new SolidColorBrush(StatusVisuals.Online);
    public Brush AfkBrush { get; } = new SolidColorBrush(StatusVisuals.Afk);
    public Brush OfflineBrush { get; } = new SolidColorBrush(StatusVisuals.Offline);
}
