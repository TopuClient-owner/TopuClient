using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
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
using CmlLib.Core.ModLoaders.FabricMC;
using CmlLib.Core.ProcessBuilder;

namespace TopuLauncher
{
    public partial class MainWindow : Window
    {
        // =========================================================
        // CMLLIB / PROCESS
        // =========================================================

        private MinecraftLauncher? _launcher;

        private ProcessWrapper? _minecraftProcess;

        private MSession? _session;

        private CancellationTokenSource? _launchCancellation;

        // =========================================================
        // HTTP
        // =========================================================

        private static readonly HttpClient Http = CreateHttpClient();

        private static HttpClient CreateHttpClient()
        {
            HttpClient client = new HttpClient(
                new HttpClientHandler
                {
                    AllowAutoRedirect = true
                });

            client.Timeout = TimeSpan.FromMinutes(15);

            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "TopuClient/1.0");

            return client;
        }

        // =========================================================
        // PATHS
        // =========================================================

        private readonly string _gamePath;

        private readonly string _configPath;

        private readonly string _logPath;

        // =========================================================
        // DEFAULTS
        // =========================================================

        private const string DefaultMinecraftVersion = "1.21.1";

        private const string Runtime21Folder = "java21";

        private const string Runtime25Folder = "java25";

        // =========================================================
        // PRECONFIGURED MODS
        // =========================================================

        private static readonly string[] PerformanceMods =
        {
            "sodium",
            "lithium",
            "indium",
            "dynamic-fps",
            "sodium-extra",
            "krypton"
        };

        // =========================================================
        // CONSTRUCTOR
        // =========================================================

