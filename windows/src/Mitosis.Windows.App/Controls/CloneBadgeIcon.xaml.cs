using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Mitosis.Windows.App.Controls;

public sealed partial class CloneBadgeIcon : UserControl
{
    public CloneBadgeIcon()
    {
        InitializeComponent();
    }

    public double IconSize
    {
        get => (double)GetValue(IconSizeProperty);
        set => SetValue(IconSizeProperty, value);
    }

    public static readonly DependencyProperty IconSizeProperty =
        DependencyProperty.Register(nameof(IconSize), typeof(double), typeof(CloneBadgeIcon), new PropertyMetadata(30.0));

    public string Initial
    {
        get => (string)GetValue(InitialProperty);
        set => SetValue(InitialProperty, value);
    }

    public static readonly DependencyProperty InitialProperty =
        DependencyProperty.Register(nameof(Initial), typeof(string), typeof(CloneBadgeIcon), new PropertyMetadata("?"));

    public string TintHex
    {
        get => (string)GetValue(TintHexProperty);
        set => SetValue(TintHexProperty, value);
    }

    public static readonly DependencyProperty TintHexProperty =
        DependencyProperty.Register(nameof(TintHex), typeof(string), typeof(CloneBadgeIcon), new PropertyMetadata("#8E8E93"));

    public double InitialFontSize
    {
        get => (double)GetValue(InitialFontSizeProperty);
        set => SetValue(InitialFontSizeProperty, value);
    }

    public static readonly DependencyProperty InitialFontSizeProperty =
        DependencyProperty.Register(nameof(InitialFontSize), typeof(double), typeof(CloneBadgeIcon), new PropertyMetadata(14.0));

    public CornerRadius TileCornerRadius
    {
        get => (CornerRadius)GetValue(TileCornerRadiusProperty);
        set => SetValue(TileCornerRadiusProperty, value);
    }

    public static readonly DependencyProperty TileCornerRadiusProperty =
        DependencyProperty.Register(nameof(TileCornerRadius), typeof(CornerRadius), typeof(CloneBadgeIcon), new PropertyMetadata(new CornerRadius(8)));
}
