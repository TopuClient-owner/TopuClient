using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace TopuLauncher
{
    public partial class MainWindow
    {
        private bool _profileSaveFixAttached;
        private string? _capturedProfileName;
        private string? _capturedVersion;
        private int _capturedRamGb;

        static MainWindow()
        {
            EventManager.RegisterClassHandler(
                typeof(MainWindow),
                FrameworkElement.LoadedEvent,
                new RoutedEventHandler(ProfileSaveFix_OnLoaded));
        }

        private static void ProfileSaveFix_OnLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is not MainWindow window || window._profileSaveFixAttached)
                return;

            window._profileSaveFixAttached = true;

            window.AddHandler(
                UIElement.PreviewMouseLeftButtonDownEvent,
                new MouseButtonEventHandler(window.ProfileSaveFix_OnPreviewMouseDown));

            window.AddHandler(
                UIElement.PreviewKeyDownEvent,
                new KeyEventHandler(window.ProfileSaveFix_OnPreviewKeyDown));

            // The existing Forge partial restores loader state when a profile changes.
            // Run one more pass after that handler so the saved Minecraft version/RAM wins.
            window.LoadSavedProfileValuesAfterExistingHandlers();
        }

        private void ProfileSaveFix_OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!IsSaveProfileButton(e.OriginalSource as DependencyObject))
                return;

            CaptureCurrentProfileValues();
            ScheduleProfileValueRestore();
        }

        private void ProfileSaveFix_OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter && e.Key != Key.Space)
                return;

            if (e.OriginalSource is not DependencyObject source ||
                !IsSaveProfileButton(source))
                return;

            CaptureCurrentProfileValues();
            ScheduleProfileValueRestore();
        }

        private bool IsSaveProfileButton(DependencyObject? source)
        {
            DependencyObject? current = source;

            while (current != null)
            {
                if (current is Button button &&
                    string.Equals(
                        button.Content?.ToString(),
                        "Save Profile Settings",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                current = GetParent(current);
            }

            return false;
        }

        private static DependencyObject? GetParent(DependencyObject source)
        {
            if (source is Visual || source is Visual3D)
            {
                DependencyObject? visualParent = VisualTreeHelper.GetParent(source);
                if (visualParent != null)
                    return visualParent;
            }

            return LogicalTreeHelper.GetParent(source);
        }

        private void CaptureCurrentProfileValues()
        {
            _capturedProfileName = GetActiveProfileName();
            _capturedVersion = GetSelectedVersion();
            _capturedRamGb = Math.Clamp((int)Math.Round(RamSlider.Value), 2, 12);
        }

        private void ScheduleProfileValueRestore()
        {
            Dispatcher.BeginInvoke(
                DispatcherPriority.ContextIdle,
                new Action(RestoreCapturedProfileValues));
        }

        private void RestoreCapturedProfileValues()
        {
            if (string.IsNullOrWhiteSpace(_capturedProfileName) ||
                string.IsNullOrWhiteSpace(_capturedVersion))
                return;

            try
            {
                string profilePath = GetProfileGamePath(_capturedProfileName);
                Directory.CreateDirectory(profilePath);

                ComboBoxItem? versionItem = null;
                foreach (ComboBoxItem item in VersionBox.Items)
                {
                    if (string.Equals(
                        item.Content?.ToString(),
                        _capturedVersion,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        versionItem = item;
                        break;
                    }
                }

                if (versionItem == null &&
                    _capturedVersion is "1.20.1" or "1.8.9")
                {
                    versionItem = new ComboBoxItem
                    {
                        Content = _capturedVersion
                    };

                    VersionBox.Items.Add(versionItem);
                }

                if (versionItem != null)
                {
                    _loadingLoaderState = true;
                    VersionBox.SelectedItem = versionItem;
                    RamSlider.Value = _capturedRamGb;
                    _loadingLoaderState = false;
                }
                else
                {
                    RamSlider.Value = _capturedRamGb;
                }

                SaveProfileSettings(
                    profilePath,
                    new ProfileSettings
                    {
                        Version = _capturedVersion,
                        RamGb = _capturedRamGb
                    });

                _gamePath = profilePath;
                SaveRememberedProfile(_capturedProfileName);

                // Keep the loader and the version/RAM in sync after the original
                // SaveProfile_Click handler has finished its reload.
                SaveLoaderState();

                UpdateProfileCard();
                UpdateLoaderUi();

                StatusText.Text =
                    $"Profile saved: {_capturedProfileName} • {GetSelectedLoaderName()} {_capturedVersion} • {_capturedRamGb}GB RAM";

                WriteLog(
                    $"PROFILE SAVE FIX: persisted profile={_capturedProfileName}, version={_capturedVersion}, RAM={_capturedRamGb}GB, loader={GetSelectedLoaderName()}");
            }
            catch (Exception ex)
            {
                _loadingLoaderState = false;
                WriteException("PROFILE SAVE FIX ERROR", ex);
            }
            finally
            {
                _capturedProfileName = null;
                _capturedVersion = null;
                _capturedRamGb = 0;
            }
        }

        private void LoadSavedProfileValuesAfterExistingHandlers()
        {
            Dispatcher.BeginInvoke(
                DispatcherPriority.ContextIdle,
                new Action(() =>
                {
                    try
                    {
                        string profileName = GetActiveProfileName();
                        string profilePath = GetProfileGamePath(profileName);
                        string settingsPath = GetProfileSettingsPath(profilePath);

                        if (!System.IO.File.Exists(settingsPath))
                            return;

                        string json = System.IO.File.ReadAllText(settingsPath);
                        ProfileSettings? settings =
                            System.Text.Json.JsonSerializer.Deserialize<ProfileSettings>(json);

                        if (settings == null || string.IsNullOrWhiteSpace(settings.Version))
                            return;

                        ComboBoxItem? item = null;
                        foreach (ComboBoxItem candidate in VersionBox.Items)
                        {
                            if (string.Equals(
                                candidate.Content?.ToString(),
                                settings.Version,
                                StringComparison.OrdinalIgnoreCase))
                            {
                                item = candidate;
                                break;
                            }
                        }

                        if (item == null &&
                            settings.Version is "1.20.1" or "1.8.9")
                        {
                            item = new ComboBoxItem
                            {
                                Content = settings.Version
                            };
                            VersionBox.Items.Add(item);
                        }

                        _loadingLoaderState = true;

                        if (item != null)
                            VersionBox.SelectedItem = item;

                        RamSlider.Value = Math.Clamp(settings.RamGb, 2, 12);
                        _loadingLoaderState = false;

                        UpdateProfileCard();
                        UpdateLoaderUi();
                    }
                    catch (Exception ex)
                    {
                        _loadingLoaderState = false;
                        WriteException("PROFILE VALUE RESTORE ERROR", ex);
                    }
                }));
        }
    }
}
