using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CmlLib.Core;

namespace TopuLauncher
{
    public partial class MainWindow
    {
        private static readonly object _vanillaManifestHook = RegisterVanillaManifestHook();
        private static readonly string[] _emptyVanillaVersions = Array.Empty<string>();
        private string[]? _mojangVanillaVersions;
        private bool _loadingMojangVanillaVersions;

        private static object RegisterVanillaManifestHook()
        {
            EventManager.RegisterClassHandler(
                typeof(MainWindow),
                FrameworkElement.LoadedEvent,
                new RoutedEventHandler(VanillaManifestMainWindowLoaded));
            EventManager.RegisterClassHandler(
                typeof(Window),
                FrameworkElement.LoadedEvent,
                new RoutedEventHandler(VanillaManifestWindowLoaded));
            return new object();
        }

        private static void VanillaManifestMainWindowLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is not MainWindow window) return;
            window.Dispatcher.BeginInvoke(new Action(() =>
            {
                _ = window.LoadMojangVanillaVersionsAsync();
            }));
        }

        private static void VanillaManifestWindowLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is not Window window || window is MainWindow) return;
            if (!string.Equals(window.Title, "Create New Topu Profile", StringComparison.OrdinalIgnoreCase)) return;

            window.Dispatcher.BeginInvoke(new Action(() =>
            {
                HookCreateProfileVersionSelector(window);
            }));
        }

        private async Task LoadMojangVanillaVersionsAsync()
        {
            if (_loadingMojangVanillaVersions) return;
            _loadingMojangVanillaVersions = true;

            try
            {
                var launcher = new MinecraftLauncher(new MinecraftPath(_gamePath));
                var versions = await launcher.GetAllVersionsAsync();
                _mojangVanillaVersions = versions
                    .Select(v => v.Name)
                    .Where(v => !string.IsNullOrWhiteSpace(v))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                WriteLog($"Mojang Vanilla manifest loaded: {_mojangVanillaVersions.Length} versions.");

                if (_loaderBox?.SelectedItem?.ToString()?.Equals("Vanilla", StringComparison.OrdinalIgnoreCase) == true)
                {
                    string current = GetSelectedVersion();
                    ApplyMojangVanillaVersionsToMain(current);
                }
            }
            catch (Exception ex)
            {
                WriteException("MOJANG VANILLA VERSION MANIFEST ERROR", ex);
            }
            finally
            {
                _loadingMojangVanillaVersions = false;
            }
        }

        private void ApplyMojangVanillaVersionsToMain(string? preferred = null)
        {
            if (VersionBox == null || _mojangVanillaVersions == null || _mojangVanillaVersions.Length == 0) return;

            VersionBox.Items.Clear();
            foreach (string version in _mojangVanillaVersions)
                VersionBox.Items.Add(new ComboBoxItem { Content = version });

            int index = Array.FindIndex(
                _mojangVanillaVersions,
                v => string.Equals(v, preferred, StringComparison.OrdinalIgnoreCase));

            VersionBox.SelectedIndex = index >= 0 ? index : 0;
            UpdateProfileCard();
            UpdateLaunchSummary();
        }

        private static void HookCreateProfileVersionSelector(Window window)
        {
            ComboBox[] boxes = FindVisualChildren<ComboBox>(window).ToArray();
            if (boxes.Length < 2) return;

            ComboBox? loaderBox = boxes.FirstOrDefault(b =>
                b.Items.Cast<object>().Any(i => string.Equals(i?.ToString(), "Vanilla", StringComparison.OrdinalIgnoreCase)));
            ComboBox? versionBox = boxes.FirstOrDefault(b => b != loaderBox &&
                (b.ItemsSource is Array || b.Items.Count > 0));

            if (loaderBox == null || versionBox == null) return;
            if (loaderBox.Tag is string tag && tag == "TopuVanillaManifestHooked") return;
            loaderBox.Tag = "TopuVanillaManifestHooked";

            loaderBox.SelectionChanged += (_, _) =>
            {
                if (!string.Equals(loaderBox.SelectedItem?.ToString(), "Vanilla", StringComparison.OrdinalIgnoreCase)) return;
                var owner = Window.GetWindow(loaderBox) as MainWindow;
                if (owner != null && owner._mojangVanillaVersions != null)
                {
                    versionBox.ItemsSource = owner._mojangVanillaVersions;
                    versionBox.SelectedIndex = 0;
                    return;
                }

                if (owner != null)
                    _ = owner.LoadMojangVanillaVersionsForDialogAsync(versionBox);
            };

            if (string.Equals(loaderBox.SelectedItem?.ToString(), "Vanilla", StringComparison.OrdinalIgnoreCase))
            {
                var owner = Window.GetWindow(loaderBox) as MainWindow;
                if (owner != null)
                {
                    if (owner._mojangVanillaVersions != null)
                        versionBox.ItemsSource = owner._mojangVanillaVersions;
                    else
                        _ = owner.LoadMojangVanillaVersionsForDialogAsync(versionBox);
                }
            }
        }

        private async Task LoadMojangVanillaVersionsForDialogAsync(ComboBox versionBox)
        {
            try
            {
                if (_mojangVanillaVersions == null)
                    await LoadMojangVanillaVersionsAsync();

                if (_mojangVanillaVersions == null || _mojangVanillaVersions.Length == 0) return;
                versionBox.ItemsSource = _mojangVanillaVersions;
                versionBox.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                WriteException("CREATE PROFILE VANILLA MANIFEST ERROR", ex);
            }
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject root) where T : DependencyObject
        {
            if (root == null) yield break;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(root, i);
                if (child is T match) yield return match;
                foreach (T descendant in FindVisualChildren<T>(child)) yield return descendant;
            }
        }
    }
}
