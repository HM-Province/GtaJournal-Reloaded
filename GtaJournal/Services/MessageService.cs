using System.Collections.ObjectModel;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;

namespace GtaJournal.Services;

public sealed record AppMessage(string Title, string? Text, InfoBarSeverity Severity);

/// <summary>Короткие всплывающие сообщения внутри окна (аналог toast из старой версии).</summary>
public sealed class MessageService
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(4);
    private DispatcherQueue? _dispatcher;

    public ObservableCollection<AppMessage> Items { get; } = [];

    public void Attach(DispatcherQueue dispatcher) => _dispatcher = dispatcher;

    public void Info(string title, string? text = null) => Show(new(title, text, InfoBarSeverity.Informational));
    public void Success(string title, string? text = null) => Show(new(title, text, InfoBarSeverity.Success));
    public void Warning(string title, string? text = null) => Show(new(title, text, InfoBarSeverity.Warning));
    public void Error(string title, string? text = null) => Show(new(title, text, InfoBarSeverity.Error));

    public void Dismiss(AppMessage message) => Items.Remove(message);

    private void Show(AppMessage message)
    {
        if (_dispatcher is null) return;
        _dispatcher.TryEnqueue(async () =>
        {
            Items.Add(message);
            while (Items.Count > 4) Items.RemoveAt(0);
            await Task.Delay(Lifetime);
            Items.Remove(message);
        });
    }
}
