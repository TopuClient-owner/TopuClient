using System;
using System.Windows;
using System.Windows.Controls;

namespace TopuLauncher
{
    // Keeps the visible launch/profile UI synchronized with topu-profile.json.
    // The original XAML contains Fabric defaults, so this partial updates those
    // display values whenever the runtime loader/profile changes.
    public partial class MainWindow
    {
        private static readonly object LoaderUiFixRegistration = RegisterLoaderUiFix();
        private bool _loaderUiFixHooked;

        private static object RegisterLoaderUiFix()
        {
            EventManager.RegisterClassHandler(
                typeof(MainWindow),
                FrameworkElement.LoadedEvent,
                new RoutedEventHandler(LoaderUiFixLoaded));
            return new object();
        }

        private static void LoaderUiFixLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is MainWindow window)
                window.Dispatcher.BeginInvoke(new Action(window.InstallLoaderUiFix));
        }

        private void InstallLoaderUiFix()
        {
            if (_loaderUiFixHooked)
                return;

            _loaderUiFixHooked = true;

            if (_loaderBox != null)
                _loaderBox.SelectionChanged += LoaderUiFix_SelectionChanged;

            if (ProfileSelector != null)
                ProfileSelector.SelectionChanged += LoaderUiFix_ProfileChanged;

            TabProfiles.AddHandler(
                Button.ClickEvent,
                new RoutedEventHandler(LoaderUiFix_ButtonClicked),
                true);

            ApplyLoaderUiFix();
        }

        private void LoaderUiFix_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (!_runtimeUiReady || e.OriginalSource != _loaderBox)
                return;

            Dispatcher.BeginInvoke(new Action(ApplyLoaderUiFix));
        }

        private void LoaderUiFix_ProfileChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (!_runtimeUiReady || e.OriginalSource != ProfileSelector)
                return;

            Dispatcher.BeginInvoke(new Action(ApplyLoaderUiFix));
        }

        private void LoaderUiFix_ButtonClicked(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is not Button button ||
                !string.Equals(button.Content?.ToString(), "Save Profile Settings", StringComparison.OrdinalIgnoreCase))
                return;

            Dispatcher.BeginInvoke(new Action(ApplyLoaderUiFix));
        }

        private void ApplyLoaderUiFix()
        {
            try
            {
                RuntimeProfileSettings settings = GetRuntimeProfile();
                string loader = settings.Loader;

                if (!loader.Equals("Fabric", StringComparison.OrdinalIgnoreCase) &&
                    !loader.Equals("Forge", StringComparison.OrdinalIgnoreCase) &&
                    !loader.Equals("Quilt", StringComparison.OrdinalIgnoreCase))
                {
                    loader = "Fabric";
                }

                string version = string.IsNullOrWhiteSpace(settings.Version)
                    ? "1.21.1"
                    : settings.Version;

                int ram = Math.Clamp(settings.RamGb, 2, 12);

                if (LaunchVersionLabel != null)
                    LaunchVersionLabel.Text = version;

                if (LaunchRamLabel != null)
                    LaunchRamLabel.Text = $"{ram}GB RAM";

                if (LaunchBtn != null)
                    LaunchBtn.Content = $"⚡   LAUNCH {loader.ToUpperInvariant()}";

                if (LaunchProfileLabel != null)
                    LaunchProfileLabel.Text = GetActiveProfileName();

                if (LaunchProfileLabel?.Parent is StackPanel profilePanel)
                {
                    foreach (UIElement child in profilePanel.Children)
                    {
                        if (child is not StackPanel details)
                            continue;

                        foreach (UIElement detail in details.Children)
                        {
                            if (detail is TextBlock text &&
                                text != LaunchVersionLabel &&
                                text != LaunchRamLabel &&
                                text.Text != "  •  " &&
                                (text.Text.Equals("Fabric", StringComparison.OrdinalIgnoreCase) ||
                                 text.Text.Equals("Forge", StringComparison.OrdinalIgnoreCase) ||
                                 text.Text.Equals("Quilt", StringComparison.OrdinalIgnoreCase)))
                            {
                                text.Text = loader;
                                break;
                            }
                        }
                    }
                }

                if (SelectedProfileLabel != null)
                    SelectedProfileLabel.Text = $"● {GetActiveProfileName()}   •   {loader} {version}   •   {ram}GB RAM";

                SetSidebarLoaderText(loader);
            }
            catch (Exception ex)
            {
                WriteException("LOADER UI UPDATE ERROR", ex);
            }
        }

        private void SetSidebarLoaderText(string loader)
        {
            foreach (TextBlock text in FindTextBlocks(this))
            {
                if (text.Text.Equals("Fabric PvP Launcher", StringComparison.OrdinalIgnoreCase) ||
                    text.Text.Equals("Multi-Loader Launcher", StringComparison.OrdinalIgnoreCase) ||
                    text.Text.Equals("Forge PvP Launcher", StringComparison.OrdinalIgnoreCase) ||
                    text.Text.Equals("Quilt PvP Launcher", StringComparison.OrdinalIgnoreCase))
                {
                    text.Text = $"{loader} PvP Launcher";
                    return;
                }
            }
        }

        private static System.Collections.Generic.IEnumerable<TextBlock> FindTextBlocks(DependencyObject root)
        {
            int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                DependencyObject child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);

                if (child is TextBlock text)
                    yield return text;

                foreach (TextBlock nested in FindTextBlocks(child))
                    yield return nested;
            }
        }
    }
}
