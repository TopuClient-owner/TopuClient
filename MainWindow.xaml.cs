using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.ProcessBuilder;

namespace TopuLauncher
{
    public partial class MainWindow : Window
    {
        private MSession? _session;

        private static readonly HttpClient _httpClient = new HttpClient(
            new HttpClientHandler
            {
                AllowAutoRedirect = true
            })
        {
            DefaultRequestHeaders =
            {
                {
                    "User-Agent",
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) TopuClient/1.0"
                }
            }
        };

        private readonly string _gamePath;
        private readonly string _configFilePath;
        private readonly string _logFilePath;

        private Process? _minecraftProcess;

        private const string MinecraftVersion = "1.21.1";
        private const string FabricLoaderVersion = "0.19.3";

        public MainWindow()
        {
            InitializeComponent();

            _gamePath = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.ApplicationData),
                ".topuclient");

            Directory.CreateDirectory(_gamePath);

            _configFilePath = Path.Combine(
                _gamePath,
                "username.txt");

            _logFilePath = Path.Combine(
                _gamePath,
                "topu-minecraft.log");

            LoadSavedUsername();
        }

        // =========================================================
        // USERNAME
        // =========================================================

        private void LoadSavedUsername()
        {
            try
            {
                if (!File.Exists(_configFilePath))
                    return;

                string savedUser =
                    File.ReadAllText(_configFilePath).Trim();

                if (string.IsNullOrWhiteSpace(savedUser))
                    return;

                if (UsernameInput != null)
                    UsernameInput.Text = savedUser;

                _session =
                    MSession.CreateOfflineSession(savedUser);
            }
            catch
            {
                // Ignore corrupted username file.
            }
        }

        private void SaveUsername(string? username)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(username))
                {
                    File.WriteAllText(
                        _configFilePath,
                        username.Trim());
                }
            }
            catch
            {
                // Non-critical.
            }
        }

        // =========================================================
        // WINDOW
        // =========================================================

        private void TitleBar_MouseDown(
            object sender,
            MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                try
                {
                    DragMove();
                }
                catch
                {
                    // Ignore drag errors.
                }
            }
        }

        private void Minimize_Click(
            object sender,
            RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void Close_Click(
            object sender,
            RoutedEventArgs e)
        {
            Close();
        }

        // =========================================================
        // TABS
        // =========================================================

        private void SwitchTab_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (sender is not Button btn)
                return;

            if (btn.Tag is not string targetTab)
                return;

            TabLaunch.Visibility = Visibility.Collapsed;
            TabProfiles.Visibility = Visibility.Collapsed;
            TabAccounts.Visibility = Visibility.Collapsed;

            Brush defaultColor =
                new SolidColorBrush(
                    Color.FromRgb(136, 136, 136));

            Thickness noBorder =
                new Thickness(0);

            TabLaunchBtn.Foreground = defaultColor;
            TabLaunchBtn.BorderThickness = noBorder;

            TabProfilesBtn.Foreground = defaultColor;
            TabProfilesBtn.BorderThickness = noBorder;

            TabAccountsBtn.Foreground = defaultColor;
            TabAccountsBtn.BorderThickness = noBorder;

            btn.Foreground =
                new SolidColorBrush(
                    Color.FromRgb(0, 255, 136));

            btn.BorderThickness =
                new Thickness(0, 0, 0, 2);

            switch (targetTab)
            {
                case "TabLaunch":
                    TabLaunch.Visibility =
                        Visibility.Visible;
                    break;

                case "TabProfiles":
                    TabProfiles.Visibility =
                        Visibility.Visible;
                    break;

                case "TabAccounts":
                    TabAccounts.Visibility =
                        Visibility.Visible;
                    break;
            }
        }

        // =========================================================
        // RAM
        // =========================================================

        private void RamSlider_ValueChanged(
            object sender,
            RoutedPropertyChangedEventArgs<double> e)
        {
            if (RamLabel != null)
            {
                RamLabel.Text =
                    $"{(int)e.NewValue}GB";
            }
        }

        // =========================================================
        // PROFILE
        // =========================================================

        private void SaveProfile_Click(
            object sender,
            RoutedEventArgs e)
        {
            string selectedVer =
                (VersionBox.SelectedItem as ComboBoxItem)
                ?.Content?.ToString()
                ?? MinecraftVersion;

            if (SelectedProfileLabel != null)
            {
                SelectedProfileLabel.Text =
                    $"Ready to launch Fabric {selectedVer}";
            }

            if (StatusText != null)
            {
                StatusText.Text =
                    $"Profile saved: Fabric {selectedVer} " +
                    $"with {(int)RamSlider.Value}GB RAM";
            }

            MessageBox.Show(
                "Profile settings saved successfully!",
                "Topu Client",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        // =========================================================
        // AUTH
        // =========================================================

        private void AuthTypeBox_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (StatusText == null)
                return;

            if (AuthTypeBox.SelectedIndex == 0)
            {
                StatusText.Text =
                    "Auth Mode: Offline / Cracked";
            }
            else
            {
                StatusText.Text =
                    "Auth Mode: Microsoft Official";
            }
        }

        private async void MsLoginBtn_Click(
            object sender,
            RoutedEventArgs e)
        {
            try
            {
                StatusText.Text =
                    "Microsoft Login requires interactive authentication.";

                MessageBox.Show(
                    "Microsoft authentication is not enabled in this build yet.\n\n" +
                    "Offline mode will continue to use a local username.",
                    "MS Login",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusText.Text =
                    "Microsoft Login Failed!";

                MessageBox.Show(
                    $"MS Login Error:\n{ex.Message}",
                    "Authentication Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                if (MsLoginBtn != null)
                    MsLoginBtn.IsEnabled = true;
            }

            await Task.CompletedTask;
        }

        // =========================================================
        // SERVER BUTTON
        // =========================================================

        private void JoinServer_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (sender is Button btn &&
                btn.Tag is string serverIp)
            {
                StatusText.Text =
                    $"Target server queued: {serverIp}";
            }
        }

        // =========================================================
        // MODRINTH SEARCH
        // =========================================================

        private async void SearchModrinth_Click(
            object sender,
            RoutedEventArgs e)
        {
            string query =
                ModSearchInput?.Text?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(query))
            {
                MessageBox.Show(
                    "Please enter a mod name to search on Modrinth.",
                    "Mod Search",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            try
            {
                ModSearchStatus.Text =
                    $"Searching Modrinth for '{query}'...";

                string searchUrl =
                    "https://api.modrinth.com/v2/search" +
                    $"?query={Uri.EscapeDataString(query)}" +
                    "&facets=%5B%5B%22project_type%3Amod%22%5D%5D";

                string response =
                    await _httpClient.GetStringAsync(
                        searchUrl);

                using JsonDocument doc =
                    JsonDocument.Parse(response);

                if (!doc.RootElement.TryGetProperty(
                        "hits",
                        out JsonElement hits) ||
                    hits.GetArrayLength() == 0)
                {
                    ModSearchStatus.Text =
                        "No mod found.";

                    return;
                }

                JsonElement firstHit = hits[0];

                string modTitle =
                    firstHit.TryGetProperty(
                        "title",
                        out JsonElement titleProp)
                    ? titleProp.GetString() ?? query
                    : query;

                string projectId =
                    firstHit.TryGetProperty(
                        "project_id",
                        out JsonElement idProp)
                    ? idProp.GetString() ?? ""
                    : "";

                if (string.IsNullOrWhiteSpace(projectId))
                {
                    ModSearchStatus.Text =
                        "Mod project ID was missing.";

                    return;
                }

                string targetVer =
                    (VersionBox?.SelectedItem as ComboBoxItem)
                    ?.Content?.ToString()
                    ?? MinecraftVersion;

                string versionsUrl =
                    "https://api.modrinth.com/v2/project/" +
                    $"{Uri.EscapeDataString(projectId)}/version" +
                    "?loaders=%5B%22fabric%22%5D" +
                    $"&game_versions=%5B%22{Uri.EscapeDataString(targetVer)}%22%5D";

                string versionsResponse =
                    await _httpClient.GetStringAsync(
                        versionsUrl);

                using JsonDocument versionsDoc =
                    JsonDocument.Parse(versionsResponse);

                JsonElement versions =
                    versionsDoc.RootElement;

                if (versions.GetArrayLength() == 0)
                {
                    ModSearchStatus.Text =
                        "No compatible Fabric version found.";

                    return;
                }

                JsonElement latest = versions[0];

                if (!latest.TryGetProperty(
                        "files",
                        out JsonElement files) ||
                    files.GetArrayLength() == 0)
                {
                    ModSearchStatus.Text =
                        "No downloadable mod file found.";

                    return;
                }

                string fileUrl = "";
                string fileName =
                    $"{SanitizeFileName(modTitle)}.jar";

                foreach (JsonElement file in
                         files.EnumerateArray())
                {
                    bool primary =
                        file.TryGetProperty(
                            "primary",
                            out JsonElement primaryProp) &&
                        primaryProp.ValueKind ==
                            JsonValueKind.True;

                    if (!primary)
                        continue;

                    if (file.TryGetProperty(
                            "url",
                            out JsonElement urlProp))
                    {
                        fileUrl =
                            urlProp.GetString() ?? "";
                    }

                    if (file.TryGetProperty(
                            "filename",
                            out JsonElement nameProp))
                    {
                        fileName =
                            nameProp.GetString() ??
                            fileName;
                    }

                    break;
                }

                if (string.IsNullOrWhiteSpace(fileUrl))
                {
                    if (files[0].TryGetProperty(
                            "url",
                            out JsonElement urlProp))
                    {
                        fileUrl =
                            urlProp.GetString() ?? "";
                    }

                    if (files[0].TryGetProperty(
                            "filename",
                            out JsonElement nameProp))
                    {
                        fileName =
                            nameProp.GetString() ??
                            fileName;
                    }
                }

                if (string.IsNullOrWhiteSpace(fileUrl))
                {
                    ModSearchStatus.Text =
                        "Mod download URL was missing.";

                    return;
                }

                string modsFolder =
                    Path.Combine(
                        _gamePath,
                        "mods");

                Directory.CreateDirectory(
                    modsFolder);

                string destination =
                    Path.Combine(
                        modsFolder,
                        SanitizeFileName(fileName));

                ModSearchStatus.Text =
                    $"Downloading {modTitle}...";

                byte[] data =
                    await _httpClient.GetByteArrayAsync(
                        fileUrl);

                await File.WriteAllBytesAsync(
                    destination,
                    data);

                ModSearchStatus.Text =
                    $"Successfully downloaded: {modTitle}!";

                MessageBox.Show(
                    $"Mod '{modTitle}' was added to:\n\n{destination}",
                    "Modrinth",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ModSearchStatus.Text =
                    "Mod search failed.";

                MessageBox.Show(
                    $"Modrinth error:\n\n{ex.Message}",
                    "Modrinth Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // =========================================================
        // MAIN LAUNCH
        // =========================================================

        private async void LaunchBtn_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_minecraftProcess != null &&
                !_minecraftProcess.HasExited)
            {
                MessageBox.Show(
                    "Minecraft is already running.",
                    "Topu Client",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }

            LaunchBtn.IsEnabled = false;

            try
            {
                KillOldMinecraftProcesses();

                string targetVer =
                    (VersionBox.SelectedItem as ComboBoxItem)
                    ?.Content?.ToString()
                    ?? MinecraftVersion;

                if (string.IsNullOrWhiteSpace(targetVer))
                    targetVer = MinecraftVersion;

                // -------------------------------------------------
                // OFFLINE SESSION
                // -------------------------------------------------

                if (AuthTypeBox.SelectedIndex == 0)
                {
                    string username =
                        UsernameInput?.Text?.Trim() ?? "";

                    if (string.IsNullOrWhiteSpace(username))
                        username = "TopuPlayer";

                    _session =
                        MSession.CreateOfflineSession(
                            username);

                    SaveUsername(username);
                }

                if (_session == null)
                {
                    throw new InvalidOperationException(
                        "No Minecraft session was created.");
                }

                // -------------------------------------------------
                // GAME DIRECTORY
                // -------------------------------------------------

                Directory.CreateDirectory(
                    _gamePath);

                var minecraftPath =
                    new MinecraftPath(_gamePath);

                // -------------------------------------------------
                // MODS
                // -------------------------------------------------

                string modsFolder =
                    Path.Combine(
                        _gamePath,
                        "mods");

                await EnsureEssentialModsDownloaded(
                    modsFolder,
                    targetVer);

                // -------------------------------------------------
                // JAVA
                // -------------------------------------------------

                StatusText.Text =
                    "Checking Java 21...";

                string javaPath =
                    FindJava21();

                if (string.IsNullOrWhiteSpace(javaPath))
                {
                    StatusText.Text =
                        "Java 21 was not found.";

                    MessageBox.Show(
                        "The launcher could not find its Java 21 runtime.\n\n" +
                        "Make sure your runtime is located at:\n" +
                        Path.Combine(
                            _gamePath,
                            "runtime",
                            "java21",
                            "bin",
                            "java.exe"),
                        "Java Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);

                    return;
                }

                StatusText.Text =
                    $"Java 21 found:\n{javaPath}";

                // -------------------------------------------------
                // MINECRAFT INSTALL
                // -------------------------------------------------

                var launcher =
                    new MinecraftLauncher(
                        minecraftPath);

                launcher.FileProgressChanged +=
                    Launcher_FileProgressChanged;

                launcher.ByteProgressChanged +=
                    Launcher_ByteProgressChanged;

                StatusText.Text =
                    $"Installing Minecraft {targetVer}...";

                await launcher.InstallAsync(
                    targetVer);

                // -------------------------------------------------
                // FABRIC
                // -------------------------------------------------

                StatusText.Text =
                    $"Installing Fabric {FabricLoaderVersion}...";

                string fabricVersion =
                    await InstallFabricProfileAsync(
                        _gamePath,
                        targetVer,
                        FabricLoaderVersion);

                StatusText.Text =
                    $"Fabric installed: {fabricVersion}";

                // -------------------------------------------------
                // LAUNCH OPTIONS
                // -------------------------------------------------

                int ramMb =
                    Math.Max(
                        1024,
                        (int)RamSlider.Value * 1024);

                var launchOptions =
                    new MLaunchOption
                    {
                        Session = _session,
                        MaximumRamMb = ramMb,
                        JavaPath = javaPath
                    };

                // -------------------------------------------------
                // BUILD PROCESS
                // -------------------------------------------------

                StatusText.Text =
                    "Creating Minecraft process...";

                var process =
                    await launcher.BuildProcessAsync(
                        fabricVersion,
                        launchOptions);

                if (process == null)
                {
                    throw new InvalidOperationException(
                        "CmlLib returned a null Minecraft process.");
                }

                _minecraftProcess = process;

                // -------------------------------------------------
                // DEBUG INFO
                // -------------------------------------------------

                string debugFile =
                    Path.Combine(
                        _gamePath,
                        "topu-launch-debug.txt");

                try
                {
                    File.WriteAllText(
                        debugFile,
                        BuildDebugInfo(
                            process,
                            javaPath,
                            fabricVersion,
                            targetVer,
                            ramMb));
                }
                catch
                {
                    // Debug file is non-critical.
                }

                // -------------------------------------------------
                // PROCESS OUTPUT
                // -------------------------------------------------

                AttachProcessLogging(
                    process);

                StatusText.Text =
                    $"Starting Fabric {targetVer}...";

                bool started =
                    process.Start();

                if (!started)
                {
                    throw new InvalidOperationException(
                        "Minecraft process.Start() returned false.");
                }

                StatusText.Text =
                    $"Topu Client running as {_session.Username}";

                _ = MonitorMinecraftProcessAsync(
                    process);
            }
            catch (Exception ex)
            {
                StatusText.Text =
                    "Launch Failed!";

                WriteLauncherException(ex);

                MessageBox.Show(
                    "Minecraft failed to start.\n\n" +
                    ex.Message +
                    "\n\n" +
                    "A detailed log was written to:\n" +
                    _logFilePath +
                    "\n\n" +
                    "Launch debug information:\n" +
                    Path.Combine(
                        _gamePath,
                        "topu-launch-debug.txt"),
                    "Topu Client Launch Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                LaunchBtn.IsEnabled = true;
            }
        }

        // =========================================================
        // JAVA
        // =========================================================

        private string FindJava21()
        {
            string localJava =
                Path.Combine(
                    _gamePath,
                    "runtime",
                    "java21",
                    "bin",
                    "java.exe");

            if (File.Exists(localJava))
                return localJava;

            string javaHome =
                Environment.GetEnvironmentVariable(
                    "JAVA_HOME") ?? "";

            if (!string.IsNullOrWhiteSpace(javaHome))
            {
                string javaHomeExe =
                    Path.Combine(
                        javaHome,
                        "bin",
                        "java.exe");

                if (File.Exists(javaHomeExe))
                {
                    try
                    {
                        if (IsJava21(javaHomeExe))
                            return javaHomeExe;
                    }
                    catch
                    {
                    }
                }
            }

            string path =
                Environment.GetEnvironmentVariable(
                    "PATH") ?? "";

            foreach (string part in
                     path.Split(
                         Path.PathSeparator,
                         StringSplitOptions.RemoveEmptyEntries))
            {
                string candidate =
                    Path.Combine(
                        part.Trim(),
                        "java.exe");

                if (!File.Exists(candidate))
                    continue;

                try
                {
                    if (IsJava21(candidate))
                        return candidate;
                }
                catch
                {
                }
            }

            return "";
        }

        private bool IsJava21(string javaPath)
        {
            var psi =
                new ProcessStartInfo
                {
                    FileName = javaPath,
                    Arguments = "-version",
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

            using Process p =
                Process.Start(psi)
                ?? throw new InvalidOperationException(
                    "Could not start java.exe.");

            string output =
                p.StandardError.ReadToEnd();

            p.WaitForExit();

            return output.Contains(
                "version \"21.",
                StringComparison.OrdinalIgnoreCase);
        }

        // =========================================================
        // FABRIC
        // =========================================================

        private async Task<string>
            InstallFabricProfileAsync(
                string gamePath,
                string mcVersion,
                string loaderVersion)
        {
            string fabricVersionId =
                $"fabric-loader-{loaderVersion}-{mcVersion}";

            string versionsFolder =
                Path.Combine(
                    gamePath,
                    "versions");

            string versionFolder =
                Path.Combine(
                    versionsFolder,
                    fabricVersionId);

            string jsonFile =
                Path.Combine(
                    versionFolder,
                    $"{fabricVersionId}.json");

            Directory.CreateDirectory(
                versionFolder);

            string apiUrl =
                "https://meta.fabricmc.net/v2/versions/loader/" +
                $"{Uri.EscapeDataString(mcVersion)}/" +
                $"{Uri.EscapeDataString(loaderVersion)}/" +
                "profile/json";

            StatusText.Text =
                $"Downloading Fabric profile {loaderVersion}...";

            string json =
                await _httpClient.GetStringAsync(
                    apiUrl);

            if (string.IsNullOrWhiteSpace(json))
            {
                throw new InvalidOperationException(
                    "Fabric returned an empty profile.");
            }

            await File.WriteAllTextAsync(
                jsonFile,
                json);

            return fabricVersionId;
        }

        // =========================================================
        // MOD DOWNLOADS
        // =========================================================

        private async Task
            EnsureEssentialModsDownloaded(
                string modsFolder,
                string mcVersion)
        {
            Directory.CreateDirectory(
                modsFolder);

            var mods =
                new[]
                {
                    ("fabric-api", "fabric-api"),
                    ("sodium", "sodium"),
                    ("lithium", "lithium"),
                    ("ferritecore", "ferritecore"),
                    ("sodium-extra", "sodium-extra"),
                    ("dynamic-fps", "dynamic-fps")
                };

            foreach (var mod in mods)
            {
                try
                {
                    StatusText.Text =
                        $"Checking {mod.Item1}...";

                    string searchUrl =
                        "https://api.modrinth.com/v2/search" +
                        $"?query={Uri.EscapeDataString(mod.Item2)}" +
                        "&facets=%5B%5B%22project_type%3Amod%22%5D%5D";

                    string searchResponse =
                        await _httpClient.GetStringAsync(
                            searchUrl);

                    using JsonDocument searchDoc =
                        JsonDocument.Parse(
                            searchResponse);

                    if (!searchDoc.RootElement
                            .TryGetProperty(
                                "hits",
                                out JsonElement hits) ||
                        hits.GetArrayLength() == 0)
                    {
                        continue;
                    }

                    string projectId =
                        hits[0]
                            .GetProperty("project_id")
                            .GetString() ?? "";

                    if (string.IsNullOrWhiteSpace(
                            projectId))
                    {
                        continue;
                    }

                    string versionsUrl =
                        "https://api.modrinth.com/v2/project/" +
                        $"{Uri.EscapeDataString(projectId)}/version" +
                        "?loaders=%5B%22fabric%22%5D" +
                        $"&game_versions=%5B%22{Uri.EscapeDataString(mcVersion)}%22%5D";

                    string versionsResponse =
                        await _httpClient.GetStringAsync(
                            versionsUrl);

                    using JsonDocument versionsDoc =
                        JsonDocument.Parse(
                            versionsResponse);

                    JsonElement versions =
                        versionsDoc.RootElement;

                    if (versions.GetArrayLength() == 0)
                        continue;

                    JsonElement version =
                        versions[0];

                    if (!version.TryGetProperty(
                            "files",
                            out JsonElement files) ||
                        files.GetArrayLength() == 0)
                    {
                        continue;
                    }

                    string fileUrl = "";
                    string fileName =
                        $"{mod.Item1}.jar";

                    foreach (JsonElement file in
                             files.EnumerateArray())
                    {
                        bool primary =
                            file.TryGetProperty(
                                "primary",
                                out JsonElement primaryProp) &&
                            primaryProp.ValueKind ==
                                JsonValueKind.True;

                        if (!primary)
                            continue;

                        if (file.TryGetProperty(
                                "url",
                                out JsonElement urlProp))
                        {
                            fileUrl =
                                urlProp.GetString() ?? "";
                        }

                        if (file.TryGetProperty(
                                "filename",
                                out JsonElement filenameProp))
                        {
                            fileName =
                                filenameProp.GetString()
                                ?? fileName;
                        }

                        break;
                    }

                    if (string.IsNullOrWhiteSpace(
                            fileUrl))
                    {
                        if (files[0].TryGetProperty(
                                "url",
                                out JsonElement urlProp))
                        {
                            fileUrl =
                                urlProp.GetString() ?? "";
                        }

                        if (files[0].TryGetProperty(
                                "filename",
                                out JsonElement filenameProp))
                        {
                            fileName =
                                filenameProp.GetString()
                                ?? fileName;
                        }
                    }

                    if (string.IsNullOrWhiteSpace(
                            fileUrl))
                    {
                        continue;
                    }

                    string destination =
                        Path.Combine(
                            modsFolder,
                            SanitizeFileName(
                                fileName));

                    if (File.Exists(destination) &&
                        new FileInfo(destination).Length >
                            5000)
                    {
                        continue;
                    }

                    StatusText.Text =
                        $"Downloading {mod.Item1}...";

                    byte[] data =
                        await _httpClient.GetByteArrayAsync(
                            fileUrl);

                    await File.WriteAllBytesAsync(
                        destination,
                        data);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(
                        $"Mod skipped: {mod.Item1}: {ex.Message}");
                }
            }
        }

        // =========================================================
        // CMLLIB PROGRESS
        // =========================================================

        private void Launcher_FileProgressChanged(
            object? sender,
            FileProgressChangedEventArgs args)
        {
            Dispatcher.Invoke(
                () =>
                {
                    StatusText.Text =
                        $"Checking {args.Name} " +
                        $"({args.EventType})";
                });
        }

        private void Launcher_ByteProgressChanged(
            object? sender,
            ByteProgress args)
        {
            Dispatcher.Invoke(
                () =>
                {
                    if (args.TotalBytes > 0)
                    {
                        double percentage =
                            args.ProgressedBytes *
                            100.0 /
                            args.TotalBytes;

                        StatusText.Text =
                            $"Downloading: {percentage:0}%";
                    }
                    else
                    {
                        StatusText.Text =
                            $"Downloading: " +
                            $"{args.ProgressedBytes:N0} bytes";
                    }
                });
        }

        // =========================================================
        // PROCESS LOGGING
        // =========================================================

        private void AttachProcessLogging(
            Process process)
        {
            try
            {
                Directory.CreateDirectory(
                    _gamePath);

                File.WriteAllText(
                    _logFilePath,
                    "===== TOPU CLIENT MINECRAFT LOG =====\r\n" +
                    $"Started: {DateTime.Now:O}\r\n\r\n");

                process.OutputDataReceived +=
                    Minecraft_OutputDataReceived;

                process.ErrorDataReceived +=
                    Minecraft_ErrorDataReceived;

                process.EnableRaisingEvents = true;

                try
                {
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                }
                catch
                {
                    // Some Process implementations may not
                    // expose redirected output.
                }
            }
            catch
            {
            }
        }

        private void Minecraft_OutputDataReceived(
            object sender,
            DataReceivedEventArgs e)
        {
            if (e.Data == null)
                return;

            AppendMinecraftLog(
                "[STDOUT] " + e.Data);
        }

        private void Minecraft_ErrorDataReceived(
            object sender,
            DataReceivedEventArgs e)
        {
            if (e.Data == null)
                return;

            AppendMinecraftLog(
                "[STDERR] " + e.Data);
        }

        private void AppendMinecraftLog(
            string text)
        {
            try
            {
                File.AppendAllText(
                    _logFilePath,
                    $"[{DateTime.Now:HH:mm:ss}] {text}\r\n");
            }
            catch
            {
            }
        }

        // =========================================================
        // PROCESS MONITOR
        // =========================================================

        private async Task
            MonitorMinecraftProcessAsync(
                Process process)
        {
            try
            {
                await Task.Run(
                    () => process.WaitForExit());

                int exitCode =
                    process.ExitCode;

                AppendMinecraftLog(
                    $"===== MINECRAFT EXITED: {exitCode} =====");

                Dispatcher.Invoke(
                    () =>
                    {
                        if (exitCode == 0)
                        {
                            StatusText.Text =
                                "Minecraft closed normally.";
                        }
                        else
                        {
                            StatusText.Text =
                                $"Minecraft exited with code {exitCode}";

                            MessageBox.Show(
                                "Minecraft exited unexpectedly.\n\n" +
                                $"Exit code: {exitCode}\n\n" +
                                "Open this file for the actual error:\n" +
                                _logFilePath,
                                "Minecraft Crash",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
                        }
                    });
            }
            catch (Exception ex)
            {
                AppendMinecraftLog(
                    "PROCESS MONITOR ERROR: " +
                    ex);

                Dispatcher.Invoke(
                    () =>
                    {
                        StatusText.Text =
                            "Minecraft process monitoring failed.";
                    });
            }
            finally
            {
                _minecraftProcess = null;
            }
        }

        // =========================================================
        // DEBUG
        // =========================================================

        private string BuildDebugInfo(
            Process process,
            string javaPath,
            string fabricVersion,
            string mcVersion,
            int ramMb)
        {
            string text =
                "===== TOPU CLIENT DEBUG =====\r\n\r\n" +
                $"Executable:\r\n" +
                $"{javaPath}\r\n\r\n" +
                $"Arguments:\r\n" +
                $"{GetProcessArguments(process)}\r\n\r\n" +
                $"Working Directory:\r\n" +
                $"{_gamePath}\r\n\r\n" +
                $"Java:\r\n" +
                $"{javaPath}\r\n\r\n" +
                $"Minecraft:\r\n" +
                $"{mcVersion}\r\n\r\n" +
                $"Fabric:\r\n" +
                $"{fabricVersion}\r\n\r\n" +
                $"RAM:\r\n" +
                $"{ramMb} MB\r\n\r\n" +
                $"Session:\r\n" +
                $"{_session?.Username}\r\n";

            return text;
        }

        private string GetProcessArguments(
            Process process)
        {
            try
            {
                return process.StartInfo.Arguments;
            }
            catch
            {
                return "(arguments unavailable)";
            }
        }

        private void WriteLauncherException(
            Exception ex)
        {
            try
            {
                File.AppendAllText(
                    _logFilePath,
                    "\r\n===== TOPU LAUNCHER EXCEPTION =====\r\n" +
                    $"Time: {DateTime.Now:O}\r\n" +
                    ex +
                    "\r\n");
            }
            catch
            {
            }
        }

        // =========================================================
        // PROCESS CLEANUP
        // =========================================================

        private void KillOldMinecraftProcesses()
        {
            try
            {
                foreach (Process process in
                         Process.GetProcessesByName("javaw"))
                {
                    try
                    {
                        process.Kill();
                    }
                    catch
                    {
                    }

                    process.Dispose();
                }

                foreach (Process process in
                         Process.GetProcessesByName("java"))
                {
                    try
                    {
                        process.Kill();
                    }
                    catch
                    {
                    }

                    process.Dispose();
                }
            }
            catch
            {
            }
        }

        // =========================================================
        // FILENAME SANITIZER
        // =========================================================

        private static string
            SanitizeFileName(string filename)
        {
            foreach (char c in
                     Path.GetInvalidFileNameChars())
            {
                filename =
                    filename.Replace(c, '_');
            }

            return filename;
        }
    }
}
