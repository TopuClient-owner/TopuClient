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
using CmlLib.Core.ProcessBuilder;

namespace TopuLauncher
{
    public partial class MainWindow : Window
    {
        // ============================================================
        // HTTP
        // ============================================================

        private static readonly HttpClient Http = CreateHttpClient();

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
        // FIELDS
        // ============================================================

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

        // ============================================================
        // CONSTRUCTOR
        // ============================================================

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
                RamLabel.Text =
                    $"{(int)RamSlider.Value}GB";
            }

            WriteLog("Topu Client initialized.");
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
            WindowState =
                WindowState.Minimized;
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
            if (RamLabel == null)
                return;

            RamLabel.Text =
                $"{(int)e.NewValue}GB";
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

            foreach (string supported in SupportedVersions)
            {
                if (string.Equals(
                        supported,
                        version,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return supported;
                }
            }

            return DefaultVersion;
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
                $"Profile saved: Minecraft={version}, RAM={ram}GB");

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

            if (versions.ValueKind !=
                    JsonValueKind.Array ||
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
                    continue;

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

            Directory.CreateDirectory(
                modsFolder);

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
        // FABRIC INSTALLER
        //
        // This implementation does NOT depend on a CmlLib Fabric
        // installer namespace that is absent from your .csproj.
        //
        // It gets Fabric Loader metadata directly from Fabric's
        // official Maven metadata service.
        // ============================================================

        private async Task<string> InstallFabricAsync(
            string minecraftVersion,
            MinecraftPath minecraftPath)
        {
            WriteLog(
                "===== FABRIC INSTALLATION START =====");

            StatusText.Text =
                "Getting Fabric Loader...";

            string loaderVersion =
                await GetFabricLoaderVersionAsync(
                    minecraftVersion);

            string fabricVersionName =
                $"fabric-loader-{loaderVersion}-{minecraftVersion}";

            WriteLog(
                $"Fabric loader: {loaderVersion}");

            WriteLog(
                $"Fabric version ID: {fabricVersionName}");

            string versionsFolder =
                Path.Combine(
                    _gamePath,
                    "versions");

            string fabricFolder =
                Path.Combine(
                    versionsFolder,
                    fabricVersionName);

            Directory.CreateDirectory(
                fabricFolder);

            string fabricJsonPath =
                Path.Combine(
                    fabricFolder,
                    fabricVersionName + ".json");

            // --------------------------------------------------------
            // Download Fabric profile JSON
            // --------------------------------------------------------

            string profileUrl =
                "https://meta.fabricmc.net/v2/versions/loader/" +
                Uri.EscapeDataString(minecraftVersion) +
                "/" +
                Uri.EscapeDataString(loaderVersion) +
                "/profile/json";

            WriteLog(
                $"Fabric profile: {profileUrl}");

            using HttpResponseMessage profileResponse =
                await Http.GetAsync(profileUrl);

            profileResponse.EnsureSuccessStatusCode();

            string profileJson =
                await profileResponse.Content
                    .ReadAsStringAsync();

            if (string.IsNullOrWhiteSpace(profileJson))
            {
                throw new InvalidOperationException(
                    "Fabric returned an empty profile.");
            }

            // --------------------------------------------------------
            // Save Fabric JSON
            // --------------------------------------------------------

            File.WriteAllText(
                fabricJsonPath,
                profileJson,
                Encoding.UTF8);

            WriteLog(
                $"Fabric JSON saved: {fabricJsonPath}");

            // --------------------------------------------------------
            // Download Fabric libraries from the profile
            // --------------------------------------------------------

            await InstallFabricLibrariesAsync(
                profileJson);

            // --------------------------------------------------------
            // Make sure Fabric version JSON contains the correct
            // inheritance information for CmlLib's process builder.
            // --------------------------------------------------------

            TryPatchFabricJson(
                fabricJsonPath,
                minecraftVersion);

            if (!ValidateFabricInstallation(
                    fabricVersionName))
            {
                throw new InvalidOperationException(
                    "Fabric installation could not be verified.");
            }

            WriteLog(
                "===== FABRIC INSTALLATION COMPLETE =====");

            return fabricVersionName;
        }

        private async Task<string> GetFabricLoaderVersionAsync(
            string minecraftVersion)
        {
            string url =
                "https://meta.fabricmc.net/v2/versions/loader/" +
                Uri.EscapeDataString(minecraftVersion);

            using HttpResponseMessage response =
                await Http.GetAsync(url);

            response.EnsureSuccessStatusCode();

            string json =
                await response.Content
                    .ReadAsStringAsync();

            using JsonDocument doc =
                JsonDocument.Parse(json);

            if (doc.RootElement.ValueKind !=
                JsonValueKind.Array)
            {
                throw new InvalidOperationException(
                    "Fabric metadata response was invalid.");
            }

            foreach (JsonElement item in
                     doc.RootElement.EnumerateArray())
            {
                if (!item.TryGetProperty(
                        "loader",
                        out JsonElement loader))
                    continue;

                if (!loader.TryGetProperty(
                        "version",
                        out JsonElement version))
                    continue;

                string? value =
                    version.GetString();

                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }

            throw new InvalidOperationException(
                $"Fabric Loader does not currently have a build for Minecraft {minecraftVersion}.");
        }

        private async Task InstallFabricLibrariesAsync(
            string profileJson)
        {
            using JsonDocument doc =
                JsonDocument.Parse(profileJson);

            if (!doc.RootElement.TryGetProperty(
                    "libraries",
                    out JsonElement libraries))
            {
                WriteLog(
                    "Fabric profile contains no libraries.");

                return;
            }

            if (libraries.ValueKind !=
                JsonValueKind.Array)
                return;

            foreach (JsonElement library in
                     libraries.EnumerateArray())
            {
                if (!library.TryGetProperty(
                        "name",
                        out JsonElement nameElement))
                    continue;

                string? name =
                    nameElement.GetString();

                if (string.IsNullOrWhiteSpace(name))
                    continue;

                string? urlBase = null;

                if (library.TryGetProperty(
                        "url",
                        out JsonElement urlElement))
                {
                    urlBase =
                        urlElement.GetString();
                }

                string? artifactUrl =
                    GetMavenArtifactUrl(
                        name,
                        urlBase);

                if (string.IsNullOrWhiteSpace(
                        artifactUrl))
                {
                    WriteLog(
                        $"Could not determine Fabric library URL: {name}");

                    continue;
                }

                string relativePath =
                    GetMavenArtifactPath(name);

                string destination =
                    Path.Combine(
                        _gamePath,
                        "libraries",
                        relativePath);

                if (File.Exists(destination))
                {
                    FileInfo existing =
                        new FileInfo(destination);

                    if (existing.Length > 0)
                        continue;
                }

                try
                {
                    StatusText.Text =
                        $"Downloading Fabric library: {name}";

                    WriteLog(
                        $"Fabric library: {name}");

                    await DownloadFileAsync(
                        artifactUrl,
                        destination);
                }
                catch (Exception ex)
                {
                    WriteLog(
                        $"Fabric library failed: {name}");

                    WriteLog(
                        ex.Message);
                }
            }
        }

        private static string? GetMavenArtifactUrl(
            string name,
            string? baseUrl)
        {
            string path =
                GetMavenArtifactPath(name);

            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                baseUrl =
                    "https://maven.fabricmc.net/";
            }

            if (!baseUrl.EndsWith("/"))
                baseUrl += "/";

            return baseUrl + path.Replace(
                '\\',
                '/');
        }

