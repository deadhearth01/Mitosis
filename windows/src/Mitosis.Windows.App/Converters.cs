using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Mitosis.Domain;
using Mitosis.Windows.ViewModels;
using Windows.UI;

namespace Mitosis.Windows.App;

/// <summary>XAML value converters. All map macOS status/display rules from
/// MainViewModel into WinUI brushes, glyphs, text, and visibility.</summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is null ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

public sealed class HexToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is string hex && hex.Length == 7 && hex[0] == '#'
            && uint.TryParse(hex.AsSpan(1), System.Globalization.NumberStyles.HexNumber, null, out uint rgb))
        {
            return new SolidColorBrush(Color.FromArgb(
                0xFF, (byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb));
        }

        return new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

/// <summary>Status dot / pill tint. macOS pairs every color with a symbol and text;
/// the symbol and text come from StatusToSymbolConverter / status text.</summary>
public sealed class StatusToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is CloneItem clone)
        {
            if (clone.BusyText != null)
            {
                return Microsoft.UI.Xaml.Application.Current.Resources["MitosisAccentBrush"];
            }

            return clone.Status switch
            {
                CloneStatusKind.OriginalMissing => new SolidColorBrush(Colors.Red),
                CloneStatusKind.UpdateAvailable => new SolidColorBrush(Colors.Orange),
                _ => clone.IsRunning
                    ? new SolidColorBrush(Colors.Green)
                    : new SolidColorBrush(Colors.Gray),
            };
        }

        return new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

/// <summary>SF Symbol → Segoe Fluent Icons mapping (provisional; see design spec §7).
/// Only Symbol-enum members are used so invalid glyphs fail the build, never silently.</summary>
public sealed class StatusToSymbolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is CloneItem clone)
        {
            return clone.Status switch
            {
                CloneStatusKind.OriginalMissing => Symbol.Important,
                CloneStatusKind.UpdateAvailable => Symbol.Refresh,
                _ => clone.IsRunning ? Symbol.Play : Symbol.Pause,
            };
        }

        return Symbol.Help;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

public sealed class CloneToStatusTextConverter : IValueConverter{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is CloneItem clone
            ? clone.BusyText ?? MainViewModel.StatusText(clone)
            : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

public sealed class CloneToSubtitleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is CloneItem clone ? $"Clone of {clone.AppName} {clone.SourceVersion}" : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

public sealed class RunningToActionTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is CloneItem { IsRunning: true } ? "Show" : "Open";

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

public sealed class SupportToSymbolConverter : IValueConverter{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is SupportKind support ? support switch
        {
            SupportKind.Full => Symbol.Accept,
            SupportKind.Limited => Symbol.Important,
            _ => Symbol.Clear,
        } : Symbol.Help;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

public sealed class ProfileSubtitleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is CloneProfile profile ? MainViewModel.ProfileSubtitleText(profile) : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

/// <summary>Loads decorative mascot content ("ms-appx:///Assets/pose-*.png") into an Image.
/// Used by Help pages and onboarding; the images stay hidden from the accessibility tree.</summary>
public sealed class StringToImageSourceConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is string uri && uri.StartsWith("ms-appx:///", StringComparison.OrdinalIgnoreCase)
            ? new BitmapImage(new Uri(uri))
            : null!;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

/// <summary>Help topic glyph mapping (provisional SF Symbol → Segoe Fluent Icons; the
/// design spec keeps this provisional until screenshots). Unknown symbols fall back to
/// Info so a mapping edit can never render an empty control.</summary>
public sealed class HelpSymbolToGlyphConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is HelpSymbol symbol ? symbol switch
        {
            HelpSymbol.Sparkles => "\uEA80",        // Lightbulb
            HelpSymbol.CheckmarkSeal => "\uE73E",   // CheckMark
            HelpSymbol.LockShield => "\uE72E",      // Lock
            HelpSymbol.Link => "\uE71E",            // Link
            HelpSymbol.CyclingArrows => "\uE895",   // Sync
            HelpSymbol.BarChart => "\uEA9C",        // AreaChart
            HelpSymbol.Trash => "\uE74D",           // Delete
            HelpSymbol.WrenchScrewdriver => "\uE90F", // Repair
            HelpSymbol.Terminal => "\uE756",        // CommandPrompt
            _ => "\uE946",
        } : "\uE946";

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
