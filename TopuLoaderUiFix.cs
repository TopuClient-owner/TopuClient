using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TopuLauncher
{
    // Keeps the visible launch/profile UI synchronized with topu-profile.json.
    // Also removes the old Fabric-only presentation and adds NeoForge everywhere
    // the loader selector is presented.
    public partial class MainWindow
    {
        private static readonly object LoaderUiFixRegistration = RegisterLoaderUiFix();
        private bool _loaderUiFixHooked;

        private static readonly string[] NeoForgeUiVersions =
        {
            "1.21.1", "1.21.4", "1.21.8", "1.21.11", "26.1.2", "26.2"
        };

        private static object RegisterLoaderUiFix()
        {
            EventManager.RegisterClassHandler(
                typeof(MainWindow),
                FrameworkElement.LoadedEvent,
                new RoutedEventHandler(LoaderUiFixLoaded));

            // The Create Profile dialog creates its ComboBoxes dynamically, so a
            // class handler makes NeoForge available there too without replacing
            // the existing dialog implementation.
            EventManager.RegisterClassHandler(
                typeof(ComboBox),
                FrameworkElement.LoadedEvent,
                new RoutedEventHandler(LoaderComboLoaded),
                true);

            EventManager.RegisterClassHandler(
                typeof(ComboBox),
                Selector.SelectionChangedEvent,
                new SelectionChangedEventHandler(LoaderComboSelectionChanged),
                true);

            return new object();
        }

        private static void LoaderUiFixLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is MainWindow window)
                window.Dispatcher.BeginInvoke(new Action(window.InstallLoaderUiFix));
        }

        private static bool IsLoaderCombo(ComboBox combo)
        {
            return combo.Items.Cast<object>()
                .Select(x => x?.ToString() ?? string.Empty)
                .Any(x => x.Equals("Vanilla", StringComparison.OrdinalIgnoreCase))
                && combo.Items.Cast<object>()
                    .Select(x => x?.ToString() ?? string.Empty)
                    .Any(x => x.Equals("Fabric", StringComparison.OrdinalIgnoreCase));
        }

        private static void LoaderComboLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is not ComboBox combo || !IsLoaderCombo(combo))
                return;

            if (!combo.Items.Cast<object>().Any(x =>
                    string.Equals(x?.ToString(), "NeoForge", StringComparison.OrdinalIgnoreCase)))
            {
                combo.Items.Add("NeoForge");
            }
        }

        private static void LoaderComboSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is not ComboBox combo || !IsLoaderCombo(combo))
                return;

            string selected = combo.SelectedItem?.ToString() ?? string.Empty;
            if (!selected.Equals("NeoForge", StringComparison.OrdinalIgnoreCase))
                return;

            // The original Create Profile dialog has its own SelectionChanged
            // handler and would otherwise fall through to the Fabric versions.
            // Run after it so the version list becomes NeoForge-specific.
            combo.Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    Window? owner = Window.GetWindow(combo);
                    if (owner == null)
                        return;

                    ComboBox? versionCombo = FindVersionCombo(owner, combo);
                    if (versionCombo == null)
                        return;

                    versionCombo.ItemsSource = NeoForgeUiVersions;
                    versionCombo.SelectedIndex = 0;
                }
                catch
                {
                    // The normal runtime UI handles its own version population.
                }
            }));
        }

        private static ComboBox? FindVersionCombo(DependencyObject root, ComboBox loaderCombo)
        {
            foreach (DependencyObject child in FindVisualChildren(root))
            {
                if (child is not ComboBox combo || ReferenceEquals(combo, loaderCombo))
                    continue;

                if (combo.ItemsSource != null &&
                    combo.ItemsSource is IEnumerable<string> source &&
                    source.Any(x => x.Equals("1.8.9", StringComparison.OrdinalIgnoreCase) ||
                                    x.Equals("1.21.1", StringComparison.OrdinalIgnoreCase)))
                {
                    return combo;
                }
            }

            return null;
        }

        private static IEnumerable<DependencyObject> FindVisualChildren(DependencyObject root)
        {
            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(root, i);
                yield return child;

                foreach (DependencyObject nested in FindVisualChildren(child))
                    yield return nested;
            }
        }

        private void InstallLoaderUiFix()
        {
            if (_loaderUiFixHooked)
                return;

            _loaderUiFixHooked = true;

            if (_loaderBox != null)
            {
                if (!_loaderBox.Items.Cast<object>().Any(x =>
                        string.Equals(x?.ToString(), "NeoForge", StringComparison.OrdinalIgnoreCase)))
                    _loaderBox.Items.Add("NeoForge");

                _loaderBox.SelectionChanged += LoaderUiFix_SelectionChanged;
            }

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

            string loader = _loaderBox?.SelectedItem?.ToString() ?? "Vanilla";
            if (loader.Equals("NeoForge", StringComparison.OrdinalIgnoreCase))
            {
                // RuntimeLoader's original handler runs first and defaults unknown
                // loaders to Fabric. Override that result for NeoForge.
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    SetVersionChoices("NeoForge", GetSelectedVersion());
                    ApplyLoaderUiFix();
                }));
            }
            else
            {
                Dispatcher.BeginInvoke(new Action(ApplyLoaderUiFix));
            }
        }

        private void LoaderUiFix_ProfileChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (!_runtimeUiReady || e.OriginalSource != ProfileSelector)
                return;

            string profile = ProfileSelector.SelectedItem?.ToString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(profile))
                return;

            try
            {
                // IMPORTANT: switch _gamePath immediately, before any queued
                // refresh reads topu-profile.json. This fixes the stale-profile
                // bug where the previous profile's loader/version/RAM remained
                // visible until the user changed the fields manually.
                SetActiveProfile(profile);
                ApplyLoaderUiFix();
            }
            catch (Exception ex)
            {
                WriteException("LOADER PROFILE SWITCH ERROR", ex);
            }
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

                if (!loader.Equals("Vanilla", StringComparison.OrdinalIgnoreCase) &&
                    !loader.Equals("Fabric", StringComparison.OrdinalIgnoreCase) &&
                    !loader.Equals("Forge", StringComparison.OrdinalIgnoreCase) &&
                    !loader.Equals("NeoForge", StringComparison.OrdinalIgnoreCase) &&
                    !loader.Equals("Quilt", StringComparison.OrdinalIgnoreCase))
                {
                    loader = "Vanilla";
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
                {
                    LaunchBtn.Content = $"⚡   LAUNCH {loader.ToUpperInvariant()}";
                    LaunchBtn.ToolTip = $"Launch {loader} {version} with {ram}GB RAM";
                }

                if (LaunchProfileLabel != null)
                    LaunchProfileLabel.Text = GetActiveProfileName();

                // The original XAML has a hard-coded Fabric TextBlock in the
                // active-profile card. Replace only the loader text, leaving the
                // visual layout untouched.
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
                                IsKnownLoaderName(text.Text))
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

        private static bool IsKnownLoaderName(string? value)
        {
            return value != null &&
                   (value.Equals("Vanilla", StringComparison.OrdinalIgnoreCase) ||
                    value.Equals("Fabric", StringComparison.OrdinalIgnoreCase) ||
                    value.Equals("Forge", StringComparison.OrdinalIgnoreCase) ||
                    value.Equals("NeoForge", StringComparison.OrdinalIgnoreCase) ||
                    value.Equals("Quilt", StringComparison.OrdinalIgnoreCase));
        }

        private void SetSidebarLoaderText(string loader)
        {
            foreach (TextBlock text in FindTextBlocks(this))
            {
                if (text.Text.Equals("Fabric PvP Launcher", StringComparison.OrdinalIgnoreCase) ||
                    text.Text.Equals("Multi-Loader Launcher", StringComparison.OrdinalIgnoreCase) ||
                    text.Text.Equals("Forge PvP Launcher", StringComparison.OrdinalIgnoreCase) ||
                    text.Text.Equals("NeoForge PvP Launcher", StringComparison.OrdinalIgnoreCase) ||
                    text.Text.Equals("Quilt PvP Launcher", StringComparison.OrdinalIgnoreCase) ||
                    text.Text.Equals("Vanilla PvP Launcher", StringComparison.OrdinalIgnoreCase))
                {
                    text.Text = loader.Equals("Vanilla", StringComparison.OrdinalIgnoreCase)
                        ? "Vanilla Minecraft"
                        : $"{loader} PvP Launcher";
                    return;
                }
            }
        }

        private static IEnumerable<TextBlock> FindTextBlocks(DependencyObject root)
        {
            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(root, i);

                if (child is TextBlock text)
                    yield return text;

                foreach (TextBlock nested in FindTextBlocks(child))
                    yield return nested;
            }
        }
    }
}
