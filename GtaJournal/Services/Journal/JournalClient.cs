using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using GtaJournal.Models;

namespace GtaJournal.Services.Journal;

/// <summary>Сессия истекла или не была установлена — нужен повторный вход.</summary>
public sealed class SessionExpiredException() : Exception("Сессия истекла, войдите заново");

/// <summary>Ошибка общения с сайтом журнала (сеть, неожиданный ответ).</summary>
public sealed class JournalException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// HTTP-клиент журнала для одного аккаунта: свой CookieContainer, свой User-Agent,
/// заголовки как у настоящего браузера.
/// </summary>
public sealed class JournalClient : IDisposable
{
    private const string AcceptLanguage = "ru-RU,ru;q=0.9,en-US;q=0.8,en;q=0.7";
    private const string AcceptDocument = "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8";

    private readonly HttpClient _http;
    private readonly CookieContainer _cookies;
    private readonly Uri _baseUri;
    private readonly string _userAgent;
    private readonly string? _clientHints;
    private readonly Func<string, Task>? _onCookiesChanged;
    private string _lastCookiesJson;

    public JournalClient(Uri baseUri, string userAgent, string? clientHints, string? cookiesJson, Func<string, Task>? onCookiesChanged = null)
    {
        _baseUri = baseUri;
        _userAgent = userAgent;
        _clientHints = clientHints;
        _onCookiesChanged = onCookiesChanged;
        _cookies = Http.CookieStore.Load(cookiesJson);
        _lastCookiesJson = Http.CookieStore.Save(_cookies);

        _http = new HttpClient(new SocketsHttpHandler
        {
            CookieContainer = _cookies,
            UseCookies = true,
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.All,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        })
        {
            BaseAddress = baseUri,
            Timeout = TimeSpan.FromSeconds(30),
        };
    }

    public Uri BaseUri => _baseUri;

    public CookieContainer Cookies => _cookies;

    public bool HasSession => _cookies.GetCookies(_baseUri).Count > 0;

    public Uri Absolute(string relative) => new(_baseUri, relative);

    // ---------- Авторизация ----------

    public async Task<LoginResult> LoginAsync(string login, string password, CancellationToken ct = default)
    {
        var json = await PostAsync("api.login", [("login", login), ("password", password)], "login", ct);
        var res = ReadRes(json);
        var pending = ReadString(json, "pending");
        return res switch
        {
            0 => new LoginResult(LoginOutcome.InvalidCredentials, Message: "Неверный логин или пароль"),
            1 => new LoginResult(LoginOutcome.Success),
            2 when pending is not null => new LoginResult(LoginOutcome.TotpRequired, pending),
            3 when pending is not null => new LoginResult(LoginOutcome.TotpSetupRequired, pending),
            _ => new LoginResult(LoginOutcome.Error, Message: "Сайт вернул неожиданный ответ"),
        };
    }

    /// <summary>Первичная настройка 2FA: возвращает otpauth:// URI с секретом.</summary>
    public async Task<string?> TotpSetupAsync(string pending, CancellationToken ct = default)
    {
        var json = await PostAsync("api.totp_setup", [("pending", pending)], "login", ct);
        return ReadRes(json) == 1 ? ReadString(json, "uri") : null;
    }

    public async Task<bool> LoginTotpAsync(string pending, string code, CancellationToken ct = default)
    {
        var json = await PostAsync("api.login_totp", [("pending", pending), ("code", code)], "login", ct);
        return ReadRes(json) == 1;
    }

    public async Task LogoutAsync(CancellationToken ct = default)
    {
        try { await GetPageAsync("logout", "dashboard", allowRedirect: true, ct); }
        catch (Exception ex) when (ex is JournalException or SessionExpiredException) { }
        foreach (Cookie cookie in _cookies.GetCookies(_baseUri))
            cookie.Expired = true;
        await PersistCookiesAsync();
    }

    // ---------- Страницы ----------

    public async Task<DashboardData> GetDashboardAsync(CancellationToken ct = default) =>
        JournalParser.ParseDashboard(await GetPageAsync("dashboard", "dashboard", ct: ct), _baseUri);

    public async Task<IReadOnlyList<TopEntry>> GetTopAsync(CancellationToken ct = default) =>
        JournalParser.ParseTop(await GetPageAsync("top", "dashboard", ct: ct));

