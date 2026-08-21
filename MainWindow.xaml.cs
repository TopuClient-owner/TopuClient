using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
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
        private static readonly HttpClient Http = CreateHttpClient();

        private MSession? _session;
        private Process? _minecraftProcess;

        private readonly string _gamePath;
        private readonly string _configPath;
        private readonly string _logPath;

        private readonly object _logLock = new object();

        private const string DefaultVersion = "1.21.1";

        private static readonly string[] SupportedVersions =
        {
            "1.21.1",
            "1.21.4",
            "1.21.8",
            "1.21.11",
            "26.1.2",
            "26.2"
        };

        private static readonly (string Slug, string Name)[] PerformanceMods =
        {
            ("sodium", "Sodium"),
            ("lithium", "Lithium"),
            ("indium", "Indium"),
            ("dynamic-fps", "Dynamic FPS"),
            ("sodium-extra", "Sodium Extra"),
            ("krypton", "Krypton")
        };

        public MainWindow()
        {
            InitializeComponent();

            _gamePath = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.ApplicationData),
                ".topuclient");

            _configPath = Path.Combine(
                _gamePath,
                "username.txt");

            _logPath = Path.Combine(
                _gamePath,
                "topu-minecraft.log");

            Directory.CreateDirectory(_gamePath);

            LoadUsername();

            if (RamLabel != null)
            {
                RamLabel.Text = $"{(int)RamSlider.Value}GB";
            }

            WriteLog("Topu Client initialized.");
            WriteLog($"Game directory: {_gamePath}");
        }

        private static HttpClient CreateHttpClient()
        {
            HttpClient client = new HttpClient(
                new HttpClientHandler
                {
                    AllowAutoRedirect = true
                });

            client.Timeout = TimeSpan.FromMinutes(30);

            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "TopuClient/1.0");

            return client;
        }

        // ============================================================
        // LOGGING
        // ============================================================

        private void WriteLog(string message)
        {
            try
            {
                Directory.CreateDirectory(_gamePath);

                lock (_logLock)
                {
                    File.AppendAllText(
                        _logPath,
                        $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
                }
            }
            catch
            {
            }
        }

        private void WriteException(
            string title,
            Exception ex)
        {
            WriteLog("");
            WriteLog($"===== {title} =====");
            WriteLog(ex.ToString());
        }

        private void StartLaunchLog()
        {
            try
            {
                Directory.CreateDirectory(_gamePath);

                lock (_logLock)
                {
                    File.WriteAllText(
                        _logPath,
                        "===== TOPU CLIENT MINECRAFT LOG =====" +
                        Environment.NewLine +
                        $"Started: {DateTime.Now:O}" +
                        Environment.NewLine +
                        Environment.NewLine);
                }
            }
            catch
            {
            }
        }

        private void AppendGameLog(string message)
        {
            WriteLog(message);
        }

        private void AppendRawGameOutput(
            string prefix,
            string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            try
            {
                lock (_logLock)
                {
                    Directory.CreateDirectory(_gamePath);

                    File.AppendAllText(
                        _logPath,
                        $"[{DateTime.Now:HH:mm:ss}] {prefix} {text}{Environment.NewLine}");
                }
            }
            catch
            {
            }
        }

        // ============================================================
        // USERNAME
        // ============================================================

        private void LoadUsername()
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
                    MSession.CreateOfflineSession(username);

                WriteLog(
                    $"Loaded username: {username}");
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
                    username);
            }
            catch (Exception ex)
            {
                WriteException(
                    "USERNAME SAVE ERROR",
                    ex);
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
            try
            {
                if (_minecraftProcess != null)
                {
                    try
                    {
                        if (!_minecraftProcess.HasExited)
                        {
                            _minecraftProcess.CloseMainWindow();
                        }
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }

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

            string tab =
                button.Tag?.ToString() ?? "";

            TabLaunch.Visibility =
                Visibility.Collapsed;

            TabProfiles.Visibility =
                Visibility.Collapsed;

            TabAccounts.Visibility =
                Visibility.Collapsed;

            Brush inactive =
                new SolidColorBrush(
                    Color.FromRgb(136, 136, 136));

            Brush active =
                new SolidColorBrush(
                    Color.FromRgb(0, 255, 136));

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
                new Thickness(0, 0, 0, 2);

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

        // ============================================================
        // RAM
        // ============================================================

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

        // ============================================================
        // VERSION
        // ============================================================

        private string GetSelectedVersion()
        {
            string version =
                (VersionBox.SelectedItem as ComboBoxItem)
                ?.Content
                ?.ToString()
                ?.Trim()
                ?? "";

            if (string.IsNullOrWhiteSpace(version))
                return DefaultVersion;

            return version;
        }

        private int GetRequiredJavaMajor(
            string minecraftVersion)
        {
            if (minecraftVersion.StartsWith(
                    "26.",
                    StringComparison.OrdinalIgnoreCase))
            {
                return 25;
            }

            return 21;
        }

        // ============================================================
        // PROFILE
        // ============================================================

        private void SaveProfile_Click(
            object sender,
            RoutedEventArgs e)
        {
            string version =
                GetSelectedVersion();

            int ram =
                (int)RamSlider.Value;

            SelectedProfileLabel.Text =
                $"Ready to launch Fabric {version}";

            StatusText.Text =
                $"Profile saved: Fabric {version}, {ram}GB RAM";

            WriteLog(
                $"Profile saved: Fabric {version}, RAM={ram}GB");

            MessageBox.Show(
                "Profile settings saved.",
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
                    "Auth Mode: Offline";
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
            await Task.CompletedTask;

            MessageBox.Show(
                "Microsoft authentication is not enabled in this build yet.",
                "Microsoft Login",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        // ============================================================
        // SERVER
        // ============================================================

        private void JoinServer_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (sender is not Button button)
                return;

            string server =
                button.Tag?.ToString() ?? "";

            StatusText.Text =
                $"Server selected: {server}";

            WriteLog(
                $"Server selected: {server}");
        }

        // ============================================================
        // MODRINTH SEARCH
        // ============================================================

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
                string minecraftVersion =
                    GetSelectedVersion();

                ModSearchStatus.Text =
                    $"Searching Modrinth for {query}...";

                string url =
                    "https://api.modrinth.com/v2/search" +
                    "?query=" +
                    Uri.EscapeDataString(query) +
                    "&facets=%5B%5B%22project_type%3Amod%22%5D%5D";

                using HttpResponseMessage response =
                    await Http.GetAsync(url);

                response.EnsureSuccessStatusCode();

                string json =
                    await response.Content.ReadAsStringAsync();

                using JsonDocument doc =
                    JsonDocument.Parse(json);

                JsonElement hits =
                    doc.RootElement.GetProperty("hits");

                if (hits.GetArrayLength() == 0)
                {
                    ModSearchStatus.Text =
                        "No mod found.";

                    return;
                }

                JsonElement hit = hits[0];

                string projectId =
                    hit.GetProperty("project_id")
                        .GetString()
                    ?? "";

                string title =
                    hit.GetProperty("title")
                        .GetString()
                    ?? query;

                await DownloadModByProjectIdAsync(
                    projectId,
                    title,
                    minecraftVersion);

                ModSearchStatus.Text =
                    $"Installed: {title}";
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

        private async Task DownloadModByProjectIdAsync(
            string projectId,
            string title,
            string minecraftVersion)
        {
            string url =
                "https://api.modrinth.com/v2/project/" +
                Uri.EscapeDataString(projectId) +
                "/version" +
                "?loaders=%5B%22fabric%22%5D" +
                "&game_versions=%5B%22" +
                Uri.EscapeDataString(minecraftVersion) +
                "%22%5D";

            using HttpResponseMessage response =
                await Http.GetAsync(url);

            response.EnsureSuccessStatusCode();

            string json =
                await response.Content.ReadAsStringAsync();

            using JsonDocument doc =
                JsonDocument.Parse(json);

            JsonElement versions =
                doc.RootElement;

            if (versions.ValueKind != JsonValueKind.Array ||
                versions.GetArrayLength() == 0)
            {
                throw new InvalidOperationException(
                    $"{title} has no compatible Fabric build for {minecraftVersion}.");
            }

            JsonElement? selectedFile = null;

            foreach (JsonElement version in
                     versions.EnumerateArray())
            {
                if (!version.TryGetProperty(
                        "files",
                        out JsonElement files))
                {
                    continue;
                }

                selectedFile =
                    FindPrimaryJar(files);

                if (selectedFile != null)
                    break;
            }

            if (selectedFile == null)
            {
                throw new InvalidOperationException(
                    $"No JAR file was returned for {title}.");
            }

            JsonElement file =
                selectedFile.Value;

            string downloadUrl =
                file.GetProperty("url")
                    .GetString()
                ?? throw new InvalidOperationException(
                    $"No download URL was returned for {title}.");

            string filename =
                file.GetProperty("filename")
                    .GetString()
                ?? $"{SanitizeFileName(title)}.jar";

            string modsFolder =
                Path.Combine(
                    _gamePath,
                    "mods");

            Directory.CreateDirectory(modsFolder);

            string destination =
                Path.Combine(
                    modsFolder,
                    SanitizeFileName(filename));

            StatusText.Text =
                $"Downloading {title}...";

            await DownloadFileAsync(
                downloadUrl,
                destination);

            WriteLog(
                $"Installed Modrinth mod: {title}");
        }

        // ============================================================
        // FABRIC INSTALLATION
        // ============================================================

        private async Task<string> InstallFabricAsync(
            string minecraftVersion,
            MinecraftPath minecraftPath)
        {
            StatusText.Text =
                $"Installing Fabric for Minecraft {minecraftVersion}...";

            WriteLog(
                "===== FABRIC INSTALLATION START =====");

            WriteLog(
                $"Minecraft version: {minecraftVersion}");

            FabricInstaller fabricInstaller =
                new FabricInstaller(Http);

            string fabricVersionName =
                await fabricInstaller.Install(
                    minecraftVersion,
                    minecraftPath);

            if (string.IsNullOrWhiteSpace(fabricVersionName))
            {
                throw new InvalidOperationException(
                    "Fabric installer returned an empty version name.");
            }

            WriteLog(
                $"Fabric installer returned: {fabricVersionName}");

            string fabricDirectory =
                Path.Combine(
                    _gamePath,
                    "versions",
                    fabricVersionName);

            string fabricJson =
                Path.Combine(
                    fabricDirectory,
                    fabricVersionName + ".json");

            if (!File.Exists(fabricJson))
            {
                throw new FileNotFoundException(
                    "Fabric installer did not create the Fabric version JSON.",
                    fabricJson);
            }

            WriteLog(
                $"Fabric profile: {fabricJson}");

            await RepairFabricProfileAsync(
                fabricVersionName,
                fabricJson);

            if (!ValidateFabricInstallation(
                fabricVersionName,
                minecraftPath))
            {
                throw new InvalidOperationException(
                    "Fabric installation is incomplete. Check topu-minecraft.log.");
            }

            WriteLog(
                "===== FABRIC INSTALLATION COMPLETE =====");

            return fabricVersionName;
        }

        // ============================================================
        // FABRIC PROFILE REPAIR
        // ============================================================

        private async Task RepairFabricProfileAsync(
            string fabricVersionName,
            string fabricJsonPath)
        {
            WriteLog(
                "===== FABRIC PROFILE REPAIR =====");

            string json =
                await File.ReadAllTextAsync(
                    fabricJsonPath);

            using JsonDocument document =
                JsonDocument.Parse(json);

            JsonElement root =
                document.RootElement;

            if (!root.TryGetProperty(
                    "libraries",
                    out JsonElement libraries) ||
                libraries.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidOperationException(
                    "Fabric profile has no libraries array.");
            }

            int checkedLibraries = 0;
            int downloadedLibraries = 0;

            foreach (JsonElement library in
                     libraries.EnumerateArray())
            {
                if (!library.TryGetProperty(
                        "name",
                        out JsonElement nameElement))
                {
                    continue;
                }

                string coordinate =
                    nameElement.GetString() ?? "";

                if (string.IsNullOrWhiteSpace(coordinate))
                    continue;

                string[] parts =
                    coordinate.Split(':');

                if (parts.Length < 3)
                {
                    WriteLog(
                        $"Skipping invalid library coordinate: {coordinate}");

                    continue;
                }

                string group =
                    parts[0];

                string artifact =
                    parts[1];

                string version =
                    parts[2];

                string relativePath =
                    group.Replace(
                        '.',
                        Path.DirectorySeparatorChar) +
                    Path.DirectorySeparatorChar +
                    artifact +
                    Path.DirectorySeparatorChar +
                    version +
                    Path.DirectorySeparatorChar +
                    artifact +
                    "-" +
                    version +
                    ".jar";

                string destination =
                    Path.Combine(
                        _gamePath,
                        "libraries",
                        relativePath);

                checkedLibraries++;

                if (File.Exists(destination) &&
                    new FileInfo(destination).Length > 0)
                {
                    continue;
                }

                string? url =
                    GetLibraryUrl(
                        library,
                        coordinate,
                        relativePath);

                if (string.IsNullOrWhiteSpace(url))
                {
                    WriteLog(
                        $"WARNING: Cannot determine URL for {coordinate}");

                    continue;
                }

                WriteLog(
                    $"Missing Fabric library: {coordinate}");

                WriteLog(
                    $"Downloading: {url}");

                StatusText.Text =
                    $"Downloading Fabric library: {artifact}-{version}.jar";

                await DownloadFileAsync(
                    url,
                    destination);

                if (!File.Exists(destination) ||
                    new FileInfo(destination).Length == 0)
                {
                    throw new IOException(
                        $"Fabric library failed verification: {destination}");
                }

                downloadedLibraries++;

                WriteLog(
                    $"Installed: {destination}");
            }

            WriteLog(
                $"Fabric libraries checked: {checkedLibraries}");

            WriteLog(
                $"Fabric libraries downloaded: {downloadedLibraries}");

            string expectedVersionJar =
                Path.Combine(
                    _gamePath,
                    "versions",
                    fabricVersionName,
                    fabricVersionName + ".jar");

            if (File.Exists(expectedVersionJar) &&
                new FileInfo(expectedVersionJar).Length > 0)
            {
                WriteLog(
                    $"Fabric version JAR already exists: {expectedVersionJar}");

                return;
            }

            string? loaderLibrary =
                FindExactFabricLoaderLibrary(
                    libraries);

            if (loaderLibrary == null)
            {
                throw new FileNotFoundException(
                    "Fabric profile references a loader, but the Fabric loader JAR could not be found.");
            }

            Directory.CreateDirectory(
                Path.GetDirectoryName(
                    expectedVersionJar)!);

            File.Copy(
                loaderLibrary,
                expectedVersionJar,
                true);

            WriteLog(
                $"Created required Fabric version JAR: {expectedVersionJar}");

            if (!File.Exists(expectedVersionJar) ||
                new FileInfo(expectedVersionJar).Length == 0)
            {
                throw new IOException(
                    $"Fabric version JAR could not be created: {expectedVersionJar}");
            }

            WriteLog(
                "Fabric version JAR verified.");
        }

        private string? FindExactFabricLoaderLibrary(
            JsonElement libraries)
        {
            foreach (JsonElement library in
                     libraries.EnumerateArray())
            {
                if (!library.TryGetProperty(
                        "name",
                        out JsonElement nameElement))
                {
                    continue;
                }

                string coordinate =
                    nameElement.GetString() ?? "";

                if (!coordinate.StartsWith(
                        "net.fabricmc:fabric-loader:",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string[] parts =
                    coordinate.Split(':');

                if (parts.Length < 3)
                    continue;

                string relativePath =
                    Path.Combine(
                        "net",
                        "fabricmc",
                        "fabric-loader",
                        parts[2],
                        $"fabric-loader-{parts[2]}.jar");

                string path =
                    Path.Combine(
                        _gamePath,
                        "libraries",
                        relativePath);

                WriteLog(
                    $"Exact Fabric loader library expected: {path}");

                if (File.Exists(path) &&
                    new FileInfo(path).Length > 0)
                {
                    return path;
                }

                string? url =
                    GetLibraryUrl(
                        library,
                        coordinate,
                        relativePath.Replace(
                            Path.DirectorySeparatorChar,
                            '/'));

                if (!string.IsNullOrWhiteSpace(url))
                {
                    try
                    {
                        WriteLog(
                            $"Fabric loader was missing. Downloading directly: {url}");

                        StatusText.Text =
                            "Downloading Fabric Loader...";

                        DownloadFileAsync(
                            url,
                            path)
                            .GetAwaiter()
                            .GetResult();

                        if (File.Exists(path) &&
                            new FileInfo(path).Length > 0)
                        {
                            return path;
                        }
                    }
                    catch (Exception ex)
                    {
                        WriteException(
                            "FABRIC LOADER DIRECT DOWNLOAD ERROR",
                            ex);
                    }
                }
            }

            string loaderRoot =
                Path.Combine(
                    _gamePath,
                    "libraries",
                    "net",
                    "fabricmc",
                    "fabric-loader");

            if (Directory.Exists(loaderRoot))
            {
                string[] jars =
                    Directory.GetFiles(
                        loaderRoot,
                        "fabric-loader-*.jar",
                        SearchOption.AllDirectories);

                foreach (string jar in jars)
                {
                    if (File.Exists(jar) &&
                        new FileInfo(jar).Length > 0)
                    {
                        WriteLog(
                            $"Found Fabric loader library: {jar}");

                        return jar;
                    }
                }
            }

            return null;
        }

        private string? GetLibraryUrl(
            JsonElement library,
            string coordinate,
            string relativePath)
        {
            try
            {
                if (library.TryGetProperty(
                        "downloads",
                        out JsonElement downloads) &&
                    downloads.ValueKind ==
                    JsonValueKind.Object &&
                    downloads.TryGetProperty(
                        "artifact",
                        out JsonElement artifact) &&
                    artifact.ValueKind ==
                    JsonValueKind.Object &&
                    artifact.TryGetProperty(
                        "url",
                        out JsonElement artifactUrl))
                {
                    string? url =
                        artifactUrl.GetString();

                    if (!string.IsNullOrWhiteSpace(url))
                        return url;
                }

                if (library.TryGetProperty(
                        "url",
                        out JsonElement urlElement))
                {
                    string? baseUrl =
                        urlElement.GetString();

                    if (!string.IsNullOrWhiteSpace(baseUrl))
                    {
                        return baseUrl.TrimEnd('/') +
                               "/" +
                               relativePath.Replace(
                                   '\\',
                                   '/');
                    }
                }

                if (coordinate.StartsWith(
                        "net.fabricmc:",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return
                        "https://maven.fabricmc.net/" +
                        relativePath.Replace(
                            '\\',
                            '/');
                }

                return
                    "https://libraries.minecraft.net/" +
                    relativePath.Replace(
                        '\\',
                        '/');
            }
            catch
            {
                return null;
            }
        }

        private bool ValidateFabricInstallation(
            string fabricVersionName,
            MinecraftPath minecraftPath)
        {
            try
            {
                WriteLog(
                    "===== FABRIC VALIDATION =====");

                string fabricDirectory =
                    Path.Combine(
                        _gamePath,
                        "versions",
                        fabricVersionName);

                string fabricJson =
                    Path.Combine(
                        fabricDirectory,
                        fabricVersionName + ".json");

                string fabricJar =
                    Path.Combine(
                        fabricDirectory,
                        fabricVersionName + ".jar");

                WriteLog(
                    $"Fabric directory: {fabricDirectory}");

                WriteLog(
                    $"Fabric JSON: {fabricJson}");

                WriteLog(
                    $"Fabric JAR: {fabricJar}");

                if (!Directory.Exists(
                        fabricDirectory))
                {
                    WriteLog(
                        "ERROR: Fabric directory does not exist.");

                    return false;
                }

                if (!File.Exists(
                        fabricJson))
                {
                    WriteLog(
                        "ERROR: Fabric JSON does not exist.");

                    return false;
                }

                using JsonDocument document =
                    JsonDocument.Parse(
                        File.ReadAllText(
                            fabricJson));

                if (document.RootElement.ValueKind !=
                    JsonValueKind.Object)
                {
                    WriteLog(
                        "ERROR: Fabric JSON is invalid.");

                    return false;
                }

                if (!File.Exists(fabricJar))
                {
                    WriteLog(
                        "ERROR: Fabric version JAR is missing.");

                    return false;
                }

                FileInfo jarInfo =
                    new FileInfo(fabricJar);

                if (jarInfo.Length <= 0)
                {
                    WriteLog(
                        "ERROR: Fabric version JAR is empty.");

                    return false;
                }

                WriteLog(
                    $"Fabric version JAR size: {jarInfo.Length} bytes.");

                string loaderRoot =
                    Path.Combine(
                        _gamePath,
                        "libraries",
                        "net",
                        "fabricmc",
                        "fabric-loader");

                if (!Directory.Exists(loaderRoot))
                {
                    WriteLog(
                        "ERROR: Fabric loader library directory is missing.");

                    return false;
                }

                string[] loaderJars =
                    Directory.GetFiles(
                        loaderRoot,
                        "fabric-loader-*.jar",
                        SearchOption.AllDirectories);

                if (loaderJars.Length == 0)
                {
                    WriteLog(
                        "ERROR: No Fabric loader JAR exists in libraries.");

                    return false;
                }

                foreach (string jar in loaderJars)
                {
                    WriteLog(
                        $"Fabric loader library found: {jar}");
                }

                WriteLog(
                    "Fabric installation validation passed.");

                WriteLog(
                    "===== FABRIC VALIDATION COMPLETE =====");

                return true;
            }
            catch (JsonException ex)
            {
                WriteException(
                    "FABRIC JSON VALIDATION ERROR",
                    ex);

                return false;
            }
            catch (Exception ex)
            {
                WriteException(
                    "FABRIC VALIDATION ERROR",
                    ex);

                return false;
            }
        }

        // ============================================================
        // PERFORMANCE MODS
        // ============================================================

        private async Task InstallPerformanceModsAsync(
            string minecraftVersion)
        {
            string modsFolder =
                Path.Combine(
                    _gamePath,
                    "mods");

            Directory.CreateDirectory(
                modsFolder);

            WriteLog(
                "===== PERFORMANCE MOD INSTALL =====");

            foreach ((string slug, string name) in
                     PerformanceMods)
            {
                try
                {
                    StatusText.Text =
                        $"Installing {name}...";

                    WriteLog(
                        $"Checking Modrinth: {slug} for {minecraftVersion}");

                    bool installed =
                        await DownloadPerformanceModAsync(
                            slug,
                            name,
                            minecraftVersion);

                    if (installed)
                    {
                        WriteLog(
                            $"Preconfigured mod installed: {name}");
                    }
                    else
                    {
                        WriteLog(
                            $"Preconfigured mod skipped: {name}");
                    }
                }
                catch (Exception ex)
                {
                    WriteLog(
                        $"Optional mod failed: {name}");

                    WriteLog(
                        ex.Message);
                }
            }

            WriteLog(
                "===== PERFORMANCE MOD INSTALL COMPLETE =====");
        }

        private async Task<bool> DownloadPerformanceModAsync(
            string slug,
            string name,
            string minecraftVersion)
        {
            string url =
                "https://api.modrinth.com/v2/project/" +
                Uri.EscapeDataString(slug) +
                "/version" +
                "?loaders=%5B%22fabric%22%5D" +
                "&game_versions=%5B%22" +
                Uri.EscapeDataString(minecraftVersion) +
                "%22%5D";

            using HttpResponseMessage response =
                await Http.GetAsync(url);

            response.EnsureSuccessStatusCode();

            string json =
                await response.Content.ReadAsStringAsync();

            using JsonDocument doc =
                JsonDocument.Parse(json);

            JsonElement versions =
                doc.RootElement;

            if (versions.ValueKind != JsonValueKind.Array ||
                versions.GetArrayLength() == 0)
            {
                WriteLog(
                    $"No compatible {name} version exists for {minecraftVersion}.");

                return false;
            }

            JsonElement? selectedFile = null;

            foreach (JsonElement version in
                     versions.EnumerateArray())
            {
                if (!version.TryGetProperty(
                        "files",
                        out JsonElement files))
                {
                    continue;
                }

                selectedFile =
                    FindPrimaryJar(files);

                if (selectedFile != null)
                    break;
            }

            if (selectedFile == null)
            {
                WriteLog(
                    $"No usable JAR was returned for {name}.");

                return false;
            }

            JsonElement file =
                selectedFile.Value;

            string downloadUrl =
                file.GetProperty("url")
                    .GetString()
                ?? throw new InvalidOperationException(
                    $"No download URL for {name}.");

            string filename =
                file.GetProperty("filename")
                    .GetString()
                ?? $"{slug}.jar";

            string destination =
                Path.Combine(
                    _gamePath,
                    "mods",
                    SanitizeFileName(filename));

            if (File.Exists(destination))
            {
                FileInfo existing =
                    new FileInfo(destination);

                if (existing.Length > 0)
                {
                    WriteLog(
                        $"Mod already installed: {name}");

                    return true;
                }

                TryDeleteFile(destination);
            }

            await DownloadFileAsync(
                downloadUrl,
                destination);

            if (!File.Exists(destination))
            {
                throw new IOException(
                    $"Downloaded mod file was not created: {destination}");
            }

            FileInfo info =
                new FileInfo(destination);

            if (info.Length == 0)
            {
                TryDeleteFile(destination);

                throw new IOException(
                    $"Downloaded mod file is empty: {destination}");
            }

            WriteLog(
                $"Installed Modrinth mod: {name}");

            WriteLog(
                $"Mod file: {destination}");

            return true;
        }

        private static JsonElement? FindPrimaryJar(
            JsonElement files)
        {
            JsonElement? fallback = null;

            if (files.ValueKind != JsonValueKind.Array)
                return null;

            foreach (JsonElement file in
                     files.EnumerateArray())
            {
                if (!file.TryGetProperty(
                        "filename",
                        out JsonElement filenameElement))
                {
                    continue;
                }

                string filename =
                    filenameElement.GetString() ?? "";

                if (!filename.EndsWith(
                        ".jar",
                        StringComparison.OrdinalIgnoreCase))
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
                    return file;

                fallback ??= file;
            }

            return fallback;
        }

        // ============================================================
        // DOWNLOAD
        // ============================================================

        private async Task DownloadFileAsync(
            string url,
            string destination)
        {
            string? directory =
                Path.GetDirectoryName(destination);

            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string temporary =
                Path.Combine(
                    directory ?? _gamePath,
                    "." +
                    Path.GetFileName(destination) +
                    "." +
                    Guid.NewGuid().ToString("N") +
                    ".download");

            try
            {
                WriteLog(
                    $"Downloading: {url}");

                using HttpResponseMessage response =
                    await Http.GetAsync(
                        url,
                        HttpCompletionOption.ResponseHeadersRead);

                response.EnsureSuccessStatusCode();

                using Stream input =
                    await response.Content.ReadAsStreamAsync();

                using FileStream output =
                    new FileStream(
                        temporary,
                        FileMode.Create,
                        FileAccess.Write,
                        FileShare.None,
                        81920,
                        FileOptions.Asynchronous);

                await input.CopyToAsync(
                    output);

                await output.FlushAsync();

                output.Close();
                input.Close();

                if (!File.Exists(temporary))
                {
                    throw new IOException(
                        "Temporary download file was not created.");
                }

                FileInfo info =
                    new FileInfo(temporary);

                if (info.Length <= 0)
                {
                    throw new IOException(
                        "Downloaded file is empty.");
                }

                if (File.Exists(destination))
                {
                    File.Delete(destination);
                }

                File.Move(
                    temporary,
                    destination);

                WriteLog(
                    $"Download complete: {destination}");
            }
            catch
            {
                TryDeleteFile(temporary);
                throw;
            }
            finally
            {
                TryDeleteFile(temporary);
            }
        }

        private static void TryDeleteFile(
            string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
            }
        }

        // ============================================================
        // JAVA
        // ============================================================

        private async Task<string> EnsureJavaAsync(
            int requiredMajor)
        {
            string runtimeFolder =
                Path.Combine(
                    _gamePath,
                    "runtime",
                    $"java{requiredMajor}");

            string javaExe =
                Path.Combine(
                    runtimeFolder,
                    "bin",
                    "java.exe");

            if (File.Exists(javaExe) &&
                IsRequiredJava(
                    javaExe,
                    requiredMajor))
            {
                WriteLog(
                    $"Using existing Java {requiredMajor}: {javaExe}");

                return javaExe;
            }

            string systemJava =
                FindSystemJava(requiredMajor);

            if (!string.IsNullOrWhiteSpace(systemJava))
            {
                WriteLog(
                    $"Using system Java {requiredMajor}: {systemJava}");

                return systemJava;
            }

            WriteLog(
                $"Java {requiredMajor} not found.");

            StatusText.Text =
                $"Downloading Java {requiredMajor}...";

            await DownloadAndInstallJavaAsync(
                requiredMajor,
                runtimeFolder);

            if (!File.Exists(javaExe))
            {
                throw new InvalidOperationException(
                    $"Java {requiredMajor} installation failed: java.exe was not found.");
            }

            if (!IsRequiredJava(
                    javaExe,
                    requiredMajor))
            {
                throw new InvalidOperationException(
                    $"Installed runtime is not Java {requiredMajor}.");
            }

            WriteLog(
                $"Java {requiredMajor} verified.");

            return javaExe;
        }

        private string FindSystemJava(
            int requiredMajor)
        {
            string javaHome =
                Environment.GetEnvironmentVariable(
                    "JAVA_HOME") ?? "";

            if (!string.IsNullOrWhiteSpace(javaHome))
            {
                string candidate =
                    Path.Combine(
                        javaHome,
                        "bin",
                        "java.exe");

                if (File.Exists(candidate) &&
                    IsRequiredJava(
                        candidate,
                        requiredMajor))
                {
                    return candidate;
                }
            }

            string path =
                Environment.GetEnvironmentVariable(
                    "PATH") ?? "";

            foreach (string folder in
                     path.Split(
                         Path.PathSeparator,
                         StringSplitOptions.RemoveEmptyEntries))
            {
                string candidate =
                    Path.Combine(
                        folder.Trim(),
                        "java.exe");

                if (!File.Exists(candidate))
                    continue;

                if (IsRequiredJava(
                        candidate,
                        requiredMajor))
                {
                    return candidate;
                }
            }

            return "";
        }

        private bool IsRequiredJava(
            string javaPath,
            int requiredMajor)
        {
            try
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

                string combined =
                    stdout +
                    Environment.NewLine +
                    stderr;

                return combined.Contains(
                    $"version \"{requiredMajor}.",
                    StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private async Task DownloadAndInstallJavaAsync(
            int major,
            string destination)
        {
            string apiUrl =
                "https://api.adoptium.net/v3/assets/latest/" +
                major +
                "/hotspot" +
                "?architecture=x64" +
                "&image_type=jre" +
                "&os=windows" +
                "&vendor=eclipse";

            using HttpResponseMessage response =
                await Http.GetAsync(apiUrl);

            response.EnsureSuccessStatusCode();

            string json =
                await response.Content.ReadAsStringAsync();

            using JsonDocument doc =
                JsonDocument.Parse(json);

            JsonElement assets =
                doc.RootElement;

            if (assets.ValueKind != JsonValueKind.Array ||
                assets.GetArrayLength() == 0)
            {
                throw new InvalidOperationException(
                    $"No Windows x64 Temurin JRE {major} was found.");
            }

            JsonElement package =
                assets[0]
                    .GetProperty("binary")
                    .GetProperty("package");

            string downloadUrl =
                package.GetProperty("link")
                    .GetString()
                ?? throw new InvalidOperationException(
                    "Java download URL was missing.");

            string archiveName =
                package.TryGetProperty(
                    "name",
                    out JsonElement nameElement)
                    ? nameElement.GetString()
                        ?? $"java{major}.zip"
                    : $"java{major}.zip";

            string tempArchive =
                Path.Combine(
                    Path.GetTempPath(),
                    "topu-java-" +
                    Guid.NewGuid().ToString("N") +
                    "-" +
                    SanitizeFileName(archiveName));

            try
            {
                WriteLog(
                    $"Java archive URL: {downloadUrl}");

                using (
                    HttpResponseMessage javaResponse =
                        await Http.GetAsync(
                            downloadUrl,
                            HttpCompletionOption.ResponseHeadersRead))
                {
                    javaResponse.EnsureSuccessStatusCode();

                    using Stream input =
                        await javaResponse.Content.ReadAsStreamAsync();

                    using FileStream output =
                        new FileStream(
                            tempArchive,
                            FileMode.Create,
                            FileAccess.Write,
                            FileShare.None,
                            81920,
                            FileOptions.Asynchronous);

                    await input.CopyToAsync(
                        output);

                    await output.FlushAsync();
                }

                if (!File.Exists(tempArchive))
                {
                    throw new IOException(
                        "Java archive download did not create a file.");
                }

                FileInfo archiveInfo =
                    new FileInfo(tempArchive);

                if (archiveInfo.Length <= 0)
                {
                    throw new IOException(
                        "Downloaded Java archive is empty.");
                }

                string extractionDirectory =
                    destination +
                    ".extracting-" +
                    Guid.NewGuid().ToString("N");

                try
                {
                    TryDeleteDirectory(
                        extractionDirectory);

                    Directory.CreateDirectory(
                        extractionDirectory);

                    ZipFile.ExtractToDirectory(
                        tempArchive,
                        extractionDirectory,
                        true);

                    string? javaRoot =
                        FindJavaRoot(
                            extractionDirectory);

                    if (javaRoot != null &&
                        !File.Exists(
                            Path.Combine(
                                extractionDirectory,
                                "bin",
                                "java.exe")))
                    {
                        MoveJavaRootContents(
                            javaRoot,
                            extractionDirectory);
                    }

                    string extractedJava =
                        Path.Combine(
                            extractionDirectory,
                            "bin",
                            "java.exe");

                    if (!File.Exists(extractedJava))
                    {
                        throw new InvalidOperationException(
                            $"Java {major} was extracted but java.exe could not be located.");
                    }

                    if (Directory.Exists(destination))
                    {
                        Directory.Delete(
                            destination,
                            true);
                    }

                    Directory.Move(
                        extractionDirectory,
                        destination);
                }
                catch
                {
                    TryDeleteDirectory(
                        extractionDirectory);

                    throw;
                }
            }
            finally
            {
                TryDeleteFile(tempArchive);
            }
        }

        private static string? FindJavaRoot(
            string destination)
        {
            foreach (string directory in
                     Directory.GetDirectories(
                         destination))
            {
                string javaExe =
                    Path.Combine(
                        directory,
                        "bin",
                        "java.exe");

                if (File.Exists(javaExe))
                    return directory;
            }

            return null;
        }

        private static void MoveJavaRootContents(
            string source,
            string destination)
        {
            foreach (string directory in
                     Directory.GetDirectories(source))
            {
                string target =
                    Path.Combine(
                        destination,
                        Path.GetFileName(directory));

                if (Directory.Exists(target))
                {
                    Directory.Delete(
                        target,
                        true);
                }

                Directory.Move(
                    directory,
                    target);
            }

            foreach (string file in
                     Directory.GetFiles(source))
            {
                string target =
                    Path.Combine(
                        destination,
                        Path.GetFileName(file));

                File.Move(
                    file,
                    target,
                    true);
            }

            TryDeleteDirectory(source);
        }

        private static void TryDeleteDirectory(
            string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(
                        path,
                        true);
                }
            }
            catch
            {
            }
        }

        // ============================================================
        // MINECRAFT VALIDATION
        // ============================================================

        private bool ValidateMinecraftInstallation(
            string minecraftVersion,
            MinecraftPath minecraftPath,
            string fabricVersion)
        {
            try
            {
                WriteLog(
                    "===== INSTALLATION VALIDATION =====");

                string vanillaDirectory =
                    Path.Combine(
                        _gamePath,
                        "versions",
                        minecraftVersion);

                string fabricDirectory =
                    Path.Combine(
                        _gamePath,
                        "versions",
                        fabricVersion);

                string vanillaJson =
                    Path.Combine(
                        vanillaDirectory,
                        minecraftVersion + ".json");

                string fabricJson =
                    Path.Combine(
                        fabricDirectory,
                        fabricVersion + ".json");

                string fabricJar =
                    Path.Combine(
                        fabricDirectory,
                        fabricVersion + ".jar");

                if (!Directory.Exists(
                        vanillaDirectory))
                {
                    WriteLog(
                        "ERROR: Vanilla version directory missing.");

                    return false;
                }

                if (!File.Exists(vanillaJson))
                {
                    WriteLog(
                        "ERROR: Vanilla JSON missing.");

                    return false;
                }

                if (!Directory.Exists(
                        fabricDirectory))
                {
                    WriteLog(
                        "ERROR: Fabric version directory missing.");

                    return false;
                }

                if (!File.Exists(fabricJson))
                {
                    WriteLog(
                        "ERROR: Fabric JSON missing.");

                    return false;
                }

                if (!File.Exists(fabricJar))
                {
                    WriteLog(
                        $"ERROR: Fabric JAR missing: {fabricJar}");

                    return false;
                }

                if (new FileInfo(fabricJar).Length <= 0)
                {
                    WriteLog(
                        "ERROR: Fabric JAR is empty.");

                    return false;
                }

                using JsonDocument vanillaDocument =
                    JsonDocument.Parse(
                        File.ReadAllText(
                            vanillaJson));

                using JsonDocument fabricDocument =
                    JsonDocument.Parse(
                        File.ReadAllText(
                            fabricJson));

                if (vanillaDocument.RootElement.ValueKind !=
                    JsonValueKind.Object)
                {
                    return false;
                }

                if (fabricDocument.RootElement.ValueKind !=
                    JsonValueKind.Object)
                {
                    return false;
                }

                WriteLog(
                    $"Vanilla JSON verified: {vanillaJson}");

                WriteLog(
                    $"Fabric JSON verified: {fabricJson}");

                WriteLog(
                    $"Fabric launch JAR verified: {fabricJar}");

                WriteLog(
                    "===== INSTALLATION VALIDATION COMPLETE =====");

                return true;
            }
            catch (Exception ex)
            {
                WriteException(
                    "INSTALLATION VALIDATION ERROR",
                    ex);

                return false;
            }
        }

        // ============================================================
        // MAIN LAUNCH
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
                }

                _minecraftProcess = null;
            }

            LaunchBtn.IsEnabled = false;

            try
            {
                StartLaunchLog();

                string minecraftVersion =
                    GetSelectedVersion();

                int ram =
                    Math.Max(
                        2048,
                        (int)RamSlider.Value * 1024);

                WriteLog(
                    $"Minecraft: {minecraftVersion}");

                WriteLog(
                    $"RAM: {ram} MB");

                WriteLog(
                    $"Game directory: {_gamePath}");

                // ----------------------------------------------------
                // AUTH
                // ----------------------------------------------------

                if (AuthTypeBox.SelectedIndex != 0)
                {
                    throw new InvalidOperationException(
                        "Microsoft login is not enabled in this build. Select Offline mode.");
                }

                string username =
                    UsernameInput.Text.Trim();

                if (string.IsNullOrWhiteSpace(username))
                {
                    username = "TopuPlayer";
                }

                _session =
                    MSession.CreateOfflineSession(
                        username);

                SaveUsername(username);

                WriteLog(
                    $"Offline username: {username}");

                WriteLog(
                    $"Session UUID: {_session.UUID}");

                // ----------------------------------------------------
                // JAVA
                // ----------------------------------------------------

                int javaMajor =
                    GetRequiredJavaMajor(
                        minecraftVersion);

                WriteLog(
                    $"Required Java major: {javaMajor}");

                string javaPath =
                    await EnsureJavaAsync(
                        javaMajor);

                WriteLog(
                    $"Java path: {javaPath}");

                // ----------------------------------------------------
                // CMLLIB
                // ----------------------------------------------------

                MinecraftPath minecraftPath =
                    new MinecraftPath(
                        _gamePath);

                MinecraftLauncher launcher =
                    new MinecraftLauncher(
                        minecraftPath);

                // ----------------------------------------------------
                // VANILLA INSTALL
                // ----------------------------------------------------

                StatusText.Text =
                    $"Installing Minecraft {minecraftVersion}...";

                WriteLog(
                    "Checking/installing base Minecraft files.");

                Progress<InstallerProgressChangedEventArgs>
                    fileProgress =
                        new Progress<InstallerProgressChangedEventArgs>(
                            args =>
                            {
                                try
                                {
                                    StatusText.Text =
                                        $"Downloading {args.Name} " +
                                        $"({args.ProgressedTasks}/{args.TotalTasks})";
                                }
                                catch
                                {
                                }
                            });

                Progress<ByteProgress>
                    byteProgress =
                        new Progress<ByteProgress>(
                            args =>
                            {
                                try
                                {
                                    if (args.TotalBytes > 0)
                                    {
                                        double percent =
                                            args.ProgressedBytes *
                                            100.0 /
                                            args.TotalBytes;

                                        StatusText.Text =
                                            $"Downloading: {percent:0}%";
                                    }
                                }
                                catch
                                {
                                }
                            });

                await launcher.InstallAsync(
                    minecraftVersion,
                    fileProgress,
                    byteProgress,
                    CancellationToken.None);

                WriteLog(
                    "Base Minecraft files are installed.");

                // ----------------------------------------------------
                // FABRIC
                // ----------------------------------------------------

                string fabricVersion =
                    await InstallFabricAsync(
                        minecraftVersion,
                        minecraftPath);

                WriteLog(
                    $"Fabric installed: {fabricVersion}");

                // ----------------------------------------------------
                // VALIDATION
                // ----------------------------------------------------

                bool valid =
                    ValidateMinecraftInstallation(
                        minecraftVersion,
                        minecraftPath,
                        fabricVersion);

                if (!valid)
                {
                    throw new InvalidOperationException(
                        "Minecraft/Fabric installation validation failed.");
                }

                // ----------------------------------------------------
                // PERFORMANCE MODS
                // ----------------------------------------------------

                StatusText.Text =
                    "Installing performance mods...";

                await InstallPerformanceModsAsync(
                    minecraftVersion);

                // ----------------------------------------------------
                // SESSION
                // ----------------------------------------------------

                if (_session == null)
                {
                    throw new InvalidOperationException(
                        "Minecraft session was not created.");
                }

                // ----------------------------------------------------
                // OPTIONS
                // ----------------------------------------------------

                MLaunchOption options =
                    new MLaunchOption
                    {
                        Session = _session,
                        MaximumRamMb = ram,
                        MinimumRamMb =
                            Math.Min(
                                1024,
                                ram),
                        JavaPath = javaPath,
                        GameLauncherName =
                            "Topu Client",
                        GameLauncherVersion =
                            "1.0.0"
                    };

                // ----------------------------------------------------
                // BUILD
                // ----------------------------------------------------

                StatusText.Text =
                    "Building Minecraft process...";

                WriteLog(
                    "Building Minecraft process.");

                Process process =
                    await launcher.BuildProcessAsync(
                        fabricVersion,
                        options,
                        CancellationToken.None);

                if (process == null)
                {
                    throw new InvalidOperationException(
                        "CmlLib returned a null Minecraft process.");
                }

                // ====================================================
                // FABRIC DUPLICATE LOADER FIX
                // ====================================================

                FixFabricDuplicateLoaderClasspath(
                    process,
                    fabricVersion);

                // ====================================================
                // INVALID FABRICMCEMU FIX
                // ====================================================

                FixFabricMcEmuArgument(
                    process);

                _minecraftProcess =
                    process;

                // ----------------------------------------------------
                // PROCESS OUTPUT
                // ----------------------------------------------------

                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;

                process.OutputDataReceived +=
                    Minecraft_OutputDataReceived;

                process.ErrorDataReceived +=
                    Minecraft_ErrorDataReceived;

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
                    fabricVersion,
                    ram);

                // ----------------------------------------------------
                // START
                // ----------------------------------------------------

                StatusText.Text =
                    $"Starting Fabric {minecraftVersion}...";

                WriteLog(
                    "Starting Minecraft.");

                bool started =
                    process.Start();

                if (!started)
                {
                    throw new InvalidOperationException(
                        "Windows failed to start Minecraft.");
                }

                WriteLog(
                    $"Minecraft started. PID={process.Id}");

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                StatusText.Text =
                    $"Topu Client running as {username}";

                _ = MonitorMinecraftAsync(
                    process);
            }
            catch (Exception ex)
            {
                StatusText.Text =
                    "Launch failed.";

                WriteException(
                    "TOPU LAUNCH ERROR",
                    ex);

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

        // ============================================================
        // FABRIC DUPLICATE LOADER CLASS FIX
        // ============================================================

        private void FixFabricDuplicateLoaderClasspath(
            Process process,
            string fabricVersion)
        {
            try
            {
                string arguments =
                    process.StartInfo.Arguments;

                string loaderLibraryRoot =
                    Path.Combine(
                        _gamePath,
                        "libraries",
                        "net",
                        "fabricmc",
                        "fabric-loader");

                string loaderVersionJar =
                    Path.Combine(
                        _gamePath,
                        "versions",
                        fabricVersion,
                        fabricVersion + ".jar");

                string loaderLibraryJar = "";

                if (Directory.Exists(loaderLibraryRoot))
                {
                    string[] jars =
                        Directory.GetFiles(
                            loaderLibraryRoot,
                            "fabric-loader-*.jar",
                            SearchOption.AllDirectories);

                    foreach (string jar in jars)
                    {
                        if (File.Exists(jar) &&
                            new FileInfo(jar).Length > 0)
                        {
                            loaderLibraryJar = jar;
                            break;
                        }
                    }
                }

                if (string.IsNullOrWhiteSpace(loaderLibraryJar))
                {
                    WriteLog(
                        "Fabric duplicate-loader fix: loader library was not found.");

                    return;
                }

                WriteLog(
                    $"Fabric version JAR kept: {loaderVersionJar}");

                WriteLog(
                    $"Fabric library JAR to remove from classpath: {loaderLibraryJar}");

                string normalizedLibrary =
                    Path.GetFullPath(
                        loaderLibraryJar)
                    .Replace(
                        '/',
                        '\\');

                string normalizedVersion =
                    Path.GetFullPath(
                        loaderVersionJar)
                    .Replace(
                        '/',
                        '\\');

                string quotedLibrary =
                    "\"" +
                    normalizedLibrary +
                    "\"";

                string quotedVersion =
                    "\"" +
                    normalizedVersion +
                    "\"";

                bool foundLibrary =
                    arguments.Contains(
                        normalizedLibrary,
                        StringComparison.OrdinalIgnoreCase);

                bool foundVersion =
                    arguments.Contains(
                        normalizedVersion,
                        StringComparison.OrdinalIgnoreCase);

                WriteLog(
                    $"Fabric loader library present before fix: {foundLibrary}");

                WriteLog(
                    $"Fabric version JAR present before fix: {foundVersion}");

                // ----------------------------------------------------
                // Remove exact loader library from classpath.
                // ----------------------------------------------------

                arguments =
                    arguments.Replace(
                        quotedLibrary + ";",
                        "",
                        StringComparison.OrdinalIgnoreCase);

                arguments =
                    arguments.Replace(
                        ";" + quotedLibrary,
                        "",
                        StringComparison.OrdinalIgnoreCase);

                arguments =
                    arguments.Replace(
                        quotedLibrary,
                        "",
                        StringComparison.OrdinalIgnoreCase);

                // ----------------------------------------------------
                // Also handle an unquoted classpath entry.
                // ----------------------------------------------------

                arguments =
                    arguments.Replace(
                        normalizedLibrary + ";",
                        "",
                        StringComparison.OrdinalIgnoreCase);

                arguments =
                    arguments.Replace(
                        ";" + normalizedLibrary,
                        "",
                        StringComparison.OrdinalIgnoreCase);

                arguments =
                    arguments.Replace(
                        normalizedLibrary,
                        "",
                        StringComparison.OrdinalIgnoreCase);

                process.StartInfo.Arguments =
                    arguments;

                int libraryOccurrences =
                    CountOccurrences(
                        process.StartInfo.Arguments,
                        normalizedLibrary);

                int versionOccurrences =
                    CountOccurrences(
                        process.StartInfo.Arguments,
                        normalizedVersion);

                WriteLog(
                    $"Fabric loader library classpath occurrences after fix: {libraryOccurrences}");

                WriteLog(
                    $"Fabric version JAR classpath occurrences after fix: {versionOccurrences}");

                if (libraryOccurrences > 0)
                {
                    WriteLog(
                        "WARNING: Fabric loader library still appears in classpath.");
                }
                else
                {
                    WriteLog(
                        "Fabric duplicate loader classpath fix applied successfully.");
                }

                if (versionOccurrences == 0)
                {
                    WriteLog(
                        "WARNING: Fabric version JAR was not found in the generated classpath.");
                }
            }
            catch (Exception ex)
            {
                WriteException(
                    "FABRIC DUPLICATE LOADER FIX ERROR",
                    ex);

                throw;
            }
        }

        private static int CountOccurrences(
            string text,
            string value)
        {
            if (string.IsNullOrEmpty(text) ||
                string.IsNullOrEmpty(value))
            {
                return 0;
            }

            int count = 0;
            int index = 0;

            while (true)
            {
                index =
                    text.IndexOf(
                        value,
                        index,
                        StringComparison.OrdinalIgnoreCase);

                if (index < 0)
                    break;

                count++;
                index += value.Length;
            }

            return count;
        }

        // ============================================================
        // FABRICMCEMU FIX
        // ============================================================

        private void FixFabricMcEmuArgument(
            Process process)
        {
            try
            {
                string arguments =
                    process.StartInfo.Arguments;

                string[] invalidArguments =
                {
                    "\"-DFabricMcEmu= net.minecraft.client.main.Main \"",
                    "\"-DFabricMcEmu= net.minecraft.client.main.Main\"",
                    "-DFabricMcEmu= net.minecraft.client.main.Main",
                    "-DFabricMcEmu=net.minecraft.client.main.Main"
                };

                bool removed = false;

                foreach (string invalidArgument in
                         invalidArguments)
                {
                    if (arguments.Contains(
                            invalidArgument,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        arguments =
                            arguments.Replace(
                                invalidArgument,
                                "",
                                StringComparison.OrdinalIgnoreCase);

                        removed = true;
                    }
                }

                if (removed)
                {
                    process.StartInfo.Arguments =
                        arguments;

                    WriteLog(
                        "Removed invalid FabricMcEmu argument.");
                }
                else
                {
                    WriteLog(
                        "No invalid FabricMcEmu argument detected.");
                }
            }
            catch (Exception ex)
            {
                WriteException(
                    "FABRIC MCEMU ARGUMENT FIX ERROR",
                    ex);

                throw;
            }
        }

        // ============================================================
        // MINECRAFT OUTPUT
        // ============================================================

        private void Minecraft_OutputDataReceived(
            object sender,
            DataReceivedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(e.Data))
                return;

            AppendRawGameOutput(
                "[MC]",
                e.Data);
        }

        private void Minecraft_ErrorDataReceived(
            object sender,
            DataReceivedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(e.Data))
                return;

            AppendRawGameOutput(
                "[MC-ERR]",
                e.Data);
        }

        // ============================================================
        // PROCESS MONITOR
        // ============================================================

        private async Task MonitorMinecraftAsync(
            Process process)
        {
            try
            {
                await Task.Run(
                    () =>
                    {
                        try
                        {
                            process.WaitForExit();
                        }
                        catch
                        {
                        }
                    });

                await Task.Delay(500);

                int exitCode = 0;

                try
                {
                    exitCode =
                        process.ExitCode;
                }
                catch
                {
                }

                AppendGameLog(
                    $"===== MINECRAFT EXITED: {exitCode} =====");

                if (exitCode != 0)
                {
                    AppendGameLog(
                        "Minecraft did not exit normally.");

                    AppendGameLog(
                        "Check [MC] and [MC-ERR] lines above.");
                }

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
                                $"Minecraft crashed (exit code {exitCode}). Check the log.";
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
                try
                {
                    process.OutputDataReceived -=
                        Minecraft_OutputDataReceived;

                    process.ErrorDataReceived -=
                        Minecraft_ErrorDataReceived;
                }
                catch
                {
                }

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
            int ram)
        {
            try
            {
                string path =
                    Path.Combine(
                        _gamePath,
                        "topu-launch-debug.txt");

                StringBuilder text =
                    new StringBuilder();

                text.AppendLine(
                    "===== TOPU CLIENT DEBUG =====");

                text.AppendLine(
                    $"Time: {DateTime.Now:O}");

                text.AppendLine();

                text.AppendLine(
                    $"Minecraft: {minecraftVersion}");

                text.AppendLine(
                    $"Fabric: {fabricVersion}");

                text.AppendLine(
                    $"Java: {javaPath}");

                text.AppendLine(
                    $"RAM: {ram} MB");

                text.AppendLine();

                text.AppendLine(
                    "Executable:");

                text.AppendLine(
                    process.StartInfo.FileName);

                text.AppendLine();

                text.AppendLine(
                    "Arguments:");

                text.AppendLine(
                    process.StartInfo.Arguments);

                text.AppendLine();

                text.AppendLine(
                    "Working directory:");

                text.AppendLine(
                    process.StartInfo.WorkingDirectory);

                File.WriteAllText(
                    path,
                    text.ToString());
            }
            catch (Exception ex)
            {
                WriteException(
                    "DEBUG FILE ERROR",
                    ex);
            }
        }

        // ============================================================
        // UTILITIES
        // ============================================================

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
    }
}
