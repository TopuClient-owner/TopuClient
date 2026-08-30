using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.Installers;
using CmlLib.Core.Installer.Forge;
using CmlLib.Core.ProcessBuilder;

namespace TopuLauncher
{
    public partial class MainWindow
    {
        private static readonly string[] ForgeMinecraftVersions =
        {
            "1.20.1",
            "1.8.9"
        };

        private static readonly (string Slug, string Name)[] ForgePerformanceMods =
        {
            ("embeddium", "Embeddium"),
            ("modernfix", "ModernFix"),
            ("dynamic-fps", "Dynamic FPS"),
            ("ferrite-core", "FerriteCore"),
            ("entityculling", "Entity Culling")
        };

        private ComboBox? _loaderSelector;
        private TextBlock? _loaderStatusLabel;
        private bool _forgeHooksReady;
        private bool _forgeLaunching;
        private bool _loadingLoaderState;

        protected override void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);

            if (_forgeHooksReady)
                return;

            _forgeHooksReady = true;
            EnsureLoaderUi();

            if (_loaderSelector != null)
                _loaderSelector.SelectionChanged += LoaderSelector_Changed;

            VersionBox.SelectionChanged += VersionBox_ForgeChanged;
            ProfileSelector.SelectionChanged += ProfileSelector_ForgeChanged;
            LaunchBtn.PreviewMouseLeftButtonDown += LaunchBtn_ForgeMouseDown;
            LaunchBtn.PreviewKeyDown += LaunchBtn_ForgeKeyDown;