        private static string GetMavenArtifactPath(
            string name)
        {
            string[] split =
                name.Split(
                    ':',
                    StringSplitOptions.RemoveEmptyEntries);

            if (split.Length < 3)
                throw new InvalidOperationException(
                    $"Invalid Maven library name: {name}");

            string group =
                split[0];

            string artifact =
                split[1];

            string version =
                split[2];

            string classifier = "";

            if (split.Length >= 4)
            {
                classifier =
                    "-" + split[3];
            }

            string groupPath =
                group.Replace(
                    '.',
                    '/');

            return Path.Combine(
                groupPath,
                artifact,
                version,
                $"{artifact}-{version}{classifier}.jar");
        }

        private static void TryPatchFabricJson(
            string fabricJsonPath,
            string minecraftVersion)
        {
            try
            {
                string json =
                    File.ReadAllText(
                        fabricJsonPath);

                using JsonDocument document =
                    JsonDocument.Parse(json);

                JsonElement root =
                    document.RootElement;

                using MemoryStream stream =
                    new MemoryStream();

                using Utf8JsonWriter writer =
                    new Utf8JsonWriter(
                        stream,
                        new JsonWriterOptions
                        {
                            Indented = true
                        });

                writer.WriteStartObject();

                foreach (JsonProperty property in
                         root.EnumerateObject())
                {
                    if (property.NameEquals(
                            "inheritsFrom"))
                    {
                        writer.WriteString(
                            "inheritsFrom",
                            minecraftVersion);

                        continue;
                    }

                    property.WriteTo(writer);
                }

                if (!root.TryGetProperty(
                        "inheritsFrom",
                        out _))
                {
                    writer.WriteString(
                        "inheritsFrom",
                        minecraftVersion);
                }

                writer.WriteEndObject();

                writer.Flush();

                File.WriteAllText(
                    fabricJsonPath,
                    Encoding.UTF8.GetString(
                        stream.ToArray()),
                    Encoding.UTF8);
            }
            catch
            {
                // The Fabric profile may already be correct.
            }
        }

