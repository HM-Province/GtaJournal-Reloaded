using System.Collections.Concurrent;
using System.Runtime.InteropServices.WindowsRuntime;
using GtaJournal.Data.Repositories;
using GtaJournal.Models;
using GtaJournal.Services.Journal;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;

namespace GtaJournal.Services;

/// <summary>
/// Загружает аватарки через HTTP-клиент активного аккаунта. BitmapImage(Uri) ходил бы в сеть
/// с системным User-Agent (MSIE/Trident) — это выдавало бы приложение и ломало «свой UA на аккаунт».
/// Ссылки на аватарки пользователей с главной хранятся локально: в топе и меню аккаунтов их нет.
/// </summary>
public sealed class AvatarService(AccountService accounts, UserAvatarRepository repository)
{
    private readonly ConcurrentDictionary<string, Task<byte[]?>> _cache = new();
    private readonly Lazy<ConcurrentDictionary<string, string>> _urls =
        new(() => new ConcurrentDictionary<string, string>(repository.GetAll(), StringComparer.OrdinalIgnoreCase));

    /// <summary>Последняя известная ссылка на аватарку по нику (тег не учитывается).</summary>
    public string? UrlFor(string? nickname)
    {
        if (string.IsNullOrWhiteSpace(nickname)) return null;
        return _urls.Value.TryGetValue(Nickname.Split(nickname).Name, out var url) ? url : null;
    }

    /// <summary>Аватарка по нику из сохранённых ссылок.</summary>
    public Task<ImageSource?> LoadForAsync(string? nickname) => LoadAsync(UrlFor(nickname));

    /// <summary>Запоминает ссылки на аватарки; в базу пишутся только изменившиеся.</summary>
    public async Task RememberAsync(IEnumerable<(string Username, string? AvatarUrl)> users)
    {
        var changed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (username, url) in users)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(url)) continue;
            if (_urls.Value.TryGetValue(username, out var known) && known == url) continue;
            _urls.Value[username] = url;
            changed[username] = url;
        }

        try { await repository.UpsertManyAsync(changed); }
        catch (Exception ex) { ErrorLog.Write(ex); } // это только кэш — не мешаем основной работе
    }

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
