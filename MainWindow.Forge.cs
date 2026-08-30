using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CmlLib.Core;
using CmlLib.Core.Installer.Forge;

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

        private bool _forgeHooksReady;
        private bool _forgeLaunching;
        private bool _loadingLoaderState;

        protected override void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);

            if (_forgeHooksReady)
                return;

            _forgeHooksReady = true;

            LoaderSelector.SelectionChanged += LoaderSelector_Changed;
            VersionBox.SelectionChanged += VersionBox_Changed;
            ProfileSelector.SelectionChanged += ProfileSelector_Changed;
            SaveProfileButton.Click += SaveProfileButton_Changed;
            LaunchBtn.PreviewMouseLeftButtonDown += LaunchBtn_ForgeMouseDown;
            LaunchBtn.PreviewKeyDown += LaunchBtn_ForgeKeyDown;

            LoadLoaderStateForProfile();
        }

        private string GetSelectedLoaderName()
        {
            return (LoaderSelector.SelectedItem as ComboBoxItem)?.Content?.ToString()?.Trim()
                   ?? "Fabric";
        }

        private string GetLoaderStatePath()
        {
            return Path.Combine(_gamePath, "topu-loader.txt");
        }

        private void SaveLoaderState()
        {
            try
            {
                Directory.CreateDirectory(_gamePath);
                File.WriteAllText(GetLoaderStatePath(), GetSelectedLoaderName());
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
                string path = Path.Combine(_gamePath, "topu-profile.json");
                if (!File.Exists(path))
                    return;

                using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
                if (!doc.RootElement.TryGetProperty("Version", out JsonElement versionElement))
                    return;

                string? savedVersion = versionElement.GetString();
                if (string.IsNullOrWhiteSpace(savedVersion))
                    return;

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

        private void LoadLoaderStateForProfile()
        {
            try
            {
                _loadingLoaderState = true;

                string loader = "Fabric";
                string path = GetLoaderStatePath();
                if (File.Exists(path))
                    loader = File.ReadAllText(path).Trim();

                LoaderSelector.SelectedIndex =
                    loader.Equals("Forge", StringComparison.OrdinalIgnoreCase) ? 1 : 0;

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

        private void LoaderSelector_Changed(object? sender, SelectionChangedEventArgs e)
        {
            if (_loadingLoaderState)
                return;

            SaveLoaderState();
            UpdateLoaderUi();
            ValidateForgeVersion();
        }

        private void VersionBox_Changed(object? sender, SelectionChangedEventArgs e)
        {
            if (_loadingLoaderState)
                return;

            UpdateLoaderUi();
            ValidateForgeVersion();
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

                LaunchLoaderLabel.Text = loader;
                LaunchVersionLabel.Text = version;
                LaunchRamLabel.Text = $"{ram}GB RAM";
                LaunchProfileLabel.Text = GetActiveProfileName();
                LaunchBtn.Content = loader.Equals("Forge", StringComparison.OrdinalIgnoreCase)
                    ? "⚡   LAUNCH FORGE"
                    : "⚡   LAUNCH FABRIC";

                SelectedProfileLabel.Text =
                    $"● {GetActiveProfileName()}   •   {loader} {version}   •   {ram}GB RAM";

                LoaderStatusLabel.Text = loader.Equals("Forge", StringComparison.OrdinalIgnoreCase)
                    ? "Forge optimization stack • 1.20.1 / 1.8.9"
                    : "Fabric optimization stack";

                ModSearchStatus.Text = loader.Equals("Forge", StringComparison.OrdinalIgnoreCase)
                    ? "Forge optimization stack installs automatically on launch for supported versions."
                    : "Fabric optimization stack installs automatically.";
            }
            catch
            {
            }
        }

        private void ValidateForgeVersion()
        {
            if (!GetSelectedLoaderName().Equals("Forge", StringComparison.OrdinalIgnoreCase))
                return;

            string version = GetSelectedVersion();
            if (version != "1.20.1" && version != "1.8.9")
                StatusText.Text = "Forge profiles currently support Minecraft 1.20.1 and 1.8.9.";
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
                catch { }

                _minecraftProcess = null;
            }

            string minecraftVersion = GetSelectedVersion();
            if (minecraftVersion != "1.20.1" && minecraftVersion != "1.8.9")
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

                int javaMajor = minecraftVersion == "1.8.9" ? 8 : 17;
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
                        catch { }
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
                        catch { }
                    });

                StatusText.Text = $"Finding latest Forge for Minecraft {minecraftVersion}...";

                ForgeInstaller forgeInstaller = new ForgeInstaller(launcher, Http);
                var forgeVersions = await forgeInstaller.GetForgeVersions(minecraftVersion);
                var latest = forgeVersions.FirstOrDefault(v => v.IsLatestVersion)
                             ?? forgeVersions.FirstOrDefault();

                if (latest == null)
                    throw new InvalidOperationException($"No Forge version was found for Minecraft {minecraftVersion}.");

                WriteLog($"Latest Forge: {latest.ForgeVersionName}");

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
                WriteLog($"Forge version profile: {forgeVersionName}");

                StatusText.Text = "Installing Minecraft dependencies...";
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
            string modsFolder = Path.Combine(_gamePath, "mods");
            Directory.CreateDirectory(modsFolder);

            WriteLog("===== FORGE PERFORMANCE MOD INSTALL =====");

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

                    using HttpResponseMessage response = await Http.GetAsync(url);
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

                    string destination = Path.Combine(modsFolder, SanitizeFileName(filename));
                    if (File.Exists(destination) && new FileInfo(destination).Length > 0)
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

            WriteLog("===== FORGE PERFORMANCE MOD INSTALL COMPLETE =====");
        }
    }
}
