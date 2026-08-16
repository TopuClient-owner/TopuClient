using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.ModLoaders.FabricMC;
using CmlLib.Core.ProcessBuilder;

namespace TopuLauncher
{
    public partial class MainWindow : Window
    {
        private MSession? _session;
        private ProcessWrapper? _minecraftProcess;

        private static readonly HttpClient Http = new HttpClient(
            new HttpClientHandler
            {
                AllowAutoRedirect = true
            });

        private readonly string _gamePath;
        private readonly string _usernameFile;
        private readonly string _logFile;

        private const string DefaultVersion = "1.21.1";

        /*
         * IMPORTANT:
         *
         * Keep this as a loader version that exists on Fabric's
         * official metadata server.
         *
         * CmlLib's FabricInstaller will handle the Fabric profile,
         * libraries and version JSON.
         */
        private const string FabricLoaderVersion = "0.19.3";

        public MainWindow()
        {
            InitializeComponent();

            _gamePath = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.ApplicationData),
                ".topuclient");

            Directory.CreateDirectory(_gamePath);

            _usernameFile = Path.Combine(
                _gamePath,
                "username.txt");

            _logFile = Path.Combine(
                _gamePath,
                "topu-minecraft.log");

            LoadUsername();

            if (RamLabel != null)
            {
                RamLabel.Text =
                    $"{(int)RamSlider.Value}GB";
            }

