using GtaJournal.Models;
using GtaJournal.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GtaJournal.Controls;

/// <summary>Данные только что созданного пользователя с кнопками копирования.</summary>
public sealed partial class UserCreatedDialog : ContentDialog
{
    private int _feedbackVersion;

    public UserCreatedDialog(NewUser user)
    {
        Login = user.Login;
        Password = user.Password;
        Bank = user.Bank;
        Rank = user.Role;
        InitializeComponent();
    }

    public string Login { get; }
    public string Password { get; }
    public string Bank { get; }
    public string Rank { get; }

    public string Credentials => $"Логин: {Login}\nПароль: {Password}";

    private void OnCopyAllClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        args.Cancel = true; // диалог остаётся открытым — закрывается кнопкой «Готово»
        Copy(Credentials);
    }

    private void OnCopyClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string value } button)
        {
            Copy(value);
            _ = FlashCheckAsync(button);
        }
    }

    private void Copy(string text)
    {
        DialogService.CopyToClipboard(text);
        _ = ShowCopiedAsync();
    }

    private async Task ShowCopiedAsync()
    {
        var version = ++_feedbackVersion;
        CopiedText.Opacity = 1;
        await Task.Delay(1800);
        if (version == _feedbackVersion) CopiedText.Opacity = 0;
    }

    private static async Task FlashCheckAsync(Button button)
    {
        if (button.Content is not FontIcon icon) return;
        icon.Glyph = "";
        await Task.Delay(1500);
        icon.Glyph = "";
    }
}
