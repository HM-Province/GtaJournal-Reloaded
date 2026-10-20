using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GtaJournal.Controls;

/// <summary>
/// Строка в стиле «Параметров» Windows: иконка, заголовок с описанием и элемент управления справа.
/// Шаблон — в Styles/Common.xaml.
/// </summary>
public sealed partial class SettingsCard : ContentControl
{
    public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register(
        nameof(Header), typeof(string), typeof(SettingsCard), new PropertyMetadata(""));

    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(
        nameof(Description), typeof(string), typeof(SettingsCard),
        new PropertyMetadata("", (d, e) => ((SettingsCard)d).DescriptionVisibility = ToVisibility(e.NewValue)));

    public static readonly DependencyProperty GlyphProperty = DependencyProperty.Register(
        nameof(Glyph), typeof(string), typeof(SettingsCard),
        new PropertyMetadata("", (d, e) => ((SettingsCard)d).GlyphVisibility = ToVisibility(e.NewValue)));

    public static readonly DependencyProperty DescriptionVisibilityProperty = DependencyProperty.Register(
        nameof(DescriptionVisibility), typeof(Visibility), typeof(SettingsCard), new PropertyMetadata(Microsoft.UI.Xaml.Visibility.Collapsed));

    public static readonly DependencyProperty GlyphVisibilityProperty = DependencyProperty.Register(
        nameof(GlyphVisibility), typeof(Visibility), typeof(SettingsCard), new PropertyMetadata(Microsoft.UI.Xaml.Visibility.Collapsed));

    public string Header { get => (string)GetValue(HeaderProperty); set => SetValue(HeaderProperty, value); }
    public string Description { get => (string)GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
    public string Glyph { get => (string)GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }

    public Visibility DescriptionVisibility { get => (Visibility)GetValue(DescriptionVisibilityProperty); private set => SetValue(DescriptionVisibilityProperty, value); }
    public Visibility GlyphVisibility { get => (Visibility)GetValue(GlyphVisibilityProperty); private set => SetValue(GlyphVisibilityProperty, value); }

    private static Visibility ToVisibility(object? value) =>
        string.IsNullOrEmpty(value as string) ? Microsoft.UI.Xaml.Visibility.Collapsed : Microsoft.UI.Xaml.Visibility.Visible;
}