            WriteLog("Topu Client initialized.");
        }

        // =========================================================
        // LOGGING
        // =========================================================

        private void WriteLog(string text)
        {
            try
            {
                Directory.CreateDirectory(_gamePath);

                File.AppendAllText(
                    _logFile,
                    $"[{DateTime.Now:HH:mm:ss}] {text}" +
                    Environment.NewLine);
            }
            catch
            {
                // Logging must never crash launcher.
            }
        }

        private void StartLaunchLog()
        {
            try
            {
                File.WriteAllText(
                    _logFile,
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

        private void WriteException(
            string title,
            Exception ex)
        {
            WriteLog("");
            WriteLog($"===== {title} =====");
            WriteLog(ex.ToString());
        }

        // =========================================================
        // USERNAME
        // =========================================================

        private void LoadUsername()
        {
            try
            {
                if (!File.Exists(_usernameFile))
                    return;

                string username =
                    File.ReadAllText(_usernameFile).Trim();

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

        private void SaveUsername(
            string username)
        {
            try
            {
                File.WriteAllText(
                    _usernameFile,
                    username);
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

        // =========================================================
        // PROFILE
        // =========================================================

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
                $"Profile saved: version={version}, RAM={ram}GB");

            MessageBox.Show(
                "Profile settings saved.",
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
                MsLoginBtn.IsEnabled = false;

                MessageBox.Show(
                    "Microsoft authentication is not configured in this build.\n\n" +
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
        // SERVER BUTTONS
        // =========================================================

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

        // =========================================================
        // MODRINTH
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

                string searchUrl =
                    "https://api.modrinth.com/v2/search" +
                    "?query=" +
                    Uri.EscapeDataString(query) +
                    "&facets=%5B%5B%22project_type%3Amod%22%5D%5D";

                using HttpResponseMessage response =
                    await Http.GetAsync(searchUrl);

                response.EnsureSuccessStatusCode();

                using var document =
                    await System.Text.Json.JsonDocument.ParseAsync(
                        await response.Content.ReadAsStreamAsync());

                var root =
                    document.RootElement;

                if (!root.TryGetProperty(
                        "hits",
                        out var hits) ||
                    hits.ValueKind !=
                        System.Text.Json.JsonValueKind.Array ||
                    hits.GetArrayLength() == 0)
                {
                    ModSearchStatus.Text =
                        "No mod found.";

                    return;
                }

                var hit =
                    hits[0];

                string projectId =
                    hit.TryGetProperty(
                        "project_id",
                        out var projectIdElement)
                    ? projectIdElement.GetString() ?? ""
                    : "";

                string title =
                    hit.TryGetProperty(
                        "title",
                        out var titleElement)
                    ? titleElement.GetString() ?? query
                    : query;

                if (string.IsNullOrWhiteSpace(projectId))
                {
                    throw new InvalidOperationException(
                        "Modrinth did not return a project ID.");
                }

                string minecraftVersion =
                    GetSelectedVersion();

                string versionsUrl =
                    "https://api.modrinth.com/v2/project/" +
                    Uri.EscapeDataString(projectId) +
                    "/version" +
                    "?loaders=%5B%22fabric%22%5D" +
                    "&game_versions=%5B%22" +
                    Uri.EscapeDataString(
                        minecraftVersion) +
                    "%22%5D";

                using HttpResponseMessage versionsResponse =
                    await Http.GetAsync(versionsUrl);

                versionsResponse.EnsureSuccessStatusCode();

                using var versionsDocument =
                    await System.Text.Json.JsonDocument.ParseAsync(
                        await versionsResponse.Content.ReadAsStreamAsync());

                var versions =
                    versionsDocument.RootElement;

                if (versions.ValueKind !=
                        System.Text.Json.JsonValueKind.Array ||
                    versions.GetArrayLength() == 0)
                {
                    ModSearchStatus.Text =
                        $"No Fabric build of {title} exists for {minecraftVersion}.";

                    return;
                }

                var version =
                    versions[0];

                if (!version.TryGetProperty(
                        "files",
                        out var files) ||
                    files.ValueKind !=
                        System.Text.Json.JsonValueKind.Array ||
                    files.GetArrayLength() == 0)
                {
                    throw new InvalidOperationException(
                        "Modrinth returned no files.");
                }

                string? downloadUrl = null;
                string filename =
                    SanitizeFileName(
                        title) + ".jar";

                foreach (var file in
                         files.EnumerateArray())
                {
                    if (!file.TryGetProperty(
                            "url",
                            out var urlElement))
                    {
                        continue;
                    }

                    string? url =
                        urlElement.GetString();

                    if (string.IsNullOrWhiteSpace(url))
                        continue;

                    bool primary =
                        file.TryGetProperty(
                            "primary",
                            out var primaryElement) &&
                        primaryElement.ValueKind ==
                            System.Text.Json.JsonValueKind.True;

                    if (file.TryGetProperty(
                            "filename",
                            out var filenameElement))
                    {
                        filename =
                            filenameElement.GetString()
                            ?? filename;
                    }

                    downloadUrl = url;

                    if (primary)
                        break;
                }

                if (string.IsNullOrWhiteSpace(downloadUrl))
                {
                    throw new InvalidOperationException(
                        "No download URL was returned.");
                }

                string modsPath =
                    Path.Combine(
                        _gamePath,
                        "mods");

                Directory.CreateDirectory(
                    modsPath);

                string destination =
                    Path.Combine(
                        modsPath,
                        SanitizeFileName(filename));

                ModSearchStatus.Text =
                    $"Downloading {title}...";

                byte[] data =
                    await Http.GetByteArrayAsync(
                        downloadUrl);

                await File.WriteAllBytesAsync(
                    destination,
                    data);

                ModSearchStatus.Text =
                    $"Installed: {title}";

                WriteLog(
                    $"Installed Modrinth mod: {title}");

                MessageBox.Show(
                    $"{title} installed successfully.",
                    "Modrinth",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                WriteException(
                    "MODRINTH ERROR",
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
                    "===== TOPU CLIENT MINECRAFT LAUNCH =====");

                WriteLog(
                    $"Minecraft: {minecraftVersion}");

                WriteLog(
                    $"Fabric Loader: {FabricLoaderVersion}");

                WriteLog(
                    $"RAM: {ram} MB");

                WriteLog(
                    $"Game directory: {_gamePath}");

                // -------------------------------------------------
                // OFFLINE SESSION
                // -------------------------------------------------

                if (AuthTypeBox.SelectedIndex != 0)
                {
                    throw new InvalidOperationException(
                        "Microsoft login is not configured yet.");
                }

                string username =
                    UsernameInput.Text.Trim();

                if (string.IsNullOrWhiteSpace(username))
                    username = "TopuPlayer";

                SaveUsername(username);

                _session =
                    MSession.CreateOfflineSession(
                        username);

                WriteLog(
                    $"Offline username: {username}");

                // -------------------------------------------------
                // CMLLIB
                // -------------------------------------------------

                StatusText.Text =
                    $"Preparing Minecraft {minecraftVersion}...";

                MinecraftPath path =
                    new MinecraftPath(
                        _gamePath);

                MinecraftLauncher launcher =
                    new MinecraftLauncher(
                        path);

                WriteLog(
                    "CmlLib launcher created.");

                // -------------------------------------------------
                // VANILLA INSTALL
                // -------------------------------------------------

                StatusText.Text =
                    $"Installing Minecraft {minecraftVersion}...";

                WriteLog(
                    "Installing Minecraft files...");

                /*
                 * CmlLib handles the Minecraft version installation.
                 *
                 * This also uses its normal file extractors.
                 */
                await launcher.InstallAsync(
                    minecraftVersion);

                WriteLog(
                    "Minecraft installation completed.");

                // -------------------------------------------------
                // FABRIC
                // -------------------------------------------------

                StatusText.Text =
                    $"Installing Fabric {FabricLoaderVersion}...";

                WriteLog(
                    "Installing Fabric through CmlLib FabricInstaller...");

                FabricInstaller fabricInstaller =
                    new FabricInstaller(
                        Http);

                string fabricVersionName =
                    await fabricInstaller.Install(
                        minecraftVersion,
                        FabricLoaderVersion,
                        path);

                WriteLog(
                    $"Fabric installed: {fabricVersionName}");

                // -------------------------------------------------
                // GET FABRIC VERSION
                // -------------------------------------------------

                StatusText.Text =
                    "Preparing Fabric libraries...";

                /*
                 * Ask CmlLib to load the Fabric version it just
                 * installed instead of manually parsing JSON.
                 */
                var fabricVersion =
                    await launcher.GetVersionAsync(
                        fabricVersionName);

                WriteLog(
                    $"Fabric profile loaded: {fabricVersionName}");

                // -------------------------------------------------
                // JAVA
                // -------------------------------------------------

                StatusText.Text =
                    "Checking Java runtime...";

                /*
                 * IMPORTANT:
                 *
                 * CmlLib 4.0.6 has GetJavaPath(IVersion).
                 *
                 * Its Java extractor/path resolver is part of the
                 * launcher system. We do NOT manually construct
                 * Java paths here.
                 *
                 * If the required Mojang Java runtime is absent,
                 * CmlLib's installation/extraction system can provide
                 * it from the Minecraft runtime metadata.
                 */
                string? javaPath =
                    launcher.GetJavaPath(
                        fabricVersion);

                if (string.IsNullOrWhiteSpace(javaPath) ||
                    !File.Exists(javaPath))
                {
                    WriteLog(
                        "Required Java runtime was not found after installation.");

                    /*
                     * Re-run installation through CmlLib so its
                     * Java extractor gets a chance to populate the
                     * runtime.
                     */
                    StatusText.Text =
                        "Installing required Java runtime...";

                    await launcher.InstallAsync(
                        fabricVersion);

                    javaPath =
                        launcher.GetJavaPath(
                            fabricVersion);
                }

                if (string.IsNullOrWhiteSpace(javaPath) ||
                    !File.Exists(javaPath))
                {
                    throw new FileNotFoundException(
                        "CmlLib could not locate the required Java runtime.",
                        javaPath);
                }

                WriteLog(
                    $"Java: {javaPath}");

                // -------------------------------------------------
                // LAUNCH OPTIONS
                // -------------------------------------------------

                MLaunchOption options =
                    new MLaunchOption
                    {
                        Session = _session,
                        JavaPath = javaPath,
                        MaximumRamMb = ram,
                        Path = path,
                        GameLauncherName = "Topu Client",
                        GameLauncherVersion = "1.0"
                    };

                WriteLog(
                    "Building Minecraft process...");

                StatusText.Text =
                    "Building Minecraft process...";

                /*
                 * VERIFIED CMLLIB 4.0.6:
                 *
                 * BuildProcessAsync returns System.Diagnostics.Process.
                 */
                Process process =
                    await launcher.BuildProcessAsync(
                        fabricVersionName,
                        options);

                if (process == null)
                {
                    throw new InvalidOperationException(
                        "CmlLib returned a null Minecraft process.");
                }

                // -------------------------------------------------
                // WRAPPER
                // -------------------------------------------------

                /*
                 * ProcessWrapper is a CmlLib wrapper around the
                 * System.Diagnostics.Process.
                 */
                ProcessWrapper wrapper =
                    new ProcessWrapper(
                        process);

                _minecraftProcess =
                    wrapper;

                wrapper.OutputReceived +=
                    Minecraft_OutputReceived;

                wrapper.Exited +=
                    Minecraft_Exited;

                // -------------------------------------------------
                // LOG COMMAND
                // -------------------------------------------------

                WriteLog(
                    $"Executable: {process.StartInfo.FileName}");

                WriteLog(
                    $"Arguments: {process.StartInfo.Arguments}");

                WriteLog(
                    $"Working Directory: {process.StartInfo.WorkingDirectory}");

                WriteLog(
                    $"Java: {javaPath}");

                WriteLog(
                    "Starting Minecraft...");

                StatusText.Text =
                    $"Starting Fabric {minecraftVersion}...";

                // -------------------------------------------------
                // START
                // -------------------------------------------------

                wrapper.StartWithEvents();

                WriteLog(
                    $"Minecraft process started. PID: {process.Id}");

                StatusText.Text =
                    $"Minecraft running as {username}";

                _ = MonitorMinecraftAsync(
                    wrapper);
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
                    "\n\nLog file:\n" +
                    _logFile,
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
        // MINECRAFT OUTPUT
        // =========================================================

        private void Minecraft_OutputReceived(
            object? sender,
            string output)
        {
            if (string.IsNullOrWhiteSpace(output))
                return;

            AppendMinecraftLog(
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
                    "EXIT EVENT ERROR",
                    ex);
            }
        }

        private async Task MonitorMinecraftAsync(
            ProcessWrapper wrapper)
        {
            try
            {
                int exitCode =
                    await wrapper.WaitForExitTaskAsync();

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
                                $"Log:\n{_logFile}",
                                "Minecraft",
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
        // MINECRAFT LOG
        // =========================================================

        private void AppendMinecraftLog(
            string message)
        {
            try
            {
                File.AppendAllText(
                    _logFile,
                    $"[{DateTime.Now:HH:mm:ss}] " +
                    message +
                    Environment.NewLine);
            }
            catch
            {
            }
        }

        // =========================================================
        // FILENAME SANITIZER
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
