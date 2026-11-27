using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Mitosis.Windows.ViewModels;

namespace Mitosis.Windows.App.Views;

/// <summary>The provisioning-plan dry run. Draws the planner's own output — including its
/// refusals — and states in the dialog title bar, the info bar, and the closing note that
/// nothing is executed. It never calls the executor, the gate, or the launcher.</summary>
internal static class PlanDialog
{
    public static async Task ShowAsync(XamlRoot? xamlRoot, PlanPreviewViewModel preview)
    {
        ArgumentNullException.ThrowIfNull(preview);
        if (xamlRoot == null)
        {
            return;
        }

        var content = new StackPanel { Spacing = 14 };

        var banner = new InfoBar
        {
            IsOpen = true,
            IsClosable = false,
            Severity = InfoBarSeverity.Informational,
            Title = "Dry run",
            Message = preview.Banner,
        };
        AutomationProperties.SetName(banner, preview.Banner);
        content.Children.Add(banner);

        content.Children.Add(new TextBlock
        {
            Style = (Style)Microsoft.UI.Xaml.Application.Current.Resources["SubtitleTextBlockStyle"],
            Text = preview.Heading,
            TextWrapping = TextWrapping.Wrap,
        });

        content.Children.Add(new TextBlock
        {
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Microsoft.UI.Xaml.Application.Current.Resources["TextFillColorSecondaryBrush"],
            Text = preview.Summary,
            TextWrapping = TextWrapping.Wrap,
        });

        content.Children.Add(Section("Would create", new[] { preview.TargetPath }, monospace: true));

        if (preview.HasBlockingIssues)
        {
            content.Children.Add(Section("Blocking issues", preview.BlockingIssues, monospace: false));
        }

        if (preview.HasSteps)
        {
            content.Children.Add(Section(
                "Planned steps",
                preview.Steps.Select(step => $"{step.Number}. {step.Description}  ·  {step.Destination}").ToList(),
                monospace: false));
        }
        else
        {
            content.Children.Add(Section("Planned steps", new[] { preview.EmptyStepsText }, monospace: false));
        }

        if (preview.HasNotProven)
        {
            content.Children.Add(Section("Not yet proven", preview.NotProven, monospace: false));
        }

        content.Children.Add(new TextBlock
        {
            FontSize = 12,
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Microsoft.UI.Xaml.Application.Current.Resources["TextFillColorSecondaryBrush"],
            Text = "Planning is read-only: Mitosis did not create, copy, move, or delete anything, "
                + "and the profile record is unchanged.",
            TextWrapping = TextWrapping.Wrap,
        });

        var dialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = "Provisioning plan",
            Content = new ScrollViewer
            {
                MaxHeight = 460,
                Content = content,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            },
            CloseButtonText = "Close",
            DefaultButton = ContentDialogButton.Close,
        };

        await dialog.ShowAsync();
    }

    private static StackPanel Section(string title, IReadOnlyList<string> lines, bool monospace)
    {
        var section = new StackPanel { Spacing = 6 };
        section.Children.Add(new TextBlock
        {
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Text = title,
        });

        foreach (var line in lines)
        {
            var text = new TextBlock
            {
                FontSize = 13,
                IsTextSelectionEnabled = true,
                TextWrapping = TextWrapping.Wrap,
            };
            if (monospace)
            {
                text.FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas");
            }

            // Bullets are drawn with padding so the text itself stays copyable and a screen
            // reader reads the sentence, not the glyph.
            text.Text = line;
            text.Margin = new Thickness(2, 0, 0, 0);
            section.Children.Add(text);
        }

        return section;
    }
}
