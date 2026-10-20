using System.ComponentModel;
using GtaJournal.Models;
using GtaJournal.Services.Journal;

namespace GtaJournal.Services;

/// <summary>Ключи <see cref="QueryCache"/> — в одном месте, чтобы разные страницы делили одни данные.</summary>
public static class QueryKeys
{
    public const string Dashboard = "dashboard";
    public const string Top = "top";
    public static string Statistics(MonthOption? month) => $"statistics:{month?.Value}";
    public static string UserStatistics(long? userId, MonthOption? month) => $"user-statistics:{userId}:{month?.Value}";
}

/// <summary>
/// Кэш ответов журнала в духе stale-while-revalidate (как TanStack Query): страница сразу показывает
/// последние полученные данные, а свежие подгружает в фоне. Одинаковые параллельные запросы
/// объединяются в один. Кэш принадлежит активному аккаунту и сбрасывается при его смене.
/// Используется только из UI-потока.
/// </summary>
public sealed class QueryCache
{
    private sealed record Entry(object Data, DateTimeOffset UpdatedAt);

    private readonly AccountService _accounts;
    private readonly Dictionary<string, Entry> _entries = [];
    private readonly Dictionary<string, Task> _inFlight = [];
    private int _generation;

    public QueryCache(AccountService accounts)
    {
        _accounts = accounts;
        _accounts.PropertyChanged += OnAccountsChanged;
    }

    public bool TryGet<T>(string key, out T data)
    {
        if (_entries.TryGetValue(key, out var entry) && entry.Data is T typed)
        {
            data = typed;
            return true;
        }
        data = default!;
        return false;
    }

    /// <summary>Когда данные по ключу были получены с сайта.</summary>
    public DateTimeOffset? UpdatedAt(string key) => _entries.TryGetValue(key, out var entry) ? entry.UpdatedAt : null;

    /// <summary>Загружает данные с сайта и кладёт в кэш; если такой запрос уже идёт — ждёт его.</summary>
    public Task<T> FetchAsync<T>(string key, Func<JournalClient, Task<T>> fetch)
    {
        if (_inFlight.TryGetValue(key, out var running) && running is Task<T> shared)
            return shared;

        var task = FetchCoreAsync(key, fetch);
        _inFlight[key] = task;
        return task;
    }

    private async Task<T> FetchCoreAsync<T>(string key, Func<JournalClient, Task<T>> fetch)
    {
        var generation = _generation;
        var client = _accounts.Client ?? throw new SessionExpiredException();
        try
        {
            var data = await fetch(client);
            // Пока грузили, аккаунт сменился — ответ относится к старой сессии.
            if (generation == _generation)
                _entries[key] = new Entry(data!, DateTimeOffset.Now);
            return data;
        }
        finally
        {
            if (generation == _generation)
                _inFlight.Remove(key);
        }
    }

    public void Clear()
    {
        _generation++;
        _entries.Clear();
        _inFlight.Clear();
    }

    private void OnAccountsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AccountService.ActiveAccount))
            Clear();
    }
}
