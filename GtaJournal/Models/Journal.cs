using System.Text.RegularExpressions;

namespace GtaJournal.Models;

/// <summary>Статус в журнале. Числовые значения — как в api.editstatus.</summary>
public enum JournalStatus
{
    Offline = 0,
    Online = 1,
    Afk = 2,
}

public static class JournalStatusExtensions
{
    /// <summary>Код статуса в админском api.user (1 — оффлайн, 2 — онлайн, 3 — АФК).</summary>
    public static int ToAdminCode(this JournalStatus status) => status switch
    {
        JournalStatus.Online => 2,
        JournalStatus.Afk => 3,
        _ => 1,
    };

    public static JournalStatus FromAdminCode(string? code) => code switch
    {
        "2" => JournalStatus.Online,
        "3" => JournalStatus.Afk,
        _ => JournalStatus.Offline,
    };

    public static string Title(this JournalStatus status) => status switch
    {
        JournalStatus.Online => "Онлайн",
        JournalStatus.Afk => "АФК",
        _ => "Оффлайн",
    };
}

/// <summary>Тег подразделения из ника "[ТЕГ] Имя_Фамилия" (настраивается, система тегов опциональна).</summary>
public sealed record UnitTag(long Id, string Tag, string Title, string ColorHex)
{
    public Windows.UI.Color Color => ParseColor(ColorHex);

    public static Windows.UI.Color ParseColor(string? hex)
    {
        var h = (hex ?? "").TrimStart('#');
        return h.Length == 6 && uint.TryParse(h, System.Globalization.NumberStyles.HexNumber, null, out var v)
            ? Windows.UI.Color.FromArgb(255, (byte)(v >> 16), (byte)(v >> 8), (byte)v)
            : Windows.UI.Color.FromArgb(255, 0xF9, 0x73, 0x16);
    }
}

/// <summary>Ник в формате "[ТЕГ] Имя_Фамилия" (тег может отсутствовать).</summary>
public static partial class Nickname
{
    [GeneratedRegex(@"^\s*\[(?<tag>[^\]]+)\]\s*(?<name>.*)$")]
    private static partial Regex TaggedRegex();

    // Латиница, цифры и подчёркивания: Ivan_Ivanov, SV_Lover, nickname29115.
    [GeneratedRegex(@"^[A-Za-z0-9]+(?:_[A-Za-z0-9]+)*$")]
    private static partial Regex RpNameRegex();

    public static (string? Tag, string Name) Split(string raw)
    {
        var text = raw.Trim();
        var match = TaggedRegex().Match(text);
        return match.Success ? (match.Groups["tag"].Value.Trim(), match.Groups["name"].Value.Trim()) : (null, text);
    }

    /// <summary>Проверка ника для формы пользователя. С тегами — "[ТЕГ] Имя_Фамилия", без — "Имя_Фамилия".</summary>
    public static bool IsValid(string raw, bool requireTag)
    {
        var (tag, name) = Split(raw);
        if (requireTag && string.IsNullOrWhiteSpace(tag)) return false;
        return name.Length >= 3 && RpNameRegex().IsMatch(name);
    }
}

public sealed record JournalProfile(string Username, string? Tag, string? AvatarUrl, JournalStatus Status, bool IsAdmin);

public sealed record JournalUser(long? Id, string Username, string? Tag, string? Note, string? AvatarUrl, bool IsAdmin, JournalStatus Status)
{
    /// <summary>VK id из имени файла аватарки: /media/server14100/359333023_1792497092.jpg → 359333023.</summary>
    public string? VkId
    {
        get
        {
            if (AvatarUrl is null) return null;
            var file = AvatarUrl.Split('?')[0].Split('/').LastOrDefault();
            var id = file?.Split('_')[0];
            return !string.IsNullOrEmpty(id) && id.All(char.IsDigit) ? id : null;
        }
    }

    public string Mention => VkId is { } id ? $"@id{id} ({Username})" : Username;
}

public sealed record DashboardData(
    JournalProfile Profile,
    IReadOnlyList<JournalUser> Online,
    IReadOnlyList<JournalUser> Afk,
    IReadOnlyList<JournalUser> Offline);

public sealed record TopEntry(int Place, string Username, string? Tag, TimeSpan? Duration, string RawTime, string LastUpdate);

/// <summary>Длительности онлайна в формате журнала: "Ч: 15 М: 36".</summary>
public static partial class OnlineTime
{
    [GeneratedRegex(@"Ч:\s*(\d+)\s*М:\s*(\d+)")]
    private static partial Regex HoursMinutesRegex();

