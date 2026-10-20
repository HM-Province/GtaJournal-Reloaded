using System.Collections.Concurrent;
using System.Runtime.InteropServices.WindowsRuntime;
using GtaJournal.Services.Journal;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;

namespace GtaJournal.Services;

/// <summary>
/// Загружает аватарки через HTTP-клиент активного аккаунта. BitmapImage(Uri) ходил бы в сеть
/// с системным User-Agent (MSIE/Trident) — это выдавало бы приложение и ломало «свой UA на аккаунт».
/// </summary>
public sealed class AvatarService(AccountService accounts)
{
    private readonly ConcurrentDictionary<string, Task<byte[]?>> _cache = new();

    /// <summary>Вызывать из UI-потока (создаётся BitmapImage).</summary>
    public async Task<ImageSource?> LoadAsync(string? url)
    {
        if (string.IsNullOrEmpty(url) || accounts.Client is not { } client) return null;

        var task = _cache.GetOrAdd(url, u => DownloadAsync(client, u));
        var bytes = await task;
        if (bytes is null)
        {
            _cache.TryRemove(url, out _); // попробуем ещё раз при следующем показе
            return null;
        }

        try
        {
            using var stream = new InMemoryRandomAccessStream();
            await stream.WriteAsync(bytes.AsBuffer());
            stream.Seek(0);
            var bitmap = new BitmapImage();
            await bitmap.SetSourceAsync(stream);
            return bitmap;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static async Task<byte[]?> DownloadAsync(JournalClient client, string url)
    {
        try { return await client.GetImageAsync(url); }
        catch (Exception ex) when (ex is JournalException or SessionExpiredException or ObjectDisposedException) { return null; }
    }
}