        private bool ValidateFabricInstallation(
            string fabricVersionName)
        {
            try
            {
                string directory =
                    Path.Combine(
                        _gamePath,
                        "versions",
                        fabricVersionName);

                string jsonPath =
                    Path.Combine(
                        directory,
                        fabricVersionName + ".json");

                if (!Directory.Exists(directory))
                {
                    WriteLog(
                        "Fabric directory missing.");

                    return false;
                }

                if (!File.Exists(jsonPath))
                {
                    WriteLog(
                        "Fabric JSON missing.");

                    return false;
                }

                string json =
                    File.ReadAllText(jsonPath);

                using JsonDocument document =
                    JsonDocument.Parse(json);

                if (document.RootElement.ValueKind !=
                    JsonValueKind.Object)
                    return false;

                WriteLog(
                    "Fabric JSON validated.");

                return true;
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

                    bool installed =
                        await DownloadPerformanceModAsync(
                            slug,
                            name,
                            minecraftVersion);

                    if (installed)
                    {
                        WriteLog(
                            $"Installed: {name}");
                    }
                    else
                    {
                        WriteLog(
                            $"Skipped: {name}");
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
                await response.Content
                    .ReadAsStringAsync();

            using JsonDocument doc =
                JsonDocument.Parse(json);

            JsonElement versions =
                doc.RootElement;

            if (versions.ValueKind !=
                    JsonValueKind.Array ||
                versions.GetArrayLength() == 0)
            {
                return false;
            }

            JsonElement? selectedFile = null;

            foreach (JsonElement version in
                     versions.EnumerateArray())
            {
                if (!version.TryGetProperty(
                        "files",
                        out JsonElement files))
                    continue;

                selectedFile =
                    FindPrimaryJar(files);

                if (selectedFile != null)
                    break;
            }

            if (selectedFile == null)
                return false;

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
                FileInfo info =
                    new FileInfo(destination);

                if (info.Length > 0)
                    return true;

                TryDeleteFile(destination);
            }

            await DownloadFileAsync(
                downloadUrl,
                destination);

            return File.Exists(destination);
        }

        private static JsonElement? FindPrimaryJar(
            JsonElement files)
        {
            if (files.ValueKind !=
                JsonValueKind.Array)
                return null;

            JsonElement? fallback = null;

            foreach (JsonElement file in
                     files.EnumerateArray())
            {
                if (!file.TryGetProperty(
                        "filename",
                        out JsonElement filenameElement))
                    continue;

                string filename =
                    filenameElement.GetString() ?? "";

                if (!filename.EndsWith(
                        ".jar",
                        StringComparison.OrdinalIgnoreCase))
                    continue;

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
                Directory.CreateDirectory(directory);

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
                using HttpResponseMessage response =
                    await Http.GetAsync(
                        url,
                        HttpCompletionOption.ResponseHeadersRead);

                response.EnsureSuccessStatusCode();

                using Stream input =
                    await response.Content
                        .ReadAsStreamAsync();

                using FileStream output =
                    new FileStream(
                        temporary,
                        FileMode.Create,
                        FileAccess.Write,
                        FileShare.None,
                        81920,
                        FileOptions.Asynchronous);

                await input.CopyToAsync(output);

                await output.FlushAsync();
            }
            catch
            {
                TryDeleteFile(temporary);
                throw;
            }

            try
            {
                if (!File.Exists(temporary))
                    throw new IOException(
                        "Temporary download was not created.");

                FileInfo info =
                    new FileInfo(temporary);

                if (info.Length <= 0)
                    throw new IOException(
                        "Downloaded file is empty.");

                if (File.Exists(destination))
                    File.Delete(destination);

                File.Move(
                    temporary,
                    destination);
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
                    File.Delete(path);
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
                return javaExe;
            }

            string systemJava =
                FindSystemJava(requiredMajor);

            if (!string.IsNullOrWhiteSpace(systemJava))
                return systemJava;

            StatusText.Text =
                $"Downloading Java {requiredMajor}...";

            await DownloadAndInstallJavaAsync(
                requiredMajor,
                runtimeFolder);

            if (!File.Exists(javaExe))
            {
                throw new InvalidOperationException(
                    $"Java {requiredMajor} installation failed.");
            }

            if (!IsRequiredJava(
                    javaExe,
                    requiredMajor))
            {
                throw new InvalidOperationException(
                    $"Installed Java is not Java {requiredMajor}.");
            }

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
                    ?? throw new InvalidOperationException();

                string stdout =
                    process.StandardOutput.ReadToEnd();

                string stderr =
                    process.StandardError.ReadToEnd();

                process.WaitForExit();

                string combined =
                    stdout +
                    Environment.NewLine +
                    stderr;

                WriteLog(
                    $"Java check: {javaPath}");

                WriteLog(
                    combined.Trim());

                return combined.Contains(
                    $"version \"{requiredMajor}.",
                    StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        // ============================================================
        // JAVA DOWNLOAD
        // ============================================================

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
                await response.Content
                    .ReadAsStringAsync();

            using JsonDocument doc =
                JsonDocument.Parse(json);

            if (doc.RootElement.ValueKind !=
                    JsonValueKind.Array ||
                doc.RootElement.GetArrayLength() == 0)
            {
                throw new InvalidOperationException(
                    $"No Java {major} runtime was found.");
            }

            JsonElement package =
                doc.RootElement[0]
                    .GetProperty("binary")
                    .GetProperty("package");

            string downloadUrl =
                package.GetProperty("link")
                    .GetString()
                ?? throw new InvalidOperationException(
                    "Java download URL missing.");

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
                StatusText.Text =
                    $"Downloading Java {major}...";

                using HttpResponseMessage javaResponse =
                    await Http.GetAsync(
                        downloadUrl,
                        HttpCompletionOption.ResponseHeadersRead);

                javaResponse.EnsureSuccessStatusCode();

                using Stream input =
                    await javaResponse.Content
                        .ReadAsStreamAsync();

                using FileStream output =
                    new FileStream(
                        tempArchive,
                        FileMode.Create,
                        FileAccess.Write,
                        FileShare.None,
                        81920,
                        FileOptions.Asynchronous);

                await input.CopyToAsync(output);

                await output.FlushAsync();

                // Streams are closed before ZipFile opens archive.

                string extractionDirectory =
                    destination +
                    ".extracting-" +
                    Guid.NewGuid().ToString("N");

                try
                {
                    Directory.CreateDirectory(
                        extractionDirectory);

                    StatusText.Text =
                        $"Installing Java {major}...";

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
                            $"Java {major} was extracted but java.exe was not found.");
                    }

                    if (Directory.Exists(destination))
                    {
                        Directory.Delete(
                            destination,
                            true);
                    }

                    string? parent =
                        Path.GetDirectoryName(destination);

                    if (!string.IsNullOrWhiteSpace(parent))
                        Directory.CreateDirectory(parent);

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
                TryDeleteFile(
                    tempArchive);
            }
        }

