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
        }

        // ============================================================
        // HTTP
        // ============================================================

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
            /*
             * Minecraft 1.20.5+ / 1.21.x:
             * Java 21
             *
             * Newer 26.x versions:
             * Java 25
             */
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
        // FABRIC
        // ============================================================

        private async Task<string> InstallFabricAsync(
            string minecraftVersion,
            MinecraftPath minecraftPath)
        {
            StatusText.Text =
                $"Installing Fabric for Minecraft {minecraftVersion}...";

            WriteLog(
                $"Installing Fabric for Minecraft {minecraftVersion}.");

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

            if (!IsFabricInstallationUsable(
                    fabricVersionName))
            {
                WriteLog(
                    "Fabric installation is incomplete.");

                WriteLog(
                    "Retrying Fabric installation once.");

                string brokenDirectory =
                    Path.Combine(
                        _gamePath,
                        "versions",
                        fabricVersionName);

                try
                {
                    if (Directory.Exists(
                            brokenDirectory))
                    {
                        Directory.Delete(
                            brokenDirectory,
                            true);

                        WriteLog(
                            $"Removed incomplete Fabric directory: {brokenDirectory}");
                    }
                }
                catch (Exception cleanupEx)
                {
                    WriteException(
                        "FABRIC CLEANUP ERROR",
                        cleanupEx);
                }

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
                    $"Fabric installer retry returned: {fabricVersionName}");

                if (!IsFabricInstallationUsable(
                        fabricVersionName))
                {
                    throw new InvalidOperationException(
                        "Fabric installation completed but required Fabric files were not created.");
                }
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

                string fabricJson =
                    Path.Combine(
                        fabricDirectory,
                        fabricVersionName + ".json");

                if (!Directory.Exists(
                        fabricDirectory))
                {
                    WriteLog(
                        $"Fabric directory missing: {fabricDirectory}");

                    return false;
                }

                if (!File.Exists(fabricJson))
                {
                    WriteLog(
                        $"Fabric JSON missing: {fabricJson}");

                    return false;
                }

                string loaderJar =
                    FindFabricLoaderJar(
                        _gamePath,
                        fabricVersionName);

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
                    "FABRIC INSTALLATION CHECK ERROR",
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

                    // Optional mods NEVER stop Minecraft.
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
                    $"No compatible {name} build for {minecraftVersion}.");

                return false;
            }

            JsonElement? selectedFile =
                null;

            foreach (JsonElement version in
                     versions.EnumerateArray())
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
                return true;
            }

            DeleteStaleDownloadFiles(
                destination);

            await DownloadFileAsync(
                downloadUrl,
                destination);

            if (!File.Exists(destination))
            {
                throw new IOException(
                    $"Mod file was not created: {destination}");
            }

            FileInfo info =
                new FileInfo(destination);

            if (info.Length == 0)
            {
                try
                {
                    File.Delete(destination);
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

        private static void DeleteStaleDownloadFiles(
            string destination)
        {
            try
            {
                string? directory =
                    Path.GetDirectoryName(destination);

                if (string.IsNullOrWhiteSpace(
                        directory) ||
                    !Directory.Exists(directory))
                {
                    return;
                }

                string filename =
                    Path.GetFileName(destination);

                foreach (string file in
                         Directory.GetFiles(
                             directory,
                             "." + filename + ".*.part"))
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
        // SAFE FILE DOWNLOAD
        // ============================================================

        private async Task DownloadFileAsync(
            string url,
            string destination)
        {
            string? directory =
                Path.GetDirectoryName(destination);

            if (!string.IsNullOrWhiteSpace(directory))
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
                    ".part");

            try
            {
                WriteLog(
                    $"Downloading: {url}");

                /*
                 * IMPORTANT:
                 *
                 * This method uses explicit using blocks rather than
                 * keeping the stream alive until later.
                 *
                 * By the time File.Move() happens, every HTTP/file
                 * stream has already been disposed.
                 */

                using (HttpResponseMessage response =
                       await Http.GetAsync(
                           url,
                           HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();

                    using (Stream input =
                           await response.Content.ReadAsStreamAsync())
                    {
                        using (FileStream output =
                               new FileStream(
                                   temporary,
                                   FileMode.Create,
                                   FileAccess.Write,
                                   FileShare.None,
                                   81920,
                                   FileOptions.SequentialScan))
                        {
                            await input.CopyToAsync(
                                output);

                            output.Flush(true);
                        }
                    }
                }

                /*
                 * ALL streams are closed here.
                 */

                if (!File.Exists(temporary))
                {
                    throw new IOException(
                        "Temporary download file was not created.");
                }

                FileInfo tempInfo =
                    new FileInfo(temporary);

                if (tempInfo.Length == 0)
                {
                    throw new IOException(
                        "Downloaded file is empty.");
                }

                /*
                 * Replace destination only after the download is
                 * complete.
                 */

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
                try
                {
                    if (File.Exists(temporary))
                    {
                        File.Delete(temporary);
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
                    if (File.Exists(temporary))
                    {
                        File.Delete(temporary);
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

            /*
             * First use Topu's own runtime.
             */
            if (File.Exists(javaExe) &&
                IsRequiredJava(
                    javaExe,
                    requiredMajor))
            {
                WriteLog(
                    $"Using Topu Java {requiredMajor}: {javaExe}");

                return javaExe;
            }

            /*
             * Then check JAVA_HOME/PATH.
             */
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
                    $"Installed Java is not Java {requiredMajor}.");
            }

            WriteLog(
                $"Java {requiredMajor} ready: {javaExe}");

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

                /*
                 * Java normally prints version to stderr.
                 */
                return combined.Contains(
                    $"version \"{requiredMajor}.",
                    StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                WriteLog(
                    $"Java validation failed for {javaPath}: {ex.Message}");

                return false;
            }
        }

        // ============================================================
        // JAVA DOWNLOAD / INSTALL
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

            WriteLog(
                $"Java API: {apiUrl}");

            using HttpResponseMessage apiResponse =
                await Http.GetAsync(apiUrl);

            apiResponse.EnsureSuccessStatusCode();

            string json =
                await apiResponse.Content.ReadAsStringAsync();

            using JsonDocument document =
                JsonDocument.Parse(json);

            JsonElement assets =
                document.RootElement;

            if (assets.ValueKind !=
                JsonValueKind.Array ||
                assets.GetArrayLength() == 0)
            {
                throw new InvalidOperationException(
                    $"No Windows x64 Java {major} JRE was found.");
            }

            JsonElement asset =
                assets[0];

            JsonElement binary =
                asset.GetProperty("binary");

            JsonElement package =
                binary.GetProperty("package");

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

            /*
             * Use a completely unique archive filename.
             */
            string tempArchive =
                Path.Combine(
                    Path.GetTempPath(),
                    "topu-java-" +
                    Guid.NewGuid().ToString("N") +
                    "-" +
                    SanitizeFileName(
                        archiveName));

            string tempPart =
                tempArchive +
                "." +
                Guid.NewGuid().ToString("N") +
                ".part";

            try
            {
                WriteLog(
                    $"Java download URL: {downloadUrl}");

                WriteLog(
                    $"Java temporary download: {tempPart}");

                StatusText.Text =
                    $"Downloading Java {major}...";

                /*
                 * ====================================================
                 * DOWNLOAD
                 * ====================================================
                 *
                 * Explicit synchronous disposal is intentional here.
                 *
                 * We DO NOT call ZipFile.ExtractToDirectory until
                 * every HTTP and FileStream has been disposed.
                 */

                using (HttpResponseMessage response =
                       await Http.GetAsync(
                           downloadUrl,
                           HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();

                    using (Stream input =
                           await response.Content.ReadAsStreamAsync())
                    {
                        using (FileStream output =
                               new FileStream(
                                   tempPart,
                                   FileMode.Create,
                                   FileAccess.Write,
                                   FileShare.None,
                                   1024 * 128,
                                   FileOptions.SequentialScan))
                        {
                            await input.CopyToAsync(
                                output);

                            /*
                             * Force all bytes onto disk.
                             */
                            output.Flush(true);
                        }
                    }
                }

                /*
                 * At this exact point:
                 *
                 * HttpResponseMessage = disposed
                 * HTTP Stream         = disposed
                 * FileStream           = disposed
                 *
                 * Windows no longer has our download stream open.
                 */

                if (!File.Exists(tempPart))
                {
                    throw new IOException(
                        "Java download did not create the temporary file.");
                }

                FileInfo partInfo =
                    new FileInfo(tempPart);

                if (partInfo.Length == 0)
                {
                    throw new IOException(
                        "Downloaded Java archive is empty.");
                }

                /*
                 * Rename the completed .part file to .zip.
                 *
                 * No stream is open at this point.
                 */
                File.Move(
                    tempPart,
                    tempArchive,
                    true);

                WriteLog(
                    $"Java archive downloaded: {tempArchive}");

                /*
                 * ====================================================
                 * EXTRACT
                 * ====================================================
                 */

                StatusText.Text =
                    $"Installing Java {major}...";

                if (Directory.Exists(destination))
                {
                    WriteLog(
                        $"Removing old Java directory: {destination}");

                    Directory.Delete(
                        destination,
                        true);
                }

                Directory.CreateDirectory(
                    destination);

                /*
                 * EXTRA SAFETY:
                 *
                 * Open the archive ourselves in Read mode and close it
                 * completely after extraction.
                 */
                using (FileStream archiveStream =
                       new FileStream(
                           tempArchive,
                           FileMode.Open,
                           FileAccess.Read,
                           FileShare.Read))
                {
                    using (ZipArchive archive =
                           new ZipArchive(
                               archiveStream,
                               ZipArchiveMode.Read,
                               false))
                    {
                        archive.ExtractToDirectory(
                            destination,
                            true);
                    }
                }

                /*
                 * archiveStream is now CLOSED.
                 */

                string javaExe =
                    Path.Combine(
                        destination,
                        "bin",
                        "java.exe");

                /*
                 * Adoptium archives normally contain:
                 *
                 * java/jdk-21.../bin/java.exe
                 *
                 * or:
                 *
                 * jdk-21.../bin/java.exe
                 *
                 * Find the actual root if java.exe isn't directly
                 * under destination/bin.
                 */
                if (!File.Exists(javaExe))
                {
                    string? javaRoot =
                        FindJavaRoot(
                            destination);

                    if (javaRoot != null)
                    {
                        WriteLog(
                            $"Java archive root detected: {javaRoot}");

                        MoveJavaRootContents(
                            javaRoot,
                            destination);
                    }
                }

                javaExe =
                    Path.Combine(
                        destination,
                        "bin",
                        "java.exe");

                if (!File.Exists(javaExe))
                {
                    throw new InvalidOperationException(
                        $"Java {major} was extracted, but java.exe could not be found.");
                }

                if (!IsRequiredJava(
                        javaExe,
                        major))
                {
                    throw new InvalidOperationException(
                        $"Java was extracted but the runtime version is not Java {major}.");
                }

                WriteLog(
                    $"Java {major} successfully installed: {javaExe}");
            }
            catch (Exception ex)
            {
                WriteException(
                    $"JAVA {major} INSTALL ERROR",
                    ex);

                throw;
            }
            finally
            {
                /*
                 * Both files should be closed before cleanup.
                 */
                TryDeleteFile(
                    tempPart);

                TryDeleteFile(
                    tempArchive);
            }
        }

        private static string? FindJavaRoot(
            string destination)
        {
            try
            {
                foreach (string directory in
                         Directory.GetDirectories(
                             destination,
                             "*",
                             SearchOption.AllDirectories))
                {
                    string javaExe =
                        Path.Combine(
                            directory,
                            "bin",
                            "java.exe");

                    if (File.Exists(javaExe))
                    {
                        return directory;
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        private static void MoveJavaRootContents(
            string source,
            string destination)
        {
            if (string.Equals(
                    Path.GetFullPath(source).TrimEnd(
                        Path.DirectorySeparatorChar),
                    Path.GetFullPath(destination).TrimEnd(
                        Path.DirectorySeparatorChar),
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            foreach (string directory in
                     Directory.GetDirectories(
                         source))
            {
                string target =
                    Path.Combine(
                        destination,
                        Path.GetFileName(
                            directory));

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

            try
            {
                Directory.Delete(
                    source,
                    true);
            }
            catch
            {
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

                string assets =
                    Path.Combine(
                        _gamePath,
                        "assets");

                string libraries =
                    Path.Combine(
                        _gamePath,
                        "libraries");

                string versions =
                    Path.Combine(
                        _gamePath,
                        "versions");

                string fabricDirectory =
                    Path.Combine(
                        versions,
                        fabricVersion);

                if (!Directory.Exists(assets))
                {
                    WriteLog(
                        "WARNING: Assets directory is missing.");
                }

                if (!Directory.Exists(libraries))
                {
                    WriteLog(
                        "ERROR: Libraries directory is missing.");

                    return false;
                }

                if (!Directory.Exists(versions))
                {
                    WriteLog(
                        "ERROR: Versions directory is missing.");

                    return false;
                }

                if (!Directory.Exists(fabricDirectory))
                {
                    WriteLog(
                        "ERROR: Fabric directory is missing.");

                    return false;
                }

                string fabricJson =
                    Path.Combine(
                        fabricDirectory,
                        fabricVersion + ".json");

                if (!File.Exists(fabricJson))
                {
                    WriteLog(
                        $"ERROR: Fabric JSON missing: {fabricJson}");

                    return false;
                }

                string loaderJar =
                    FindFabricLoaderJar(
                        _gamePath,
                        fabricVersion);

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

        private string FindFabricLoaderJar(
            string root,
            string fabricVersion)
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
                             fabricVersion + ".jar",
                             SearchOption.AllDirectories))
                {
                    if (File.Exists(file))
                        return file;
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
                        "Microsoft login is not enabled yet. Select Offline mode.");
                }

                string username =
                    UsernameInput.Text.Trim();

                if (string.IsNullOrWhiteSpace(username))
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

                // ----------------------------------------------------
                // JAVA
                // ----------------------------------------------------

                int javaMajor =
                    GetRequiredJavaMajor(
                        minecraftVersion);

                WriteLog(
                    $"Required Java: {javaMajor}");

                string javaPath =
                    await EnsureJavaAsync(
                        javaMajor);

                WriteLog(
                    $"Java executable: {javaPath}");

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
                // VANILLA
                // ----------------------------------------------------

                StatusText.Text =
                    $"Installing Minecraft {minecraftVersion}...";

                WriteLog(
                    "Installing Minecraft files...");

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
                    "Minecraft installation completed.");

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
                // OPTIONAL MODS
                // ----------------------------------------------------

                StatusText.Text =
                    "Installing performance mods...";

                await InstallPerformanceModsAsync(
                    minecraftVersion);

                // ----------------------------------------------------
                // LAUNCH OPTIONS
                // ----------------------------------------------------

                if (_session == null)
                {
                    throw new InvalidOperationException(
                        "Minecraft session was not created.");
                }

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
                    "Building Minecraft process...");

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

                /*
                 * Redirect output before Start().
                 */
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
                    "Starting Minecraft...");

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
                catch (Exception outputEx)
                {
                    WriteException(
                        "OUTPUT REDIRECTION ERROR",
                        outputEx);
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
                LaunchBtn.IsEnabled = true;
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

                try
                {
                    await Task.Delay(500);
                }
                catch
                {
                }

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
                        "Check [MC] and [MC-ERR] lines above for the real Minecraft error.");
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
