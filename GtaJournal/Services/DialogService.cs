using GtaJournal.Controls;
using GtaJournal.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;

namespace GtaJournal.Services;

public sealed class DialogService
{
    private XamlRoot? _xamlRoot;

    public void Attach(XamlRoot root) => _xamlRoot = root;

    /// <summary>Диалог живёт в отдельном слое и сам тему окна не наследует.</summary>
    private ElementTheme RootTheme => (_xamlRoot?.Content as FrameworkElement)?.ActualTheme ?? ElementTheme.Default;

    public async Task<bool> ConfirmAsync(string title, string message, string confirm, string cancel = "Отмена", bool destructive = false)
    {
        if (_xamlRoot is null) return false;
        var dialog = new ContentDialog
        {
            XamlRoot = _xamlRoot,
            RequestedTheme = RootTheme,
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
            RequestedTheme = RootTheme,
            Title = title,
            Content = box,
            PrimaryButtonText = confirm,
            CloseButtonText = "Отмена",
            DefaultButton = ContentDialogButton.Primary,
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary ? box.Text : null;
    }

    /// <summary>Окно с данными нового пользователя и кнопками копирования.</summary>
    public async Task ShowUserCreatedAsync(NewUser user)
    {
        if (_xamlRoot is null) return;
        var dialog = new UserCreatedDialog(user) { XamlRoot = _xamlRoot, RequestedTheme = RootTheme };
        await dialog.ShowAsync();
    }

    /// <summary>
    /// Выполняет <paramref name="work"/>, показывая диалог с полосой прогресса и кнопкой «Отмена».
    /// Через переданный колбэк работа сообщает долю (0..1, null — неизвестно) и строку состояния.
    /// Закрытие диалога отменяет токен — работа завершится <see cref="OperationCanceledException"/>.
    /// </summary>
    public async Task<T> RunWithProgressAsync<T>(string title, Func<Action<double?, string>, CancellationToken, Task<T>> work)
    {
        if (_xamlRoot is null) throw new InvalidOperationException("Окно приложения ещё не готово");

        using var cts = new CancellationTokenSource();
        var bar = new ProgressBar { IsIndeterminate = true, Minimum = 0, Maximum = 1 };
        var status = new TextBlock { Text = "Подготовка…", TextWrapping = TextWrapping.Wrap };
        var dialog = new ContentDialog
        {
            XamlRoot = _xamlRoot,
            RequestedTheme = RootTheme,
            Title = title,
            Content = new StackPanel { Spacing = 12, MinWidth = 360, Children = { bar, status } },
            CloseButtonText = "Отмена",
        };

        var finished = false;
        dialog.Closing += (_, _) =>
        {
            if (!finished) cts.Cancel();
        };
        _ = dialog.ShowAsync();

        try
        {
            return await work((fraction, text) =>
            {
                bar.IsIndeterminate = fraction is null;
                if (fraction is { } f) bar.Value = Math.Clamp(f, 0, 1);
                status.Text = text;
            }, cts.Token);
        }
        finally
        {
            finished = true;
            dialog.Hide();
        }
    }

    public static void CopyToClipboard(string text)
    {
        var package = new DataPackage();
        package.SetText(text);
        Clipboard.SetContent(package);
    }
}
