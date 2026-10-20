using GtaJournal.Core.Navigation;
using GtaJournal.ViewModels;

namespace GtaJournal.Views;

public sealed partial class StatsPage : RoutedPage
{
    public StatsPage()
    {
        ViewModel = App.GetService<StatsViewModel>();
        InitializeComponent();
    }

    public StatsViewModel ViewModel { get; }

    protected override object? NavigationTarget => ViewModel;

    public string ProgressText(double value) => $"{Math.Round(value)}%";
}
