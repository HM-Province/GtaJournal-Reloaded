using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GtaJournal.Services.Http;

public sealed record StoredCookie(string Name, string Value, string Domain, string Path, DateTime? Expires, bool Secure, bool HttpOnly);

[JsonSerializable(typeof(List<StoredCookie>))]
internal sealed partial class CookieJsonContext : JsonSerializerContext;

/// <summary>Сериализация CookieContainer в JSON для хранения в БД.</summary>
public static class CookieStore
{
    public static CookieContainer Load(string? json)
    {
        var container = new CookieContainer();
        if (string.IsNullOrWhiteSpace(json)) return container;

        try
        {
            foreach (var c in JsonSerializer.Deserialize(json, CookieJsonContext.Default.ListStoredCookie) ?? [])
            {
                if (c.Expires is { } exp && exp <= DateTime.UtcNow) continue;
                var cookie = new Cookie(c.Name, c.Value, c.Path, c.Domain) { Secure = c.Secure, HttpOnly = c.HttpOnly };
                if (c.Expires is { } e) cookie.Expires = e;
                container.Add(cookie);
            }
        }
        catch (Exception ex) when (ex is JsonException or CookieException)
        {
            // Повреждённые cookie — просто начинаем с пустой сессии.
        }
        return container;
    }

    public static string Save(CookieContainer container)
    {
        var cookies = container.GetAllCookies()
            .Where(c => !c.Expired)
            .Select(c => new StoredCookie(c.Name, c.Value, c.Domain, c.Path,
                c.Expires == DateTime.MinValue ? null : c.Expires.ToUniversalTime(), c.Secure, c.HttpOnly))
            .ToList();
        return JsonSerializer.Serialize(cookies, CookieJsonContext.Default.ListStoredCookie);
    }
}
