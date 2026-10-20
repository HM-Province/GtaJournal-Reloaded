using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GtaJournal.Models;
using GtaJournal.Services;

namespace GtaJournal.ViewModels;

public sealed partial class StatsViewModel(ViewModelContext context) : ViewModelBase(context)
{
    public IReadOnlyList<MonthOption> Months { get; } = MonthOption.All();

    [ObservableProperty] public partial MonthOption? SelectedMonth { get; set; }
    [ObservableProperty] public partial CalendarData? Calendar { get; set; }
    [ObservableProperty] public partial CalendarSummary? Summary { get; set; }
    [ObservableProperty] public partial string DayTitle { get; set; } = "Сегодня";
    [ObservableProperty] public partial string DayTimeText { get; set; } = "—";
    [ObservableProperty] public partial string NormText { get; set; } = "";
    [ObservableProperty] public partial double DayProgress { get; set; }
    [ObservableProperty] public partial bool DayNormReached { get; set; }
    [ObservableProperty] public partial bool IsLoaded { get; set; }

    public string MonthTitle => Calendar?.Month.Title ?? SelectedMonth?.Title ?? "";

    public override Task OnNavigatedToAsync(object? parameter)
    {
        SelectedMonth = Months[0];
        return Task.CompletedTask; // загрузку запустит смена месяца
    }

    partial void OnSelectedMonthChanged(MonthOption? value) => _ = LoadAsync();

    partial void OnCalendarChanged(CalendarData? value) => OnPropertyChanged(nameof(MonthTitle));

    [RelayCommand]
    private Task LoadAsync()
    {
        // Текущий месяц запрашиваем без даты — так сайт отдаёт и онлайн за сегодня.
        var month = SelectedMonth is null || SelectedMonth == Months[0] ? null : SelectedMonth;
        return QueryAsync(QueryKeys.Statistics(month),c => c.GetStatisticsAsync(month), Apply, "Не удалось загрузить статистику");
    }

    private void Apply(StatisticsData data)
    {
        DayTitle = $"Онлайн за {data.DayDate}";
        DayTimeText = OnlineTime.Format(data.DayOnline);
        DayProgress = Math.Min(100, data.DayOnline.TotalSeconds / data.DailyNorm.TotalSeconds * 100);
        DayNormReached = data.DayOnline >= data.DailyNorm;
        NormText = DayNormReached
            ? $"Норма {OnlineTime.Format(data.DailyNorm)} выполнена"
            : $"До нормы осталось {OnlineTime.Format(data.DailyNorm - data.DayOnline)}";

        Calendar = data.Calendar;
        Summary = data.Calendar.Summary;
        IsLoaded = true;
    }
}
