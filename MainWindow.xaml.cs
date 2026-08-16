```csharp
using System;
using System.Collections.Generic;
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

        private string? _selectedServer;

        private const string DefaultMinecraftVersion = "1.21.1";

        /*
         * Preferred loader.
         *
         * If this exact loader is not available for the selected
         * Minecraft version, the launcher automatically selects the
         * newest stable Fabric loader returned by Fabric.
         */
        private const string PreferredFabricLoader = "0.19.3";

        private static readonly HttpClient Http = CreateHttpClient();

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient(
                new HttpClientHandler
                {
                    AllowAutoRedirect = true
                });

            client.Timeout = TimeSpan.FromMinutes(10);

            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "TopuClient/1.0");

            return client;
        }

        /*
         * These are Modrinth project slugs, not hard-coded JAR URLs.
         * The launcher asks Modrinth for a version compatible with
         * the selected Minecraft version and Fabric.
         */
        private static readonly string[] PerformanceMods =
        {
            "sodium",
            "lithium",
            "dynamic-fps",
            "sodium-extra",
            "krypton"
        };

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

            if (RamLabel != null && RamSlider != null)
            {
                RamLabel.Text =
                    $"{(int)RamSlider.Value}GB";
            }

            WriteLog("Topu Client initialized.");
            WriteLog($"Game directory: {_gamePath}");
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
                    _logFilePath,
                    $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
            }
            catch
            {
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

        private void StartNewLaunchLog()
        {
            try
            {
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

        private void AppendGameLog(string message)
        {
            WriteLog(message);
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

                string username =
                    File.ReadAllText(_configFilePath).Trim();

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
                    _configFilePath,
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
            if (RamLabel == null)
                return;

            RamLabel.Text =
                $"{(int)e.NewValue}GB";
        }

        // =========================================================
        // VERSION
        // =========================================================

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
                $"Profile saved: Minecraft={version}, RAM={ram}MB");

            MessageBox.Show(
                "Profile settings saved successfully.",
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
            MsLoginBtn.IsEnabled = false;

            try
            {
                StatusText.Text =
                    "Microsoft login is not configured in this build.";

                MessageBox.Show(
                    "Microsoft authentication is not configured yet.\n\n" +
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

            _selectedServer =
                button.Tag?.ToString();

            if (string.IsNullOrWhiteSpace(
                    _selectedServer))
                return;

            StatusText.Text =
                $"Server selected: {_selectedServer}";

            WriteLog(
                $"Quick server selected: {_selectedServer}");
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
                string version =
                    GetSelectedMinecraftVersion();

                ModSearchStatus.Text =
                    $"Searching Modrinth for {query}...";

                string projectId =
                    await FindModrinthProjectAsync(
                        query);

                if (string.IsNullOrWhiteSpace(
                        projectId))
                {
                    ModSearchStatus.Text =
                        "No mod found.";

                    return;
                }

                string? installed =
                    await DownloadModrinthProjectAsync(
                        projectId,
                        version,
                        true);

                if (installed == null)
                {
                    ModSearchStatus.Text =
                        $"No Fabric version found for {version}.";

                    return;
                }

                ModSearchStatus.Text =
                    $"Installed: {installed}";
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
                $"?query={Uri.EscapeDataString(query)}" +
                "&facets=%5B%5B%22project_type%3Amod%22%5D%5D";

            using HttpResponseMessage response =
                await Http.GetAsync(url);

            response.EnsureSuccessStatusCode();

            string json =
                await response.Content.ReadAsStringAsync();

            using JsonDocument document =
                JsonDocument.Parse(json);

            if (!document.RootElement.TryGetProperty(
                    "hits",
                    out JsonElement hits) ||
                hits.ValueKind != JsonValueKind.Array ||
                hits.GetArrayLength() == 0)
            {
                return "";
            }

            JsonElement first =
                hits[0];

            if (first.TryGetProperty(
                    "project_id",
                    out JsonElement projectId))
            {
                return projectId.GetString() ?? "";
            }

            return "";
        }

        // =========================================================
        // MAIN LAUNCH
        // =========================================================

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
                StartNewLaunchLog();

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
                // OFFLINE SESSION
                // -------------------------------------------------

                if (AuthTypeBox.SelectedIndex == 0)
                {
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
                }
                else
                {
                    throw new InvalidOperationException(
                        "Microsoft authentication is not configured yet.");
                }

                // -------------------------------------------------
                // CMLLIB PATH
                // -------------------------------------------------

                MinecraftPath minecraftPath =
                    new MinecraftPath(
                        _gamePath);

                MinecraftLauncher launcher =
                    new MinecraftLauncher(
                        minecraftPath);

                launcher.FileProgressChanged +=
                    Launcher_FileProgressChanged;

                launcher.ByteProgressChanged +=
                    Launcher_ByteProgressChanged;

                // -------------------------------------------------
                // STEP 1: VANILLA
                // -------------------------------------------------

                SetStatus(
                    $"1/6 Installing Minecraft {minecraftVersion}...");

                WriteLog(
                    $"Installing vanilla Minecraft {minecraftVersion}...");

                await launcher.InstallAsync(
                    minecraftVersion);

                WriteLog(
                    "Vanilla Minecraft installation completed.");

                // -------------------------------------------------
                // STEP 2: FABRIC LOADER
                // -------------------------------------------------

                SetStatus(
                    "2/6 Installing Fabric Loader...");

                string fabricLoader =
                    await SelectFabricLoaderAsync(
                        minecraftVersion);

                WriteLog(
                    $"Selected Fabric Loader: {fabricLoader}");

                FabricInstaller fabricInstaller =
                    new FabricInstaller(
                        Http);

                string fabricVersion =
                    await fabricInstaller.Install(
                        minecraftVersion,
                        fabricLoader,
                        minecraftPath);

                if (string.IsNullOrWhiteSpace(
                        fabricVersion))
                {
                    throw new InvalidOperationException(
                        "Fabric installer returned an empty version name.");
                }

                WriteLog(
                    $"Fabric installed: {fabricVersion}");

                // -------------------------------------------------
                // STEP 3: INSTALL FABRIC PROFILE FILES
                // -------------------------------------------------

                SetStatus(
                    "3/6 Installing Fabric libraries and runtime...");

                /*
                 * IMPORTANT:
                 *
                 * FabricInstaller creates the Fabric version.
                 *
                 * We then ask CmlLib to install THAT version.
                 * This is the part that installs the Fabric profile's
                 * libraries/assets/runtime instead of stopping after
                 * vanilla Minecraft.
                 */
                WriteLog(
                    $"Installing files for Fabric profile {fabricVersion}...");

                await launcher.InstallAsync(
                    fabricVersion);

                WriteLog(
                    "Fabric profile files installed.");

                // -------------------------------------------------
                // STEP 4: JAVA
                // -------------------------------------------------

                SetStatus(
                    "4/6 Checking required Java runtime...");

                IVersion installedFabricVersion =
                    await launcher.GetVersionAsync(
                        fabricVersion);

                string? javaPath =
                    launcher.GetJavaPath(
                        installedFabricVersion);

                if (string.IsNullOrWhiteSpace(javaPath) ||
                    !File.Exists(javaPath))
                {
                    /*
                     * CmlLib has its own Java path resolver.
                     * Try the default resolved Java path as well.
                     */
                    javaPath =
                        launcher.GetDefaultJavaPath();
                }

                if (string.IsNullOrWhiteSpace(javaPath) ||
                    !File.Exists(javaPath))
                {
                    /*
                     * Last fallback to a system Java.
                     * This is intentionally NOT forced to Java 21,
                     * because Minecraft 26.1+ requires Java 25.
                     */
                    javaPath =
                        FindSystemJava();
                }

                if (string.IsNullOrWhiteSpace(javaPath) ||
                    !File.Exists(javaPath))
                {
                    throw new FileNotFoundException(
                        "CmlLib could not resolve the required Java runtime.");
                }

                WriteLog(
                    $"Java resolved to: {javaPath}");

                string javaVersion =
                    GetJavaVersion(
                        javaPath);

                WriteLog(
                    $"Java version: {javaVersion}");

                // -------------------------------------------------
                // STEP 5: MODS
                // -------------------------------------------------

                SetStatus(
                    "5/6 Installing performance mods...");

                await InstallPerformanceModsAsync(
                    minecraftVersion);

                // -------------------------------------------------
                // STEP 6: BUILD FABRIC PROCESS
                // -------------------------------------------------

                SetStatus(
                    $"6/6 Building Fabric {minecraftVersion} process...");

                WriteLog(
                    $"Building process for Fabric profile: {fabricVersion}");

                MLaunchOption launchOptions =
                    new MLaunchOption
                    {
                        Session = _session,
                        MaximumRamMb = ramMb,
                        MinimumRamMb = Math.Min(
                            1024,
                            ramMb),
                        JavaPath = javaPath
                    };

                if (!string.IsNullOrWhiteSpace(
                        _selectedServer))
                {
                    launchOptions.ServerIp =
                        _selectedServer;
                }

                /*
                 * CmlLib.Core 4.0.6 BuildProcessAsync returns
                 * System.Diagnostics.Process.
                 *
                 * It does NOT return ProcessWrapper.
                 */
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

                WriteDebugFile(
                    process,
                    minecraftVersion,
                    fabricVersion,
                    javaPath,
                    ramMb);

                process.EnableRaisingEvents = true;

                process.Exited +=
                    MinecraftProcess_Exited;

                WriteLog(
                    $"Minecraft executable: {process.StartInfo.FileName}");

                WriteLog(
                    $"Minecraft arguments: {process.StartInfo.Arguments}");

                WriteLog(
                    $"Working directory: {process.StartInfo.WorkingDirectory}");

                WriteLog(
                    "Starting Minecraft...");

                process.Start();

                WriteLog(
                    $"Minecraft process started. PID={process.Id}");

                SetStatus(
                    $"Minecraft running: Fabric {minecraftVersion}");

                /*
                 * Wait asynchronously so the launcher remains responsive.
                 */
                _ = MonitorMinecraftAsync(
                    process);
            }
            catch (Exception ex)
            {
                SetStatus(
                    "Launch failed.");

                WriteException(
                    "TOPU LAUNCH ERROR",
                    ex);

                MessageBox.Show(
                    "Minecraft failed to launch.\n\n" +
                    ex.Message +
                    "\n\nDetailed log:\n" +
                    _logFilePath,
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
        // FABRIC LOADER SELECTION
        // =========================================================

        private async Task<string> SelectFabricLoaderAsync(
            string minecraftVersion)
        {
            FabricInstaller installer =
                new FabricInstaller(Http);

            WriteLog(
                $"Querying Fabric loaders for {minecraftVersion}...");

            IReadOnlyCollection<FabricLoader> loaders =
                await installer.GetLoaders(
                    minecraftVersion);

            if (loaders == null ||
                loaders.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Fabric does not currently list a loader for Minecraft {minecraftVersion}.");
            }

            /*
             * First try the loader requested by the launcher.
             */
            FabricLoader? preferred =
                loaders.FirstOrDefault(
                    loader =>
                        string.Equals(
                            loader.Version,
                            PreferredFabricLoader,
                            StringComparison.OrdinalIgnoreCase) &&
                        loader.Stable);

            if (preferred?.Version != null)
            {
                WriteLog(
                    $"Using preferred stable Fabric Loader {preferred.Version}");

                return preferred.Version;
            }

            /*
             * Otherwise choose the newest stable loader returned by
             * Fabric. This makes the launcher work when 0.19.3 isn't
             * compatible with a future Minecraft version.
             */
            FabricLoader? stable =
                loaders
                    .Where(
                        loader =>
                            loader.Stable &&
                            !string.IsNullOrWhiteSpace(
                                loader.Version))
                    .OrderByDescending(
                        loader =>
                            loader.Build)
                    .FirstOrDefault();

            if (stable?.Version != null)
            {
                WriteLog(
                    $"Preferred loader unavailable. Using stable Fabric Loader {stable.Version}");

                return stable.Version;
            }

            FabricLoader? fallback =
                loaders
                    .Where(
                        loader =>
                            !string.IsNullOrWhiteSpace(
                                loader.Version))
                    .OrderByDescending(
                        loader =>
                            loader.Build)
                    .FirstOrDefault();

            if (fallback?.Version == null)
            {
                throw new InvalidOperationException(
                    $"No usable Fabric Loader was returned for {minecraftVersion}.");
            }

            WriteLog(
                $"Using Fabric Loader {fallback.Version}");

            return fallback.Version;
        }

        // =========================================================
        // PERFORMANCE MODS
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

            WriteLog(
                $"Mods directory: {modsFolder}");

            /*
             * Fabric API is a required base dependency for many
             * Fabric mods. It is not counted as one of the six
             * performance mods.
             */
            string? fabricApi =
                await DownloadModrinthProjectAsync(
                    "fabric-api",
                    minecraftVersion,
                    false);

            if (fabricApi != null)
            {
                WriteLog(
                    $"Installed Fabric API: {fabricApi}");
            }

            foreach (string mod in PerformanceMods)
            {
                try
                {
                    SetStatus(
                        $"Installing {mod}...");

                    /*
                     * Indium is intentionally handled separately below.
                     */
                    string? installed =
                        await DownloadModrinthProjectAsync(
                            mod,
                            minecraftVersion,
                            false);

                    if (installed == null)
                    {
                        WriteLog(
                            $"No compatible Fabric release found for {mod} on {minecraftVersion}.");

                        continue;
                    }

                    WriteLog(
                        $"Installed performance mod: {installed}");
                }
                catch (Exception ex)
                {
                    /*
                     * One optional optimization mod should not prevent
                     * Minecraft from launching.
                     */
                    WriteException(
                        $"OPTIONAL MOD ERROR: {mod}",
                        ex);
                }
            }

            /*
             * Indium:
             *
             * Indium is not compatible with Sodium 0.6+.
             * Modern Sodium already includes the relevant Fabric
             * Rendering API support.
             *
             * Therefore we only install Indium when it is actually
             * useful for the selected Sodium release.
             */
            await InstallIndiumIfCompatibleAsync(
                minecraftVersion);
        }

        private async Task InstallIndiumIfCompatibleAsync(
            string minecraftVersion)
        {
            try
            {
                string? sodiumFile =
                    FindInstalledModFile(
                        "sodium");

                if (sodiumFile == null)
                {
                    WriteLog(
                        "Sodium JAR was not found; skipping Indium.");
                    return;
                }

                string? sodiumVersion =
                    await ReadModVersionFromJarAsync(
                        sodiumFile);

                if (string.IsNullOrWhiteSpace(
                        sodiumVersion))
                {
                    WriteLog(
                        "Could not determine Sodium version; skipping Indium for safety.");
                    return;
                }

                WriteLog(
                    $"Installed Sodium version: {sodiumVersion}");

                if (IsSodium06OrNewer(
                        sodiumVersion))
                {
                    WriteLog(
                        "Indium skipped: Sodium 0.6+ already provides the required rendering support and Indium is incompatible.");
                    return;
                }

                string? indium =
                    await DownloadModrinthProjectAsync(
                        "indium",
                        minecraftVersion,
                        false);

                if (indium != null)
                {
                    WriteLog(
                        $"Installed compatible Indium: {indium}");
                }
            }
            catch (Exception ex)
            {
                WriteException(
                    "INDIUM CHECK ERROR",
                    ex);
            }
        }

        private bool IsSodium06OrNewer(
            string version)
        {
            /*
             * Handles versions such as:
             *
             * 0.6.0
             * 0.6.13
             * 0.7.0
             * 0.8.1
             * 0.9.0
             *
             * Also tolerates prefixes such as mc1.21.1-0.6.13.
             */
            string cleaned =
                version;

            int dash =
                cleaned.LastIndexOf('-');

            if (dash >= 0 &&
                dash + 1 < cleaned.Length)
            {
                cleaned =
                    cleaned[(dash + 1)..];
            }

            cleaned =
                cleaned.TrimStart(
                    'v',
                    'V');

            string[] parts =
                cleaned.Split(
                    '.',
                    StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 0)
                return false;

            if (!int.TryParse(
                    parts[0],
                    out int major))
                return false;

            if (major > 0)
                return true;

            if (parts.Length < 2)
                return false;

            return int.TryParse(
                       parts[1],
                       out int minor)
                   && minor >= 6;
        }

        // =========================================================
        // MODRINTH DOWNLOAD
        // =========================================================

        private async Task<string?> DownloadModrinthProjectAsync(
            string project,
            string minecraftVersion,
            bool showMessage)
        {
            string versionsUrl =
                "https://api.modrinth.com/v2/project/" +
                Uri.EscapeDataString(project) +
                "/version" +
                "?loaders=%5B%22fabric%22%5D" +
                "&game_versions=%5B%22" +
                Uri.EscapeDataString(minecraftVersion) +
                "%22%5D";

            WriteLog(
                $"Modrinth lookup: {project} / {minecraftVersion}");

            using HttpResponseMessage response =
                await Http.GetAsync(
                    versionsUrl);

            response.EnsureSuccessStatusCode();

            string json =
                await response.Content.ReadAsStringAsync();

            using JsonDocument document =
                JsonDocument.Parse(json);

            JsonElement root =
                document.RootElement;

            if (root.ValueKind !=
                JsonValueKind.Array ||
                root.GetArrayLength() == 0)
            {
                return null;
            }

            JsonElement selectedVersion =
                root[0];

            string title =
                project;

            if (selectedVersion.TryGetProperty(
                    "name",
                    out JsonElement nameElement))
            {
                title =
                    nameElement.GetString()
                    ?? project;
            }

            if (!selectedVersion.TryGetProperty(
                    "files",
                    out JsonElement files) ||
                files.ValueKind !=
                JsonValueKind.Array ||
                files.GetArrayLength() == 0)
            {
                return null;
            }

            JsonElement? selectedFile =
                null;

            foreach (JsonElement file in
                     files.EnumerateArray())
            {
                bool primary =
                    file.TryGetProperty(
                        "primary",
                        out JsonElement primaryElement) &&
                    primaryElement.ValueKind ==
                        JsonValueKind.True;

                if (primary)
                {
                    selectedFile =
                        file;
                    break;
                }
            }

            if (selectedFile == null)
            {
                selectedFile =
                    files[0];
            }

            JsonElement fileElement =
                selectedFile.Value;

            if (!fileElement.TryGetProperty(
                    "url",
                    out JsonElement urlElement))
            {
                throw new InvalidOperationException(
                    $"Modrinth did not provide a download URL for {project}.");
            }

            string downloadUrl =
                urlElement.GetString() ?? "";

            if (string.IsNullOrWhiteSpace(
                    downloadUrl))
            {
                throw new InvalidOperationException(
                    $"Modrinth returned an empty download URL for {project}.");
            }

            string filename =
                fileElement.TryGetProperty(
                    "filename",
                    out JsonElement filenameElement)
                ? filenameElement.GetString()
                    ?? $"{project}.jar"
                : $"{project}.jar";

            filename =
                SanitizeFileName(
                    filename);

            string modsFolder =
                Path.Combine(
                    _gamePath,
                    "mods");

            Directory.CreateDirectory(
                modsFolder);

            string destination =
                Path.Combine(
                    modsFolder,
                    filename);

            await DownloadFileWithRetryAsync(
                downloadUrl,
                destination);

            if (!File.Exists(destination))
            {
                throw new IOException(
                    $"Mod file was not created: {destination}");
            }

            long length =
                new FileInfo(destination).Length;

            if (length <= 0)
            {
                try
                {
                    File.Delete(destination);
                }
                catch
                {
                }

                throw new IOException(
                    $"Modrinth returned a 0-byte file for {project}.");
            }

            WriteLog(
                $"Mod installed: {title} -> {destination} ({length:N0} bytes)");

            if (showMessage)
            {
                MessageBox.Show(
                    $"{title} installed successfully.",
                    "Modrinth",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }

            return filename;
        }

        private async Task DownloadFileWithRetryAsync(
            string url,
            string destination)
        {
            string tempFile =
                destination + ".download";

            Exception? lastException = null;

            for (int attempt = 1;
                 attempt <= 4;
                 attempt++)
            {
                try
                {
                    WriteLog(
                        $"Downloading {Path.GetFileName(destination)} " +
                        $"(attempt {attempt}/4)");

                    if (File.Exists(tempFile))
                    {
                        try
                        {
                            File.Delete(tempFile);
                        }
                        catch
                        {
                        }
                    }

                    using HttpResponseMessage response =
                        await Http.GetAsync(
                            url,
                            HttpCompletionOption.ResponseHeadersRead);

                    response.EnsureSuccessStatusCode();

                    await using Stream input =
                        await response.Content.ReadAsStreamAsync();

                    await using FileStream output =
                        new FileStream(
                            tempFile,
                            FileMode.Create,
                            FileAccess.Write,
                            FileShare.None,
                            81920,
                            FileOptions.SequentialScan);

                    await input.CopyToAsync(
                        output);

                    await output.FlushAsync();

                    long size =
                        new FileInfo(tempFile).Length;

                    if (size <= 0)
                    {
                        throw new IOException(
                            "The server returned 0 bytes.");
                    }

                    if (File.Exists(destination))
                    {
                        File.Delete(destination);
                    }

                    File.Move(
                        tempFile,
                        destination);

                    return;
                }
                catch (Exception ex)
                {
                    lastException = ex;

                    WriteLog(
                        $"Download failed: {ex.Message}");

                    try
                    {
                        if (File.Exists(tempFile))
                            File.Delete(tempFile);
                    }
                    catch
                    {
                    }

                    if (attempt < 4)
                    {
                        await Task.Delay(
                            TimeSpan.FromSeconds(
                                attempt * 2));
                    }
                }
            }

            throw new IOException(
                $"Failed to download {Path.GetFileName(destination)} after multiple attempts.",
                lastException);
        }

        // =========================================================
        // MOD FILE HELPERS
        // =========================================================

        private string? FindInstalledModFile(
            string projectName)
        {
            string modsFolder =
                Path.Combine(
                    _gamePath,
                    "mods");

            if (!Directory.Exists(modsFolder))
                return null;

            string[] files =
                Directory.GetFiles(
                    modsFolder,
                    "*.jar");

            string project =
                projectName.ToLowerInvariant();

            return files.FirstOrDefault(
                file =>
                    Path.GetFileName(file)
                        .ToLowerInvariant()
                        .Contains(project));
        }

        private async Task<string?> ReadModVersionFromJarAsync(
            string jarPath)
        {
            /*
             * We intentionally avoid adding another ZIP library.
             * A JAR is a ZIP archive, and .NET provides ZipArchive.
             */
            try
            {
                using FileStream stream =
                    File.OpenRead(jarPath);

                using System.IO.Compression.ZipArchive archive =
                    new System.IO.Compression.ZipArchive(
                        stream,
                        System.IO.Compression.ZipArchiveMode.Read);

                var entry =
                    archive.GetEntry(
                        "fabric.mod.json");

                if (entry == null)
                    return null;

                using Stream entryStream =
                    entry.Open();

                using StreamReader reader =
                    new StreamReader(entryStream);

                string json =
                    await reader.ReadToEndAsync();

                using JsonDocument document =
                    JsonDocument.Parse(json);

                if (document.RootElement.TryGetProperty(
                        "version",
                        out JsonElement versionElement))
                {
                    return
                        versionElement.GetString();
                }
            }
            catch
            {
            }

            return null;
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
                            e.Name ?? "Installing";

                        if (e.TotalTasks > 0)
                        {
                            StatusText.Text =
                                $"{name} " +
                                $"({e.ProgressedTasks}/{e.TotalTasks})";
                        }
                        else
                        {
                            StatusText.Text =
                                name;
                        }
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
                            double percent =
                                e.ProgressedBytes *
                                100.0 /
                                e.TotalBytes;

                            StatusText.Text =
                                $"Downloading {percent:0}%";
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
        // JAVA
        // =========================================================

        private string? FindSystemJava()
        {
            string? javaHome =
                Environment.GetEnvironmentVariable(
                    "JAVA_HOME");

            if (!string.IsNullOrWhiteSpace(
                    javaHome))
            {
                string java =
                    Path.Combine(
                        javaHome,
                        "bin",
                        "java.exe");

                if (File.Exists(java))
                    return java;
            }

            string? path =
                Environment.GetEnvironmentVariable(
                    "PATH");

            if (string.IsNullOrWhiteSpace(path))
                return null;

            foreach (string directory in
                     path.Split(
                         Path.PathSeparator,
                         StringSplitOptions.RemoveEmptyEntries))
            {
                string java =
                    Path.Combine(
                        directory.Trim(),
                        "java.exe");

                if (File.Exists(java))
                {
                    return java;
                }
            }

            return null;
        }

        private string GetJavaVersion(
            string javaPath)
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

                using Process? process =
                    Process.Start(info);

                if (process == null)
                    return "Unknown";

                string stdout =
                    process.StandardOutput.ReadToEnd();

                string stderr =
                    process.StandardError.ReadToEnd();

                process.WaitForExit();

                return
                    (stdout +
                     Environment.NewLine +
                     stderr)
                    .Trim();
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // =========================================================
        // PROCESS
        // =========================================================

        private void MinecraftProcess_Exited(
            object? sender,
            EventArgs e)
        {
            try
            {
                if (_minecraftProcess == null)
                    return;

                int exitCode =
                    _minecraftProcess.ExitCode;

                AppendGameLog(
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
            Process process)
        {
            try
            {
                await process.WaitForExitAsync();

                int exitCode =
                    process.ExitCode;

                AppendGameLog(
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
                                $"Log:\n{_logFilePath}",
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
            string minecraftVersion,
            string fabricVersion,
            string javaPath,
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
                    $"Java: {javaPath}" +
                    Environment.NewLine +
                    $"RAM: {ramMb} MB" +
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
                    $"Working Directory:" +
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
        // UI STATUS
        // =========================================================

        private void SetStatus(
            string text)
        {
            try
            {
                if (Dispatcher.CheckAccess())
                {
                    StatusText.Text = text;
                }
                else
                {
                    Dispatcher.Invoke(
                        () =>
                        {
                            StatusText.Text = text;
                        });
                }
            }
            catch
            {
            }
        }

        // =========================================================
        // FILE NAME
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
    }
}
```
