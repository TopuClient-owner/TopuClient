using System;
using System.Windows;
using System.Windows.Controls;

namespace TopuLauncher
{
    public partial class MainWindow
    {
        static MainWindow()
        {
            EventManager.RegisterClassHandler(
                typeof(MainWindow),
                Button.ClickEvent,
                new RoutedEventHandler(MainWindow_ButtonClick),
                true);
        }

        private static void MainWindow_ButtonClick(object sender, RoutedEventArgs e)
        {
            if (sender is not MainWindow window)
                return;

            if (e.OriginalSource is not Button button)
                return;

            if (!string.Equals(button.Content?.ToString(), "Save Profile Settings", StringComparison.OrdinalIgnoreCase))
                return;

            e.Handled = true;
            window.SaveCurrentProfileSettingsWithoutReloading();
        }

        private void SaveCurrentProfileSettingsWithoutReloading()
        {
            try
            {
                string profileName = GetActiveProfileName();
                string normalizedProfile = NormalizeProfileName(profileName);
                string version = GetSelectedVersion();
                int ram = Math.Clamp((int)Math.Round(RamSlider.Value), 2, 12);
                string loader = GetSelectedLoaderName();

                // IMPORTANT: Do NOT call SetActiveProfile here.
                // SetActiveProfile reloads the previous values from disk before saving,
                // which was why the UI reverted to Fabric 1.21.1 / 4GB.
                SaveProfileSettings(
                    _gamePath,
                    new ProfileSettings
                    {
                        Version = version,
                        RamGb = ram
                    });

                SaveLoaderState();

                if (SelectedProfileLabel != null)
                {
                    SelectedProfileLabel.Text =
                        $"● {GetDisplayProfileName(normalizedProfile)}   •   {loader} {version}   •   {ram}GB RAM";
                }

                if (LaunchProfileLabel != null)
                    LaunchProfileLabel.Text = GetDisplayProfileName(normalizedProfile);

                if (LaunchVersionLabel != null)
                    LaunchVersionLabel.Text = version;

                if (LaunchRamLabel != null)
                    LaunchRamLabel.Text = $"{ram}GB RAM";

                if (StatusText != null)
                    StatusText.Text = $"Profile saved: {GetDisplayProfileName(normalizedProfile)}";

                WriteLog($"Profile saved without reload: {normalizedProfile}");
                WriteLog($"Profile Minecraft directory: {_gamePath}");
                WriteLog($"Profile loader: {loader}");
                WriteLog($"Profile version: {version}");
                WriteLog($"Profile RAM: {ram}GB");
            }
            catch (Exception ex)
            {
                WriteException("PROFILE SAVE ERROR", ex);
                MessageBox.Show(
                    "Could not save the profile.\n\n" + ex.Message,
                    "Topu Client",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}
