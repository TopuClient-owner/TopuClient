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
        private static readonly (string Slug, string Name)[] ForgePerformanceMods =
        {
            ("embeddium", "Embeddium"),
            ("modernfix", "ModernFix"),
            ("dynamic-fps", "Dynamic FPS"),
            ("ferrite-core", "FerriteCore"),
            ("entityculling", "Entity Culling")
        };

        private static readonly string[] ForgeMinecraftVersions =
        {
            "1.20.1",
            "1.8.9"
        };

        private bool _forgeHooksReady;
        private bool _forgeLaunching;
        private bool _loadingLoaderState;

        private ComboBox? LoaderSelector;
        private TextBlock? LoaderStatusLabel;
        private Button? SaveProfileButton;

        protected override void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);

            if (_forgeHooksReady)
                return;

            _forgeHooksReady = true;

            EnsureLoaderUi();

            if (LoaderSelector != null)
                LoaderSelector.SelectionChanged += LoaderSelector_Changed;

            VersionBox.SelectionChanged += VersionBox_Changed;
            ProfileSelector.SelectionChanged += ProfileSelector_Changed;

            if (SaveProfileButton != null)
                SaveProfileButton.Click += SaveProfileButton_Changed;

            LaunchBtn.PreviewMouseLeftButtonDown += LaunchBtn_ForgeMouseDown;
            LaunchBtn.PreviewKeyDown += LaunchBtn_ForgeKeyDown;

            LoadLoaderStateForProfile();
        }

        private void EnsureLoaderUi()
        {
            try
            {
                if (LoaderSelector != null)
                    return;

                LoaderSelector = new ComboBox
                {
                    Height = 36,
                    Style = (Style)FindResource("ModernComboBox"),
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };

                LoaderSelector.Items.Add(new ComboBoxItem { Content = "Fabric" });
                LoaderSelector.Items.Add(new ComboBoxItem { Content = "Forge" });
                LoaderSelector.SelectedIndex = 0;

                LoaderStatusLabel = new TextBlock
                {
                    Foreground = new SolidColorBrush(Color.FromRgb(119, 125, 136)),
                    FontSize = 10,
                    Margin = new Thickness(0, 8, 0, 0),
                    TextWrapping = TextWrapping.Wrap
                };

                StackPanel profileStack = TabProfiles;

                Border loaderCard = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(25, 27, 32)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(41, 44, 51)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(12),
                    Padding = new Thickness(16),
                    Margin = new Thickness(0, 0, 0, 14)
                };

                StackPanel cardContent = new StackPanel();

                cardContent.Children.Add(new TextBlock
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
                row.Children.Add(label);

                Grid.SetColumn(LoaderSelector, 1);
                row.Children.Add(LoaderSelector);

                cardContent.Children.Add(row);
                cardContent.Children.Add(LoaderStatusLabel);
                loaderCard.Child = cardContent;

                int insertIndex = Math.Min(1, profileStack.Children.Count);
                profileStack.Children.Insert(insertIndex, loaderCard);

                SaveProfileButton = FindButtonByContent(profileStack, "Save Profile Settings");
            }
            catch (Exception ex)
            {
                WriteException("LOADER UI INITIALIZATION ERROR", ex);
            }
        }

        private static Button? FindButtonByContent(DependencyObject root, string content)
        {
            if (root is Button button &&
                string.Equals(button.Content?.ToString(), content, StringComparison.OrdinalIgnoreCase))
            {
                return button;
            }

            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(root, i);
                Button? found = FindButtonByContent(child, content);
                if (found != null)
                    return found;
            }

            return null;
        }

        private string GetSelectedLoaderName()
        {
            return (LoaderSelector?.SelectedItem as ComboBoxItem)?.Content?.ToString()?.Trim()
                   ?? "Fabric";
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

        private void RestoreProfileVersion()
        {
            try
            {
                string path = System.IO.Path.Combine(_gamePath, "topu-profile.json");
                if (!System.IO.File.Exists(path))
                    return;

                using JsonDocument doc = JsonDocument.Parse(System.IO.File.ReadAllText(path));
                if (!doc.RootElement.TryGetProperty("Version", out JsonElement versionElement))
                    return;

                string? savedVersion = versionElement.GetString();
                if (string.IsNullOrWhiteSpace(savedVersion))
                    return;

                EnsureVersionItem(savedVersion, GetSelectedLoaderName());

                foreach (ComboBoxItem item in VersionBox.Items.OfType<ComboBoxItem>())
                {
                    if (string.Equals(item.Content?.ToString(), savedVersion, StringComparison.OrdinalIgnoreCase))
                    {
                        VersionBox.SelectedItem = item;
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                WriteException("PROFILE VERSION RESTORE ERROR", ex);
            }
        }

        private void EnsureVersionItem(string version, string loader)
        {
            if (VersionBox.Items.OfType<ComboBoxItem>().Any(i =>
                    string.Equals(i.Content?.ToString(), version, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            if (!loader.Equals("Forge", StringComparison.OrdinalIgnoreCase))
                return;

            VersionBox.Items.Add(new ComboBoxItem { Content = version });
        }

        private void LoadLoaderStateForProfile()
        {
            try
            {
                _loadingLoaderState = true;

                string loader = "Fabric";
                string path = GetLoaderStatePath();
                if (System.IO.File.Exists(path))
                    loader = System.IO.File.ReadAllText(path).Trim();

                if (!loader.Equals("Forge", StringComparison.OrdinalIgnoreCase))
                    loader = "Fabric";

                LoaderSelector!.SelectedIndex = loader.Equals("Forge", StringComparison.OrdinalIgnoreCase) ? 1 : 0;

                UpdateVersionListForLoader(loader);
                RestoreProfileVersion();
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
            if (loader.Equals("Forge", StringComparison.OrdinalIgnoreCase))
            {
                foreach (string version in ForgeMinecraftVersions)
                    EnsureVersionItem(version, "Forge");
            }
        }

        private void LoaderSelector_Changed(object? sender, SelectionChangedEventArgs e)
        {
            if (_loadingLoaderState || LoaderSelector == null)
                return;

            string loader = GetSelectedLoaderName();
            UpdateVersionListForLoader(loader);
            SaveLoaderState();
            UpdateLoaderUi();

            if (loader.Equals("Forge", StringComparison.OrdinalIgnoreCase) &&
                !ForgeMinecraftVersions.Contains(GetSelectedVersion(), StringComparer.OrdinalIgnoreCase))
            {
                ComboBoxItem? target = VersionBox.Items.OfType<ComboBoxItem>()
                    .FirstOrDefault(i => string.Equals(i.Content?.ToString(), "1.20.1", StringComparison.OrdinalIgnoreCase));

                if (target != null)
                    VersionBox.SelectedItem = target;
            }
        }

        private void VersionBox_Changed(object? sender, SelectionChangedEventArgs e)
        {
            if (_loadingLoaderState)
                return;

            UpdateLoaderUi();

            if (GetSelectedLoaderName().Equals("Forge", StringComparison.OrdinalIgnoreCase) &&
                !ForgeMinecraftVersions.Contains(GetSelectedVersion(), StringComparer.OrdinalIgnoreCase))
            {
                StatusText.Text = "Forge profiles support Minecraft 1.20.1 and 1.8.9.";
            }
        }

        private void ProfileSelector_Changed(object? sender, SelectionChangedEventArgs e)
        {
            if (!_forgeHooksReady)
                return;

            Dispatcher.BeginInvoke(new Action(LoadLoaderStateForProfile));
        }

        private void SaveProfileButton_Changed(object? sender, RoutedEventArgs e)
        {
            SaveLoaderState();
            UpdateLoaderUi();
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
                    SelectedProfileLabel.Text =
                        $"● {GetActiveProfileName()}   •   {loader} {version}   •   {ram}GB RAM";

                if (LoaderStatusLabel != null)
                {
                    LoaderStatusLabel.Text = loader.Equals("Forge", StringComparison.OrdinalIgnoreCase)
                        ? "Forge optimization stack • Minecraft 1.20.1 / 1.8.9"
                        : "Fabric optimization stack";
                }

                if (ModSearchStatus != null)
                {
                    ModSearchStatus.Text = loader.Equals("Forge", StringComparison.OrdinalIgnoreCase)
                        ? "Forge optimization stack installs automatically on launch."
                        : "Fabric optimization stack installs automatically.";
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

            if (_minecraftProcess != null)
            {
                try
                {
                    if (!_minecraftProcess.HasExited)
                    {
                        MessageBox.Show("Minecraft is already running.", "Topu Client", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }
                }
                catch
                {
                }

                _minecraftProcess = null;
            }

            string minecraftVersion = GetSelectedVersion();
            if (!ForgeMinecraftVersions.Contains(minecraftVersion, StringComparer.OrdinalIgnoreCase))
            {
                MessageBox.Show(
                    "Forge support currently targets Minecraft 1.20.1 and 1.8.9.\n\nSelect one of those versions for this profile.",
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
                    throw new InvalidOperationException("Could not create a Minecraft session.");

                _session = session;

                int javaMajor = minecraftVersion.Equals("1.8.9", StringComparison.OrdinalIgnoreCase) ? 8 : 17;
                string javaPath = await EnsureJavaAsync(javaMajor);
                WriteLog($"Java: {javaPath}");

                MinecraftPath minecraftPath = new MinecraftPath(_gamePath);
                MinecraftLauncher launcher = new MinecraftLauncher(minecraftPath);

                Progress<InstallerProgressChangedEventArgs> fileProgress =
                    new Progress<InstallerProgressChangedEventArgs>(args =>
                    {
                        try
                        {
                            StatusText.Text = $"Downloading {args.Name} ({args.ProgressedTasks}/{args.TotalTasks})";
                        }
                        catch
                        {
                        }
                    });

                Progress<ByteProgress> byteProgress =
                    new Progress<ByteProgress>(args =>
                    {
                        try
                        {
                            if (args.TotalBytes > 0)
                            {
                                double percent = args.ProgressedBytes * 100.0 / args.TotalBytes;
                                StatusText.Text = $"Downloading: {percent:0}%";
                            }
                        }
                        catch
                        {
                        }
                    });

                using HttpClient forgeHttp = new HttpClient();
                ForgeInstaller forgeInstaller = new ForgeInstaller(launcher, forgeHttp);
                StatusText.Text = $"Finding latest Forge for Minecraft {minecraftVersion}...";

                IEnumerable<ForgeVersion> forgeVersions = await forgeInstaller.GetForgeVersions(minecraftVersion);
                ForgeVersion? latest = forgeVersions.FirstOrDefault(v => v.IsLatestVersion)
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
                WriteLog($"Forge profile: {forgeVersionName}");

                StatusText.Text = "Installing Forge dependencies...";
                await launcher.InstallAsync(forgeVersionName, fileProgress, byteProgress, CancellationToken.None);

                await InstallForgePerformanceModsAsync(minecraftVersion);

                int ram = Math.Max(2048, (int)RamSlider.Value * 1024);
                MLaunchOption options = new MLaunchOption
                {
                    Session = session,
                    MaximumRamMb = ram,
                    MinimumRamMb = Math.Min(1024, ram),
                    JavaPath = javaPath,
                    GameLauncherName = "Topu Client",
                    GameLauncherVersion = "1.0.0"
                };

                StatusText.Text = "Building Forge Minecraft process...";
                Process process = await launcher.BuildProcessAsync(forgeVersionName, options, CancellationToken.None);

                if (process == null)
                    throw new InvalidOperationException("CmlLib returned a null Forge Minecraft process.");

                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;
                process.OutputDataReceived += Minecraft_OutputDataReceived;
                process.ErrorDataReceived += Minecraft_ErrorDataReceived;

                WriteLog($"Forge executable: {process.StartInfo.FileName}");
                WriteLog($"Forge arguments: {process.StartInfo.Arguments}");
                WriteLog($"Forge working directory: {process.StartInfo.WorkingDirectory}");

                WriteDebugFile(process, javaPath, minecraftVersion, forgeVersionName, ram);

                StatusText.Text = $"Starting Forge {minecraftVersion}...";

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

            WriteLog("===== FORGE PERFORMANCE MOD INSTALL =====");

            using HttpClient http = new HttpClient();

            foreach ((string slug, string name) in ForgePerformanceMods)
            {
                try
                {
                    StatusText.Text = $"Checking Forge optimization: {name}...";

                    string url =
                        "https://api.modrinth.com/v2/project/" +
                        Uri.EscapeDataString(slug) +
                        "/version?loaders=%5B%22forge%22%5D&game_versions=%5B%22" +
                        Uri.EscapeDataString(minecraftVersion) +
                        "%22%5D";

                    using HttpResponseMessage response = await http.GetAsync(url);
                    response.EnsureSuccessStatusCode();

                    using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                    JsonElement versions = doc.RootElement;

                    if (versions.ValueKind != JsonValueKind.Array || versions.GetArrayLength() == 0)
                    {
                        WriteLog($"Skipped Forge optimization {name}: no compatible build for {minecraftVersion}.");
                        continue;
                    }

                    JsonElement? selectedFile = null;
                    foreach (JsonElement version in versions.EnumerateArray())
                    {
                        if (!version.TryGetProperty("files", out JsonElement files))
                            continue;

                        selectedFile = FindPrimaryJar(files);
                        if (selectedFile != null)
                            break;
                    }

                    if (selectedFile == null)
                    {
                        WriteLog($"Skipped Forge optimization {name}: no JAR returned.");
                        continue;
                    }

                    JsonElement file = selectedFile.Value;
                    string? downloadUrl = file.GetProperty("url").GetString();
                    string filename = file.GetProperty("filename").GetString() ?? slug + ".jar";

                    if (string.IsNullOrWhiteSpace(downloadUrl))
                    {
                        WriteLog($"Skipped Forge optimization {name}: download URL missing.");
                        continue;
                    }

                    string destination = System.IO.Path.Combine(modsFolder, SanitizeFileName(filename));
                    if (System.IO.File.Exists(destination) && new System.IO.FileInfo(destination).Length > 0)
                    {
                        WriteLog($"Forge optimization already installed: {name}");
                        continue;
                    }

                    using HttpResponseMessage modResponse = await http.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
                    modResponse.EnsureSuccessStatusCode();
                    await using System.IO.Stream source = await modResponse.Content.ReadAsStreamAsync();
                    await using System.IO.FileStream destinationStream = System.IO.File.Create(destination);
                    await source.CopyToAsync(destinationStream, CancellationToken.None);

                    if (!System.IO.File.Exists(destination) || new System.IO.FileInfo(destination).Length <= 0)
                        throw new InvalidOperationException($"Downloaded Forge optimization is empty: {name}");

                    WriteLog($"Installed Forge optimization: {name}");
                }
                catch (Exception ex)
                {
                    WriteLog($"Skipped Forge optimization {name}: {ex.Message}");
                }
            }

            WriteLog("===== FORGE PERFORMANCE MOD INSTALL COMPLETE =====");
        }
    }
}
