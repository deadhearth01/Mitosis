using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Mitosis.Domain;
using Mitosis.Windows.ViewModels;

namespace Mitosis.Windows.App.Views;

/// <summary>Dialogs for metadata-only profile editing and removal. Every action is
/// restricted to <see cref="CloneProfileState.Planned"/> records; no application files,
/// logins, or data are touched. Failures are shown in place (edit) or after the dialog
/// closes (remove), so the user never loses input to a silent failure.</summary>
internal static class ProfileDialogs
{
    public static async Task EditAsync(XamlRoot? xamlRoot, MainViewModel viewModel, CloneProfile profile)
    {
        if (xamlRoot == null)
        {
            return;
        }

        if (!MainViewModel.CanModifyProfile(profile))
        {
            await ShowMessageAsync(xamlRoot, "Profile unavailable",
                "This profile has left the planning stage. Managing it needs the clone engine, which is not implemented yet. Nothing was changed.");
            return;
        }

        var nameBox = new TextBox
        {
            Header = "Name",
            Text = profile.Name,
            MaxLength = CloneName.MaxLength,
        };
        var badgeBox = new TextBox
        {
            Header = "Badge",
            Text = profile.BadgeText ?? string.Empty,
            MaxLength = 2,
        };
        var colors = BadgePreset.Presets
            .Select(p => new ComboBoxItem { Content = p.Name, Tag = p.Hex })
            .ToList();
        var colorBox = new ComboBox
        {
            Header = "Badge color",
            ItemsSource = colors,
        };
        colorBox.SelectedItem = colors.FirstOrDefault(c =>
            string.Equals(c.Tag as string, profile.BadgeColor, StringComparison.OrdinalIgnoreCase)) ?? colors[0];

        var errorBar = new InfoBar
        {
            IsOpen = false,
            IsClosable = false,
            Severity = InfoBarSeverity.Error,
        };

        var content = new StackPanel { Spacing = 12 };
        content.Children.Add(nameBox);
        content.Children.Add(badgeBox);
        content.Children.Add(colorBox);
        content.Children.Add(errorBar);

        var dialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = "Edit Profile",
            Content = content,
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
        };
        dialog.Opened += (_, _) => nameBox.Focus(FocusState.Programmatic);
        dialog.PrimaryButtonClick += async (_, args) =>
        {
            var deferral = args.GetDeferral();
            try
            {
                var color = (colorBox.SelectedItem as ComboBoxItem)?.Tag as string;
                var result = await viewModel.UpdateProfileAsync(
                    profile.Id, nameBox.Text, badgeBox.Text, color);
                if (!result.Success)
                {
                    args.Cancel = true;
                    errorBar.Message = result.Error ?? "Nothing was changed.";
                    errorBar.IsOpen = true;
                }
            }
            finally
            {
                deferral.Complete();
            }
        };

        await dialog.ShowAsync();
    }

    public static async Task RemoveAsync(XamlRoot? xamlRoot, MainViewModel viewModel, CloneProfile profile)
    {
        if (xamlRoot == null)
        {
            return;
        }

        if (!MainViewModel.CanModifyProfile(profile))
        {
            await ShowMessageAsync(xamlRoot, "Profile unavailable",
                "This profile has left the planning stage. Removing it needs the clone engine, which is not implemented yet. Nothing was changed.");
            return;
        }

        var dialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = "Remove Profile?",
            Content = new TextBlock
            {
                Text = $"Remove \"{profile.Name}\"? This deletes the saved metadata record only. "
                    + "No application files, logins, or data are affected. "
                    + "This profile has never been provisioned.",
                TextWrapping = TextWrapping.Wrap,
            },
            PrimaryButtonText = "Remove",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
        };

        ProfileOperationResult? result = null;
        dialog.PrimaryButtonClick += async (_, args) =>
        {
            var deferral = args.GetDeferral();
            try
            {
                result = await viewModel.RemoveProfileAsync(profile.Id);
            }
            finally
            {
                deferral.Complete();
            }
        };

        await dialog.ShowAsync();

        if (result is { Success: false })
        {
            await ShowMessageAsync(xamlRoot, "Couldn't remove profile",
                result.Error ?? "Nothing was changed.");
        }
    }

    public static async Task ShowMessageAsync(XamlRoot xamlRoot, string title, string message)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = title,
            Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
            CloseButtonText = "OK",
            DefaultButton = ContentDialogButton.Close,
        };
        await dialog.ShowAsync();
    }
}