        private static string? FindJavaRoot(
            string directory)
        {
            foreach (string subdirectory in
                     Directory.GetDirectories(directory))
            {
                if (File.Exists(
                        Path.Combine(
                            subdirectory,
                            "bin",
                            "java.exe")))
                {
                    return subdirectory;
                }
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
                    Directory.Delete(target, true);

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
                    Directory.Delete(path, true);
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
            string fabricVersion)
        {
            try
            {
                string versions =
                    Path.Combine(
                        _gamePath,
                        "versions");

                string vanilla =
                    Path.Combine(
                        versions,
                        minecraftVersion);

                string fabric =
                    Path.Combine(
                        versions,
                        fabricVersion);

                if (!Directory.Exists(
                        Path.Combine(
                            _gamePath,
                            "libraries")))
                {
                    WriteLog(
                        "ERROR: libraries directory missing.");

                    return false;
                }

                if (!Directory.Exists(vanilla))
                    return false;

                if (!Directory.Exists(fabric))
                    return false;

                string vanillaJson =
                    Path.Combine(
                        vanilla,
                        minecraftVersion + ".json");

                string fabricJson =
                    Path.Combine(
                        fabric,
                        fabricVersion + ".json");

                if (!File.Exists(vanillaJson))
                    return false;

                if (!File.Exists(fabricJson))
                    return false;

                using JsonDocument vanillaDocument =
                    JsonDocument.Parse(
                        File.ReadAllText(vanillaJson));

                using JsonDocument fabricDocument =
                    JsonDocument.Parse(
                        File.ReadAllText(fabricJson));

                return
                    vanillaDocument.RootElement.ValueKind ==
                    JsonValueKind.Object &&
                    fabricDocument.RootElement.ValueKind ==
                    JsonValueKind.Object;
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
                        "Microsoft login is not enabled yet. Select Offline.");
                }

                string username =
                    UsernameInput.Text.Trim();

                if (string.IsNullOrWhiteSpace(username))
                    username = "TopuPlayer";

                _session =
                    MSession.CreateOfflineSession(username);

                SaveUsername(username);

                // ----------------------------------------------------
                // JAVA
                // ----------------------------------------------------

                int javaMajor =
                    GetRequiredJavaMajor(
                        minecraftVersion);

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
                // CMLLIB 4.0.6 PROGRESS API
                // ----------------------------------------------------

                launcher.FileProgressChanged +=
                    Launcher_FileProgressChanged;

                launcher.ByteProgressChanged +=
                    Launcher_ByteProgressChanged;

                // ----------------------------------------------------
                // VANILLA
                // ----------------------------------------------------

                StatusText.Text =
                    $"Installing Minecraft {minecraftVersion}...";

                WriteLog(
                    "Installing vanilla Minecraft.");

                await launcher.InstallAsync(
                    minecraftVersion);

                WriteLog(
                    "Vanilla installation complete.");

                // ----------------------------------------------------
                // FABRIC
                // ----------------------------------------------------

                string fabricVersion =
                    await InstallFabricAsync(
                        minecraftVersion,
                        minecraftPath);

                WriteLog(
                    $"Fabric version: {fabricVersion}");

                // ----------------------------------------------------
                // VALIDATION
                // ----------------------------------------------------

                if (!ValidateMinecraftInstallation(
                        minecraftVersion,
                        fabricVersion))
                {
                    throw new InvalidOperationException(
                        "Minecraft/Fabric installation validation failed.");
                }

                // ----------------------------------------------------
                // PERFORMANCE MODS
                // ----------------------------------------------------

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
                        options);

