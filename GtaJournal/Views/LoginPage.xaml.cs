using GtaJournal.Core.Navigation;
using GtaJournal.ViewModels;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace GtaJournal.Views;

public sealed partial class LoginPage : RoutedPage
{
    public LoginPage()
    {
        ViewModel = App.GetService<LoginViewModel>();
        InitializeComponent();
    }

    public LoginViewModel ViewModel { get; }

    protected override object? NavigationTarget => ViewModel;

    /// <summary>0 — хранить секрет, 1 — не хранить.</summary>
    public int SecretChoiceIndex
    {
        get => ViewModel.StoreSecret ? 0 : 1;
        set => ViewModel.StoreSecret = value == 0;
    }

    private void OnInputKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter && ViewModel.SubmitCommand.CanExecute(null))
        {
            e.Handled = true;
            ViewModel.SubmitCommand.Execute(null);
        }
    }
}