    public async Task<StatisticsData> GetStatisticsAsync(MonthOption? month, CancellationToken ct = default)
    {
        var url = month is null ? "statistics" : $"statistics?date={Uri.EscapeDataString(month.Value)}";
        return JournalParser.ParseStatistics(await GetPageAsync(url, "statistics", ct: ct), month);
    }

    public async Task<UserStatisticsData> GetUserStatisticsAsync(long? userId, MonthOption? month, CancellationToken ct = default)
    {
        var query = new List<string>();
        if (userId is not null) query.Add($"user={userId}");
        if (month is not null) query.Add($"date={Uri.EscapeDataString(month.Value)}");
        var url = "user/statistics" + (query.Count > 0 ? "?" + string.Join('&', query) : "");
        return JournalParser.ParseUserStatistics(await GetPageAsync(url, "user/statistics", ct: ct), month);
    }

    public async Task<bool> CanAddUsersAsync(CancellationToken ct = default) =>
        JournalParser.HasUserForm(await GetPageAsync("user/add", "dashboard", ct: ct));

    public async Task<EditableUser?> GetUserAsync(long id, CancellationToken ct = default) =>
        JournalParser.ParseEditableUser(await GetPageAsync($"user?id={id}", "dashboard", ct: ct));

    /// <summary>Картинка (аватар) — тем же клиентом, чтобы UA и cookie совпадали с остальными запросами.</summary>
    public async Task<byte[]?> GetImageAsync(string url, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        ApplyCommonHeaders(request, "dashboard");
        request.Headers.Accept.ParseAdd("image/avif,image/webp,image/apng,image/svg+xml,image/*,*/*;q=0.8");
        request.Headers.TryAddWithoutValidation("Sec-Fetch-Dest", "image");
        request.Headers.TryAddWithoutValidation("Sec-Fetch-Mode", "no-cors");
        request.Headers.TryAddWithoutValidation("Sec-Fetch-Site", "same-origin");

        using var response = await SendAsync(request, ct);
        if (!response.IsSuccessStatusCode || response.Content.Headers.ContentType?.MediaType?.StartsWith("image/") != true)
            return null;
        return await response.Content.ReadAsByteArrayAsync(ct);
    }

    // ---------- Действия ----------

    public async Task EditStatusAsync(JournalStatus status, CancellationToken ct = default) =>
        await PostAsync("api.editstatus", [("status", ((int)status).ToString())], "dashboard", ct);

    public async Task<ApiResult> SetUserStatusAsync(long userId, JournalStatus status, CancellationToken ct = default) =>
        ToResult(await PostAsync("api.user", [("id", userId.ToString()), ("status", status.ToAdminCode().ToString())], "dashboard", ct));

    public async Task<ApiResult> DeleteUserAsync(long userId, CancellationToken ct = default) =>
        ToResult(await PostAsync("api.deluser", [("id", userId.ToString())], "dashboard", ct));

    public async Task<ApiResult> AddUserAsync(NewUser user, CancellationToken ct = default) =>
        ToResult(await PostAsync("api.user/add",
            [("login", user.Login), ("password", user.Password), ("bank", user.Bank), ("vk", user.VkLink), ("role", user.Role)],
            "user/add", ct));

    public async Task<ApiResult> UpdateUserAsync(long id, EditableUser user, string? newPassword, bool updateAvatar, CancellationToken ct = default) =>
        ToResult(await PostAsync("api.user",
        [
            ("id", id.ToString()),
            ("status", user.Status.ToAdminCode().ToString()),
            ("login", user.Login),
            ("password", newPassword ?? ""),
            ("role", user.Role),
            ("bank", user.Bank),
            ("vk", updateAvatar ? "1" : "0"),
            ("admin", user.IsAdmin ? "1" : "0"),
        ], $"user?id={id}", ct));

    // ---------- Транспорт ----------

