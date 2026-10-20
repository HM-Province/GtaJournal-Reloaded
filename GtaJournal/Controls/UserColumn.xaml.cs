using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace GtaJournal.Controls;

/// <summary>Колонка пользователей на главной: заголовок со счётчиком, список, заглушки загрузки и пустое состояние.</summary>
public sealed partial class UserColumn : UserControl
{
    public static readonly DependencyProperty TitleProperty = Register(nameof(Title), typeof(string), "");
    public static readonly DependencyProperty CountTextProperty = Register(nameof(CountText), typeof(string), "0");
    public static readonly DependencyProperty AccentProperty = Register(nameof(Accent), typeof(Brush), null);
    public static readonly DependencyProperty GlyphProperty = Register(nameof(Glyph), typeof(string), "");
    public static readonly DependencyProperty ItemsSourceProperty = Register(nameof(ItemsSource), typeof(object), null);
    public static readonly DependencyProperty IsEmptyProperty = Register(nameof(IsEmpty), typeof(bool), false);
    public static readonly DependencyProperty EmptyTextProperty = Register(nameof(EmptyText), typeof(string), "");
    public static readonly DependencyProperty EmptyGlyphProperty = Register(nameof(EmptyGlyph), typeof(string), "");

    public static readonly DependencyProperty IsLoadingProperty = DependencyProperty.Register(
        nameof(IsLoading), typeof(bool), typeof(UserColumn),
        new PropertyMetadata(false, (d, e) => ((UserColumn)d).UpdatePulse((bool)e.NewValue)));

    public UserColumn()
    {
        InitializeComponent();
        Loaded += (_, _) => UpdatePulse(IsLoading);
        Unloaded += (_, _) => PulseStoryboard.Stop();
    }

    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string CountText { get => (string)GetValue(CountTextProperty); set => SetValue(CountTextProperty, value); }
    public Brush? Accent { get => (Brush?)GetValue(AccentProperty); set => SetValue(AccentProperty, value); }
    public string Glyph { get => (string)GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }
    public object? ItemsSource { get => GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }
    public bool IsEmpty { get => (bool)GetValue(IsEmptyProperty); set => SetValue(IsEmptyProperty, value); }
    public string EmptyText { get => (string)GetValue(EmptyTextProperty); set => SetValue(EmptyTextProperty, value); }
    public string EmptyGlyph { get => (string)GetValue(EmptyGlyphProperty); set => SetValue(EmptyGlyphProperty, value); }
    public bool IsLoading { get => (bool)GetValue(IsLoadingProperty); set => SetValue(IsLoadingProperty, value); }

    private void UpdatePulse(bool loading)
    {
        if (loading) PulseStoryboard.Begin();
        else PulseStoryboard.Stop();
    }

    private static DependencyProperty Register(string name, Type type, object? defaultValue) =>
        DependencyProperty.Register(name, type, typeof(UserColumn), new PropertyMetadata(defaultValue));
}
