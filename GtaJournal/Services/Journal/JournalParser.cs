using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using GtaJournal.Models;

namespace GtaJournal.Services.Journal;

/// <summary>Разбор HTML-страниц журнала.</summary>
public static partial class JournalParser
{
    private static readonly HtmlParser Parser = new();

    [GeneratedRegex(@"id=(\d+)")]
    private static partial Regex IdRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    public static DashboardData ParseDashboard(string html, Uri baseUri)
    {
        using var doc = Parser.ParseDocument(html);

        // .action-status есть только у залогиненного пользователя.
        var actionStatus = doc.QuerySelector(".action-status") ?? throw new SessionExpiredException();
        var activeClass = actionStatus.QuerySelector(".active")?.ClassList.FirstOrDefault(c => c != "active");

        var profileElement = doc.QuerySelector(".profile");
        var (tag, name) = Nickname.Split(Text(doc.QuerySelector("p.username")));
        var profile = new JournalProfile(
            name,
            tag,
            AbsoluteUrl(doc.QuerySelector(".profile div.avatar img")?.GetAttribute("src"), baseUri),
            ParseStatusClass(activeClass),
            profileElement?.InnerHtml.Contains("/user/add", StringComparison.Ordinal) == true);

        // Колонки: онлайн, АФК, оффлайн — в этом порядке.
        var columns = doc.QuerySelectorAll("main .col-12.col-lg-4");
        IReadOnlyList<JournalUser> Column(int index, JournalStatus status) =>
            index < columns.Length ? columns[index].QuerySelectorAll(".item").Select(i => ParseUserItem(i, status, baseUri)).ToList() : [];

        return new DashboardData(profile, Column(0, JournalStatus.Online), Column(1, JournalStatus.Afk), Column(2, JournalStatus.Offline));
    }

    private static JournalUser ParseUserItem(IElement item, JournalStatus status, Uri baseUri)
    {
        var link = item.QuerySelector(".username");
        var (tag, name) = Nickname.Split(Text(link));
        var note = Text(item.QuerySelector(".tag"));
        // Скрипт сайта отправляет "undefined", если тег статуса не выбран.
        if (note is "undefined" or "null") note = "";
        return new JournalUser(
            ParseId(link?.GetAttribute("href")),
            name,
            tag,
            string.IsNullOrEmpty(note) ? null : note,
            AbsoluteUrl(item.QuerySelector("img")?.GetAttribute("src"), baseUri),
            item.QuerySelector("span.admin") is not null,
            status);
    }

    public static IReadOnlyList<TopEntry> ParseTop(string html)
    {
        using var doc = Parser.ParseDocument(html);
        var table = doc.QuerySelector("table.table") ?? throw new JournalException("Не найдена таблица топа");

        // Строки: <th>место</th><td>ник</td><td>время</td><td>обновлено</td>; шапка — только <th>.
        var result = new List<TopEntry>();
        foreach (var row in table.QuerySelectorAll("tr"))
        {
            var cells = row.QuerySelectorAll("td");
            if (cells.Length == 0) continue;
            var (tag, name) = Nickname.Split(Text(cells[0]));
            var place = int.TryParse(Text(row.QuerySelector("th")), out var p) ? p : result.Count + 1;
            var rawTime = cells.Length > 1 ? Text(cells[1]) : "";
            result.Add(new TopEntry(place, name, tag, OnlineTime.Parse(rawTime), rawTime,
                cells.Length > 2 ? Text(cells[2]) : "—"));
        }
        return result.OrderBy(r => r.Place).ToList();
    }

    public static StatisticsData ParseStatistics(string html, MonthOption? requested)
    {
        using var doc = Parser.ParseDocument(html);
        var widget = doc.QuerySelector("div.box-widget") ?? throw new JournalException("Не найден блок статистики");

        var date = widget.QuerySelector("input[name=last_k]")?.GetAttribute("value") ?? "—";
        var seconds = double.TryParse(widget.QuerySelector("input[name=last_v]")?.GetAttribute("value"), out var s) ? s : 0;
        // Норма — aria-valuemax прогресс-бара (сейчас 3 часа), по умолчанию 3 часа.
        var norm = double.TryParse(widget.QuerySelector("[role=progressbar]")?.GetAttribute("aria-valuemax"), out var n) && n > 0 ? n : 10800;
        // Скрипт сайта ограничивает норму 10800 с, даже если aria-valuemax больше.
        norm = Math.Min(norm, 10800);

        var month = MonthOption.TryParseTitle(Text(doc.QuerySelector("h2.page-title-month"))) ?? requested ?? MonthOption.Current;
        var calendar = ParseCalendar(doc, month) ?? throw new JournalException("Не найден календарь онлайна");
        return new StatisticsData(date, TimeSpan.FromSeconds(seconds), TimeSpan.FromSeconds(norm), calendar);
    }