    private async Task<string> GetPageAsync(string path, string referer, bool allowRedirect = false, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        ApplyCommonHeaders(request, referer);
        request.Headers.Accept.ParseAdd(AcceptDocument);
        request.Headers.TryAddWithoutValidation("Upgrade-Insecure-Requests", "1");
        request.Headers.TryAddWithoutValidation("Sec-Fetch-Dest", "document");
        request.Headers.TryAddWithoutValidation("Sec-Fetch-Mode", "navigate");
        request.Headers.TryAddWithoutValidation("Sec-Fetch-Site", "same-origin");
        request.Headers.TryAddWithoutValidation("Sec-Fetch-User", "?1");

        using var response = await SendAsync(request, ct);
        if (IsRedirect(response.StatusCode))
        {
            if (allowRedirect) return "";
            // Неавторизованного пользователя журнал отправляет на /login (через / ).
            throw new SessionExpiredException();
        }
        if (!response.IsSuccessStatusCode)
            throw new JournalException($"Сайт ответил {(int)response.StatusCode} {response.ReasonPhrase}");
        return await response.Content.ReadAsStringAsync(ct);
    }

    private async Task<JsonElement?> PostAsync(string path, (string Key, string Value)[] form, string referer, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        ApplyCommonHeaders(request, referer);
        request.Headers.Accept.ParseAdd("*/*");
        request.Headers.TryAddWithoutValidation("X-Requested-With", "XMLHttpRequest");
        request.Headers.TryAddWithoutValidation("Origin", _baseUri.GetLeftPart(UriPartial.Authority));
        request.Headers.TryAddWithoutValidation("Sec-Fetch-Dest", "empty");
        request.Headers.TryAddWithoutValidation("Sec-Fetch-Mode", "cors");
        request.Headers.TryAddWithoutValidation("Sec-Fetch-Site", "same-origin");

        // Кодируем как jQuery (encodeURIComponent), а не '+' для пробелов.
        var body = string.Join('&', form.Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));
        request.Content = new StringContent(body, Encoding.UTF8);
        request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded; charset=UTF-8");

        using var response = await SendAsync(request, ct);
        if (IsRedirect(response.StatusCode))
            throw new SessionExpiredException();
        if (!response.IsSuccessStatusCode)
            throw new JournalException($"Сайт ответил {(int)response.StatusCode} {response.ReasonPhrase}");

        var text = await response.Content.ReadAsStringAsync(ct);
        try
        {
            using var doc = JsonDocument.Parse(text);
            return doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new JournalException($"Не удалось связаться с {_baseUri.Host}: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new JournalException($"{_baseUri.Host} не ответил вовремя", ex);
        }
        await PersistCookiesAsync();
        return response;
    }

    private void ApplyCommonHeaders(HttpRequestMessage request, string referer)
    {
        request.Headers.TryAddWithoutValidation("User-Agent", _userAgent);
        request.Headers.TryAddWithoutValidation("Accept-Language", AcceptLanguage);
        request.Headers.Referrer = new Uri(_baseUri, referer);
        if (_clientHints is not null)
        {
            request.Headers.TryAddWithoutValidation("sec-ch-ua", _clientHints);
            request.Headers.TryAddWithoutValidation("sec-ch-ua-mobile", "?0");
            request.Headers.TryAddWithoutValidation("sec-ch-ua-platform", "\"Windows\"");
        }
    }

    private async Task PersistCookiesAsync()
    {
        var json = Http.CookieStore.Save(_cookies);
        if (json == _lastCookiesJson) return;
        _lastCookiesJson = json;
        if (_onCookiesChanged is not null)
            await _onCookiesChanged(json);
    }

    private static bool IsRedirect(HttpStatusCode code) => (int)code is >= 300 and < 400;

    private static int? ReadRes(JsonElement? json)
    {
        if (json is not { ValueKind: JsonValueKind.Object } obj || !obj.TryGetProperty("res", out var res)) return null;
        return res.ValueKind switch
        {
            JsonValueKind.Number when res.TryGetInt32(out var n) => n,
            JsonValueKind.String when int.TryParse(res.GetString(), out var n) => n,
            JsonValueKind.True => 1,
            JsonValueKind.False => 0,
            _ => null,
        };
    }

    private static string? ReadString(JsonElement? json, string name) =>
        json is { ValueKind: JsonValueKind.Object } obj && obj.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;

    private static ApiResult ToResult(JsonElement? json) =>
        new(ReadRes(json) ?? -1, ReadString(json, "text"), json?.GetRawText());

    public void Dispose() => _http.Dispose();
}
