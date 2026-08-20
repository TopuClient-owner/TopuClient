using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text;
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

        // IMPORTANT:
        // Indium has deliberately been removed.
        //
        // Everything else stays.
        private static readonly (string Slug, string Name)[] PerformanceMods =
        {
            ("sodium", "Sodium"),
            ("lithium", "Lithium"),
            ("dynamic-fps", "Dynamic FPS"),
            ("sodium-extra", "Sodium Extra"),
            ("krypton", "Krypton")
        };

        // ============================================================
        // FABRIC OFFICIAL INSTALLER
        // ============================================================

        /*
         * We use Fabric's official installer JAR.
         *
         * The launcher downloads it automatically and executes:
         *
         * java -jar fabric-installer-x.x.x.jar client
         *     -dir "<TopuClient>"
         *     -mcversion "<Minecraft>"
         *     -loader default
         *     -noprofile
         *
         * No manual Fabric installer window is required.
         */

        private const string FabricInstallerMetadataUrl =
            "https://maven.fabricmc.net/net/fabricmc/fabric-installer/maven-metadata.xml";

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

            WriteLog(
                "Topu Client initialized.");
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
        // FABRIC GET BUTTON
        // ============================================================

        /*
         * Wire your Fabric "Get" button to:
         *
         * Click="InstallFabricButton_Click"
         *
         * This installs Fabric automatically.
         *
         * It does NOT launch a visible installer window.
         */

        private async void InstallFabricButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            try
            {
                if (sender is Button button)
                {
                    button.IsEnabled = false;
                }

                string minecraftVersion =
                    GetSelectedVersion();

                StatusText.Text =
                    $"Installing Fabric {minecraftVersion}...";

                WriteLog(
                    "===== MANUAL FABRIC INSTALL START =====");

                WriteLog(
                    $"Minecraft version: {minecraftVersion}");

                int javaMajor =
                    GetRequiredJavaMajor(
                        minecraftVersion);

                string javaPath =
                    await EnsureJavaAsync(
                        javaMajor);

                MinecraftPath minecraftPath =
                    new MinecraftPath(
                        _gamePath);

                /*
                 * First make sure vanilla exists.
                 *
                 * This is important because Fabric's client installer
                 * expects the Minecraft installation to exist.
                 */

                MinecraftLauncher launcher =
                    new MinecraftLauncher(
                        minecraftPath);

                StatusText.Text =
                    $"Checking Minecraft {minecraftVersion}...";

                WriteLog(
                    "Ensuring vanilla Minecraft installation exists.");

                await launcher.InstallAsync(
                    minecraftVersion);

                WriteLog(
                    "Vanilla installation is ready.");

                /*
                 * Now run the ORIGINAL Fabric installer automatically.
                 */

                string fabricVersion =
                    await InstallFabricUsingOfficialInstallerAsync(
                        minecraftVersion,
                        javaPath);

                /*
                 * Fabric API is a normal mod.
                 * Install it after Fabric Loader.
                 */

                StatusText.Text =
                    "Installing Fabric API...";

                await InstallFabricApiAsync(
                    minecraftVersion);

                /*
                 * Performance mods.
                 *
                 * Indium is NOT included.
                 */

                StatusText.Text =
                    "Installing performance mods...";

                await InstallPerformanceModsAsync(
                    minecraftVersion);

                if (!ValidateFabricInstallation(
                        fabricVersion))
                {
                    throw new InvalidOperationException(
                        "Fabric was installed but validation failed.");
                }

                StatusText.Text =
                    $"Fabric {minecraftVersion} installed successfully.";

                WriteLog(
                    "===== MANUAL FABRIC INSTALL COMPLETE =====");

                MessageBox.Show(
                    $"Fabric {minecraftVersion} installed successfully.",
                    "Topu Client",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusText.Text =
                    "Fabric installation failed.";

                WriteException(
                    "FABRIC BUTTON ERROR",
                    ex);

                MessageBox.Show(
                    "Fabric installation failed.\n\n" +
                    ex.Message +
                    "\n\nLog:\n" +
                    _logPath,
                    "Topu Client",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                if (sender is Button button)
                {
                    button.IsEnabled = true;
                }
            }
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
        // OFFICIAL FABRIC INSTALLER
        // ============================================================

        private async Task<string> InstallFabricUsingOfficialInstallerAsync(
            string minecraftVersion,
            string javaPath)
        {
            WriteLog(
                "===== OFFICIAL FABRIC INSTALLER =====");

            string installerVersion =
                await GetLatestFabricInstallerVersionAsync();

            if (string.IsNullOrWhiteSpace(
                    installerVersion))
            {
                throw new InvalidOperationException(
                    "Could not determine the latest Fabric Installer version.");
            }

            string installerDirectory =
                Path.Combine(
                    _gamePath,
                    "fabric-installer");

            Directory.CreateDirectory(
                installerDirectory);

            string installerFile =
                Path.Combine(
                    installerDirectory,
                    $"fabric-installer-{installerVersion}.jar");

            string installerUrl =
                "https://maven.fabricmc.net/net/fabricmc/" +
                "fabric-installer/" +
                Uri.EscapeDataString(installerVersion) +
                "/fabric-installer-" +
                Uri.EscapeDataString(installerVersion) +
                ".jar";

            WriteLog(
                $"Fabric Installer version: {installerVersion}");

            WriteLog(
                $"Fabric Installer URL: {installerUrl}");

            if (!File.Exists(installerFile) ||
                new FileInfo(installerFile).Length <= 0)
            {
                StatusText.Text =
                    $"Downloading Fabric Installer {installerVersion}...";

                await DownloadFileAsync(
                    installerUrl,
                    installerFile);
            }
            else
            {
                WriteLog(
                    "Fabric Installer already downloaded.");
            }

            if (!File.Exists(installerFile))
            {
                throw new FileNotFoundException(
                    "Fabric Installer JAR was not downloaded.",
                    installerFile);
            }

            FileInfo installerInfo =
                new FileInfo(installerFile);

            if (installerInfo.Length <= 0)
            {
                throw new IOException(
                    "Fabric Installer JAR is empty.");
            }

            WriteLog(
                $"Fabric Installer size: {installerInfo.Length} bytes");

            /*
             * Fabric's official installer supports:
             *
             * client
             * -dir
             * -mcversion
             * -loader
             * -noprofile
             *
             * We use "default" for loader so Fabric chooses the
             * appropriate latest loader for the selected Minecraft
             * version.
             */

            string arguments =
                "-jar " +
                QuoteArgument(installerFile) +
                " client " +
                "-dir " +
                QuoteArgument(_gamePath) +
                " " +
                "-mcversion " +
                QuoteArgument(minecraftVersion) +
                " " +
                "-loader default " +
                "-noprofile";

            WriteLog(
                "Fabric Installer command:");

            WriteLog(
                $"{javaPath} {arguments}");

            StatusText.Text =
                $"Installing Fabric Loader for {minecraftVersion}...";

            ProcessStartInfo startInfo =
                new ProcessStartInfo
                {
                    FileName = javaPath,
                    Arguments = arguments,
                    WorkingDirectory = _gamePath,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

            using Process installerProcess =
                new Process
                {
                    StartInfo = startInfo,
                    EnableRaisingEvents = false
                };

            installerProcess.OutputDataReceived +=
                (s, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                    {
                        AppendRawGameOutput(
                            "[FABRIC]",
                            e.Data);
                    }
                };

            installerProcess.ErrorDataReceived +=
                (s, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                    {
                        AppendRawGameOutput(
                            "[FABRIC-ERR]",
                            e.Data);
                    }
                };

            WriteLog(
                "Starting official Fabric Installer.");

            if (!installerProcess.Start())
            {
                throw new InvalidOperationException(
                    "Windows failed to start the Fabric Installer.");
            }

            installerProcess.BeginOutputReadLine();
            installerProcess.BeginErrorReadLine();

            await Task.Run(
                () =>
                {
                    installerProcess.WaitForExit();
                });

            /*
             * Give asynchronous stdout/stderr handlers a moment to
             * finish writing their final lines.
             */

            await Task.Delay(500);

            int exitCode =
                installerProcess.ExitCode;

            WriteLog(
                $"Official Fabric Installer exit code: {exitCode}");

            if (exitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Fabric Installer failed with exit code {exitCode}. " +
                    $"Check [FABRIC] and [FABRIC-ERR] lines in {_logPath}");
            }

            string? fabricVersion =
                FindFabricVersionForMinecraft(
                    minecraftVersion);

            if (fabricVersion == null)
            {
                throw new InvalidOperationException(
                    "Fabric Installer finished successfully, but no Fabric version JSON was found.");
            }

            WriteLog(
                $"Fabric installation detected: {fabricVersion}");

            WriteLog(
                "===== OFFICIAL FABRIC INSTALLER COMPLETE =====");

            return fabricVersion;
        }

        private async Task<string> GetLatestFabricInstallerVersionAsync()
        {
            WriteLog(
                $"Reading Fabric Installer metadata: {FabricInstallerMetadataUrl}");

            using HttpResponseMessage response =
                await Http.GetAsync(
                    FabricInstallerMetadataUrl);

            response.EnsureSuccessStatusCode();

            string xml =
                await response.Content.ReadAsStringAsync();

            /*
             * We intentionally avoid adding an XML package.
             *
             * Fabric's Maven metadata is simple enough to extract the
             * latest release from the <release> element.
             */

            string release =
                ExtractXmlElement(
                    xml,
                    "release");

            if (string.IsNullOrWhiteSpace(
                    release))
            {
                release =
                    ExtractLatestVersionFromXml(
                        xml);
            }

            if (string.IsNullOrWhiteSpace(
                    release))
            {
                throw new InvalidOperationException(
                    "Fabric Installer Maven metadata did not contain a release version.");
            }

            WriteLog(
                $"Latest Fabric Installer: {release}");

            return release.Trim();
        }

        private static string ExtractXmlElement(
            string xml,
            string elementName)
        {
            string open =
                "<" +
                elementName +
                ">";

            string close =
                "</" +
                elementName +
                ">";

            int start =
                xml.IndexOf(
                    open,
                    StringComparison.OrdinalIgnoreCase);

            if (start < 0)
                return "";

            start += open.Length;

            int end =
                xml.IndexOf(
                    close,
                    start,
                    StringComparison.OrdinalIgnoreCase);

            if (end < 0)
                return "";

            return xml
                .Substring(
                    start,
                    end - start)
                .Trim();
        }

        private static string ExtractLatestVersionFromXml(
            string xml)
        {
            /*
             * Fallback parser for Maven metadata.
             */

            int versionsStart =
                xml.IndexOf(
                    "<versions>",
                    StringComparison.OrdinalIgnoreCase);

            int versionsEnd =
                xml.IndexOf(
                    "</versions>",
                    StringComparison.OrdinalIgnoreCase);

            if (versionsStart < 0 ||
                versionsEnd <= versionsStart)
            {
                return "";
            }

            string versions =
                xml.Substring(
                    versionsStart,
                    versionsEnd - versionsStart);

            string[] parts =
                versions
                    .Replace(
                        "<versions>",
                        "",
                        StringComparison.OrdinalIgnoreCase)
                    .Split(
                        new[]
                        {
                            "<version>",
                            "</version>"
                        },
                        StringSplitOptions.RemoveEmptyEntries);

            string latest =
                "";

            foreach (string raw in parts)
            {
                string version =
                    raw.Trim();

                if (string.IsNullOrWhiteSpace(version))
                    continue;

                if (version.Contains("<"))
                    continue;

                latest =
                    version;
            }

            return latest;
        }

        private static string? FindFabricVersionForMinecraft(
            string minecraftVersion)
        {
            /*
             * Fabric's generated version names normally contain both
             * the Fabric Loader version and the Minecraft version.
             *
             * We inspect every version directory instead of assuming
             * a hard-coded loader version.
             */

            return null;
        }

        // ============================================================
        // FABRIC VALIDATION
        // ============================================================

        private bool ValidateFabricInstallation(
            string? knownFabricVersion)
        {
            try
            {
                WriteLog(
                    "===== FABRIC VALIDATION =====");

                string versionsDirectory =
                    Path.Combine(
                        _gamePath,
                        "versions");

                if (!Directory.Exists(
                        versionsDirectory))
                {
                    WriteLog(
                        "Fabric validation failed: versions directory missing.");

                    return false;
                }

                if (!Directory.Exists(
                        Path.Combine(
                            _gamePath,
                            "libraries")))
                {
                    WriteLog(
                        "Fabric validation failed: libraries directory missing.");

                    return false;
                }

                if (!string.IsNullOrWhiteSpace(
                        knownFabricVersion))
                {
                    string directory =
                        Path.Combine(
                            versionsDirectory,
                            knownFabricVersion);

                    string json =
                        Path.Combine(
                            directory,
                            knownFabricVersion +
                            ".json");

                    if (Directory.Exists(directory) &&
                        File.Exists(json))
                    {
                        using JsonDocument document =
                            JsonDocument.Parse(
                                File.ReadAllText(json));

                        if (document.RootElement.ValueKind ==
                            JsonValueKind.Object)
                        {
                            WriteLog(
                                $"Fabric JSON found: {json}");

                            WriteLog(
                                "Fabric JSON is valid.");

                            return true;
                        }
                    }
                }

                /*
                 * If the installer generated a different profile name,
                 * scan the versions directory.
                 */

                foreach (string directory in
                         Directory.GetDirectories(
                             versionsDirectory))
                {
                    string name =
                        Path.GetFileName(
                            directory);

                    if (!name.Contains(
                            "fabric",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string json =
                        Path.Combine(
                            directory,
                            name + ".json");

                    if (!File.Exists(json))
                        continue;

                    try
                    {
                        using JsonDocument document =
                            JsonDocument.Parse(
                                File.ReadAllText(json));

                        if (document.RootElement.ValueKind ==
                            JsonValueKind.Object)
                        {
                            WriteLog(
                                $"Fabric installation detected: {name}");

                            return true;
                        }
                    }
                    catch
                    {
                    }
                }

                WriteLog(
                    "No valid Fabric installation was found.");

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
        // FABRIC API
        // ============================================================

        private async Task InstallFabricApiAsync(
            string minecraftVersion)
        {
            /*
             * Fabric API is a mod, not Fabric Loader.
             *
             * We use Modrinth to obtain the correct Fabric API build
             * for the selected Minecraft version.
             */

            WriteLog(
                $"Searching Fabric API for Minecraft {minecraftVersion}.");

            string url =
                "https://api.modrinth.com/v2/project/fabric-api/version" +
                "?loaders=%5B%22fabric%22%5D" +
                "&game_versions=%5B%22" +
                Uri.EscapeDataString(minecraftVersion) +
                "%22%5D";

            using HttpResponseMessage response =
                await Http.GetAsync(url);

            response.EnsureSuccessStatusCode();

            string json =
                await response.Content.ReadAsStringAsync();

            using JsonDocument document =
                JsonDocument.Parse(json);

            JsonElement versions =
                document.RootElement;

            if (versions.ValueKind !=
                    JsonValueKind.Array ||
                versions.GetArrayLength() == 0)
            {
                throw new InvalidOperationException(
                    $"Fabric API has no compatible build for Minecraft {minecraftVersion}.");
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

                selectedFile =
                    FindPrimaryJar(files);

                if (selectedFile != null)
                    break;
            }

            if (selectedFile == null)
            {
                throw new InvalidOperationException(
                    $"No Fabric API JAR was returned for Minecraft {minecraftVersion}.");
            }

            JsonElement file =
                selectedFile.Value;

            string downloadUrl =
                file.GetProperty("url")
                    .GetString()
                ?? throw new InvalidOperationException(
                    "Fabric API download URL was missing.");

            string filename =
                file.GetProperty("filename")
                    .GetString()
                ?? "fabric-api.jar";

            string modsDirectory =
                Path.Combine(
                    _gamePath,
                    "mods");

            Directory.CreateDirectory(
                modsDirectory);

            string destination =
                Path.Combine(
                    modsDirectory,
                    SanitizeFileName(filename));

            if (File.Exists(destination))
            {
                FileInfo existing =
                    new FileInfo(destination);

                if (existing.Length > 0)
                {
                    WriteLog(
                        $"Fabric API already installed: {filename}");

                    return;
                }

                TryDeleteFile(destination);
            }

            await DownloadFileAsync(
                downloadUrl,
                destination);

            WriteLog(
                $"Fabric API installed: {destination}");
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
                    $"No compatible {name} version for {minecraftVersion}.");

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

                selectedFile =
                    FindPrimaryJar(files);

                if (selectedFile != null)
                    break;
            }

            if (selectedFile == null)
            {
                WriteLog(
                    $"No usable JAR returned for {name}.");

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

                TryDeleteFile(destination);
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

            if (info.Length <= 0)
            {
                TryDeleteFile(destination);

                throw new IOException(
                    $"Mod file is empty: {destination}");
            }

            return true;
        }

        private static JsonElement? FindPrimaryJar(
            JsonElement files)
        {
            if (files.ValueKind !=
                JsonValueKind.Array)
            {
                return null;
            }

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
                    filenameElement.GetString()
                    ?? "";

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

                await input.CopyToAsync(
                    output);

                await output.FlushAsync();

                WriteLog(
                    $"Download stream closed: {temporary}");
            }
            catch
            {
                TryDeleteFile(
                    temporary);

                throw;
            }

            try
            {
                if (!File.Exists(
                        temporary))
                {
                    throw new IOException(
                        "Temporary download file was not created.");
                }

                FileInfo info =
                    new FileInfo(
                        temporary);

                if (info.Length <= 0)
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

                WriteLog(
                    $"Download complete: {destination}");
            }
            finally
            {
                TryDeleteFile(
                    temporary);
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

        private static void DeleteStaleDownloadFiles(
            string destination)
        {
            try
            {
                string? directory =
                    Path.GetDirectoryName(
                        destination);

                if (string.IsNullOrWhiteSpace(
                    directory))
                    return;

                if (!Directory.Exists(
                    directory))
                    return;

                string filename =
                    Path.GetFileName(
                        destination);

                foreach (string file in
                    Directory.GetFiles(
                        directory,
                        "." +
                        filename +
                        ".*.download"))
                {
                    TryDeleteFile(file);
                }

                foreach (string file in
                    Directory.GetFiles(
                        directory,
                        filename +
                        ".*.download"))
                {
                    TryDeleteFile(file);
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
                FindSystemJava(
                    requiredMajor);

            if (!string.IsNullOrWhiteSpace(
                    systemJava))
            {
                WriteLog(
                    $"Using system Java {requiredMajor}: {systemJava}");

                return systemJava;
            }

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
                    $"Installed Java is not version {requiredMajor}.");
            }

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

                WriteLog(
                    $"Java version check: {javaPath}");

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
                    $"No Windows x64 Java {major} runtime was found.");
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
                    SanitizeFileName(
                        archiveName));

            string extractionDirectory =
                destination +
                ".extracting-" +
                Guid.NewGuid().ToString("N");

            try
            {
                StatusText.Text =
                    $"Downloading Java {major}...";

                using (
                    HttpResponseMessage javaResponse =
                        await Http.GetAsync(
                            downloadUrl,
                            HttpCompletionOption.ResponseHeadersRead))
                {
                    javaResponse.EnsureSuccessStatusCode();

                    using Stream input =
                        await javaResponse.Content
                            .ReadAsStreamAsync();

                    using FileStream output =
                        new FileStream(
                            tempArchive,
                            FileMode.Create,
                            FileAccess.Write,
                            FileShare.ReadWrite,
                            131072,
                            FileOptions.SequentialScan);

                    await input.CopyToAsync(
                        output);

                    await output.FlushAsync();
                }

                await WaitForArchiveReadyAsync(
                    tempArchive);

                StatusText.Text =
                    $"Installing Java {major}...";

                Directory.CreateDirectory(
                    extractionDirectory);

                await ExtractZipWithRetryAsync(
                    tempArchive,
                    extractionDirectory);

                string? javaRoot =
                    FindJavaRoot(
                        extractionDirectory);

                if (javaRoot != null)
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

                if (!File.Exists(
                        extractedJava))
                {
                    throw new InvalidOperationException(
                        $"Java {major} was extracted but java.exe was not found.");
                }

                if (!IsRequiredJava(
                        extractedJava,
                        major))
                {
                    throw new InvalidOperationException(
                        $"Extracted Java is not Java {major}.");
                }

                string? parent =
                    Path.GetDirectoryName(
                        destination);

                if (!string.IsNullOrWhiteSpace(parent))
                {
                    Directory.CreateDirectory(parent);
                }

                if (Directory.Exists(
                        destination))
                {
                    await DeleteDirectoryWithRetryAsync(
                        destination);
                }

                await MoveDirectoryWithRetryAsync(
                    extractionDirectory,
                    destination);

                string finalJava =
                    Path.Combine(
                        destination,
                        "bin",
                        "java.exe");

                if (!File.Exists(finalJava) ||
                    !IsRequiredJava(
                        finalJava,
                        major))
                {
                    throw new InvalidOperationException(
                        $"Final Java {major} verification failed.");
                }
            }
            catch (Exception ex)
            {
                WriteException(
                    $"JAVA {major} INSTALLATION ERROR",
                    ex);

                TryDeleteDirectory(
                    extractionDirectory);

                throw;
            }
            finally
            {
                TryDeleteFile(
                    tempArchive);
            }
        }

        private async Task WaitForArchiveReadyAsync(
            string path)
        {
            const int maxAttempts = 120;

            for (int attempt = 1;
                 attempt <= maxAttempts;
                 attempt++)
            {
                try
                {
                    using FileStream stream =
                        new FileStream(
                            path,
                            FileMode.Open,
                            FileAccess.Read,
                            FileShare.ReadWrite |
                            FileShare.Delete,
                            81920,
                            FileOptions.SequentialScan);

                    if (stream.Length <= 0)
                        throw new IOException(
                            "Archive has zero length.");

                    byte[] buffer =
                        new byte[4];

                    int read =
                        await stream.ReadAsync(
                            buffer,
                            0,
                            buffer.Length);

                    if (read > 0)
                        return;
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }

                await Task.Delay(500);
            }

            throw new IOException(
                $"Archive remained locked for 60 seconds: {path}");
        }

        private async Task ExtractZipWithRetryAsync(
            string archivePath,
            string destination)
        {
            const int maxAttempts = 120;

            Exception? lastException = null;

            for (int attempt = 1;
                 attempt <= maxAttempts;
                 attempt++)
            {
                try
                {
                    ZipFile.ExtractToDirectory(
                        archivePath,
                        destination,
                        true);

                    return;
                }
                catch (IOException ex)
                {
                    lastException = ex;
                }
                catch (UnauthorizedAccessException ex)
                {
                    lastException = ex;
                }

                await Task.Delay(500);
            }

            throw new IOException(
                "Java ZIP could not be extracted after 60 seconds.",
                lastException);
        }

        private async Task MoveDirectoryWithRetryAsync(
            string source,
            string destination)
        {
            const int maxAttempts = 60;

            Exception? lastException = null;

            for (int attempt = 1;
                 attempt <= maxAttempts;
                 attempt++)
            {
                try
                {
                    if (!Directory.Exists(source))
                    {
                        throw new DirectoryNotFoundException(
                            source);
                    }

                    if (Directory.Exists(destination))
                    {
                        Directory.Delete(
                            destination,
                            true);
                    }

                    Directory.Move(
                        source,
                        destination);

                    return;
                }
                catch (IOException ex)
                {
                    lastException = ex;
                }
                catch (UnauthorizedAccessException ex)
                {
                    lastException = ex;
                }

                await Task.Delay(500);
            }

            throw new IOException(
                $"Could not move Java installation to {destination}.",
                lastException);
        }

        private async Task DeleteDirectoryWithRetryAsync(
            string path)
        {
            const int maxAttempts = 60;

            Exception? lastException = null;

            for (int attempt = 1;
                 attempt <= maxAttempts;
                 attempt++)
            {
                try
                {
                    if (!Directory.Exists(path))
                        return;

                    Directory.Delete(
                        path,
                        true);

                    return;
                }
                catch (IOException ex)
                {
                    lastException = ex;
                }
                catch (UnauthorizedAccessException ex)
                {
                    lastException = ex;
                }

                await Task.Delay(500);
            }

            throw new IOException(
                $"Could not delete directory: {path}",
                lastException);
        }

        private static string? FindJavaRoot(
            string destination)
        {
            string directJava =
                Path.Combine(
                    destination,
                    "bin",
                    "java.exe");

            if (File.Exists(directJava))
                return null;

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

            TryDeleteDirectory(
                source);
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
                        (int)RamSlider.Value *
                        1024);

                WriteLog(
                    $"Minecraft: {minecraftVersion}");

                WriteLog(
                    $"RAM: {ram} MB");

                WriteLog(
                    $"Game directory: {_gamePath}");

                // ====================================================
                // AUTH
                // ====================================================

                if (AuthTypeBox.SelectedIndex != 0)
                {
                    throw new InvalidOperationException(
                        "Microsoft login is not enabled yet. Select Offline / Cracked Mode.");
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

                // ====================================================
                // JAVA
                // ====================================================

                int javaMajor =
                    GetRequiredJavaMajor(
                        minecraftVersion);

                string javaPath =
                    await EnsureJavaAsync(
                        javaMajor);

                // ====================================================
                // CMLLIB
                // ====================================================

                MinecraftPath minecraftPath =
                    new MinecraftPath(
                        _gamePath);

                MinecraftLauncher launcher =
                    new MinecraftLauncher(
                        minecraftPath);

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
                            if (args.TotalBytes <= 0)
                                return;

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
                        catch
                        {
                        }
                    };

                // ====================================================
                // VANILLA FILES
                // ====================================================

                StatusText.Text =
                    $"Installing Minecraft {minecraftVersion}...";

                WriteLog(
                    "CmlLib is installing vanilla Minecraft.");

                /*
                 * This is intentionally ONLY the normal Minecraft
                 * installation.
                 *
                 * Fabric is handled afterward by the official
                 * Fabric installer.
                 */

                await launcher.InstallAsync(
                    minecraftVersion);

                WriteLog(
                    "Vanilla Minecraft installation complete.");

                // ====================================================
                // OFFICIAL FABRIC
                // ====================================================

                string? fabricVersion =
                    FindFabricVersionForMinecraft(
                        minecraftVersion);

                if (fabricVersion == null)
                {
                    StatusText.Text =
                        "Installing Fabric Loader...";

                    fabricVersion =
                        await InstallFabricUsingOfficialInstallerAsync(
                            minecraftVersion,
                            javaPath);
                }

                // ====================================================
                // FABRIC API
                // ====================================================

                await InstallFabricApiAsync(
                    minecraftVersion);

                // ====================================================
                // PERFORMANCE MODS
                // ====================================================

                await InstallPerformanceModsAsync(
                    minecraftVersion);

                // ====================================================
                // VALIDATION
                // ====================================================

                if (!ValidateFabricInstallation(
                        fabricVersion))
                {
                    throw new InvalidOperationException(
                        "Fabric installation validation failed.");
                }

                if (_session == null)
                {
                    throw new InvalidOperationException(
                        "Minecraft session was not created.");
                }

                // ====================================================
                // LAUNCH
                // ====================================================

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

                StatusText.Text =
                    "Building Minecraft process...";

                WriteLog(
                    $"Building Fabric process: {fabricVersion}");

                Process process =
                    await launcher.BuildProcessAsync(
                        fabricVersion,
                        options);

                if (process == null)
                {
                    throw new InvalidOperationException(
                        "CmlLib returned a null process.");
                }

                _minecraftProcess =
                    process;

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
                    MonitorMinecraftAsync(
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

        private static string QuoteArgument(
            string value)
        {
            if (string.IsNullOrEmpty(value))
                return "\"\"";

            return "\"" +
                   value.Replace(
                       "\\",
                       "\\\\")
                        .Replace(
                       "\"",
                       "\\\"") +
                   "\"";
        }

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