                if (process == null)
                {
                    throw new InvalidOperationException(
                        "CmlLib returned a null process.");
                }

                _minecraftProcess = process;

                // ----------------------------------------------------
                // OUTPUT
                // ----------------------------------------------------

                process.StartInfo.RedirectStandardOutput =
                    true;

                process.StartInfo.RedirectStandardError =
                    true;

                process.StartInfo.UseShellExecute =
                    false;

                process.StartInfo.CreateNoWindow =
                    true;

                process.OutputDataReceived +=
                    Minecraft_OutputDataReceived;

                process.ErrorDataReceived +=
                    Minecraft_ErrorDataReceived;

                WriteDebugFile(
                    process,
                    javaPath,
                    minecraftVersion,
                    fabricVersion,
                    ram);

                WriteLog(
                    $"Executable: {process.StartInfo.FileName}");

                WriteLog(
                    $"Arguments: {process.StartInfo.Arguments}");

                WriteLog(
                    $"Working directory: {process.StartInfo.WorkingDirectory}");

                // ----------------------------------------------------
                // START
                // ----------------------------------------------------

                StatusText.Text =
                    $"Starting Fabric {minecraftVersion}...";

                if (!process.Start())
                {
                    throw new InvalidOperationException(
                        "Windows failed to start Minecraft.");
                }

