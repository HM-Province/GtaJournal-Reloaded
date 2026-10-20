using System.Security.Cryptography;

namespace GtaJournal.Services.Http;

public sealed record BrowserIdentity(string UserAgent, string? ClientHints);

/// <summary>
/// Генерирует правдоподобный десктопный User-Agent (Windows) с актуальной версией браузера,
/// вычисленной от даты по 4-недельному релизному циклу. Для Chromium-браузеров дополнительно
/// формируется sec-ch-ua по тому же алгоритму GREASE, что и в Chromium.
/// </summary>
public static class UserAgentGenerator
{
    // Опорные релизы: Chrome 131 — 12.11.2024, Firefox 133 — 26.11.2024. Обе линейки выходят раз в 4 недели.
    private static readonly DateTime ChromeAnchorDate = new(2024, 11, 12);
    private const int ChromeAnchorVersion = 131;
    private static readonly DateTime FirefoxAnchorDate = new(2024, 11, 26);
    private const int FirefoxAnchorVersion = 133;
    private const int ReleaseCycleDays = 28;

    private const string WindowsPlatform = "Windows NT 10.0; Win64; x64";

    private enum Browser { Chrome, Edge, Yandex, Firefox }

    // Примерное распределение браузеров у русскоязычных пользователей Windows.
    private static readonly (Browser Browser, int Weight)[] Weights =
    [
        (Browser.Chrome, 55),
        (Browser.Yandex, 20),
        (Browser.Edge, 15),
        (Browser.Firefox, 10),
    ];

    public static BrowserIdentity Generate(DateTime? now = null)
    {
        var today = (now ?? DateTime.UtcNow).Date;
        var browser = PickBrowser();
        // Пользователи не всегда на самой свежей версии: отстаём на 0–2 релиза.
        var lag = RandomNumberGenerator.GetInt32(0, 3);

        if (browser == Browser.Firefox)
        {
            var ff = VersionAt(FirefoxAnchorVersion, FirefoxAnchorDate, today) - lag;
            return new($"Mozilla/5.0 ({WindowsPlatform}; rv:{ff}.0) Gecko/20100101 Firefox/{ff}.0", null);
        }

        var major = VersionAt(ChromeAnchorVersion, ChromeAnchorDate, today) - lag;
        var chromeUa = $"Mozilla/5.0 ({WindowsPlatform}) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/{major}.0.0.0";

        switch (browser)
        {
            case Browser.Edge:
            {
                // Edge использует полную версию: 131.0.2903.51 — сборка растёт примерно на 65 за релиз.
                var build = 2903 + (major - 131) * 65 + RandomNumberGenerator.GetInt32(0, 20);
                var patch = RandomNumberGenerator.GetInt32(10, 120);
                return new($"{chromeUa} Safari/537.36 Edg/{major}.0.{build}.{patch}", ClientHints(major, "Microsoft Edge", major.ToString()));
            }
            case Browser.Yandex:
            {
                // Версия YaBrowser — год.месяц выхода; Chromium в нём отстаёт примерно на месяц. Не из будущего.
                var releaseDate = ChromeAnchorDate.AddDays((major - ChromeAnchorVersion) * ReleaseCycleDays).AddMonths(1);
                if (releaseDate > today) releaseDate = today;
                var ya = $"{releaseDate:yy}.{releaseDate.Month}";
                var yaBuild = RandomNumberGenerator.GetInt32(0, 6);
                return new($"{chromeUa} YaBrowser/{ya}.{yaBuild}.0 Safari/537.36",
                    ClientHints(major, "YaBrowser", ya) + ", \"Yowser\";v=\"2.5\"");
            }
            default:
                return new($"{chromeUa} Safari/537.36", ClientHints(major, "Google Chrome", major.ToString()));
        }
    }

    private static int VersionAt(int anchorVersion, DateTime anchorDate, DateTime today) =>
        anchorVersion + (int)Math.Floor((today - anchorDate).TotalDays / ReleaseCycleDays);

    private static Browser PickBrowser()
    {
        var roll = RandomNumberGenerator.GetInt32(0, Weights.Sum(w => w.Weight));
        foreach (var (browser, weight) in Weights)
        {
            if (roll < weight) return browser;
            roll -= weight;
        }
        return Browser.Chrome;
    }

    /// <summary>Порт GetGreasedUserAgentBrandVersion / GenerateBrandVersionList из Chromium.</summary>
    private static string ClientHints(int seed, string brand, string brandVersion)
    {
        string[] greaseyChars = [" ", "(", ":", "-", ".", "/", ")", ";", "=", "?", "_"];
        string[] greasedVersions = ["8", "99", "24"];
        int[][] orders = [[0, 1, 2], [0, 2, 1], [1, 0, 2], [1, 2, 0], [2, 0, 1], [2, 1, 0]];

        var grease = $"\"Not{greaseyChars[seed % greaseyChars.Length]}A{greaseyChars[(seed + 1) % greaseyChars.Length]}Brand\";v=\"{greasedVersions[seed % greasedVersions.Length]}\"";
        var chromium = $"\"Chromium\";v=\"{seed}\"";
        var browser = $"\"{brand}\";v=\"{brandVersion}\"";

        var order = orders[seed % orders.Length];
        var list = new string[3];
        list[order[0]] = grease;
        list[order[1]] = chromium;
        list[order[2]] = browser;
        return string.Join(", ", list);
    }
}
