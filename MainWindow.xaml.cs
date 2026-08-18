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
                RamLabel.Text =
                    $"{(int)RamSlider.Value}GB";
            }

            WriteLog("Topu Client initialized.");
        }

        private static HttpClient CreateHttpClient()
        {
            HttpClient client =
                new HttpClient(
                    new HttpClientHandler
                    {
                        AllowAutoRedirect = true
                    });

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
                Directory.CreateDirectory(
                    _gamePath);

                lock (_logLock)
                {
                    File.AppendAllText(
                        _logPath,
                        $"[{DateTime.Now:HH:mm:ss}] {message}" +
                        Environment.NewLine);
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
                Directory.CreateDirectory(
                    _gamePath);

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

        private void AppendGameLog(
            string message)
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
                    File.AppendAllText(
                        _logPath,
                        $"[{DateTime.Now:HH:mm:ss}] " +
                        $"{prefix} {text}" +
                        Environment.NewLine);
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
                    File.ReadAllText(
                        _configPath).Trim();

                if (string.IsNullOrWhiteSpace(username))
                    return;

                UsernameInput.Text =
                    username;

                _session =
                    MSession.CreateOfflineSession(
                        username);

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

        private void SaveUsername(
            string username)
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
            if (e.ChangedButton !=
                MouseButton.Left)
            {
                return;
            }

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

            TabLaunchBtn.Foreground =
                inactive;

            TabProfilesBtn.Foreground =
                inactive;

            TabAccountsBtn.Foreground =
                inactive;

            TabLaunchBtn.BorderThickness =
                new Thickness(0);

            TabProfilesBtn.BorderThickness =
                new Thickness(0);

            TabAccountsBtn.BorderThickness =
                new Thickness(0);

            button.Foreground =
                active;

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

                JsonElement hit =
                    hits[0];

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

            JsonElement selected =
                versions[0];

            JsonElement files =
                selected.GetProperty("files");

            JsonElement? selectedFile =
                FindPrimaryJar(files);

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
        // FABRIC INSTALLATION
        // ============================================================

        private async Task<string> InstallFabricAsync(
            string minecraftVersion,
            MinecraftPath minecraftPath,
            MinecraftLauncher launcher)
        {
            StatusText.Text =
                $"Preparing Fabric {minecraftVersion}...";

            WriteLog(
                $"Preparing Fabric for Minecraft {minecraftVersion}.");

            /*
             * CmlLib's Fabric installer creates the Fabric version
             * profile and its required loader libraries.
             *
             * The base Minecraft files must also exist because the
             * Fabric profile inherits from the Minecraft version.
             */

            WriteLog(
                "Checking base Minecraft installation.");

            await launcher.InstallAsync(
                minecraftVersion,
                CancellationToken.None);

            WriteLog(
                "Base Minecraft files are installed.");

            StatusText.Text =
                $"Installing Fabric for {minecraftVersion}...";

            FabricInstaller fabricInstaller =
                new FabricInstaller(Http);

            string fabricVersionName =
                await fabricInstaller.Install(
                    minecraftVersion,
                    minecraftPath);

            if (string.IsNullOrWhiteSpace(
                    fabricVersionName))
            {
                throw new InvalidOperationException(
                    "Fabric installer returned an empty version name.");
            }

            WriteLog(
                $"Fabric installer returned: {fabricVersionName}");

            if (IsFabricInstallationUsable(
                    fabricVersionName))
            {
                WriteLog(
                    "Fabric installation verified.");

                return fabricVersionName;
            }

            /*
             * Sometimes an interrupted installation leaves a partial
             * Fabric profile behind. Remove ONLY that Fabric profile
             * and retry once.
             */

            WriteLog(
                "Fabric installation appears incomplete.");

            string fabricDirectory =
                Path.Combine(
                    _gamePath,
                    "versions",
                    fabricVersionName);

            try
            {
                if (Directory.Exists(
                        fabricDirectory))
                {
                    Directory.Delete(
                        fabricDirectory,
                        true);

                    WriteLog(
                        $"Removed incomplete Fabric profile: {fabricDirectory}");
                }
            }
            catch (Exception ex)
            {
                WriteException(
                    "FABRIC PROFILE CLEANUP ERROR",
                    ex);
            }

            WriteLog(
                "Retrying Fabric installation.");

            fabricVersionName =
                await fabricInstaller.Install(
                    minecraftVersion,
                    minecraftPath);

            if (string.IsNullOrWhiteSpace(
                    fabricVersionName))
            {
                throw new InvalidOperationException(
                    "Fabric installer returned an empty version name on retry.");
            }

            WriteLog(
                $"Fabric retry returned: {fabricVersionName}");

            if (!IsFabricInstallationUsable(
                    fabricVersionName))
            {
                throw new InvalidOperationException(
                    "Fabric installation completed but the required files were not created.");
            }

            WriteLog(
                $"Fabric installed and verified: {fabricVersionName}");

            return fabricVersionName;
        }

        private bool IsFabricInstallationUsable(
            string fabricVersionName)
        {
            try
            {
                string fabricDirectory =
                    Path.Combine(
                        _gamePath,
                        "versions",
                        fabricVersionName);

                if (!Directory.Exists(
                        fabricDirectory))
                {
                    WriteLog(
                        $"Fabric directory missing: {fabricDirectory}");

                    return false;
                }

                string fabricJson =
                    Path.Combine(
                        fabricDirectory,
                        fabricVersionName + ".json");

                if (!File.Exists(
                        fabricJson))
                {
                    WriteLog(
                        $"Fabric JSON missing: {fabricJson}");

                    return false;
                }

                string json =
                    File.ReadAllText(
                        fabricJson);

                if (string.IsNullOrWhiteSpace(json))
                {
                    WriteLog(
                        "Fabric JSON is empty.");

                    return false;
                }

                using JsonDocument document =
                    JsonDocument.Parse(json);

                JsonElement root =
                    document.RootElement;

                if (!root.TryGetProperty(
                        "mainClass",
                        out JsonElement mainClass))
                {
                    WriteLog(
                        "Fabric JSON does not contain mainClass.");

                    return false;
                }

                if (string.IsNullOrWhiteSpace(
                        mainClass.GetString()))
                {
                    WriteLog(
                        "Fabric JSON mainClass is empty.");

                    return false;
                }

                string loaderJar =
                    FindFabricLoaderJar(
                        _gamePath);

                if (string.IsNullOrWhiteSpace(
                        loaderJar))
                {
                    WriteLog(
                        "Fabric loader JAR was not found.");

                    return false;
                }

                WriteLog(
                    $"Fabric loader JAR: {loaderJar}");

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

        private string FindFabricLoaderJar(
            string root)
        {
            try
            {
                string librariesRoot =
                    Path.Combine(
                        root,
                        "libraries");

                if (!Directory.Exists(
                        librariesRoot))
                {
                    return "";
                }

                foreach (string file in
                         Directory.EnumerateFiles(
                             librariesRoot,
                             "fabric-loader-*.jar",
                             SearchOption.AllDirectories))
                {
                    if (File.Exists(file))
                        return file;
                }
            }
            catch (Exception ex)
            {
                WriteException(
                    "FABRIC LOADER SEARCH ERROR",
                    ex);
            }

            return "";
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

            foreach ((string slug, string name)
                     in PerformanceMods)
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
                            $"Installed optional mod: {name}");
                    }
                    else
                    {
                        WriteLog(
                            $"Skipped optional mod: {name}");
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

            if (versions.ValueKind !=
                JsonValueKind.Array ||
                versions.GetArrayLength() == 0)
            {
                WriteLog(
                    $"No compatible {name} version exists for {minecraftVersion}.");

                return false;
            }

            JsonElement? selectedFile =
                null;

            foreach (JsonElement version
                     in versions.EnumerateArray())
            {
                if (!version.TryGetProperty(
                        "files",
                        out JsonElement files))
                {
                    continue;
                }

                JsonElement? primary =
                    FindPrimaryJar(files);

                if (primary != null)
                {
                    selectedFile =
                        primary;

                    break;
                }
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
                    return true;
                }

                try
                {
                    File.Delete(
                        destination);
                }
                catch
                {
                }
            }

            DeleteStaleDownloadFiles(
                destination);

            await DownloadFileAsync(
                downloadUrl,
                destination);

            if (!File.Exists(
                    destination))
            {
                throw new IOException(
                    $"Mod file was not created: {destination}");
            }

            FileInfo info =
                new FileInfo(destination);

            if (info.Length <= 0)
            {
                try
                {
                    File.Delete(
                        destination);
                }
                catch
                {
                }

                throw new IOException(
                    $"Downloaded mod is empty: {destination}");
            }

            return true;
        }

        private static JsonElement? FindPrimaryJar(
            JsonElement files)
        {
            JsonElement? fallback =
                null;

            foreach (JsonElement file
                     in files.EnumerateArray())
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

        private static void DeleteStaleDownloadFiles(
            string destination)
        {
            try
            {
                string? directory =
                    Path.GetDirectoryName(
                        destination);

                if (string.IsNullOrWhiteSpace(
                        directory) ||
                    !Directory.Exists(
                        directory))
                {
                    return;
                }

                string filename =
                    Path.GetFileName(
                        destination);

                foreach (string file in
                         Directory.GetFiles(
                             directory,
                             filename + ".*.download"))
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }
        }

        // ============================================================
        // DOWNLOAD
        // ============================================================

        private async Task DownloadFileAsync(
            string url,
            string destination)
        {
            string? directory =
                Path.GetDirectoryName(
                    destination);

            if (!string.IsNullOrWhiteSpace(
                    directory))
            {
                Directory.CreateDirectory(
                    directory);
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

                await using Stream input =
                    await response.Content.ReadAsStreamAsync();

                await using FileStream output =
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

                /*
                 * Both input and output streams are disposed before
                 * we touch the temporary file again.
                 *
                 * This is important for Windows because a file can
                 * remain locked while the download stream is alive.
                 */

                if (!File.Exists(
                        temporary))
                {
                    throw new IOException(
                        "Temporary download file was not created.");
                }

                FileInfo tempInfo =
                    new FileInfo(
                        temporary);

                if (tempInfo.Length <= 0)
                {
                    throw new IOException(
                        "Downloaded file is empty.");
                }

                if (File.Exists(
                        destination))
                {
                    File.Delete(
                        destination);
                }

                File.Move(
                    temporary,
                    destination);

                if (!File.Exists(
                        destination))
                {
                    throw new IOException(
                        "Final downloaded file was not created.");
                }

                WriteLog(
                    $"Download complete: {destination}");
            }
            catch
            {
                try
                {
                    if (File.Exists(
                            temporary))
                    {
                        File.Delete(
                            temporary);
                    }
                }
                catch
                {
                }

                throw;
            }
            finally
            {
                try
                {
                    if (File.Exists(
                            temporary))
                    {
                        File.Delete(
                            temporary);
                    }
                }
                catch
                {
                }
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

            if (File.Exists(
                    javaExe) &&
                IsRequiredJava(
                    javaExe,
                    requiredMajor))
            {
                WriteLog(
                    $"Using existing Java {requiredMajor}: {javaExe}");

                return javaExe;
            }

            string systemJava =
                FindSystemJava(
                    requiredMajor);

            if (!string.IsNullOrWhiteSpace(
                    systemJava))
            {
                WriteLog(
                    $"Using system Java {requiredMajor}: {systemJava}");

                return systemJava;
            }

            WriteLog(
                $"Java {requiredMajor} not found.");

            StatusText.Text =
                $"Downloading Java {requiredMajor}...";

            /*
             * Remove an incomplete previous runtime.
             */
            try
            {
                if (Directory.Exists(
                        runtimeFolder))
                {
                    Directory.Delete(
                        runtimeFolder,
                        true);

                    WriteLog(
                        $"Removed incomplete Java runtime: {runtimeFolder}");
                }
            }
            catch (Exception ex)
            {
                WriteException(
                    "JAVA OLD RUNTIME CLEANUP ERROR",
                    ex);
            }

            await DownloadAndInstallJavaAsync(
                requiredMajor,
                runtimeFolder);

            if (!File.Exists(
                    javaExe))
            {
                throw new InvalidOperationException(
                    $"Java {requiredMajor} installation finished but java.exe was not found.");
            }

            if (!IsRequiredJava(
                    javaExe,
                    requiredMajor))
            {
                throw new InvalidOperationException(
                    $"Installed Java is not Java {requiredMajor}.");
            }

            WriteLog(
                $"Java {requiredMajor} is ready.");

            return javaExe;
        }

        private string FindSystemJava(
            int requiredMajor)
        {
            string javaHome =
                Environment.GetEnvironmentVariable(
                    "JAVA_HOME") ?? "";

            if (!string.IsNullOrWhiteSpace(
                    javaHome))
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

                if (!File.Exists(
                        candidate))
                {
                    continue;
                }

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

            WriteLog(
                $"Java API: {apiUrl}");

            using HttpResponseMessage response =
                await Http.GetAsync(
                    apiUrl);

            response.EnsureSuccessStatusCode();

            string json =
                await response.Content.ReadAsStringAsync();

            using JsonDocument doc =
                JsonDocument.Parse(json);

            JsonElement assets =
                doc.RootElement;

            if (assets.ValueKind !=
                JsonValueKind.Array ||
                assets.GetArrayLength() == 0)
            {
                throw new InvalidOperationException(
                    $"No Windows x64 Temurin JRE {major} was found.");
            }

            JsonElement asset =
                assets[0];

            JsonElement package =
                asset.GetProperty("binary")
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
                    SanitizeFileName(
                        archiveName));

            try
            {
                WriteLog(
                    $"Java download URL: {downloadUrl}");

                WriteLog(
                    $"Java temporary archive: {tempArchive}");

                StatusText.Text =
                    $"Downloading Java {major}...";

                /*
                 * IMPORTANT:
                 *
                 * The response and BOTH streams are completely
                 * disposed before ZipFile touches tempArchive.
                 *
                 * This directly fixes your:
                 *
                 * "file ... is being used by another process"
                 *
                 * error.
                 */

                using (HttpResponseMessage javaResponse =
                       await Http.GetAsync(
                           downloadUrl,
                           HttpCompletionOption.ResponseHeadersRead))
                {
                    javaResponse.EnsureSuccessStatusCode();

                    await using Stream input =
                        await javaResponse.Content.ReadAsStreamAsync();

                    await using FileStream output =
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

                /*
                 * At this exact point the HTTP response,
                 * HTTP stream and FileStream are all disposed.
                 */

                if (!File.Exists(
                        tempArchive))
                {
                    throw new IOException(
                        "Java archive was not downloaded.");
                }

                FileInfo archiveInfo =
                    new FileInfo(
                        tempArchive);

                if (archiveInfo.Length <= 0)
                {
                    throw new IOException(
                        "Java archive is empty.");
                }

                WriteLog(
                    $"Java archive downloaded: {archiveInfo.Length} bytes");

                /*
                 * Extract to a separate temporary directory first.
                 *
                 * This prevents a half-installed Java runtime if
                 * extraction fails.
                 */

                string extractionFolder =
                    Path.Combine(
                        Path.GetTempPath(),
                        "topu-java-extract-" +
                        Guid.NewGuid().ToString("N"));

                try
                {
                    Directory.CreateDirectory(
                        extractionFolder);

                    StatusText.Text =
                        $"Installing Java {major}...";

                    ZipFile.ExtractToDirectory(
                        tempArchive,
                        extractionFolder,
                        true);

                    string? javaRoot =
                        FindJavaRoot(
                            extractionFolder);

                    if (javaRoot == null)
                    {
                        throw new InvalidOperationException(
                            $"Java {major} archive was extracted, but its bin\\java.exe could not be found.");
                    }

                    /*
                     * The extraction is complete before we touch
                     * the final runtime folder.
                     */

                    if (Directory.Exists(
                            destination))
                    {
                        Directory.Delete(
                            destination,
                            true);
                    }

                    Directory.CreateDirectory(
                        destination);

                    MoveDirectoryContents(
                        javaRoot,
                        destination);

                    string javaExe =
                        Path.Combine(
                            destination,
                            "bin",
                            "java.exe");

                    if (!File.Exists(
                            javaExe))
                    {
                        throw new InvalidOperationException(
                            $"Java {major} installation completed but java.exe was not found.");
                    }

                    WriteLog(
                        $"Java {major} installed successfully: {javaExe}");
                }
                finally
                {
                    try
                    {
                        if (Directory.Exists(
                                extractionFolder))
                        {
                            Directory.Delete(
                                extractionFolder,
                                true);
                        }
                    }
                    catch
                    {
                    }
                }
            }
            finally
            {
                /*
                 * The archive is no longer needed.
                 */

                try
                {
                    if (File.Exists(
                            tempArchive))
                    {
                        File.Delete(
                            tempArchive);
                    }
                }
                catch (Exception ex)
                {
                    WriteException(
                        "JAVA TEMP ARCHIVE CLEANUP ERROR",
                        ex);
                }
            }
        }

        private static string? FindJavaRoot(
            string directory)
        {
            /*
             * Some Temurin archives contain:
             *
             * jdk-21.x.x+xx/
             *     bin/java.exe
             *
             * Others may put bin directly in the extraction folder.
             */

            string directJava =
                Path.Combine(
                    directory,
                    "bin",
                    "java.exe");

            if (File.Exists(
                    directJava))
            {
                return directory;
            }

            foreach (string subDirectory in
                     Directory.GetDirectories(
                         directory))
            {
                string javaExe =
                    Path.Combine(
                        subDirectory,
                        "bin",
                        "java.exe");

                if (File.Exists(
                        javaExe))
                {
                    return subDirectory;
                }
            }

            return null;
        }

        private static void MoveDirectoryContents(
            string source,
            string destination)
        {
            foreach (string directory in
                     Directory.GetDirectories(
                         source))
            {
                string target =
                    Path.Combine(
                        destination,
                        Path.GetFileName(
                            directory));

                if (Directory.Exists(
                        target))
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
                     Directory.GetFiles(
                         source))
            {
                string target =
                    Path.Combine(
                        destination,
                        Path.GetFileName(
                            file));

                File.Move(
                    file,
                    target,
                    true);
            }
        }

        // ============================================================
        // INSTALLATION VALIDATION
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

                string root =
                    _gamePath;

                string assets =
                    Path.Combine(
                        root,
                        "assets");

                string libraries =
                    Path.Combine(
                        root,
                        "libraries");

                string versions =
                    Path.Combine(
                        root,
                        "versions");

                string vanillaDirectory =
                    Path.Combine(
                        versions,
                        minecraftVersion);

                string fabricDirectory =
                    Path.Combine(
                        versions,
                        fabricVersion);

                bool assetsExists =
                    Directory.Exists(
                        assets);

                bool librariesExists =
                    Directory.Exists(
                        libraries);

                bool versionsExists =
                    Directory.Exists(
                        versions);

                bool vanillaExists =
                    Directory.Exists(
                        vanillaDirectory);

                bool fabricExists =
                    Directory.Exists(
                        fabricDirectory);

                WriteLog(
                    $"Assets: {assetsExists}");

                WriteLog(
                    $"Libraries: {librariesExists}");

                WriteLog(
                    $"Versions: {versionsExists}");

                WriteLog(
                    $"Vanilla version directory: {vanillaExists}");

                WriteLog(
                    $"Fabric version directory: {fabricExists}");

                if (!librariesExists)
                {
                    WriteLog(
                        "ERROR: libraries directory missing.");

                    return false;
                }

                if (!versionsExists)
                {
                    WriteLog(
                        "ERROR: versions directory missing.");

                    return false;
                }

                if (!vanillaExists)
                {
                    WriteLog(
                        "ERROR: vanilla Minecraft version directory missing.");

                    return false;
                }

                if (!fabricExists)
                {
                    WriteLog(
                        "ERROR: Fabric version directory missing.");

                    return false;
                }

                string vanillaJson =
                    Path.Combine(
                        vanillaDirectory,
                        minecraftVersion + ".json");

                if (!File.Exists(
                        vanillaJson))
                {
                    WriteLog(
                        $"ERROR: vanilla JSON missing: {vanillaJson}");

                    return false;
                }

                string fabricJson =
                    Path.Combine(
                        fabricDirectory,
                        fabricVersion + ".json");

                if (!File.Exists(
                        fabricJson))
                {
                    WriteLog(
                        $"ERROR: Fabric JSON missing: {fabricJson}");

                    return false;
                }

                string loaderJar =
                    FindFabricLoaderJar(
                        root);

                if (string.IsNullOrWhiteSpace(
                        loaderJar))
                {
                    WriteLog(
                        "ERROR: Fabric loader JAR missing.");

                    return false;
                }

                WriteLog(
                    $"Fabric loader JAR: {loaderJar}");

                WriteLog(
                    "===== INSTALLATION VALIDATION PASSED =====");

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

            LaunchBtn.IsEnabled =
                false;

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

                if (string.IsNullOrWhiteSpace(
                        username))
                {
                    username =
                        "TopuPlayer";
                }

                _session =
                    MSession.CreateOfflineSession(
                        username);

                SaveUsername(
                    username);

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
                // PROGRESS
                // ----------------------------------------------------

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

                /*
                 * Subscribe to CmlLib's documented progress events too.
                 * This gives better progress reporting for installers that
                 * don't use the callback overload.
                 */

                launcher.FileProgressChanged +=
                    (sender2, args) =>
                    {
                        try
                        {
                            Dispatcher.Invoke(
                                () =>
                                {
                                    StatusText.Text =
                                        $"Downloading {args.Name} " +
                                        $"({args.ProgressedTasks}/{args.TotalTasks})";
                                });
                        }
                        catch
                        {
                        }
                    };

                launcher.ByteProgressChanged +=
                    (sender2, args) =>
                    {
                        try
                        {
                            if (args.TotalBytes > 0)
                            {
                                double percent =
                                    args.ProgressedBytes *
                                    100.0 /
                                    args.TotalBytes;

                                Dispatcher.Invoke(
                                    () =>
                                    {
                                        StatusText.Text =
                                            $"Downloading: {percent:0}%";
                                    });
                            }
                        }
                        catch
                        {
                        }
                    };

                // ----------------------------------------------------
                // INSTALL MINECRAFT + FABRIC
                // ----------------------------------------------------

                StatusText.Text =
                    $"Installing Fabric {minecraftVersion}...";

                WriteLog(
                    "===== FABRIC INSTALLATION START =====");

                string fabricVersion =
                    await InstallFabricAsync(
                        minecraftVersion,
                        minecraftPath,
                        launcher);

                WriteLog(
                    $"Fabric version: {fabricVersion}");

                WriteLog(
                    "===== FABRIC INSTALLATION FINISHED =====");

                // ----------------------------------------------------
                // VALIDATE
                // ----------------------------------------------------

                StatusText.Text =
                    "Validating installation...";

                bool valid =
                    ValidateMinecraftInstallation(
                        minecraftVersion,
                        minecraftPath,
                        fabricVersion);

                if (!valid)
                {
                    throw new InvalidOperationException(
                        "Minecraft/Fabric installation validation failed. Check topu-minecraft.log.");
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
                // LAUNCH OPTIONS
                // ----------------------------------------------------

                MLaunchOption options =
                    new MLaunchOption
                    {
                        Session = _session,

                        MaximumRamMb =
                            ram,

                        MinimumRamMb =
                            Math.Min(
                                1024,
                                ram),

                        JavaPath =
                            javaPath,

                        GameLauncherName =
                            "Topu Client",

                        GameLauncherVersion =
                            "1.0.0"
                    };

                // ----------------------------------------------------
                // BUILD FABRIC PROCESS
                // ----------------------------------------------------

                StatusText.Text =
                    "Building Fabric process...";

                WriteLog(
                    $"Building Fabric process: {fabricVersion}");

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

                _minecraftProcess =
                    process;

                // ----------------------------------------------------
                // PROCESS OUTPUT
                // ----------------------------------------------------

                try
                {
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
                }
                catch (Exception ex)
                {
                    WriteException(
                        "PROCESS OUTPUT SETUP ERROR",
                        ex);

                    throw;
                }

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
                    "\n\nFull log:\n" +
                    _logPath,
                    "Topu Client",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                LaunchBtn.IsEnabled =
                    true;
            }
        }

        // ============================================================
        // MINECRAFT OUTPUT
        // ============================================================

        private void Minecraft_OutputDataReceived(
            object sender,
            DataReceivedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(
                    e.Data))
            {
                return;
            }

            AppendRawGameOutput(
                "[MC]",
                e.Data);
        }

        private void Minecraft_ErrorDataReceived(
            object sender,
            DataReceivedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(
                    e.Data))
            {
                return;
            }

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

                /*
                 * Give asynchronous stdout/stderr handlers time to
                 * write the final crash messages.
                 */

                await Task.Delay(
                    500);

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
                        "Check the [MC] and [MC-ERR] lines above.");
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

                _minecraftProcess =
                    null;
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
