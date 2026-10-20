using GtaJournal.Core.Navigation;
using GtaJournal.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GtaJournal.Views;

public sealed partial class SettingsPage : RoutedPage
{
    public SettingsPage()
    {
        ViewModel = App.GetService<SettingsViewModel>();
        InitializeComponent();
    }

    public SettingsViewModel ViewModel { get; }

    protected override object? NavigationTarget => ViewModel;

    private void OnProcessTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            ViewModel.UpdateProcessSuggestions(sender.Text);
    }

    private void OnProcessFocus(object sender, RoutedEventArgs e) => ViewModel.UpdateProcessSuggestions("");

    private void OnRemoveTagClick(object sender, RoutedEventArgs e) =>
        ViewModel.RemoveTagCommand.Execute((sender as FrameworkElement)?.Tag as UnitTagEditor);
}