            LoadLoaderStateForProfile();
        }

        private void EnsureLoaderUi()
        {
            if (_loaderSelector != null)
                return;

            _loaderSelector = new ComboBox
            {
                Height = 36,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Style = (Style)FindResource("ModernComboBox")
            };

            _loaderSelector.Items.Add(new ComboBoxItem { Content = "Fabric" });
            _loaderSelector.Items.Add(new ComboBoxItem { Content = "Forge" });
            _loaderSelector.SelectedIndex = 0;

            _loaderStatusLabel = new TextBlock
            {
                Foreground = new SolidColorBrush(Color.FromRgb(119, 125, 136)),
                FontSize = 10,
                Margin = new Thickness(0, 8, 0, 0),
                TextWrapping = TextWrapping.Wrap
            };

            Border card = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(25, 27, 32)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(41, 44, 51)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(16),
                Margin = new Thickness(0, 0, 0, 14)
            };

            StackPanel content = new StackPanel();
            content.Children.Add(new TextBlock
            {
                Text = "MOD LOADER",
                Foreground = new SolidColorBrush(Color.FromRgb(102, 108, 118)),
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 8)
            });

            Grid row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            TextBlock label = new TextBlock
            {
                Text = "Loader",
                Foreground = new SolidColorBrush(Color.FromRgb(200, 205, 213)),
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center
            };

            Grid.SetColumn(label, 0);
            Grid.SetColumn(_loaderSelector, 1);
            row.Children.Add(label);
            row.Children.Add(_loaderSelector);

            content.Children.Add(row);
            content.Children.Add(_loaderStatusLabel);
            card.Child = content;

            TabProfiles.Children.Insert(Math.Min(1, TabProfiles.Children.Count), card);
        }

        private string GetSelectedLoaderName()
        {
            return (_loaderSelector?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Fabric";
        }

        private string GetLoaderStatePath()
        {
            return System.IO.Path.Combine(_gamePath, "topu-loader.txt");
        }

        private void SaveLoaderState()
        {
            try
            {
                System.IO.Directory.CreateDirectory(_gamePath);
                System.IO.File.WriteAllText(GetLoaderStatePath(), GetSelectedLoaderName());
            }
            catch (Exception ex)
            {
                WriteException("LOADER STATE SAVE ERROR", ex);
            }
        }

        private void LoadLoaderStateForProfile()
        {
            try
            {
                _loadingLoaderState = true;

                string loader = "Fabric";
                string statePath = GetLoaderStatePath();
                if (System.IO.File.Exists(statePath))
                    loader = System.IO.File.ReadAllText(statePath).Trim();

                bool forge = loader.Equals("Forge", StringComparison.OrdinalIgnoreCase);
                if (_loaderSelector != null)
                    _loaderSelector.SelectedIndex = forge ? 1 : 0;

                UpdateVersionListForLoader(forge ? "Forge" : "Fabric");
            }
            catch (Exception ex)
            {
                WriteException("LOADER STATE LOAD ERROR", ex);
            }
            finally
            {
                _loadingLoaderState = false;
                UpdateLoaderUi();
            }
        }

        private void UpdateVersionListForLoader(string loader)
        {
            if (!loader.Equals("Forge", StringComparison.OrdinalIgnoreCase))
                return;

            foreach (string version in ForgeMinecraftVersions)
            {
                if (!VersionBox.Items.OfType<ComboBoxItem>().Any(i =>
                        string.Equals(i.Content?.ToString(), version, StringComparison.OrdinalIgnoreCase)))
                {
                    VersionBox.Items.Add(new ComboBoxItem { Content = version });
                }
            }
        }

        private void LoaderSelector_Changed(object? sender, SelectionChangedEventArgs e)
        {
            if (_loadingLoaderState)
                return;

            string loader = GetSelectedLoaderName();
            UpdateVersionListForLoader(loader);
            SaveLoaderState();

            if (loader.Equals("Forge", StringComparison.OrdinalIgnoreCase) &&
                !ForgeMinecraftVersions.Contains(GetSelectedVersion(), StringComparer.OrdinalIgnoreCase))
            {
                ComboBoxItem? target = VersionBox.Items.OfType<ComboBoxItem>().FirstOrDefault(i =>
                    string.Equals(i.Content?.ToString(), "1.20.1", StringComparison.OrdinalIgnoreCase));
                if (target != null)
                    VersionBox.SelectedItem = target;
            }

            UpdateLoaderUi();
        }

        private void VersionBox_ForgeChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_loadingLoaderState)
                return;

            UpdateLoaderUi();
        }

        private void ProfileSelector_ForgeChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (!_forgeHooksReady)
                return;

            Dispatcher.BeginInvoke(new Action(LoadLoaderStateForProfile));
        }

        private void UpdateLoaderUi()
        {
            try
            {
                string loader = GetSelectedLoaderName();
                string version = GetSelectedVersion();
                int ram = (int)RamSlider.Value;

                if (LaunchProfileLabel != null)
                    LaunchProfileLabel.Text = GetActiveProfileName();
                if (LaunchVersionLabel != null)
                    LaunchVersionLabel.Text = version;
                if (LaunchRamLabel != null)
                    LaunchRamLabel.Text = $"{ram}GB RAM";

                LaunchBtn.Content = loader.Equals("Forge", StringComparison.OrdinalIgnoreCase)
                    ? "⚡   LAUNCH FORGE"
                    : "⚡   LAUNCH FABRIC";

                if (SelectedProfileLabel != null)
                    SelectedProfileLabel.Text = $"● {GetActiveProfileName()} • {loader} {version} • {ram}GB RAM";

                if (_loaderStatusLabel != null)
                {
                    _loaderStatusLabel.Text = loader.Equals("Forge", StringComparison.OrdinalIgnoreCase)
                        ? "Forge • Minecraft 1.20.1 / 1.8.9 • performance stack enabled on launch"
                        : "Fabric • your existing Fabric optimization stack";
                }

                if (ModSearchStatus != null)
                {
                    ModSearchStatus.Text = loader.Equals("Forge", StringComparison.OrdinalIgnoreCase)
                        ? "Forge performance mods install automatically on launch."
                        : "Optimization stack installs automatically.";
                }
            }
            catch
            {
            }
        }

        private void LaunchBtn_ForgeMouseDown(object? sender, MouseButtonEventArgs e)
        {
            if (!GetSelectedLoaderName().Equals("Forge", StringComparison.OrdinalIgnoreCase))
                return;

            e.Handled = true;
            _ = LaunchForgeAsync();
        }

        private void LaunchBtn_ForgeKeyDown(object? sender, KeyEventArgs e)
        {
            if (!GetSelectedLoaderName().Equals("Forge", StringComparison.OrdinalIgnoreCase))
                return;

            if (e.Key != Key.Enter && e.Key != Key.Space)
                return;

            e.Handled = true;
            _ = LaunchForgeAsync();
        }

        private async Task LaunchForgeAsync()
        {
            if (_forgeLaunching)
                return;

            string minecraftVersion = GetSelectedVersion();
            if (!ForgeMinecraftVersions.Contains(minecraftVersion, StringComparer.OrdinalIgnoreCase))
            {
                MessageBox.Show(
                    "Forge currently supports Minecraft 1.20.1 and 1.8.9 in Topu Client.",
                    "Forge Version",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            _forgeLaunching = true;
            LaunchBtn.IsEnabled = false;

            try
            {
                string profileName = GetActiveProfileName();
                SetActiveProfile(profileName);
                SaveLoaderState();

                StartLaunchLog();
                WriteLog("===== TOPU FORGE LAUNCH =====");
                WriteLog($"Profile: {profileName}");
                WriteLog($"Minecraft: {minecraftVersion}");

                MSession? session = await AuthenticateSelectedAccountAsync();
                if (session == null)
                    throw new InvalidOperationException("Could not authenticate the selected Minecraft account.");

                _session = session;

                int javaMajor = minecraftVersion.Equals("1.8.9", StringComparison.OrdinalIgnoreCase) ? 8 : 17;
                string javaPath = await EnsureJavaAsync(javaMajor);

                MinecraftPath minecraftPath = new MinecraftPath(_gamePath);
                MinecraftLauncher launcher = new MinecraftLauncher(minecraftPath);

                var fileProgress = new Progress<InstallerProgressChangedEventArgs>(args =>
                {
                    try
                    {
                        StatusText.Text = $"Downloading {args.Name} ({args.ProgressedTasks}/{args.TotalTasks})";
                    }
                    catch { }
                });

                var byteProgress = new Progress<ByteProgress>(args =>
                {
                    try
                    {
                        if (args.TotalBytes > 0)
                        {
                            double percent = args.ProgressedBytes * 100.0 / args.TotalBytes;
                            StatusText.Text = $"Downloading: {percent:0}%";
                        }
                    }
                    catch { }
                });

                using HttpClient forgeHttp = new HttpClient();
                ForgeInstaller forgeInstaller = new ForgeInstaller(launcher, forgeHttp);
                StatusText.Text = $"Finding latest Forge for Minecraft {minecraftVersion}...";

                var forgeVersions = await forgeInstaller.GetForgeVersions(minecraftVersion);
                var latest = forgeVersions.FirstOrDefault(v => v.IsLatestVersion)
                             ?? forgeVersions.FirstOrDefault(v => v.IsRecommendedVersion)
                             ?? forgeVersions.FirstOrDefault();

                if (latest == null)
                    throw new InvalidOperationException($"No Forge version was found for Minecraft {minecraftVersion}.");

                WriteLog($"Selected Forge: {latest.ForgeVersionName}");

                ForgeInstallOptions forgeOptions = new ForgeInstallOptions
                {
                    FileProgress = fileProgress,
                    ByteProgress = byteProgress,
                    InstallerOutput = new Progress<string>(line => WriteLog("[FORGE] " + line)),
                    CancellationToken = CancellationToken.None,
                    JavaPath = javaPath,
                    SkipIfAlreadyInstalled = true
                };

                StatusText.Text = $"Installing Forge {latest.ForgeVersionName}...";
                string forgeVersionName = await forgeInstaller.Install(latest, forgeOptions);

                StatusText.Text = "Installing Minecraft and Forge dependencies...";
                await launcher.InstallAsync(forgeVersionName, fileProgress, byteProgress, CancellationToken.None);

                await InstallForgePerformanceModsAsync(minecraftVersion);

                int ramMb = Math.Max(2048, (int)RamSlider.Value * 1024);
                MLaunchOption options = new MLaunchOption
                {
                    Session = session,
                    MaximumRamMb = ramMb,
                    MinimumRamMb = Math.Min(1024, ramMb),
                    JavaPath = javaPath,
                    GameLauncherName = "Topu Client",
                    GameLauncherVersion = "1.0.0"
                };

                StatusText.Text = "Building Forge Minecraft process...";
                System.Diagnostics.Process process = await launcher.BuildProcessAsync(forgeVersionName, options, CancellationToken.None);

                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;
                process.OutputDataReceived += Minecraft_OutputDataReceived;
                process.ErrorDataReceived += Minecraft_ErrorDataReceived;

                if (!process.Start())
                    throw new InvalidOperationException("Windows failed to start Forge Minecraft.");

                _minecraftProcess = process;
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                StatusText.Text = $"Topu Client Forge running as {session.Username}";
                _ = MonitorMinecraftAsync(process);
            }
            catch (Exception ex)
            {
                StatusText.Text = "Forge launch failed.";
                WriteException("TOPU FORGE LAUNCH ERROR", ex);
                MessageBox.Show(
                    "Forge failed to launch.\n\n" + ex.Message + "\n\nLog:\n" + _logPath,
                    "Topu Client",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                _forgeLaunching = false;
                LaunchBtn.IsEnabled = true;
                UpdateLoaderUi();
            }
        }

        private async Task InstallForgePerformanceModsAsync(string minecraftVersion)
        {
            string modsFolder = System.IO.Path.Combine(_gamePath, "mods");
            System.IO.Directory.CreateDirectory(modsFolder);

            foreach ((string slug, string name) in ForgePerformanceMods)
            {
                try
                {
                    string url =
                        "https://api.modrinth.com/v2/project/" + Uri.EscapeDataString(slug) +
                        "/version?loaders=%5B%22forge%22%5D&game_versions=%5B%22" +
                        Uri.EscapeDataString(minecraftVersion) + "%22%5D";

                    using HttpResponseMessage response = await new HttpClient().GetAsync(url);
                    response.EnsureSuccessStatusCode();

                    using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                    JsonElement versions = document.RootElement;
                    if (versions.ValueKind != JsonValueKind.Array)
                        continue;

                    JsonElement? jar = null;
                    foreach (JsonElement version in versions.EnumerateArray())
                    {
                        if (!version.TryGetProperty("files", out JsonElement files))
                            continue;

                        foreach (JsonElement file in files.EnumerateArray())
                        {
                            string? filename = file.TryGetProperty("filename", out JsonElement fn) ? fn.GetString() : null;
                            bool primary = !file.TryGetProperty("primary", out JsonElement p) || p.GetBoolean();
                            if (primary && !string.IsNullOrWhiteSpace(filename) && filename.EndsWith(".jar", StringComparison.OrdinalIgnoreCase))
                            {
                                jar = file;
                                break;
                            }
                        }

                        if (jar != null)
                            break;
                    }

                    if (jar == null)
                    {
                        WriteLog($"Skipped {name}: no compatible Forge build for {minecraftVersion}.");
                        continue;
                    }

                    JsonElement selected = jar.Value;
                    string? downloadUrl = selected.TryGetProperty("url", out JsonElement urlElement) ? urlElement.GetString() : null;
                    string filenameFinal = selected.TryGetProperty("filename", out JsonElement nameElement)
                        ? (nameElement.GetString() ?? slug + ".jar")
                        : slug + ".jar";

                    if (string.IsNullOrWhiteSpace(downloadUrl))
                        continue;

                    string destination = System.IO.Path.Combine(modsFolder, filenameFinal);
                    if (System.IO.File.Exists(destination) && new System.IO.FileInfo(destination).Length > 0)
                    {
                        WriteLog($"Forge optimization already installed: {name}");
                        continue;
                    }

                    await DownloadFileAsync(downloadUrl, destination);
                    WriteLog($"Installed Forge optimization: {name}");
                }
                catch (Exception ex)
                {
                    WriteLog($"Skipped Forge optimization {name}: {ex.Message}");
                }
            }
        }
    }
}
