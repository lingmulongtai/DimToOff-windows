using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DimToOff.Settings.Controls;

/// <summary>
/// One row of the settings list: an icon, a title, an explanation, and the control
/// that changes the value.
/// </summary>
public sealed partial class SettingRow : UserControl
{
    public static readonly DependencyProperty GlyphProperty = DependencyProperty.Register(
        nameof(Glyph),
        typeof(string),
        typeof(SettingRow),
        new PropertyMetadata(string.Empty, OnGlyphChanged));

    public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register(
        nameof(Header),
        typeof(string),
        typeof(SettingRow),
        new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(
        nameof(Description),
        typeof(string),
        typeof(SettingRow),
        new PropertyMetadata(string.Empty, OnDescriptionChanged));

    public static readonly DependencyProperty ActionProperty = DependencyProperty.Register(
        nameof(Action),
        typeof(object),
        typeof(SettingRow),
        new PropertyMetadata(null));

    public SettingRow()
    {
        InitializeComponent();
        UpdateGlyphVisibility();
        UpdateDescriptionVisibility();
    }

    public string Glyph
    {
        get => (string)GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    public string Header
    {
        get => (string)GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public object? Action
    {
        get => GetValue(ActionProperty);
        set => SetValue(ActionProperty, value);
    }

    private static void OnGlyphChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) =>
        ((SettingRow)sender).UpdateGlyphVisibility();

    private static void OnDescriptionChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) =>
        ((SettingRow)sender).UpdateDescriptionVisibility();

    private void UpdateGlyphVisibility() =>
        RowIcon.Visibility = string.IsNullOrEmpty(Glyph) ? Visibility.Collapsed : Visibility.Visible;

    private void UpdateDescriptionVisibility() =>
        DescriptionText.Visibility = string.IsNullOrEmpty(Description) ? Visibility.Collapsed : Visibility.Visible;
}