        public MainWindow()
        {
            InitializeComponent();

            _gamePath = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.ApplicationData),
                ".topuclient");

            Directory.CreateDirectory(_gamePath);

            _configPath = Path.Combine(
                _gamePath,
                "username.txt");

            _logPath = Path.Combine(
                _gamePath,
                "topu-minecraft.log");

            LoadSavedUsername();

            if (RamLabel != null && RamSlider != null)
            {
                RamLabel.Text =
                    $"{(int)RamSlider.Value}GB";
            }

            WriteLog("Topu Client initialized.");
        }

        // =========================================================
        // LOGGING
        // =========================================================

        private void WriteLog(string message)
        {
            try
            {
                Directory.CreateDirectory(_gamePath);

                File.AppendAllText(
                    _logPath,
                    $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
            }
            catch
            {
                // Logging must never crash the launcher.
            }
        }

        private void WriteException(
            string title,
            Exception exception)
        {
            WriteLog("");
            WriteLog($"===== {title} =====");
            WriteLog(exception.ToString());
        }

        private void StartLaunchLog()
        {
            try
            {
                File.WriteAllText(
                    _logPath,
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

        private void AppendMinecraftLog(string message)
        {
            try
            {
                File.AppendAllText(
                    _logPath,
                    $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
            }
            catch
            {
            }
        }

        // =========================================================
        // USERNAME
        // =========================================================

        private void LoadSavedUsername()
        {
            try
            {
                if (!File.Exists(_configPath))
                    return;

                string username =
                    File.ReadAllText(_configPath).Trim();

                if (string.IsNullOrWhiteSpace(username))
                    return;

                UsernameInput.Text = username;

                _session =
                    MSession.CreateOfflineSession(
                        username);
            }
            catch (Exception ex)
            {
                WriteException(
                    "USERNAME LOAD ERROR",
                    ex);
            }
        }

        private void SaveUsername(string username)
        {
            try
            {
                File.WriteAllText(
                    _configPath,
                    username.Trim());
            }
            catch (Exception ex)
            {
                WriteException(
                    "USERNAME SAVE ERROR",
                    ex);
            }
        }

        // =========================================================
        // WINDOW
        // =========================================================

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
            try
            {
                _launchCancellation?.Cancel();
            }
            catch
            {
            }

            Close();
        }

        // =========================================================
        // TABS
        // =========================================================

        private void SwitchTab_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (sender is not Button button)
                return;

            if (button.Tag is not string tab)
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

            Brush active =
                new SolidColorBrush(
                    Color.FromRgb(
                        0,
                        255,
                        136));

            TabLaunchBtn.Foreground = inactive;
            TabProfilesBtn.Foreground = inactive;
            TabAccountsBtn.Foreground = inactive;

            TabLaunchBtn.BorderThickness =
                new Thickness(0);

            TabProfilesBtn.BorderThickness =
                new Thickness(0);

            TabAccountsBtn.BorderThickness =
                new Thickness(0);

            button.Foreground = active;

            button.BorderThickness =
                new Thickness(
                    0,
                    0,
                    0,
                    2);

            switch (tab)
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
            string version =
                GetSelectedMinecraftVersion();

            int ram =
                (int)RamSlider.Value;

            SelectedProfileLabel.Text =
                $"Ready to launch Fabric {version}";

            StatusText.Text =
                $"Profile saved: Fabric {version} with {ram}GB RAM";

            WriteLog(
                $"Profile saved. Version={version}, RAM={ram}GB");

            MessageBox.Show(
                "Profile settings saved successfully!",
                "Topu Client",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private string GetSelectedMinecraftVersion()
        {
            string version =
                (VersionBox.SelectedItem as ComboBoxItem)
                ?.Content
                ?.ToString()
                ?.Trim()
                ?? "";

            if (string.IsNullOrWhiteSpace(version))
                return DefaultMinecraftVersion;

            return version;
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
                MsLoginBtn.IsEnabled = false;

                MessageBox.Show(
                    "Microsoft authentication is not configured in this build yet.\n\n" +
                    "Offline mode is currently available.",
                    "Microsoft Login",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                WriteException(
                    "MICROSOFT LOGIN ERROR",
                    ex);
            }
            finally
            {
                MsLoginBtn.IsEnabled = true;
            }

            await Task.CompletedTask;
        }

        // =========================================================
        // SERVER
        // =========================================================

        private void JoinServer_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (sender is not Button button)
                return;

            if (button.Tag is not string server)
                return;

            StatusText.Text =
                $"Server selected: {server}";

            WriteLog(
                $"Server selected: {server}");
        }

        // =========================================================
        // MODRINTH SEARCH
        // =========================================================

        private async void SearchModrinth_Click(
            object sender,
            RoutedEventArgs e)
        {
            string query =
                ModSearchInput.Text.Trim();

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

                string projectId =
                    await FindModrinthProjectAsync(
                        query);

                if (string.IsNullOrWhiteSpace(projectId))
                {
                    ModSearchStatus.Text =
                        "No mod found.";

                    return;
                }

                string minecraftVersion =
                    GetSelectedMinecraftVersion();

                ModrinthFile? file =
                    await GetCompatibleModrinthFileAsync(
                        projectId,
                        minecraftVersion);

                if (file == null)
                {
                    ModSearchStatus.Text =
                        $"No Fabric build found for {minecraftVersion}.";

                    return;
                }

                await DownloadModFileAsync(
                    file);

                ModSearchStatus.Text =
                    $"Installed: {file.Title}";

                MessageBox.Show(
                    $"{file.Title} was installed successfully.",
                    "Modrinth",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                WriteException(
                    "MODRINTH SEARCH ERROR",
                    ex);

                ModSearchStatus.Text =
                    "Modrinth download failed.";

                MessageBox.Show(
                    ex.Message,
                    "Modrinth Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async Task<string> FindModrinthProjectAsync(
            string query)
        {
            string url =
                "https://api.modrinth.com/v2/search" +
                "?query=" +
                Uri.EscapeDataString(query) +
                "&facets=" +
                Uri.EscapeDataString(
                    "[[\"project_type:mod\"]]");

            string json =
                await Http.GetStringAsync(url);

            using JsonDocument document =
                JsonDocument.Parse(json);

            if (!document.RootElement.TryGetProperty(
                    "hits",
                    out JsonElement hits))
            {
                return "";
            }

            if (hits.ValueKind != JsonValueKind.Array)
                return "";

            if (hits.GetArrayLength() == 0)
                return "";

            JsonElement first =
                hits[0];

            if (first.TryGetProperty(
                    "project_id",
                    out JsonElement id))
            {
                return id.GetString() ?? "";
            }

            return "";
        }

        // =========================================================
        // MODRINTH COMPATIBILITY
        // =========================================================

        private async Task<ModrinthFile?> GetCompatibleModrinthFileAsync(
            string projectId,
            string minecraftVersion)
        {
            string gameVersions =
                Uri.EscapeDataString(
                    JsonSerializer.Serialize(
                        new[] { minecraftVersion }));

            string loaders =
                Uri.EscapeDataString(
                    JsonSerializer.Serialize(
                        new[] { "fabric" }));

            string url =
                $"https://api.modrinth.com/v2/project/{Uri.EscapeDataString(projectId)}/version" +
                $"?loaders={loaders}" +
                $"&game_versions={gameVersions}" +
                "&include_changelog=false";

            string json =
                await Http.GetStringAsync(url);

            using JsonDocument document =
                JsonDocument.Parse(json);

            JsonElement root =
                document.RootElement;

            if (root.ValueKind != JsonValueKind.Array)
                return null;

            if (root.GetArrayLength() == 0)
                return null;

            foreach (JsonElement version in
                     root.EnumerateArray())
            {
                string title =
                    version.TryGetProperty(
                        "name",
                        out JsonElement titleElement)
                    ? titleElement.GetString() ?? projectId
                    : projectId;

                if (!version.TryGetProperty(
                        "files",
                        out JsonElement files))
                {
                    continue;
                }

                if (files.ValueKind !=
                    JsonValueKind.Array)
                {
                    continue;
                }

                JsonElement? selectedFile = null;

                foreach (JsonElement file in
                         files.EnumerateArray())
                {
                    if (!file.TryGetProperty(
                            "url",
                            out _))
                    {
                        continue;
                    }

                    bool primary =
                        file.TryGetProperty(
                            "primary",
                            out JsonElement primaryElement) &&
                        primaryElement.ValueKind ==
                            JsonValueKind.True;

                    if (primary)
                    {
                        selectedFile = file;
                        break;
                    }

                    if (selectedFile == null)
                        selectedFile = file;
                }

                if (selectedFile == null)
                    continue;

                JsonElement selected =
                    selectedFile.Value;

                string downloadUrl =
                    selected.TryGetProperty(
                        "url",
                        out JsonElement urlElement)
                    ? urlElement.GetString() ?? ""
                    : "";

                string filename =
                    selected.TryGetProperty(
                        "filename",
                        out JsonElement filenameElement)
                    ? filenameElement.GetString() ?? ""
                    : "";

                if (string.IsNullOrWhiteSpace(downloadUrl))
                    continue;

                if (string.IsNullOrWhiteSpace(filename))
                    filename =
                        $"{projectId}.jar";

                return new ModrinthFile(
                    title,
                    filename,
                    downloadUrl);
            }

            return null;
        }

        private async Task DownloadModFileAsync(
            ModrinthFile file)
        {
            string modsFolder =
                Path.Combine(
                    _gamePath,
                    "mods");

            Directory.CreateDirectory(
                modsFolder);

            string destination =
                Path.Combine(
                    modsFolder,
                    SanitizeFileName(
                        file.Filename));

            ModSearchStatus.Text =
                $"Downloading {file.Title}...";

            using HttpResponseMessage response =
                await Http.GetAsync(
                    file.Url,
                    HttpCompletionOption.ResponseHeadersRead);

            response.EnsureSuccessStatusCode();

            await using Stream input =
                await response.Content.ReadAsStreamAsync();

            await using FileStream output =
                new FileStream(
                    destination,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    81920,
                    true);

            await input.CopyToAsync(output);

            WriteLog(
                $"Installed Modrinth mod: {file.Title}");

            WriteLog(
                $"Mod file: {destination}");
        }

        // =========================================================
        // PRECONFIGURED PERFORMANCE STACK
        // =========================================================

        private async Task InstallPerformanceModsAsync(
            string minecraftVersion)
        {
            string modsFolder =
                Path.Combine(
                    _gamePath,
                    "mods");

            Directory.CreateDirectory(
                modsFolder);

            StatusText.Text =
                "Installing Topu performance mods...";

            WriteLog(
                "===== PERFORMANCE MOD INSTALL =====");

            // Fabric API is an important dependency for the stack.
            await InstallPreconfiguredModAsync(
                "fabric-api",
                minecraftVersion);

            foreach (string mod in PerformanceMods)
            {
                await InstallPreconfiguredModAsync(
                    mod,
                    minecraftVersion);
            }

            WriteLog(
                "===== PERFORMANCE MOD INSTALL COMPLETE =====");
        }

        private async Task InstallPreconfiguredModAsync(
            string slug,
            string minecraftVersion)
        {
            try
            {
                WriteLog(
                    $"Checking Modrinth: {slug} for {minecraftVersion}");

                ModrinthFile? file =
                    await GetCompatibleModrinthFileBySlugAsync(
                        slug,
                        minecraftVersion);

                if (file == null)
                {
                    WriteLog(
                        $"No compatible Fabric build found: {slug}");

                    return;
                }

                await DownloadModFileAsync(
                    file);

                WriteLog(
                    $"Preconfigured mod installed: {slug}");
            }
            catch (Exception ex)
            {
                // One incompatible mod should NOT prevent Minecraft
                // from launching.
                WriteLog(
                    $"Skipped mod {slug}: {ex.Message}");
            }
        }

        private async Task<ModrinthFile?> GetCompatibleModrinthFileBySlugAsync(
            string slug,
            string minecraftVersion)
        {
            string gameVersions =
                Uri.EscapeDataString(
                    JsonSerializer.Serialize(
                        new[] { minecraftVersion }));

            string loaders =
                Uri.EscapeDataString(
                    JsonSerializer.Serialize(
                        new[] { "fabric" }));

            string url =
                $"https://api.modrinth.com/v2/project/{Uri.EscapeDataString(slug)}/version" +
                $"?loaders={loaders}" +
                $"&game_versions={gameVersions}" +
                "&include_changelog=false";

            using HttpResponseMessage response =
                await Http.GetAsync(url);

            if (!response.IsSuccessStatusCode)
                return null;

            string json =
                await response.Content.ReadAsStringAsync();

            using JsonDocument document =
                JsonDocument.Parse(json);

            JsonElement versions =
                document.RootElement;

            if (versions.ValueKind !=
                JsonValueKind.Array)
            {
                return null;
            }

            if (versions.GetArrayLength() == 0)
                return null;

            // Modrinth returns versions newest first.
            foreach (JsonElement version in
                     versions.EnumerateArray())
            {
                string title =
                    version.TryGetProperty(
                        "name",
                        out JsonElement nameElement)
                    ? nameElement.GetString() ?? slug
                    : slug;

                if (!version.TryGetProperty(
                        "files",
                        out JsonElement files))
                {
                    continue;
                }

                if (files.ValueKind !=
                    JsonValueKind.Array)
                {
                    continue;
                }

                JsonElement? primaryFile = null;
                JsonElement? firstFile = null;

                foreach (JsonElement file in
                         files.EnumerateArray())
                {
                    if (!file.TryGetProperty(
                            "url",
                            out JsonElement urlElement))
                    {
                        continue;
                    }

                    string urlValue =
                        urlElement.GetString() ?? "";

                    if (string.IsNullOrWhiteSpace(urlValue))
                        continue;

                    if (firstFile == null)
                        firstFile = file;

                    bool primary =
                        file.TryGetProperty(
                            "primary",
                            out JsonElement primaryElement) &&
                        primaryElement.ValueKind ==
                            JsonValueKind.True;

                    if (primary)
                    {
                        primaryFile = file;
                        break;
                    }
                }

                JsonElement? chosen =
                    primaryFile ?? firstFile;

                if (chosen == null)
                    continue;

                JsonElement fileElement =
                    chosen.Value;

                string downloadUrl =
                    fileElement.GetProperty("url")
                        .GetString()
                        ?? "";

                string filename =
                    fileElement.TryGetProperty(
                        "filename",
                        out JsonElement filenameElement)
                    ? filenameElement.GetString()
                        ?? $"{slug}.jar"
                    : $"{slug}.jar";

                return new ModrinthFile(
                    title,
                    filename,
                    downloadUrl);
            }

            return null;
        }

        // =========================================================
        // LAUNCH
        // =========================================================

        private async void LaunchBtn_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_minecraftProcess != null)
            {
                try
                {
                    if (!_minecraftProcess.Process.HasExited)
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
                }

                _minecraftProcess = null;
            }

            LaunchBtn.IsEnabled = false;

            _launchCancellation =
                new CancellationTokenSource();

            try
            {
                StartLaunchLog();

                string minecraftVersion =
                    GetSelectedMinecraftVersion();

                int ramMb =
                    Math.Max(
                        2048,
                        (int)RamSlider.Value * 1024);

                WriteLog(
                    "===== TOPU CLIENT MINECRAFT LAUNCH =====");

                WriteLog(
                    $"Minecraft: {minecraftVersion}");

                WriteLog(
                    $"RAM: {ramMb} MB");

                WriteLog(
                    $"Game directory: {_gamePath}");

                // -------------------------------------------------
                // SESSION
                // -------------------------------------------------

                if (AuthTypeBox.SelectedIndex != 0)
                {
                    throw new InvalidOperationException(
                        "Microsoft authentication is not configured yet.");
                }

                string username =
                    UsernameInput.Text.Trim();

                if (string.IsNullOrWhiteSpace(username))
                    username = "TopuPlayer";

                _session =
                    MSession.CreateOfflineSession(
                        username);

                SaveUsername(username);

                WriteLog(
                    $"Offline username: {username}");

                // -------------------------------------------------
                // CMLLIB
                // -------------------------------------------------

                MinecraftPath path =
                    new MinecraftPath(
                        _gamePath);

                _launcher =
                    new MinecraftLauncher(
                        path);

                // Use CmlLib's actual 4.0.6 progress events.
                _launcher.FileProgressChanged +=
                    Launcher_FileProgressChanged;

                _launcher.ByteProgressChanged +=
                    Launcher_ByteProgressChanged;

                // -------------------------------------------------
                // VANILLA INSTALL
                // -------------------------------------------------

                StatusText.Text =
                    $"Installing Minecraft {minecraftVersion}...";

                WriteLog(
                    "Installing vanilla Minecraft files...");

                await _launcher.InstallAsync(
                    minecraftVersion,
                    _launchCancellation.Token);

                WriteLog(
                    "Minecraft installation completed.");

                // -------------------------------------------------
                // FABRIC
                // -------------------------------------------------

                StatusText.Text =
                    "Finding compatible Fabric Loader...";

                FabricInstaller fabricInstaller =
                    new FabricInstaller(
                        Http);

                FabricLoader? loader =
                    await fabricInstaller.GetFirstLoader(
                        minecraftVersion);

                if (loader == null ||
                    string.IsNullOrWhiteSpace(loader.Version))
                {
                    throw new InvalidOperationException(
                        $"No Fabric Loader is currently available for Minecraft {minecraftVersion}.");
                }

                string loaderVersion =
                    loader.Version;

                WriteLog(
                    $"Fabric Loader selected: {loaderVersion}");

                StatusText.Text =
                    $"Installing Fabric {loaderVersion}...";

                string fabricVersionName =
                    await fabricInstaller.Install(
                        minecraftVersion,
                        loaderVersion,
                        path);

                WriteLog(
                    $"Fabric installed: {fabricVersionName}");

                // -------------------------------------------------
                // PERFORMANCE MODS
                // -------------------------------------------------

                await InstallPerformanceModsAsync(
                    minecraftVersion);

                // -------------------------------------------------
                // JAVA
                // -------------------------------------------------

                StatusText.Text =
                    "Preparing Java runtime...";

                int requiredJava =
                    GetRequiredJavaVersion(
                        minecraftVersion);

                WriteLog(
                    $"Required Java major version: {requiredJava}");

                string javaPath =
                    await EnsureJavaRuntimeAsync(
                        requiredJava,
                        _launchCancellation.Token);

                if (!File.Exists(javaPath))
                {
                    throw new FileNotFoundException(
                        "Java runtime was not found after installation.",
                        javaPath);
                }

                WriteLog(
                    $"Java: {javaPath}");

                string javaVersion =
                    GetJavaVersion(
                        javaPath);

                WriteLog(
                    $"Java version: {javaVersion}");

                // -------------------------------------------------
                // LAUNCH OPTIONS
                // -------------------------------------------------

                MLaunchOption options =
                    new MLaunchOption
                    {
                        Session = _session,
                        MaximumRamMb = ramMb,
                        MinimumRamMb = 1024,
                        JavaPath = javaPath,
                        Path = path,
                        GameLauncherName = "Topu Client",
                        GameLauncherVersion = "1.0"
                    };

                WriteLog(
                    "Building Minecraft process...");

                StatusText.Text =
                    "Building Minecraft process...";

                // IMPORTANT:
                // CmlLib.Core 4.0.6 returns System.Diagnostics.Process.
                Process process =
                    await _launcher.BuildProcessAsync(
                        fabricVersionName,
                        options,
                        _launchCancellation.Token);

                if (process == null)
                {
                    throw new InvalidOperationException(
                        "CmlLib returned a null Minecraft process.");
                }

                // CmlLib 4.0.6 ProcessWrapper constructor:
                // ProcessWrapper(Process)
                _minecraftProcess =
                    new ProcessWrapper(
                        process);

                _minecraftProcess.OutputReceived +=
                    Minecraft_OutputReceived;

                _minecraftProcess.Exited +=
                    Minecraft_Exited;

                WriteLog(
                    $"Executable: {process.StartInfo.FileName}");

                WriteLog(
                    $"Arguments: {process.StartInfo.Arguments}");

                WriteLog(
                    $"Working directory: {process.StartInfo.WorkingDirectory}");

                WriteDebugFile(
                    process,
                    javaPath,
                    minecraftVersion,
                    fabricVersionName,
                    ramMb);

                // -------------------------------------------------
                // START
                // -------------------------------------------------

                StatusText.Text =
                    $"Starting Fabric {minecraftVersion}...";

                WriteLog(
                    "Starting Minecraft process...");

                _minecraftProcess.StartWithEvents();

                WriteLog(
                    $"Minecraft process started. PID: {process.Id}");

                StatusText.Text =
                    $"Topu Client running as {username}";

                _ = MonitorMinecraftAsync(
                    _minecraftProcess);
            }
            catch (OperationCanceledException)
            {
                StatusText.Text =
                    "Launch cancelled.";

                WriteLog(
                    "Launch cancelled.");
            }
            catch (Exception ex)
            {
                StatusText.Text =
                    "Launch Failed!";

                WriteException(
                    "TOPU LAUNCH ERROR",
                    ex);

                MessageBox.Show(
                    "Minecraft failed to launch.\n\n" +
                    ex.Message +
                    "\n\n" +
                    "Full log:\n" +
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

        // =========================================================
        // JAVA VERSION
        // =========================================================

        private static int GetRequiredJavaVersion(
            string minecraftVersion)
        {
            // Minecraft 26.1+ requires Java 25.
            if (minecraftVersion.StartsWith(
                    "26.",
                    StringComparison.OrdinalIgnoreCase))
            {
                return 25;
            }

            // Minecraft 1.20.5+ / 1.21.x use Java 21.
            if (minecraftVersion.StartsWith(
                    "1.20.",
                    StringComparison.OrdinalIgnoreCase) ||
                minecraftVersion.StartsWith(
                    "1.21.",
                    StringComparison.OrdinalIgnoreCase))
            {
                return 21;
            }

            // Fallback for the versions offered by your XAML.
            return 21;
        }

        // =========================================================
        // JAVA AUTO INSTALL
        // =========================================================

        private async Task<string> EnsureJavaRuntimeAsync(
            int majorVersion,
            CancellationToken cancellationToken)
        {
            string runtimeFolder =
                majorVersion >= 25
                    ? Runtime25Folder
                    : Runtime21Folder;

            string runtimeRoot =
                Path.Combine(
                    _gamePath,
                    "runtime",
                    runtimeFolder);

            string javaExe =
                Path.Combine(
                    runtimeRoot,
                    "bin",
                    "java.exe");

            if (File.Exists(javaExe) &&
                IsCorrectJavaVersion(
                    javaExe,
                    majorVersion))
            {
                WriteLog(
                    $"Using cached Java {majorVersion}: {javaExe}");

                return javaExe;
            }

            // -----------------------------------------------------
            // First try an already installed Java.
            // -----------------------------------------------------

            string? systemJava =
                FindSystemJava(
                    majorVersion);

            if (!string.IsNullOrWhiteSpace(systemJava))
            {
                WriteLog(
                    $"Using system Java {majorVersion}: {systemJava}");

                return systemJava;
            }

            // -----------------------------------------------------
            // Download Temurin JRE from Adoptium.
            // -----------------------------------------------------

            StatusText.Text =
                $"Downloading Java {majorVersion}...";

            WriteLog(
                $"Java {majorVersion} not found. Downloading Temurin JRE...");

            string packageUrl =
                await GetAdoptiumJreUrlAsync(
                    majorVersion,
                    cancellationToken);

            if (string.IsNullOrWhiteSpace(packageUrl))
            {
                throw new InvalidOperationException(
                    $"Could not find a Java {majorVersion} Windows x64 JRE.");
            }

            string tempZip =
                Path.Combine(
                    Path.GetTempPath(),
                    $"topu-java-{majorVersion}-{Guid.NewGuid():N}.zip");

            try
            {
                using HttpResponseMessage response =
                    await Http.GetAsync(
                        packageUrl,
                        HttpCompletionOption.ResponseHeadersRead,
                        cancellationToken);

                response.EnsureSuccessStatusCode();

                await using Stream input =
                    await response.Content.ReadAsStreamAsync(
                        cancellationToken);

                await using FileStream output =
                    new FileStream(
                        tempZip,
                        FileMode.Create,
                        FileAccess.Write,
                        FileShare.None,
                        81920,
                        true);

                await input.CopyToAsync(
                    output,
                    cancellationToken);

                WriteLog(
                    $"Java archive downloaded: {tempZip}");

                if (Directory.Exists(runtimeRoot))
                {
                    try
                    {
                        Directory.Delete(
                            runtimeRoot,
                            true);
                    }
                    catch
                    {
                    }
                }

                Directory.CreateDirectory(
                    runtimeRoot);

                StatusText.Text =
                    $"Extracting Java {majorVersion}...";

                ExtractJavaArchive(
                    tempZip,
                    runtimeRoot);

                // Some JRE archives contain one top-level directory.
                string? extractedJava =
                    FindJavaExecutable(
                        runtimeRoot);

                if (string.IsNullOrWhiteSpace(
                        extractedJava))
                {
                    throw new InvalidOperationException(
                        $"Java {majorVersion} archive was extracted but java.exe was not found.");
                }

                string actualRoot =
                    Path.GetDirectoryName(
                        Path.GetDirectoryName(
                            extractedJava)!)
                    ?? runtimeRoot;

                if (!string.Equals(
                        extractedJava,
                        javaExe,
                        StringComparison.OrdinalIgnoreCase))
                {
                    Directory.CreateDirectory(
                        Path.GetDirectoryName(javaExe)!);

                    CopyDirectory(
                        actualRoot,
                        runtimeRoot);

                    extractedJava =
                        FindJavaExecutable(
                            runtimeRoot);
                }

                if (string.IsNullOrWhiteSpace(
                        extractedJava) ||
                    !IsCorrectJavaVersion(
                        extractedJava,
                        majorVersion))
                {
                    throw new InvalidOperationException(
                        $"Downloaded Java runtime is not Java {majorVersion}.");
                }

                WriteLog(
                    $"Java {majorVersion} installed: {extractedJava}");

                return extractedJava;
            }
            finally
            {
                try
                {
                    if (File.Exists(tempZip))
                        File.Delete(tempZip);
                }
                catch
                {
                }
            }
        }

        private async Task<string> GetAdoptiumJreUrlAsync(
            int majorVersion,
            CancellationToken cancellationToken)
        {
            string url =
                "https://api.adoptium.net/v3/assets/latest/" +
                majorVersion +
                "/hotspot" +
                "?architecture=x64" +
                "&image_type=jre" +
                "&os=windows" +
                "&vendor=eclipse";

            string json =
                await Http.GetStringAsync(
                    url,
                    cancellationToken);

            using JsonDocument document =
                JsonDocument.Parse(json);

            if (document.RootElement.ValueKind !=
                JsonValueKind.Array)
            {
                throw new InvalidOperationException(
                    "Adoptium returned an invalid response.");
            }

            foreach (JsonElement item in
                     document.RootElement.EnumerateArray())
            {
                if (!item.TryGetProperty(
                        "binary",
                        out JsonElement binary))
                {
                    continue;
                }

                if (!binary.TryGetProperty(
                        "package",
                        out JsonElement package))
                {
                    continue;
                }

                if (!package.TryGetProperty(
                        "link",
                        out JsonElement link))
                {
                    continue;
                }

                string? value =
                    link.GetString();

                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }

            return "";
        }

        private static void ExtractJavaArchive(
            string zipPath,
            string destination)
        {
            ZipFile.ExtractToDirectory(
                zipPath,
                destination,
                true);
        }

        private static string? FindJavaExecutable(
            string root)
        {
            string direct =
                Path.Combine(
                    root,
                    "bin",
                    "java.exe");

            if (File.Exists(direct))
                return direct;

            foreach (string file in
                     Directory.EnumerateFiles(
                         root,
                         "java.exe",
                         SearchOption.AllDirectories))
            {
                if (file.Contains(
                        $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return file;
                }
            }

            return null;
        }

        private static string? FindSystemJava(
            int majorVersion)
        {
            string? javaHome =
                Environment.GetEnvironmentVariable(
                    "JAVA_HOME");

            if (!string.IsNullOrWhiteSpace(javaHome))
            {
                string java =
                    Path.Combine(
                        javaHome,
                        "bin",
                        "java.exe");

                if (File.Exists(java) &&
                    IsCorrectJavaVersion(
                        java,
                        majorVersion))
                {
                    return java;
                }
            }

            string? path =
                Environment.GetEnvironmentVariable(
                    "PATH");

            if (string.IsNullOrWhiteSpace(path))
                return null;

            foreach (string folder in
                     path.Split(
                         Path.PathSeparator,
                         StringSplitOptions.RemoveEmptyEntries))
            {
                string java =
                    Path.Combine(
                        folder.Trim(),
                        "java.exe");

                if (!File.Exists(java))
                    continue;

                if (IsCorrectJavaVersion(
                        java,
                        majorVersion))
                {
                    return java;
                }
            }

            return null;
        }

        private static bool IsCorrectJavaVersion(
            string javaPath,
            int requiredMajor)
        {
            try
            {
                string version =
                    GetJavaVersion(
                        javaPath);

                string marker =
                    $"version \"{requiredMajor}.";

                return version.Contains(
                    marker,
                    StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static string GetJavaVersion(
            string javaPath)
        {
            ProcessStartInfo info =
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
                Process.Start(info)
                ?? throw new InvalidOperationException(
                    "Could not start Java.");

            string stdout =
                process.StandardOutput.ReadToEnd();

            string stderr =
                process.StandardError.ReadToEnd();

            process.WaitForExit();

            return
                (stdout +
                 Environment.NewLine +
                 stderr).Trim();
        }

        // =========================================================
        // CMLLIB PROGRESS
        // =========================================================

        private void Launcher_FileProgressChanged(
            object? sender,
            InstallerProgressChangedEventArgs e)
        {
            try
            {
                Dispatcher.Invoke(
                    () =>
                    {
                        string name =
                            e.Name ?? "File";

                        StatusText.Text =
                            $"Installing {name}";
                    });
            }
            catch
            {
            }
        }

        private void Launcher_ByteProgressChanged(
            object? sender,
            ByteProgress e)
        {
            try
            {
                Dispatcher.Invoke(
                    () =>
                    {
                        if (e.TotalBytes > 0)
                        {
                            double percentage =
                                e.ProgressedBytes *
                                100.0 /
                                e.TotalBytes;

                            StatusText.Text =
                                $"Downloading {percentage:0}%";
                        }
                        else
                        {
                            StatusText.Text =
                                $"Downloading {e.ProgressedBytes:N0} bytes";
                        }
                    });
            }
            catch
            {
            }
        }

        // =========================================================
        // MINECRAFT PROCESS OUTPUT
        // =========================================================

        private void Minecraft_OutputReceived(
            object? sender,
            string output)
        {
            if (string.IsNullOrWhiteSpace(output))
                return;

            AppendMinecraftLog(
                "[MINECRAFT] " +
                output);
        }

        private void Minecraft_Exited(
            object? sender,
            EventArgs e)
        {
            try
            {
                if (_minecraftProcess == null)
                    return;

                int exitCode =
                    _minecraftProcess.Process.ExitCode;

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
                        }
                    });
            }
            catch (Exception ex)
            {
                WriteException(
                    "PROCESS EXIT EVENT ERROR",
                    ex);
            }
        }

        private async Task MonitorMinecraftAsync(
            ProcessWrapper processWrapper)
        {
            try
            {
                int exitCode =
                    await processWrapper.WaitForExitTaskAsync();

                AppendMinecraftLog(
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
                                $"Full log:\n{_logPath}",
                                "Minecraft Exit",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
                        }
                    });
            }
            catch (Exception ex)
            {
                WriteException(
                    "PROCESS MONITOR ERROR",
                    ex);
            }
            finally
            {
                _minecraftProcess = null;
            }
        }

        // =========================================================
        // DEBUG
        // =========================================================

        private void WriteDebugFile(
            Process process,
            string javaPath,
            string minecraftVersion,
            string fabricVersion,
            int ramMb)
        {
            try
            {
                string path =
                    Path.Combine(
                        _gamePath,
                        "topu-launch-debug.txt");

                string text =
                    "===== TOPU CLIENT DEBUG =====" +
                    Environment.NewLine +
                    Environment.NewLine +
                    $"Minecraft: {minecraftVersion}" +
                    Environment.NewLine +
                    $"Fabric: {fabricVersion}" +
                    Environment.NewLine +
                    $"RAM: {ramMb} MB" +
                    Environment.NewLine +
                    $"Java: {javaPath}" +
                    Environment.NewLine +
                    Environment.NewLine +
                    $"Executable:" +
                    Environment.NewLine +
                    process.StartInfo.FileName +
                    Environment.NewLine +
                    Environment.NewLine +
                    $"Arguments:" +
                    Environment.NewLine +
                    process.StartInfo.Arguments +
                    Environment.NewLine +
                    Environment.NewLine +
                    $"Working directory:" +
                    Environment.NewLine +
                    process.StartInfo.WorkingDirectory +
                    Environment.NewLine;

                File.WriteAllText(
                    path,
                    text);
            }
            catch (Exception ex)
            {
                WriteException(
                    "DEBUG FILE ERROR",
                    ex);
            }
        }

        // =========================================================
        // DIRECTORY COPY
        // =========================================================

        private static void CopyDirectory(
            string source,
            string destination)
        {
            Directory.CreateDirectory(
                destination);

            foreach (string directory in
                     Directory.GetDirectories(
                         source,
                         "*",
                         SearchOption.AllDirectories))
            {
                string relative =
                    Path.GetRelativePath(
                        source,
                        directory);

                Directory.CreateDirectory(
                    Path.Combine(
                        destination,
                        relative));
            }

            foreach (string file in
                     Directory.GetFiles(
                         source,
                         "*",
                         SearchOption.AllDirectories))
            {
                string relative =
                    Path.GetRelativePath(
                        source,
                        file);

                string target =
                    Path.Combine(
                        destination,
                        relative);

                Directory.CreateDirectory(
                    Path.GetDirectoryName(
                        target)!);

                File.Copy(
                    file,
                    target,
                    true);
            }
        }

        // =========================================================
        // SANITIZE
        // =========================================================

        private static string SanitizeFileName(
            string filename)
        {
            foreach (char c in
                     Path.GetInvalidFileNameChars())
            {
                filename =
                    filename.Replace(
                        c,
                        '_');
            }

            return filename;
        }

        // =========================================================
        // MODRINTH RESULT
        // =========================================================

        private sealed class ModrinthFile
        {
            public string Title { get; }

            public string Filename { get; }

            public string Url { get; }

            public ModrinthFile(
                string title,
                string filename,
                string url)
            {
                Title = title;
                Filename = filename;
                Url = url;
            }
        }
    }
}
