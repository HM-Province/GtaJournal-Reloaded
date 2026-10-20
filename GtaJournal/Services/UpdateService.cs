using System.Diagnostics;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace GtaJournal.Services;

/// <summary>Сведения о релизе с установщиком под текущую архитектуру.</summary>
public sealed record AppRelease(Version Version, string Notes, string InstallerName, Uri InstallerUrl, long Size, string? Sha256);

/// <summary>
/// Автообновление: последний релиз на GitHub → скачанный установщик Inno Setup запускается в тихом режиме,
/// приложение закрывается, установщик дожидается выхода, ставит новую версию и запускает её.
/// </summary>
public sealed partial class UpdateService(MessageService messages, DialogService dialogs)
{
    private const string Repository = "HM-Province/GtaJournal-Reloaded";
    private const string LatestReleaseApi = $"https://api.github.com/repos/{Repository}/releases/latest";

    /// <summary>Ключи установщика: окно с прогрессом без вопросов, перезагрузку не предлагать.</summary>
    private const string InstallerArguments = "/SILENT /SUPPRESSMSGBOXES /NORESTART";

    private static readonly TimeSpan ApiTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ProgressInterval = TimeSpan.FromMilliseconds(250);

    // Порядок важен: клиент берёт версию в User-Agent.
    public static Version CurrentVersion { get; } = Normalize(Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(1, 0, 0));

    private static readonly HttpClient Http = CreateClient();

    private bool _busy;
    private Version? _declined;

    private static string Architecture => RuntimeInformation.ProcessArchitecture switch
    {
        System.Runtime.InteropServices.Architecture.X86 => "x86",
        System.Runtime.InteropServices.Architecture.Arm64 => "arm64",
        _ => "x64",
    };

    /// <summary>
    /// Проверяет наличие новой версии и предлагает обновиться.
    /// <paramref name="interactive"/> — проверка по кнопке: сообщаем и об отсутствии обновлений, и об ошибках.
    /// </summary>
    public async Task CheckAsync(bool interactive)
    {
        if (_busy) return;
        _busy = true;
        // Отмену загрузки из диалога отличаем от таймаута запроса к GitHub.
        var downloading = false;
        try
        {
            var release = await GetLatestReleaseAsync();
            if (release is null || release.Version <= CurrentVersion)
            {
                if (interactive) messages.Success("Обновлений нет", $"Установлена последняя версия {CurrentVersion.ToString(3)}");
                return;
            }
            // Автоматически не надоедаем версией, от которой уже отказались.
            if (!interactive && release.Version == _declined) return;

            var plainNotes = PlainNotes(release.Notes);
            var notes = plainNotes.Length == 0 ? "" : $"\n\nЧто нового:\n{Truncate(plainNotes, 600)}";
            if (!await dialogs.ConfirmAsync("Доступно обновление",
                    $"Вышла версия {release.Version.ToString(3)} (у вас {CurrentVersion.ToString(3)}). " +
                    $"Установить сейчас? Приложение перезапустится.{notes}",
                    "Обновить", "Позже"))
            {
                _declined = release.Version;
                return;
            }

            downloading = true;
            var installer = await dialogs.RunWithProgressAsync($"Загрузка версии {release.Version.ToString(3)}",
                (report, ct) => DownloadAsync(release, report, ct));

            Process.Start(new ProcessStartInfo(installer, InstallerArguments) { UseShellExecute = true });
            App.Current.ExitApplication();
        }
        catch (OperationCanceledException) when (downloading)
        {
            messages.Info("Обновление отменено", "Его можно установить позже: Настройки → Проверить обновления");
        }
        catch (Exception ex)
        {
            ErrorLog.Write(ex);
            if (interactive) messages.Error("Не удалось обновить приложение", ex.Message);
        }
        finally
        {
            _busy = false;
        }
    }

    private static async Task<AppRelease?> GetLatestReleaseAsync()
    {
        using var timeout = new CancellationTokenSource(ApiTimeout);
        using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseApi);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        using var response = await Http.SendAsync(request, timeout.Token);
        // 404 — релизов ещё нет.
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();