                WriteLog(
                    $"Minecraft started. PID={process.Id}");

                try
                {
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                }
                catch (Exception ex)
                {
                    WriteException(
                        "OUTPUT REDIRECTION ERROR",
                        ex);
                }

                StatusText.Text =
                    $"Topu Client running as {username}";

                _ =
                    MonitorMinecraftAsync(process);
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
        // CMLLIB PROGRESS
        // ============================================================

        private void Launcher_FileProgressChanged(
            object? sender,
            InstallerProgressChangedEventArgs args)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    StatusText.Text =
                        $"Downloading {args.Name} " +
                        $"({args.ProgressedTasks}/{args.TotalTasks})";
                });
            }
            catch
            {
            }
        }

        private void Launcher_ByteProgressChanged(
            object? sender,
            ByteProgress args)
        {
            try
            {
                Dispatcher.Invoke(() =>
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
                });
            }
            catch
            {
            }
        }

        // ============================================================
        // MINECRAFT OUTPUT
        // ============================================================

        private void Minecraft_OutputDataReceived(
            object sender,
            DataReceivedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                AppendRawGameOutput(
                    "[MC]",
                    e.Data);
            }
        }

        private void Minecraft_ErrorDataReceived(
            object sender,
            DataReceivedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                AppendRawGameOutput(
                    "[MC-ERR]",
                    e.Data);
            }
        }

        // ============================================================
        // MONITOR
        // ============================================================

        private async Task MonitorMinecraftAsync(
            Process process)
        {
            try
            {
                await Task.Run(() =>
                {
                    try
                    {
                        process.WaitForExit();
                    }
                    catch
                    {
                    }
                });

                int exitCode = 0;

                try
                {
                    exitCode =
                        process.ExitCode;
                }
                catch
                {
                }

                WriteLog(
                    $"===== MINECRAFT EXITED: {exitCode} =====");

                await Dispatcher.InvokeAsync(() =>
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
        // DEBUG
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
        // UTILITY
        // ============================================================

        private static string SanitizeFileName(
            string filename)
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
