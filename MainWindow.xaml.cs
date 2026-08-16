using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
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
using CmlLib.Core.ModLoaders.FabricMC;
using CmlLib.Core.ProcessBuilder;

namespace TopuLauncher
{
    public partial class MainWindow : Window
    {
        private MSession? _session;
        private Process? _minecraftProcess;

        private readonly string _gamePath;
        private readonly string _configFilePath;
        private readonly string _logFilePath;
        private readonly string _debugFilePath;

        private string? _serverToJoin;

        private const string DefaultMinecraftVersion = "1.21.1";
        private const string FabricLoaderVersion = "0.19.3";

        private static readonly HttpClient _httpClient =
            new HttpClient(
                new HttpClientHandler
                {
                    AllowAutoRedirect = true
                })
            {
                Timeout = TimeSpan.FromMinutes(15)
            };

        static MainWindow()
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
                "TopuClient/1.0");
        }

        public MainWindow()
        {
            InitializeComponent();

            _gamePath = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.ApplicationData),
                ".topuclient");

            _configFilePath =
                Path.Combine(
                    _gamePath,
                    "username.txt");

            _logFilePath =
                Path.Combine(
                    _gamePath,
                    "topu-minecraft.log");

            _debugFilePath =
                Path.Combine(
                    _gamePath,
                    "topu-launch-debug.txt");

            Directory.CreateDirectory(_gamePath);

            LoadSavedUsername();

            if (RamLabel != null)
            {
                RamLabel.Text =
                    $"{(int)RamSlider.Value}GB";
            }

            if (StatusText != null)
            {
                StatusText.Text = "Ready.";
            }
        }

        // ============================================================
        // USERNAME
        // ============================================================

        private void LoadSavedUsername()
        {
            try
            {
                if (!File.Exists(_configFilePath))
                    return;

                string username =
                    File.ReadAllText(_configFilePath).Trim();

                if (string.IsNullOrWhiteSpace(username))
                    return;

                if (UsernameInput != null)
                    UsernameInput.Text = username;

                _session =
                    MSession.CreateOfflineSession(username);
            }
            catch
            {
                // Ignore invalid saved username.
            }
        }

        private void SaveUsername(string username)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(username))
                    return;

                File.WriteAllText(
                    _configFilePath,
                    username.Trim());
            }
            catch
            {
                // Not critical.
            }
        }

        // ============================================================
        // WINDOW
        // ============================================================

        private void TitleBar_MouseDown(
            object sender,
            MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left)
                return;

            try
            {
                DragMove();
            }
            catch
            {
                // Ignore drag errors.
            }
        }

        private void Minimize_Click(
            object sender,
            RoutedEventArgs e)
        {
            WindowState =
                WindowState.Minimized;
        }

        private void Close_Click(
            object sender,
            RoutedEventArgs e)
        {
            Close();
        }

        // ============================================================
        // TABS
        // ============================================================

        private void SwitchTab_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (sender is not Button button)
                return;

            if (button.Tag is not string targetTab)
                return;

            TabLaunch.Visibility =
                Visibility.Collapsed;

            TabProfiles.Visibility =
                Visibility.Collapsed;

            TabAccounts.Visibility =
                Visibility.Collapsed;

            Brush inactive =
                new SolidColorBrush(
                    Color.FromRgb(
                        136,
                        136,
                        136));

            TabLaunchBtn.Foreground = inactive;
            TabLaunchBtn.BorderThickness =
                new Thickness(0);

            TabProfilesBtn.Foreground = inactive;
            TabProfilesBtn.BorderThickness =
                new Thickness(0);

            TabAccountsBtn.Foreground = inactive;
            TabAccountsBtn.BorderThickness =
                new Thickness(0);

            button.Foreground =
                new SolidColorBrush(
                    Color.FromRgb(
                        0,
                        255,
                        136));

            button.BorderThickness =
                new Thickness(
                    0,
                    0,
                    0,
                    2);

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

        // ============================================================
        // RAM
        // ============================================================

        private void RamSlider_ValueChanged(
            object sender,
            RoutedPropertyChangedEventArgs<double> e)
        {
            if (RamLabel == null)
                return;

            RamLabel.Text =
                $"{(int)e.NewValue}GB";
        }

        // ============================================================
        // PROFILE
        // ============================================================

        private void SaveProfile_Click(
            object sender,
            RoutedEventArgs e)
        {
            string version =
                GetSelectedMinecraftVersion();

            int ram =
                (int)RamSlider.Value;

            if (SelectedProfileLabel != null)
            {
                SelectedProfileLabel.Text =
                    $"Ready to launch Fabric {version}";
            }

            if (StatusText != null)
            {
                StatusText.Text =
                    $"Profile saved: Fabric {version} with {ram}GB RAM";
            }

            MessageBox.Show(
                "Profile settings saved successfully.",
                "Topu Client",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        // ============================================================
        // AUTH
        // ============================================================

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
                    "Microsoft login is not enabled in this build.";

                MessageBox.Show(
                    "Microsoft authentication is not implemented in this build yet.\n\n" +
                    "Offline mode remains available.",
                    "Microsoft Login",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusText.Text =
                    "Microsoft Login Failed.";

                MessageBox.Show(
                    ex.Message,
                    "Authentication Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }

            await Task.CompletedTask;
        }

        // ============================================================
        // SERVER BUTTONS
        // ============================================================

        private void JoinServer_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (sender is not Button button)
                return;

            if (button.Tag is not string server)
                return;

            _serverToJoin = server;

            StatusText.Text =
                $"Server selected: {server}";
        }

        // ============================================================
        // VERSION
        // ============================================================

        private string GetSelectedMinecraftVersion()
        {
            string version =
                (VersionBox.SelectedItem as ComboBoxItem)
                    ?.Content
                    ?.ToString()
                ?? DefaultMinecraftVersion;

            if (string.IsNullOrWhiteSpace(version))
                return DefaultMinecraftVersion;

            return version.Trim();
        }

        // ============================================================
        // MODRINTH SEARCH
        // ============================================================

        private async void SearchModrinth_Click(
            object sender,
            RoutedEventArgs e)
        {
            string query =
                ModSearchInput?.Text?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(query))
            {
                MessageBox.Show(
                    "Enter a mod name first.",
                    "Modrinth",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            try
            {
                ModSearchStatus.Text =
                    $"Searching Modrinth for {query}...";

                string searchUrl =
                    "https://api.modrinth.com/v2/search" +
                    $"?query={Uri.EscapeDataString(query)}" +
                    "&facets=%5B%5B%22project_type%3Amod%22%5D%5D";

                string response =
                    await _httpClient.GetStringAsync(
                        searchUrl);

                using JsonDocument document =
                    JsonDocument.Parse(response);

                if (!document.RootElement.TryGetProperty(
                        "hits",
                        out JsonElement hits) ||
                    hits.GetArrayLength() == 0)
                {
                    ModSearchStatus.Text =
                        "No compatible mod found.";

                    return;
                }

                JsonElement hit =
                    hits[0];

                string title =
                    hit.TryGetProperty(
                        "title",
                        out JsonElement titleElement)
                    ? titleElement.GetString() ?? query
                    : query;

                string projectId =
                    hit.TryGetProperty(
                        "project_id",
                        out JsonElement idElement)
                    ? idElement.GetString() ?? ""
                    : "";

                if (string.IsNullOrWhiteSpace(projectId))
                {
                    ModSearchStatus.Text =
                        "Mod project ID was missing.";

                    return;
                }

                string version =
                    GetSelectedMinecraftVersion();

                string fileUrl =
                    await GetModrinthFileUrl(
                        projectId,
                        version);

                if (string.IsNullOrWhiteSpace(fileUrl))
                {
                    ModSearchStatus.Text =
                        $"No Fabric {version} file found for {title}.";

                    return;
                }

                string modsFolder =
                    Path.Combine(
                        _gamePath,
                        "mods");

                Directory.CreateDirectory(
                    modsFolder);

                string filename =
                    GetFilenameFromUrl(
                        fileUrl,
                        SanitizeFileName(title) + ".jar");

                string destination =
                    Path.Combine(
                        modsFolder,
                        filename);

                ModSearchStatus.Text =
                    $"Downloading {title}...";

                byte[] data =
                    await _httpClient.GetByteArrayAsync(
                        fileUrl);

                await File.WriteAllBytesAsync(
                    destination,
                    data);

                ModSearchStatus.Text =
                    $"Added {title}.";

                MessageBox.Show(
                    $"Installed {title}.\n\n{destination}",
                    "Modrinth",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ModSearchStatus.Text =
                    "Modrinth search failed.";

                WriteLauncherException(
                    "MODRINTH ERROR",
                    ex);

                MessageBox.Show(
                    $"Modrinth error:\n\n{ex.Message}",
                    "Modrinth Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async Task<string> GetModrinthFileUrl(
            string projectId,
            string minecraftVersion)
        {
            string versionsUrl =
                "https://api.modrinth.com/v2/project/" +
                Uri.EscapeDataString(projectId) +
                "/version" +
                "?loaders=%5B%22fabric%22%5D" +
                $"&game_versions=%5B%22" +
                Uri.EscapeDataString(minecraftVersion) +
                "%22%5D";

            string response =
                await _httpClient.GetStringAsync(
                    versionsUrl);

            using JsonDocument document =
                JsonDocument.Parse(response);

            JsonElement versions =
                document.RootElement;

            foreach (JsonElement version in
                     versions.EnumerateArray())
            {
                if (!version.TryGetProperty(
                        "files",
                        out JsonElement files))
                {
                    continue;
                }

                foreach (JsonElement file in
                         files.EnumerateArray())
                {
                    bool primary =
                        file.TryGetProperty(
                            "primary",
                            out JsonElement primaryElement) &&
                        primaryElement.ValueKind ==
                            JsonValueKind.True;

                    if (file.TryGetProperty(
                            "url",
                            out JsonElement urlElement))
                    {
                        string url =
                            urlElement.GetString() ?? "";

                        if (primary &&
                            !string.IsNullOrWhiteSpace(url))
                        {
                            return url;
                        }
                    }
                }

                foreach (JsonElement file in
                         files.EnumerateArray())
                {
                    if (file.TryGetProperty(
                            "url",
                            out JsonElement urlElement))
                    {
                        string url =
                            urlElement.GetString() ?? "";

                        if (!string.IsNullOrWhiteSpace(url))
                            return url;
                    }
                }
            }

            return "";
        }

        // ============================================================
        // LAUNCH
        // ============================================================

        private async void LaunchBtn_Click(
            object sender,
            RoutedEventArgs e)
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
                catch
                {
                    // Process already disappeared.
                }

                _minecraftProcess = null;
            }

            LaunchBtn.IsEnabled = false;

            try
            {
                string minecraftVersion =
                    GetSelectedMinecraftVersion();

                int ramMb =
                    Math.Max(
                        1024,
                        (int)RamSlider.Value * 1024);

                StartNewLog();

                WriteLog(
                    "===== TOPU CLIENT LAUNCH =====");

                WriteLog(
                    $"Time: {DateTime.Now:O}");

                WriteLog(
                    $"Minecraft: {minecraftVersion}");

                WriteLog(
                    $"Fabric Loader: {FabricLoaderVersion}");

                WriteLog(
                    $"RAM: {ramMb} MB");

                // ----------------------------------------------------
                // SESSION
                // ----------------------------------------------------

                StatusText.Text =
                    "Creating Minecraft session...";

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

                    WriteLog(
                        $"Offline username: {username}");
                }
                else
                {
                    throw new InvalidOperationException(
                        "Microsoft authentication is not implemented yet. " +
                        "Select Offline / Cracked Mode.");
                }

                if (_session == null)
                {
                    throw new InvalidOperationException(
                        "Minecraft session could not be created.");
                }

                // ----------------------------------------------------
                // GAME DIRECTORY
                // ----------------------------------------------------

                Directory.CreateDirectory(
                    _gamePath);

                MinecraftPath minecraftPath =
                    new MinecraftPath(
                        _gamePath);

                WriteLog(
                    $"Game directory: {_gamePath}");

                // ----------------------------------------------------
                // JAVA
                // ----------------------------------------------------

                StatusText.Text =
                    "Checking Java 21...";

                string javaPath =
                    await EnsureJava21Async();

                WriteLog(
                    $"Java: {javaPath}");

                StatusText.Text =
                    $"Java 21 ready.";

                // ----------------------------------------------------
                // MINECRAFT INSTALL
                // ----------------------------------------------------

                MinecraftLauncher launcher =
                    new MinecraftLauncher(
                        minecraftPath);

                var fileProgress =
                    new Progress<InstallerProgressChangedEventArgs>(
                        progress =>
                        {
                            Dispatcher.Invoke(
                                () =>
                                {
                                    string name =
                                        progress.Name ?? "file";

                                    StatusText.Text =
                                        $"Installing: {name} " +
                                        $"({progress.ProgressedTasks}/" +
                                        $"{progress.TotalTasks})";
                                });
                        });

                var byteProgress =
                    new Progress<ByteProgress>(
                        progress =>
                        {
                            Dispatcher.Invoke(
                                () =>
                                {
                                    double ratio =
                                        progress.ToRatio();

                                    if (ratio >= 0 &&
                                        ratio <= 1)
                                    {
                                        StatusText.Text =
                                            $"Downloading: " +
                                            $"{ratio * 100:0}%";
                                    }
                                });
                        });

                StatusText.Text =
                    $"Installing Minecraft {minecraftVersion}...";

                await launcher.InstallAsync(
                    minecraftVersion,
                    fileProgress,
                    byteProgress);

                WriteLog(
                    "Minecraft installation completed.");

                // ----------------------------------------------------
                // FABRIC
                // ----------------------------------------------------

                StatusText.Text =
                    $"Installing Fabric {FabricLoaderVersion}...";

                FabricInstaller fabricInstaller =
                    new FabricInstaller(
                        _httpClient);

                string fabricVersion =
                    await fabricInstaller.Install(
                        minecraftVersion,
                        FabricLoaderVersion,
                        minecraftPath);

                WriteLog(
                    $"Fabric version installed: {fabricVersion}");

                StatusText.Text =
                    $"Fabric {FabricLoaderVersion} installed.";

                // ----------------------------------------------------
                // OPTIONAL MODS
                // ----------------------------------------------------

                StatusText.Text =
                    "Checking optimization mods...";

                await EnsureOptimizationModsAsync(
                    minecraftVersion);

                // ----------------------------------------------------
                // LAUNCH OPTIONS
                // ----------------------------------------------------

                MLaunchOption launchOptions =
                    new MLaunchOption
                    {
                        Session = _session,

                        MaximumRamMb = ramMb,

                        MinimumRamMb =
                            Math.Min(
                                1024,
                                ramMb),

                        JavaPath = javaPath,

                        GameLauncherName =
                            "TopuClient",

                        GameLauncherVersion =
                            "1.0.0",

                        VersionType =
                            "release"
                    };

                if (!string.IsNullOrWhiteSpace(
                        _serverToJoin))
                {
                    launchOptions.ServerIp =
                        _serverToJoin;

                    WriteLog(
                        $"Server: {_serverToJoin}");
                }

                // ----------------------------------------------------
                // BUILD
                // ----------------------------------------------------

                StatusText.Text =
                    "Creating Minecraft process...";

                Process process =
                    await launcher.BuildProcessAsync(
                        fabricVersion,
                        launchOptions);

                if (process == null)
                {
                    throw new InvalidOperationException(
                        "CmlLib returned a null process.");
                }

                _minecraftProcess =
                    process;

                // ----------------------------------------------------
                // DEBUG FILE
                // ----------------------------------------------------

                WriteDebugFile(
                    process,
                    javaPath,
                    minecraftVersion,
                    fabricVersion,
                    ramMb);

                // ----------------------------------------------------
                // PROCESS OUTPUT
                // ----------------------------------------------------

                PrepareProcessLogging(
                    process);

                StatusText.Text =
                    "Starting Minecraft...";

                bool started =
                    process.Start();

                if (!started)
                {
                    throw new InvalidOperationException(
                        "Minecraft process failed to start.");
                }

                WriteLog(
                    "Minecraft process started.");

                WriteLog(
                    $"PID: {process.Id}");

                StatusText.Text =
                    $"Minecraft running as {_session.Username}";

                _ = MonitorMinecraftProcessAsync(
                    process);
            }
            catch (Exception ex)
            {
                StatusText.Text =
                    "Launch failed.";

                WriteLauncherException(
                    "LAUNCH ERROR",
                    ex);

                MessageBox.Show(
                    "Minecraft failed to start.\n\n" +
                    ex.Message +
                    "\n\n" +
                    "Log:\n" +
                    _logFilePath +
                    "\n\n" +
                    "Debug:\n" +
                    _debugFilePath,
                    "Topu Client",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                LaunchBtn.IsEnabled = true;
            }
        }

        // ============================================================
        // JAVA 21
        // ============================================================

        private async Task<string> EnsureJava21Async()
        {
            string runtimeRoot =
                Path.Combine(
                    _gamePath,
                    "runtime",
                    "java21");

            string existing =
                FindJavaExecutable(
                    runtimeRoot);

            if (!string.IsNullOrWhiteSpace(existing) &&
                IsJava21(existing))
            {
                return existing;
            }

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

                if (File.Exists(javaHomeExe) &&
                    IsJava21(javaHomeExe))
                {
                    return javaHomeExe;
                }
            }

            string pathEnvironment =
                Environment.GetEnvironmentVariable(
                    "PATH") ?? "";

            foreach (string directory in
                     pathEnvironment.Split(
                         Path.PathSeparator,
                         StringSplitOptions.RemoveEmptyEntries))
            {
                string candidate =
                    Path.Combine(
                        directory.Trim(),
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
                    // Continue searching.
                }
            }

            StatusText.Text =
                "Java 21 not found. Downloading Java 21...";

            WriteLog(
                "Java 21 not found. Downloading JRE.");

            Directory.CreateDirectory(
                Path.GetDirectoryName(
                    runtimeRoot)!);

            string temporaryFolder =
                Path.Combine(
                    _gamePath,
                    "runtime",
                    "java21-download");

            string zipFile =
                Path.Combine(
                    _gamePath,
                    "runtime",
                    "java21.zip");

            try
            {
                if (Directory.Exists(
                        temporaryFolder))
                {
                    Directory.Delete(
                        temporaryFolder,
                        true);
                }

                Directory.CreateDirectory(
                    temporaryFolder);

                string apiUrl =
                    "https://api.adoptium.net/v3/assets/latest/21/" +
                    "hotspot?architecture=x64" +
                    "&image_type=jre" +
                    "&os=windows" +
                    "&vendor=eclipse";

                WriteLog(
                    "Requesting Java 21 from Adoptium.");

                string metadata =
                    await _httpClient.GetStringAsync(
                        apiUrl);

                using JsonDocument document =
                    JsonDocument.Parse(
                        metadata);

                JsonElement root =
                    document.RootElement;

                if (root.ValueKind != JsonValueKind.Array ||
                    root.GetArrayLength() == 0)
                {
                    throw new InvalidOperationException(
                        "Java 21 download metadata was empty.");
                }

                JsonElement binary =
                    root[0]
                        .GetProperty("binary");

                JsonElement package =
                    binary
                        .GetProperty("package");

                string downloadUrl =
                    package
                        .GetProperty("link")
                        .GetString()
                    ?? "";

                if (string.IsNullOrWhiteSpace(
                        downloadUrl))
                {
                    throw new InvalidOperationException(
                        "Java 21 download URL was missing.");
                }

                StatusText.Text =
                    "Downloading Java 21...";

                using HttpResponseMessage response =
                    await _httpClient.GetAsync(
                        downloadUrl,
                        HttpCompletionOption.ResponseHeadersRead);

                response.EnsureSuccessStatusCode();

                await using Stream input =
                    await response.Content.ReadAsStreamAsync();

                await using FileStream output =
                    new FileStream(
                        zipFile,
                        FileMode.Create,
                        FileAccess.Write,
                        FileShare.None);

                await input.CopyToAsync(
                    output);

                output.Close();

                StatusText.Text =
                    "Extracting Java 21...";

                ZipFile.ExtractToDirectory(
                    zipFile,
                    temporaryFolder,
                    true);

                string? extractedJava =
                    FindJavaExecutable(
                        temporaryFolder);

                if (string.IsNullOrWhiteSpace(
                        extractedJava))
                {
                    throw new InvalidOperationException(
                        "Downloaded Java archive did not contain java.exe.");
                }

                if (Directory.Exists(
                        runtimeRoot))
                {
                    Directory.Delete(
                        runtimeRoot,
                        true);
                }

                Directory.CreateDirectory(
                    runtimeRoot);

                string extractedHome =
                    Path.GetDirectoryName(
                        Path.GetDirectoryName(
                            extractedJava)!)!;

                CopyDirectory(
                    extractedHome,
                    runtimeRoot);

                string finalJava =
                    Path.Combine(
                        runtimeRoot,
                        "bin",
                        "java.exe");

                if (!File.Exists(finalJava))
                {
                    finalJava =
                        FindJavaExecutable(
                            runtimeRoot)
                        ?? "";
                }

                if (string.IsNullOrWhiteSpace(
                        finalJava) ||
                    !File.Exists(finalJava))
                {
                    throw new InvalidOperationException(
                        "Java 21 extraction completed, but java.exe was not found.");
                }

                if (!IsJava21(finalJava))
                {
                    throw new InvalidOperationException(
                        "The downloaded Java runtime is not Java 21.");
                }

                WriteLog(
                    $"Java 21 installed at: {finalJava}");

                return finalJava;
            }
            finally
            {
                try
                {
                    if (File.Exists(zipFile))
                        File.Delete(zipFile);
                }
                catch
                {
                }

                try
                {
                    if (Directory.Exists(
                            temporaryFolder))
                    {
                        Directory.Delete(
                            temporaryFolder,
                            true);
                    }
                }
                catch
                {
                }
            }
        }

        private string? FindJavaExecutable(
            string root)
        {
            if (!Directory.Exists(root))
                return null;

            try
            {
                return Directory
                    .EnumerateFiles(
                        root,
                        "java.exe",
                        SearchOption.AllDirectories)
                    .FirstOrDefault();
            }
            catch
            {
                return null;
            }
        }

        private bool IsJava21(
            string javaPath)
        {
            if (!File.Exists(javaPath))
                return false;

            try
            {
                ProcessStartInfo startInfo =
                    new ProcessStartInfo
                    {
                        FileName = javaPath,
                        Arguments = "-version",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };

                using Process process =
                    Process.Start(startInfo)
                    ?? throw new InvalidOperationException(
                        "Unable to start Java.");

                string stdout =
                    process.StandardOutput.ReadToEnd();

                string stderr =
                    process.StandardError.ReadToEnd();

                process.WaitForExit();

                string combined =
                    stdout + "\n" + stderr;

                return combined.Contains(
                    "version \"21.",
                    StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        // ============================================================
        // FABRIC OPTIMIZATION MODS
        // ============================================================

        private async Task EnsureOptimizationModsAsync(
            string minecraftVersion)
        {
            string modsFolder =
                Path.Combine(
                    _gamePath,
                    "mods");

            Directory.CreateDirectory(
                modsFolder);

            string[] projects =
            {
                "fabric-api",
                "sodium",
                "lithium",
                "ferrite-core",
                "sodium-extra",
                "dynamic-fps"
            };

            foreach (string projectId in projects)
            {
                try
                {
                    string? existingJar =
                        Directory
                            .EnumerateFiles(
                                modsFolder,
                                "*.jar")
                            .FirstOrDefault(
                                file =>
                                    Path.GetFileName(
                                        file)
                                    .Contains(
                                        projectId.Replace(
                                            "-",
                                            "",
                                            StringComparison.Ordinal),
                                        StringComparison.OrdinalIgnoreCase));

                    if (!string.IsNullOrWhiteSpace(
                            existingJar))
                    {
                        continue;
                    }

                    StatusText.Text =
                        $"Checking {projectId}...";

                    string fileUrl =
                        await GetModrinthFileUrl(
                            projectId,
                            minecraftVersion);

                    if (string.IsNullOrWhiteSpace(
                            fileUrl))
                    {
                        WriteLog(
                            $"No compatible version for {projectId}.");
                        continue;
                    }

                    string filename =
                        GetFilenameFromUrl(
                            fileUrl,
                            projectId + ".jar");

                    string destination =
                        Path.Combine(
                            modsFolder,
                            SanitizeFileName(
                                filename));

                    if (File.Exists(destination))
                        continue;

                    StatusText.Text =
                        $"Downloading {projectId}...";

                    byte[] data =
                        await _httpClient.GetByteArrayAsync(
                            fileUrl);

                    await File.WriteAllBytesAsync(
                        destination,
                        data);

                    WriteLog(
                        $"Installed mod: {projectId}");
                }
                catch (Exception ex)
                {
                    WriteLog(
                        $"Skipped {projectId}: {ex.Message}");
                }
            }
        }

        // ============================================================
        // PROCESS LOGGING
        // ============================================================

        private void PrepareProcessLogging(
            Process process)
        {
            try
            {
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;

                process.OutputDataReceived +=
                    Minecraft_OutputDataReceived;

                process.ErrorDataReceived +=
                    Minecraft_ErrorDataReceived;

                process.EnableRaisingEvents = true;
            }
            catch (Exception ex)
            {
                WriteLog(
                    $"Could not configure process logging: {ex.Message}");
            }
        }

        private void Minecraft_OutputDataReceived(
            object sender,
            DataReceivedEventArgs e)
        {
            if (e.Data == null)
                return;

            WriteLog(
                "[STDOUT] " + e.Data);
        }

        private void Minecraft_ErrorDataReceived(
            object sender,
            DataReceivedEventArgs e)
        {
            if (e.Data == null)
                return;

            WriteLog(
                "[STDERR] " + e.Data);
        }

        // ============================================================
        // PROCESS MONITOR
        // ============================================================

        private async Task MonitorMinecraftProcessAsync(
            Process process)
        {
            try
            {
                try
                {
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                }
                catch (Exception ex)
                {
                    WriteLog(
                        $"Output redirection error: {ex.Message}");
                }

                await Task.Run(
                    () =>
                    {
                        process.WaitForExit();
                    });

                int exitCode =
                    process.ExitCode;

                WriteLog(
                    $"===== MINECRAFT EXITED: {exitCode} =====");

                await Dispatcher.InvokeAsync(
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
                                "Check:\n" +
                                _logFilePath,
                                "Minecraft Exit",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
                        }
                    });
            }
            catch (Exception ex)
            {
                WriteLog(
                    "PROCESS MONITOR ERROR:");
                WriteLog(
                    ex.ToString());
            }
            finally
            {
                _minecraftProcess = null;
            }
        }

        // ============================================================
        // DEBUG FILE
        // ============================================================

        private void WriteDebugFile(
            Process process,
            string javaPath,
            string minecraftVersion,
            string fabricVersion,
            int ramMb)
        {
            try
            {
                StringBuilder builder =
                    new StringBuilder();

                builder.AppendLine(
                    "===== TOPU CLIENT DEBUG =====");

                builder.AppendLine();

                builder.AppendLine(
                    $"Time: {DateTime.Now:O}");

                builder.AppendLine();

                builder.AppendLine(
                    "Executable:");

                builder.AppendLine(
                    javaPath);

                builder.AppendLine();

                builder.AppendLine(
                    "Arguments:");

                builder.AppendLine(
                    process.StartInfo.Arguments);

                builder.AppendLine();

                builder.AppendLine(
                    "Working Directory:");

                builder.AppendLine(
                    _gamePath);

                builder.AppendLine();

                builder.AppendLine(
                    "Java:");

                builder.AppendLine(
                    javaPath);

                builder.AppendLine();

                builder.AppendLine(
                    "Minecraft:");

                builder.AppendLine(
                    minecraftVersion);

                builder.AppendLine();

                builder.AppendLine(
                    "Fabric:");

                builder.AppendLine(
                    fabricVersion);

                builder.AppendLine();

                builder.AppendLine(
                    "RAM:");

                builder.AppendLine(
                    $"{ramMb} MB");

                builder.AppendLine();

                builder.AppendLine(
                    "Username:");

                builder.AppendLine(
                    _session?.Username ?? "(none)");

                builder.AppendLine();

                builder.AppendLine(
                    "Process ID:");

                try
                {
                    builder.AppendLine(
                        process.Id.ToString());
                }
                catch
                {
                    builder.AppendLine(
                        "(unavailable)");
                }

                builder.AppendLine();

                builder.AppendLine(
                    "StartInfo:");

                builder.AppendLine(
                    $"FileName: {process.StartInfo.FileName}");

                builder.AppendLine(
                    $"UseShellExecute: {process.StartInfo.UseShellExecute}");

                builder.AppendLine(
                    $"RedirectStdout: {process.StartInfo.RedirectStandardOutput}");

                builder.AppendLine(
                    $"RedirectStderr: {process.StartInfo.RedirectStandardError}");

                File.WriteAllText(
                    _debugFilePath,
                    builder.ToString());
            }
            catch (Exception ex)
            {
                WriteLog(
                    $"Could not write debug file: {ex.Message}");
            }
        }

        // ============================================================
        // LOG FILE
        // ============================================================

        private void StartNewLog()
        {
            try
            {
                Directory.CreateDirectory(
                    _gamePath);

                File.WriteAllText(
                    _logFilePath,
                    "===== TOPU CLIENT MINECRAFT LOG =====" +
                    Environment.NewLine +
                    $"Started: {DateTime.Now:O}" +
                    Environment.NewLine +
                    Environment.NewLine);
            }
            catch
            {
            }
        }

        private void WriteLog(
            string text)
        {
            try
            {
                File.AppendAllText(
                    _logFilePath,
                    $"[{DateTime.Now:HH:mm:ss}] {text}" +
                    Environment.NewLine);
            }
            catch
            {
            }
        }

        private void WriteLauncherException(
            string title,
            Exception exception)
        {
            try
            {
                WriteLog("");
                WriteLog(
                    $"===== {title} =====");
                WriteLog(
                    exception.ToString());
            }
            catch
            {
            }
        }

        // ============================================================
        // FILE HELPERS
        // ============================================================

        private static void CopyDirectory(
            string sourceDirectory,
            string destinationDirectory)
        {
            Directory.CreateDirectory(
                destinationDirectory);

            foreach (string file in
                     Directory.GetFiles(
                         sourceDirectory))
            {
                string destination =
                    Path.Combine(
                        destinationDirectory,
                        Path.GetFileName(file));

                File.Copy(
                    file,
                    destination,
                    true);
            }

            foreach (string directory in
                     Directory.GetDirectories(
                         sourceDirectory))
            {
                string destination =
                    Path.Combine(
                        destinationDirectory,
                        Path.GetFileName(directory));

                CopyDirectory(
                    directory,
                    destination);
            }
        }

        private static string GetFilenameFromUrl(
            string url,
            string fallback)
        {
            try
            {
                Uri uri =
                    new Uri(url);

                string filename =
                    Path.GetFileName(
                        uri.LocalPath);

                if (!string.IsNullOrWhiteSpace(
                        filename))
                {
                    return filename;
                }
            }
            catch
            {
            }

            return fallback;
        }

        private static string SanitizeFileName(
            string filename)
        {
            foreach (char invalid in
                     Path.GetInvalidFileNameChars())
            {
                filename =
                    filename.Replace(
                        invalid,
                        '_');
            }

            return filename;
        }
    }
}