        using var json = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(timeout.Token));
        var root = json.RootElement;
        var tag = root.GetProperty("tag_name").GetString() ?? "";
        if (!Version.TryParse(tag.TrimStart('v', 'V'), out var version)) return null;

        // Установщик: GtaJournal-Setup-<версия>-<архитектура>.exe
        var suffix = $"-{Architecture}.exe";
        foreach (var asset in root.GetProperty("assets").EnumerateArray())
        {
            var name = asset.GetProperty("name").GetString() ?? "";
            if (!name.StartsWith("GtaJournal-Setup-", StringComparison.OrdinalIgnoreCase) || !name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                continue;

            var digest = asset.TryGetProperty("digest", out var d) ? d.GetString() : null;
            return new AppRelease(
                Normalize(version),
                root.TryGetProperty("body", out var body) ? body.GetString() ?? "" : "",
                name,
                new Uri(asset.GetProperty("browser_download_url").GetString()!),
                asset.GetProperty("size").GetInt64(),
                digest?.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) == true ? digest["sha256:".Length..] : null);
        }
        return null;
    }

    /// <summary>Скачивает установщик, сообщая через <paramref name="report"/> объём, скорость и оставшееся время.</summary>
    private static async Task<string> DownloadAsync(AppRelease release, Action<double?, string> report, CancellationToken ct)
    {
        var folder = Path.Combine(Path.GetTempPath(), "GtaJournal-Update");
        if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, Path.GetFileName(release.InstallerName));

        report(null, "Подключение…");
        using (var response = await Http.GetAsync(release.InstallerUrl, HttpCompletionOption.ResponseHeadersRead, ct))
        {
            response.EnsureSuccessStatusCode();
            var total = response.Content.Headers.ContentLength ?? release.Size;

            await using var source = await response.Content.ReadAsStreamAsync(ct);
            await using var file = File.Create(path);
            var buffer = new byte[81920];
            var received = 0L;
            var clock = Stopwatch.StartNew();
            var lastReport = TimeSpan.Zero;
            var lastReceived = 0L;
            double? speed = null;

            int read;
            while ((read = await source.ReadAsync(buffer, ct)) > 0)
            {
                await file.WriteAsync(buffer.AsMemory(0, read), ct);
                received += read;

                // Обновляем диалог 4 раза в секунду; скорость сглаживаем, чтобы цифры не прыгали.
                var elapsed = clock.Elapsed - lastReport;
                if (elapsed < ProgressInterval && received < total) continue;
                var current = (received - lastReceived) / Math.Max(elapsed.TotalSeconds, 0.001);
                speed = speed is { } s ? s * 0.7 + current * 0.3 : current;
                lastReport = clock.Elapsed;
                lastReceived = received;
                report(total > 0 ? (double)received / total : null, ProgressText(received, total, speed.Value));
            }
        }

        report(null, "Проверка файла…");
        var info = new FileInfo(path);
        if (info.Length != release.Size)
            throw new InvalidOperationException("Установщик скачался не полностью, попробуйте ещё раз");

        if (release.Sha256 is { } expected)
        {
            await using var stream = File.OpenRead(path);
            var actual = Convert.ToHexString(await SHA256.HashDataAsync(stream, ct));
            if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Контрольная сумма установщика не совпадает");
        }

        report(1, "Запуск установки…");
        return path;
    }

    /// <summary>«12,3 из 85,0 МБ · 2,4 МБ/с · осталось ~30 с»</summary>
    private static string ProgressText(long received, long total, double bytesPerSecond)
    {
        var text = total > 0 ? $"{FormatSize(received)} из {FormatSize(total)}" : FormatSize(received);
        if (bytesPerSecond < 1) return text;
        text += $" · {FormatSize((long)bytesPerSecond)}/с";
        if (total > received)
            text += $" · осталось ~{FormatDuration(TimeSpan.FromSeconds((total - received) / bytesPerSecond))}";
        return text;
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        >= 1024 * 1024 => $"{bytes / 1024d / 1024:0.0} МБ",
        >= 1024 => $"{bytes / 1024d:0} КБ",
        _ => $"{bytes} Б",
    };

    private static string FormatDuration(TimeSpan time) => time.TotalSeconds switch
    {
        < 60 => $"{Math.Max(1, (int)Math.Ceiling(time.TotalSeconds))} с",
        < 3600 => $"{(int)time.TotalMinutes} мин {time.Seconds} с",
        _ => $"{(int)time.TotalHours} ч {time.Minutes} мин",
    };

    private static HttpClient CreateClient()
    {
        // Без общего таймаута: на медленном интернете загрузка установщика может идти дольше любого лимита.
        // Запрос к API ограничен ApiTimeout, загрузку можно отменить из диалога.
        var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        // GitHub API требует User-Agent.
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("GtaJournal", CurrentVersion.ToString(3)));
        return client;
    }

    /// <summary>1.2 / 1.2.0 / 1.2.0.0 → 1.2.0, иначе 1.2.0 &lt; 1.2.0.0 при сравнении.</summary>
    private static Version Normalize(Version v) => new(v.Major, v.Minor, Math.Max(v.Build, 0));

    /// <summary>
    /// Описание релиза, сгенерированное GitHub из pull request'ов, — простым текстом для диалога:
    /// «* Название by @автор in https://…/pull/12» → «• Название»; заголовки, «Full Changelog» и новые участники убираются.
    /// </summary>
    private static string PlainNotes(string notes) => string.Join('\n', notes
        .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
        .Where(line => !line.StartsWith('#')
                       && !line.Contains("Full Changelog", StringComparison.OrdinalIgnoreCase)
                       && !line.Contains("made their first contribution", StringComparison.OrdinalIgnoreCase))
        .Select(line => PullRequestLine().Replace(line, "• $1")));

    [GeneratedRegex(@"^[*-]\s+(.+?)(?:\s+by\s+@\S+)?(?:\s+in\s+https://\S+)?$")]
    private static partial Regex PullRequestLine();

    private static string Truncate(string text, int max) => text.Length <= max ? text : text[..max].TrimEnd() + "…";
}
