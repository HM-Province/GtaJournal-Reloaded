using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;

namespace GtaJournal.Services;

public sealed class DialogService
{
    private XamlRoot? _xamlRoot;

    public void Attach(XamlRoot root) => _xamlRoot = root;

    public async Task<bool> ConfirmAsync(string title, string message, string confirm, string cancel = "Отмена", bool destructive = false)
    {
        if (_xamlRoot is null) return false;
        var dialog = new ContentDialog
        {
            XamlRoot = _xamlRoot,
            Title = title,
            Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
            PrimaryButtonText = confirm,
            CloseButtonText = cancel,
            DefaultButton = ContentDialogButton.Close,
        };
        // Для опасных действий фокус по умолчанию на «Отмена», для обычных — на подтверждении.
        if (!destructive) dialog.DefaultButton = ContentDialogButton.Primary;
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    public async Task<string?> PromptAsync(string title, string label, string placeholder, string confirm)
    {
        if (_xamlRoot is null) return null;
        var box = new TextBox { Header = label, PlaceholderText = placeholder };
        var dialog = new ContentDialog
        {
            XamlRoot = _xamlRoot,
            Title = title,
            Content = box,
            PrimaryButtonText = confirm,
            CloseButtonText = "Отмена",
            DefaultButton = ContentDialogButton.Primary,
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary ? box.Text : null;
    }

    public static void CopyToClipboard(string text)
    {
        var package = new DataPackage();
        package.SetText(text);
        Clipboard.SetContent(package);
    }
}