    public static TimeSpan? Parse(string? text)
    {
        var match = text is null ? null : HoursMinutesRegex().Match(text);
        return match is { Success: true }
            ? TimeSpan.FromHours(int.Parse(match.Groups[1].Value)) + TimeSpan.FromMinutes(int.Parse(match.Groups[2].Value))
            : null;
    }

    public static string Format(TimeSpan? value)
    {
        if (value is not { } t) return "—";
        var hours = (int)t.TotalHours;
        return hours > 0 ? $"{hours} ч {t.Minutes:00} мин" : $"{t.Minutes} мин";
    }
}

public enum CalendarCellKind { None, Normal, Success, Error, Inactive }

/// <summary>Ячейка календаря: день месяца (null — пустая ячейка или недельный итог) и онлайн за него.</summary>
public sealed record CalendarCell(int? Day, TimeSpan? Duration, CalendarCellKind Kind);

public sealed record MonthOption(int Year, int Month)
{
    private static readonly string[] Names =
        ["Январь", "Февраль", "Март", "Апрель", "Май", "Июнь", "Июль", "Август", "Сентябрь", "Октябрь", "Ноябрь", "Декабрь"];

    /// <summary>Журнал ведётся с января 2019 года.</summary>
    private static readonly DateOnly FirstMonth = new(2019, 1, 1);

    /// <summary>Значение параметра date в формате "MM,YYYY".</summary>
    public string Value => $"{Month:00},{Year}";
    public string Title => $"{Names[Month - 1]} {Year}";

    public static MonthOption Current => new(DateTime.Now.Year, DateTime.Now.Month);

    /// <summary>Все месяцы от текущего к январю 2019 — сайт строит этот список скриптом в браузере.</summary>
    public static IReadOnlyList<MonthOption> All()
    {
        var result = new List<MonthOption>();
        for (var d = new DateOnly(DateTime.Now.Year, DateTime.Now.Month, 1); d >= FirstMonth; d = d.AddMonths(-1))
            result.Add(new MonthOption(d.Year, d.Month));
        return result;
    }

    /// <summary>"Октябрь 2026" → месяц.</summary>
    public static MonthOption? TryParseTitle(string? title)
    {
        var parts = (title ?? "").Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 || !int.TryParse(parts[1], out var year)) return null;
        var index = Array.FindIndex(Names, n => n.Equals(parts[0], StringComparison.OrdinalIgnoreCase));
        return index < 0 ? null : new MonthOption(year, index + 1);
    }

    public override string ToString() => Title;
}

public sealed record CalendarSummary(TimeSpan Total, int SuccessDays, int NormalDays, int MissedDays);

public sealed record CalendarData(MonthOption Month, IReadOnlyList<IReadOnlyList<CalendarCell>> Weeks)
{
    /// <summary>Дни месяца (без пустых ячеек и недельных итогов).</summary>
    public IEnumerable<CalendarCell> Days => Weeks.SelectMany(w => w.Take(7)).Where(c => c.Day is not null);

    public CalendarSummary Summary => new(
        Days.Aggregate(TimeSpan.Zero, (sum, d) => sum + (d.Duration ?? TimeSpan.Zero)),
        Days.Count(d => d.Kind == CalendarCellKind.Success),
        Days.Count(d => d.Kind == CalendarCellKind.Normal),
        Days.Count(d => d.Kind == CalendarCellKind.Error));
}

public sealed record StatisticsData(string DayDate, TimeSpan DayOnline, TimeSpan DailyNorm, CalendarData Calendar);

public sealed record UserOption(long Id, string Name)
{
    public override string ToString() => Name;
}

/// <summary>Статистика пользователя. Календарь и сведения есть, только когда пользователь выбран.</summary>
public sealed record UserStatisticsData(
    IReadOnlyList<UserOption> Users,
    long? SelectedUserId,
    string? Bank,
    string? FirstHalf,
    string? SecondHalf,
    CalendarData? Calendar);
public sealed record EditableUser(string Login, string Bank, string Role, bool IsAdmin, JournalStatus Status);

public sealed record NewUser(string Login, string Password, string Bank, string VkLink, string Role);

public enum LoginOutcome { Success, InvalidCredentials, TotpRequired, TotpSetupRequired, Error }

public sealed record LoginResult(LoginOutcome Outcome, string? Pending = null, string? Message = null);

/// <summary>Ответ JSON-эндпоинтов вида {"res":N,"text":"..."}.</summary>
public sealed record ApiResult(int Res, string? Text, string? Raw)
{
    public bool IsSuccess => Res == 1;
}