    public static UserStatisticsData ParseUserStatistics(string html, MonthOption? requested)
    {
        using var doc = Parser.ParseDocument(html);

        var users = new List<UserOption>();
        long? selected = null;
        foreach (var option in doc.QuerySelector("select[name=user]")?.QuerySelectorAll("option") ?? Enumerable.Empty<IElement>())
        {
            if (!long.TryParse(option.GetAttribute("value"), out var id) || id == 0) continue;
            users.Add(new UserOption(id, Text(option)));
            if (option.HasAttribute("selected")) selected = id;
        }

        // Без выбранного пользователя сайт не показывает ни календарь, ни сведения.
        string? Value(string selector) => doc.QuerySelector(selector) is { } e ? (e is IHtmlInputElement input ? input.Value : Text(e)) : null;
        return new UserStatisticsData(
            users,
            selected,
            Value("input.bank-number"),
            Value("span.zp15"),
            Value("span.zp30"),
            ParseCalendar(doc, requested ?? MonthOption.Current));
    }
    public static bool HasUserForm(string html)
    {
        using var doc = Parser.ParseDocument(html);
        return doc.GetElementById("bank") is not null;
    }

    public static EditableUser? ParseEditableUser(string html)
    {
        using var doc = Parser.ParseDocument(html);
        if (doc.GetElementById("bank") is not IHtmlInputElement bank) return null;

        string SelectedOption(string id) =>
            (doc.GetElementById(id)?.QuerySelector("option[selected]") ?? doc.GetElementById(id)?.QuerySelector("option"))
                ?.GetAttribute("value") ?? "";

        return new EditableUser(
            (doc.GetElementById("login") as IHtmlInputElement)?.Value ?? "",
            bank.Value ?? "",
            SelectedOption("role"),
            (doc.GetElementById("admin") as IHtmlInputElement)?.IsChecked == true,
            JournalStatusExtensions.FromAdminCode(SelectedOption("status")));
    }

    private static CalendarData? ParseCalendar(IDocument doc, MonthOption month)
    {
        if (doc.QuerySelector("table.calendar") is not { } table) return null;
        var weeks = table.QuerySelectorAll("tr")
            .Where(row => row.QuerySelector("td.calendar-day-head") is null)
            .Select(row => (IReadOnlyList<CalendarCell>)row.QuerySelectorAll("td").Select(ParseCalendarCell).ToList())
            .Where(row => row.Count > 0)
            .ToList();
        return new CalendarData(month, weeks);
    }

    private static CalendarCell ParseCalendarCell(IElement td)
    {
        var status = td.QuerySelector(".day-status");
        var kind = status is null ? CalendarCellKind.None
            : status.ClassList.Contains("status-err") ? CalendarCellKind.Error
            : status.ClassList.Contains("status-success") ? CalendarCellKind.Success
            : status.ClassList.Contains("status-text") ? CalendarCellKind.Normal
            : CalendarCellKind.Inactive;
        var day = int.TryParse(Text(td.QuerySelector(".day-number")), out var d) ? d : (int?)null;
        return new CalendarCell(day, status is null ? null : OnlineTime.Parse(Text(status)), kind);
    }
    private static JournalStatus ParseStatusClass(string? cls) => cls switch
    {
        "online" => JournalStatus.Online,
        "afk" => JournalStatus.Afk,
        _ => JournalStatus.Offline,
    };

    private static long? ParseId(string? href)
    {
        var match = href is null ? null : IdRegex().Match(href);
        return match is { Success: true } && long.TryParse(match.Groups[1].Value, out var id) ? id : null;
    }

    private static string? AbsoluteUrl(string? src, Uri baseUri) =>
        string.IsNullOrWhiteSpace(src) ? null : new Uri(baseUri, src).ToString();

    private static string Text(IElement? element) =>
        element is null ? "" : WhitespaceRegex().Replace(element.TextContent, " ").Trim();
}
