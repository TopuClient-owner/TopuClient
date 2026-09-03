using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CmlLib.Core;
using CmlLib.Core.Installer.NeoForge;
using CmlLib.Core.Installer.NeoForge.Installers;

namespace TopuLauncher
{
    // NeoForge support is isolated here so the existing Fabric, Quilt and
    // Forge implementation remains untouched.
    public partial class MainWindow
    {
        private static readonly string[] RuntimeNeoForgeVersions =
        {
            "1.21.1",
            "1.21.2",
            "1.21.4",
            "1.21.5",
            "1.21.11"
        };

        private bool _neoForgeHooksInstalled;
        private bool _neoForgeLaunchHookInstalled;

        // Register a Window Loaded hook without changing the existing
        // MainWindow constructor/static constructor.
        private static readonly bool NeoForgeWindowHook = RegisterNeoForgeWindowHook();

        private static bool RegisterNeoForgeWindowHook()
        {
            EventManager.RegisterClassHandler(
                typeof(Window),
                FrameworkElement.LoadedEvent,
                new RoutedEventHandler(NeoForgeWindowLoaded));
            return true;
        }

        private static void NeoForgeWindowLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is not Window window)
                return;

            window.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (window is MainWindow main)
                {
                    main.InstallNeoForgeMainUiHooks();
                }
                else if (string.Equals(
                    window.Title,
                    "Create New Topu Profile",
                    StringComparison.OrdinalIgnoreCase))
                {
                    InstallNeoForgeCreateProfileHooks(window);
                }
            }));
        }

        private void InstallNeoForgeMainUiHooks()
        {
            if (_neoForgeHooksInstalled)
                return;

            _neoForgeHooksInstalled = true;

            if (_loaderBox != null)
            {
                if (!_loaderBox.Items.Cast<object>().Any(x =>
                    string.Equals(x?.ToString(), "NeoForge", StringComparison.OrdinalIgnoreCase)))
                {
                    _loaderBox.Items.Add("NeoForge");
                }

                _loaderBox.SelectionChanged += NeoForgeMainLoaderSelectionChanged;
            }

            ProfileSelector.SelectionChanged += NeoForgeProfileSelectionChanged;

            // Replace only the launch event wrapper. Forge/Quilt still use the
            // existing LaunchNonFabricProfileAsync implementation.
            if (!_neoForgeLaunchHookInstalled)
            {
                LaunchBtn.PreviewMouseLeftButtonDown -= LaunchPreview;
                LaunchBtn.PreviewMouseLeftButtonDown += NeoForgeAwareLaunchPreview;
                _neoForgeLaunchHookInstalled = true;
            }

            ApplyNeoForgeProfileToUi();
        }

        private void NeoForgeMainLoaderSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_loaderBox?.SelectedItem?.ToString()?.Equals(
                    "NeoForge", StringComparison.OrdinalIgnoreCase) != true)
                return;

            SetNeoForgeVersionChoices(GetSelectedVersion());
        }

        private void SetNeoForgeVersionChoices(string? preferred = null)
        {
            string current = preferred ?? GetSelectedVersion();

            VersionBox.Items.Clear();
            foreach (string version in RuntimeNeoForgeVersions)
            {
                VersionBox.Items.Add(new ComboBoxItem
                {
                    Content = version
                });
            }

            int index = Array.IndexOf(RuntimeNeoForgeVersions, current);
            if (index < 0)
                index = 0;

            VersionBox.SelectedIndex = index;
            UpdateProfileCard();
            UpdateLaunchSummary();
        }

        private void NeoForgeProfileSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.OriginalSource != ProfileSelector)
                return;

            Dispatcher.BeginInvoke(new Action(ApplyNeoForgeProfileToUi));
        }

        private void ApplyNeoForgeProfileToUi()
        {
            if (_loaderBox == null)
                return;

            RuntimeProfileSettings settings = GetRuntimeProfile();
            if (!settings.Loader.Equals("NeoForge", StringComparison.OrdinalIgnoreCase))
                return;

            _loaderBox.SelectedItem = "NeoForge";
            SetNeoForgeVersionChoices(settings.Version);

            int ram = Math.Clamp(settings.RamGb, 2, 12);
            RamSlider.Value = ram;
            RamLabel.Text = $"{ram}GB";
            UpdateLaunchSummary();
        }

        private void NeoForgeAwareLaunchPreview(object sender, MouseButtonEventArgs e)
        {
            string loader = GetRuntimeProfile().Loader;

            if (loader.Equals("Fabric", StringComparison.OrdinalIgnoreCase))
                return;

            e.Handled = true;

            if (loader.Equals("NeoForge", StringComparison.OrdinalIgnoreCase))
            {
                _ = LaunchNeoForgeProfileAsync();
                return;
            }

            _ = LaunchNonFabricProfileAsync();
        }

        private async Task LaunchNeoForgeProfileAsync()
        {
            if (_minecraftProcess != null)
            {
                try
                {
                    if (!_minecraftProcess.HasExited)
                    {
                        MessageBox.Show(
                            "Minecraft is already running.",
                            "Topu Client",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                        return;
                    }
                }
                catch { }

                _minecraftProcess = null;
            }

            LaunchBtn.IsEnabled = false;

            try
            {
                RuntimeProfileSettings profile = GetRuntimeProfile();
                string minecraftVersion = profile.Version;
                int ram = Math.Max(2048, profile.RamGb * 1024);

                if (!RuntimeNeoForgeVersions.Contains(minecraftVersion))
                    throw new InvalidOperationException(
                        $"NeoForge does not support Minecraft {minecraftVersion} in TopuClient.");

                StartLaunchLog();
                WriteLog("===== TOPU NEOFORGE LAUNCH =====");
                WriteLog("Loader: NeoForge");
                WriteLog($"Minecraft: {minecraftVersion}");
                WriteLog($"Profile: {_gamePath}");
                WriteLog($"RAM: {ram} MB");

                _session = await AuthenticateSelectedAccountAsync();
                if (_session == null)
                    throw new InvalidOperationException("Could not create a Minecraft session.");

                // All requested NeoForge versions use Java 21.
                string javaPath = await EnsureJavaAsync(21);
                MinecraftPath minecraftPath = new MinecraftPath(_gamePath);
                MinecraftLauncher launcher = new MinecraftLauncher(minecraftPath);

                StatusText.Text = $"Installing Minecraft {minecraftVersion}...";
                await launcher.InstallAsync(minecraftVersion, CancellationToken.None);

                StatusText.Text = $"Installing NeoForge for {minecraftVersion}...";
                WriteLog("Querying NeoForge builds...");

                NeoForgeInstaller neoForge = new NeoForgeInstaller(launcher, Http);
                NeoForgeInstallOptions installOptions = new NeoForgeInstallOptions
                {
                    JavaPath = javaPath,
                    SkipIfAlreadyInstalled = true,
                    CancellationToken = CancellationToken.None,
                    InstallerOutput = new Progress<string>(line => WriteLog("[NeoForge] " + line))
                };

                string loaderVersionName = await neoForge.Install(
                    minecraftVersion,
                    installOptions);

                WriteLog($"Selected NeoForge version: {loaderVersionName}");

                // NeoForge's installer leaves some vanilla/runtime files to
                // CmlLib, so complete the installed version before launching.
                StatusText.Text = "Installing NeoForge dependencies...";
                await launcher.InstallAsync(loaderVersionName, CancellationToken.None);

                MLaunchOption options = new MLaunchOption
                {
                    Session = _session,
                    MaximumRamMb = ram,
                    MinimumRamMb = Math.Min(1024, ram),
                    JavaPath = javaPath,
                    GameLauncherName = "Topu Client",
                    GameLauncherVersion = "1.0.0"
                };

                StatusText.Text = "Building NeoForge process...";
                Process process = await launcher.BuildProcessAsync(
                    loaderVersionName,
                    options,
                    CancellationToken.None);

                if (process == null)
                    throw new InvalidOperationException(
                        "CmlLib returned a null Minecraft process.");

                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;
                process.OutputDataReceived += Minecraft_OutputDataReceived;
                process.ErrorDataReceived += Minecraft_ErrorDataReceived;

                WriteLog($"Loader version: {loaderVersionName}");
                WriteLog($"Executable: {process.StartInfo.FileName}");
                WriteLog($"Arguments: {process.StartInfo.Arguments}");
                WriteLog($"Working directory: {process.StartInfo.WorkingDirectory}");
                WriteDebugFile(
                    process,
                    javaPath,
                    minecraftVersion,
                    loaderVersionName,
                    ram);

                StatusText.Text = $"Starting NeoForge {minecraftVersion}...";
                if (!process.Start())
                    throw new InvalidOperationException(
                        "Windows failed to start Minecraft.");

                _minecraftProcess = process;
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                StatusText.Text = $"Topu Client running as {_session.Username}";
                _ = MonitorMinecraftAsync(process);
            }
            catch (Exception ex)
            {
                StatusText.Text = "Launch failed.";
                WriteException("TOPU NEOFORGE LAUNCH ERROR", ex);
                MessageBox.Show(
                    "Minecraft failed to launch.\n\n" +
                    ex.Message +
                    "\n\nLog:\n" +
                    _logPath,
                    "Topu Client",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                LaunchBtn.IsEnabled = true;
            }
        }

        private static void InstallNeoForgeCreateProfileHooks(Window dialog)
        {
            List<ComboBox> combos = FindComboBoxes(dialog).ToList();
            ComboBox? loaderBox = combos.FirstOrDefault(IsLoaderComboBox);
            if (loaderBox == null)
                return;

            if (!loaderBox.Items.Cast<object>().Any(x =>
                string.Equals(x?.ToString(), "NeoForge", StringComparison.OrdinalIgnoreCase)))
            {
                loaderBox.Items.Add("NeoForge");
            }

            ComboBox? versionBox = combos.FirstOrDefault(x => x != loaderBox);
            if (versionBox == null)
                return;

            loaderBox.SelectionChanged -= CreateProfileNeoForgeSelectionChanged;
            loaderBox.SelectionChanged += CreateProfileNeoForgeSelectionChanged;

            if (loaderBox.SelectedItem?.ToString()?.Equals(
                    "NeoForge", StringComparison.OrdinalIgnoreCase) == true)
            {
                versionBox.ItemsSource = RuntimeNeoForgeVersions;
                versionBox.SelectedIndex = 0;
            }
        }

        private static bool IsLoaderComboBox(ComboBox combo)
        {
            bool hasFabric = combo.Items.Cast<object>().Any(x =>
                string.Equals(x?.ToString(), "Fabric", StringComparison.OrdinalIgnoreCase));
            bool hasForge = combo.Items.Cast<object>().Any(x =>
                string.Equals(x?.ToString(), "Forge", StringComparison.OrdinalIgnoreCase));
            bool hasQuilt = combo.Items.Cast<object>().Any(x =>
                string.Equals(x?.ToString(), "Quilt", StringComparison.OrdinalIgnoreCase));

            return hasFabric && hasForge && hasQuilt;
        }

        private static void CreateProfileNeoForgeSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (sender is not ComboBox loaderBox ||
                loaderBox.Parent is not Panel parent)
                return;

            ComboBox? versionBox = FindComboBoxes(parent)
                .FirstOrDefault(x => x != loaderBox);
            if (versionBox == null)
                return;

            string loader = loaderBox.SelectedItem?.ToString() ?? "Fabric";

            if (loader.Equals("NeoForge", StringComparison.OrdinalIgnoreCase))
            {
                versionBox.ItemsSource = RuntimeNeoForgeVersions;
                versionBox.SelectedIndex = 0;
            }
        }

        private static IEnumerable<ComboBox> FindComboBoxes(DependencyObject root)
        {
            if (root is ComboBox combo)
                yield return combo;

            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(root, i);
                foreach (ComboBox combo in FindComboBoxes(child))
                    yield return combo;
            }
        }
    }
}
