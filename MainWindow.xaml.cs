using System;
using System.Diagnostics;
using System.IO;
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
using CmlLib.Core.ModLoaders.FabricMC;
using CmlLib.Core.ProcessBuilder;

namespace TopuLauncher
{
    public partial class MainWindow : Window
    {
        private MSession? _session;
        private Process? _minecraftProcess;

        private static readonly HttpClient _httpClient =
            new HttpClient(
                new HttpClientHandler
                {
                    AllowAutoRedirect = true
                })
            {
                DefaultRequestHeaders =
                {
                    {
                        "User-Agent",
                        "TopuClient/1.0"
                    }
                }
            };

        private readonly string _gamePath;
        private readonly string _configFilePath;
        private readonly string _logFilePath;

        private const string DefaultMinecraftVersion = "1.21.1";

        /*
         * These are the versions shown by your XAML.
         *
         * Fabric is NOT hard-coded to 0.19.3 anymore.
         * CmlLib's FabricInstaller asks Fabric for a compatible
         * loader and installs it properly.
         */
        private static readonly string[] SupportedMinecraftVersions =
        {
            "1.21.1",
            "1.21.4",
            "1.21.8",
            "1.21.11",
            "26.1.2",
            "26.2"
        };

        public MainWindow()
        {
            InitializeComponent();

            _gamePath =
                Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.ApplicationData),
                    ".topuclient");

            Directory.CreateDirectory(_gamePath);

            _configFilePath =
                Path.Combine(
                    _gamePath,
                    "username.txt");

            _logFilePath =
                Path.Combine(
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
                    $"[{DateTime.Now:HH:mm:ss}] {message}" +
                    Environment.NewLine);
            }
            catch
            {
                // Logging must never crash the launcher.
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

        private void StartNewLaunchLog()
        {
            try
            {
                Directory.CreateDirectory(_gamePath);

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

        private void AppendMinecraftLog(string message)
        {
            try
            {
                File.AppendAllText(
                    _logFilePath,
                    $"[{DateTime.Now:HH:mm:ss}] {message}" +
                    Environment.NewLine);
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
                if (!File.Exists(_configFilePath))
                    return;

                string username =
                    File.ReadAllText(
                        _configFilePath).Trim();

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
                version = DefaultMinecraftVersion;

            return version;
        }

        private bool IsSupportedMinecraftVersion(
            string version)
        {
            foreach (string supported in
                     SupportedMinecraftVersions)
            {
                if (string.Equals(
                        supported,
                        version,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
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
                $"Profile saved. Minecraft={version}, RAM={ram}GB");

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
                MsLoginBtn.IsEnabled = false;

                StatusText.Text =
                    "Microsoft authentication is not configured.";

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

                MessageBox.Show(
                    ex.Message,
                    "Microsoft Login Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
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

            string server =
                button.Tag?.ToString() ?? "";

            if (string.IsNullOrWhiteSpace(server))
                return;

            StatusText.Text =
                $"Server selected: {server}";

            WriteLog(
                $"Server selected: {server}");

            /*
             * The current XAML only has a queue/select button.
             *
             * When launching, you can assign this value to:
             *
             * MLaunchOption.ServerIp
             *
             * if you want automatic server connection.
             */
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

                string minecraftVersion =
                    GetSelectedMinecraftVersion();

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
                    hits.ValueKind != JsonValueKind.Array ||
                    hits.GetArrayLength() == 0)
                {
                    ModSearchStatus.Text =
                        "No mod found.";

                    return;
                }

                JsonElement hit =
                    hits[0];

                string projectId =
                    GetJsonString(
                        hit,
                        "project_id");

                string title =
                    GetJsonString(
                        hit,
                        "title");

                if (string.IsNullOrWhiteSpace(title))
                    title = query;

                if (string.IsNullOrWhiteSpace(projectId))
                {
                    throw new InvalidOperationException(
                        "Modrinth did not return a project ID.");
                }

                string versionsUrl =
                    "https://api.modrinth.com/v2/project/" +
                    Uri.EscapeDataString(projectId) +
                    "/version" +
                    "?loaders=%5B%22fabric%22%5D" +
                    $"&game_versions=%5B%22{Uri.EscapeDataString(minecraftVersion)}%22%5D";

                string versionsResponse =
                    await _httpClient.GetStringAsync(
                        versionsUrl);

                using JsonDocument versionsDocument =
                    JsonDocument.Parse(
                        versionsResponse);

                JsonElement versions =
                    versionsDocument.RootElement;

                if (versions.ValueKind !=
                    JsonValueKind.Array ||
                    versions.GetArrayLength() == 0)
                {
                    ModSearchStatus.Text =
                        $"No Fabric build of {title} exists for {minecraftVersion}.";

                    return;
                }

                JsonElement version =
                    versions[0];

                if (!version.TryGetProperty(
                        "files",
                        out JsonElement files) ||
                    files.ValueKind !=
                    JsonValueKind.Array ||
                    files.GetArrayLength() == 0)
                {
                    throw new InvalidOperationException(
                        "Modrinth returned no downloadable files.");
                }

                string downloadUrl = "";
                string filename =
                    SanitizeFileName(title) + ".jar";

                foreach (JsonElement file in
                         files.EnumerateArray())
                {
                    string url =
                        GetJsonString(
                            file,
                            "url");

                    string possibleFilename =
                        GetJsonString(
                            file,
                            "filename");

                    bool primary =
                        file.TryGetProperty(
                            "primary",
                            out JsonElement primaryElement) &&
                        primaryElement.ValueKind ==
                        JsonValueKind.True;

                    if (!string.IsNullOrWhiteSpace(url))
                    {
                        if (!string.IsNullOrWhiteSpace(
                                possibleFilename))
                        {
                            filename =
                                possibleFilename;
                        }

                        downloadUrl =
                            url;

                        if (primary)
                            break;
                    }
                }

                if (string.IsNullOrWhiteSpace(downloadUrl))
                {
                    throw new InvalidOperationException(
                        "Modrinth did not provide a download URL.");
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
                        SanitizeFileName(filename));

                ModSearchStatus.Text =
                    $"Downloading {title}...";

                byte[] data =
                    await _httpClient.GetByteArrayAsync(
                        downloadUrl);

                await File.WriteAllBytesAsync(
                    destination,
                    data);

                ModSearchStatus.Text =
                    $"Installed: {title}";

                WriteLog(
                    $"Installed Modrinth mod: {title}");

                MessageBox.Show(
                    $"{title} was installed successfully.",
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

        private static string GetJsonString(
            JsonElement element,
            string property)
        {
            if (!element.TryGetProperty(
                    property,
                    out JsonElement value))
            {
                return "";
            }

            return value.ValueKind ==
                   JsonValueKind.String
                ? value.GetString() ?? ""
                : "";
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

                if (!IsSupportedMinecraftVersion(
                        minecraftVersion))
                {
                    throw new InvalidOperationException(
                        $"Minecraft version {minecraftVersion} is not in the supported version list.");
                }

                // -------------------------------------------------
                // AUTH
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
                // JAVA
                // -------------------------------------------------

                StatusText.Text =
                    "Checking Java 21...";

                string? javaPath =
                    FindJava21();

                if (string.IsNullOrWhiteSpace(javaPath))
                {
                    throw new FileNotFoundException(
                        "Java 21 was not found.\n\n" +
                        "Install Java 21 or place a Java 21 runtime at:\n" +
                        Path.Combine(
                            _gamePath,
                            "runtime",
                            "java21",
                            "bin",
                            "java.exe"));
                }

                WriteLog(
                    $"Java: {javaPath}");

                string javaVersion =
                    GetJavaVersion(
                        javaPath);

                WriteLog(
                    $"Java version: {javaVersion}");

                // -------------------------------------------------
                // CMLLIB PATH
                // -------------------------------------------------

                MinecraftPath minecraftPath =
                    new MinecraftPath(
                        _gamePath);

                MinecraftLauncher launcher =
                    new MinecraftLauncher(
                        minecraftPath);

                /*
                 * CmlLib 4.0.6 exposes these events directly.
                 *
                 * We intentionally use lambda handlers with
                 * inferred args, so the code does NOT depend on
                 * manually declaring InstallerProgressChangedEventArgs.
                 */

                launcher.FileProgressChanged +=
                    (sender, args) =>
                    {
                        Dispatcher.Invoke(
                            () =>
                            {
                                StatusText.Text =
                                    $"{args.Name} " +
                                    $"{args.ProgressedTasks}/{args.TotalTasks}";
                            });
                    };

                launcher.ByteProgressChanged +=
                    (sender, args) =>
                    {
                        Dispatcher.Invoke(
                            () =>
                            {
                                if (args.TotalBytes > 0)
                                {
                                    double percent =
                                        args.ProgressedBytes *
                                        100.0 /
                                        args.TotalBytes;

                                    StatusText.Text =
                                        $"Downloading {percent:0}%";
                                }
                                else
                                {
                                    StatusText.Text =
                                        $"Downloading {args.ProgressedBytes:N0} bytes";
                                }
                            });
                    };

                // -------------------------------------------------
                // VANILLA INSTALL
                // -------------------------------------------------

                StatusText.Text =
                    $"Installing Minecraft {minecraftVersion}...";

                WriteLog(
                    $"Installing vanilla Minecraft {minecraftVersion}...");

                await launcher.InstallAsync(
                    minecraftVersion);

                WriteLog(
                    "Minecraft installation completed.");

                // -------------------------------------------------
                // FABRIC INSTALL
                // -------------------------------------------------

                StatusText.Text =
                    $"Finding Fabric loader for {minecraftVersion}...";

                FabricInstaller fabricInstaller =
                    new FabricInstaller(
                        _httpClient);

                FabricLoader? fabricLoader =
                    await fabricInstaller.GetFirstLoader(
                        minecraftVersion);

                if (fabricLoader == null)
                {
                    throw new InvalidOperationException(
                        $"Fabric does not currently provide a loader for Minecraft {minecraftVersion}.");
                }

                if (string.IsNullOrWhiteSpace(
                        fabricLoader.Version))
                {
                    throw new InvalidOperationException(
                        $"Fabric returned an invalid loader for Minecraft {minecraftVersion}.");
                }

                string loaderVersion =
                    fabricLoader.Version;

                WriteLog(
                    $"Fabric loader selected: {loaderVersion}");

                StatusText.Text =
                    $"Installing Fabric {loaderVersion}...";

                string fabricVersionName =
                    await fabricInstaller.Install(
                        minecraftVersion,
                        loaderVersion,
                        minecraftPath);

                if (string.IsNullOrWhiteSpace(
                        fabricVersionName))
                {
                    throw new InvalidOperationException(
                        "Fabric installer returned an empty version name.");
                }

                WriteLog(
                    $"Fabric version installed: {fabricVersionName}");

                // -------------------------------------------------
                // LAUNCH OPTIONS
                // -------------------------------------------------

                MLaunchOption launchOptions =
                    new MLaunchOption
                    {
                        Session = _session,

                        MaximumRamMb =
                            ramMb,

                        MinimumRamMb =
                            Math.Min(
                                1024,
                                ramMb),

                        JavaPath =
                            javaPath,

                        Path =
                            minecraftPath,

                        GameLauncherName =
                            "Topu Client",

                        GameLauncherVersion =
                            "1.0"
                    };

                // -------------------------------------------------
                // BUILD PROCESS
                // -------------------------------------------------

                StatusText.Text =
                    "Building Minecraft process...";

                WriteLog(
                    "Building Minecraft process...");

                /*
                 * IMPORTANT:
                 *
                 * In CmlLib.Core 4.0.6:
                 *
                 * BuildProcessAsync returns System.Diagnostics.Process.
                 *
                 * It does NOT return ProcessWrapper.
                 */

                Process process =
                    await launcher.BuildProcessAsync(
                        fabricVersionName,
                        launchOptions);

                if (process == null)
                {
                    throw new InvalidOperationException(
                        "CmlLib returned a null Minecraft process.");
                }

                _minecraftProcess =
                    process;

                // -------------------------------------------------
                // PROCESS SETTINGS
                // -------------------------------------------------

                process.EnableRaisingEvents =
                    true;

                process.OutputDataReceived +=
                    Minecraft_OutputDataReceived;

                process.ErrorDataReceived +=
                    Minecraft_ErrorDataReceived;

                process.Exited +=
                    Minecraft_ProcessExited;

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
                    loaderVersion,
                    ramMb);

                // -------------------------------------------------
                // START
                // -------------------------------------------------

                StatusText.Text =
                    $"Starting Fabric {minecraftVersion}...";

                WriteLog(
                    "Starting Minecraft process...");

                /*
                 * CmlLib's BuildProcessAsync creates the Process.
                 *
                 * Starting it manually is appropriate here because
                 * the returned object is System.Diagnostics.Process.
                 */

                process.Start();

                /*
                 * Begin reading Minecraft's console output.
                 *
                 * These calls only work because the process builder
                 * configures redirected streams.
                 */
                try
                {
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                }
                catch (Exception streamEx)
                {
                    WriteLog(
                        $"Could not begin redirected output: {streamEx.Message}");
                }

                WriteLog(
                    $"Minecraft process started. PID: {process.Id}");

                StatusText.Text =
                    $"Topu Client running as {username}";

                // -------------------------------------------------
                // MONITOR
                // -------------------------------------------------

                _ = MonitorMinecraftProcessAsync(
                    process);
            }
            catch (Exception ex)
            {
                StatusText.Text =
                    "Launch Failed!";

                WriteException(
                    "TOPU CLIENT LAUNCH ERROR",
                    ex);

                MessageBox.Show(
                    "Minecraft failed to start.\n\n" +
                    ex.Message +
                    "\n\nDetailed log:\n" +
                    _logFilePath,
                    "Topu Client Launch Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                LaunchBtn.IsEnabled =
                    true;
            }
        }

        // =========================================================
        // MINECRAFT OUTPUT
        // =========================================================

        private void Minecraft_OutputDataReceived(
            object sender,
            DataReceivedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(e.Data))
                return;

            AppendMinecraftLog(
                "[STDOUT] " + e.Data);
        }

        private void Minecraft_ErrorDataReceived(
            object sender,
            DataReceivedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(e.Data))
                return;

            AppendMinecraftLog(
                "[STDERR] " + e.Data);
        }

        private void Minecraft_ProcessExited(
            object? sender,
            EventArgs e)
        {
            if (_minecraftProcess == null)
                return;

            try
            {
                int exitCode =
                    _minecraftProcess.ExitCode;

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
                                $"Minecraft exited with code {exitCode}.";
                        }
                    });
            }
            catch (Exception ex)
            {
                WriteException(
                    "PROCESS EXIT ERROR",
                    ex);
            }
        }

        private async Task MonitorMinecraftProcessAsync(
            Process process)
        {
            try
            {
                await process.WaitForExitAsync();

                int exitCode =
                    process.ExitCode;

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
                                $"Minecraft exited with code {exitCode}.";
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
                    process.Dispose();
                }
                catch
                {
                }

                _minecraftProcess =
                    null;
            }
        }

        // =========================================================
        // JAVA
        // =========================================================

        private string? FindJava21()
        {
            // 1. Topu bundled Java
            string bundledJava =
                Path.Combine(
                    _gamePath,
                    "runtime",
                    "java21",
                    "bin",
                    "java.exe");

            if (File.Exists(bundledJava) &&
                IsJava21(bundledJava))
            {
                return bundledJava;
            }

            // 2. JAVA_HOME
            string javaHome =
                Environment.GetEnvironmentVariable(
                    "JAVA_HOME") ?? "";

            if (!string.IsNullOrWhiteSpace(javaHome))
            {
                string java =
                    Path.Combine(
                        javaHome,
                        "bin",
                        "java.exe");

                if (File.Exists(java) &&
                    IsJava21(java))
                {
                    return java;
                }
            }

            // 3. PATH
            string path =
                Environment.GetEnvironmentVariable(
                    "PATH") ?? "";

            foreach (string directory in
                     path.Split(
                         Path.PathSeparator,
                         StringSplitOptions.RemoveEmptyEntries))
            {
                string java =
                    Path.Combine(
                        directory.Trim(),
                        "java.exe");

                if (!File.Exists(java))
                    continue;

                if (IsJava21(java))
                    return java;
            }

            return null;
        }

        private bool IsJava21(
            string javaPath)
        {
            try
            {
                ProcessStartInfo info =
                    new ProcessStartInfo
                    {
                        FileName =
                            javaPath,

                        Arguments =
                            "-version",

                        UseShellExecute =
                            false,

                        RedirectStandardOutput =
                            true,

                        RedirectStandardError =
                            true,

                        CreateNoWindow =
                            true
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

                string output =
                    stdout + Environment.NewLine + stderr;

                return
                    output.Contains(
                        "version \"21.",
                        StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private string GetJavaVersion(
            string javaPath)
        {
            try
            {
                ProcessStartInfo info =
                    new ProcessStartInfo
                    {
                        FileName =
                            javaPath,

                        Arguments =
                            "-version",

                        UseShellExecute =
                            false,

                        RedirectStandardOutput =
                            true,

                        RedirectStandardError =
                            true,

                        CreateNoWindow =
                            true
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
            catch (Exception ex)
            {
                return ex.Message;
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
            string loaderVersion,
            int ramMb)
        {
            try
            {
                string debugPath =
                    Path.Combine(
                        _gamePath,
                        "topu-launch-debug.txt");

                string text =
                    "===== TOPU CLIENT DEBUG =====" +
                    Environment.NewLine +
                    Environment.NewLine +

                    $"Minecraft: {minecraftVersion}" +
                    Environment.NewLine +

                    $"Fabric version: {fabricVersion}" +
                    Environment.NewLine +

                    $"Fabric loader: {loaderVersion}" +
                    Environment.NewLine +

                    $"RAM: {ramMb} MB" +
                    Environment.NewLine +

                    $"Java: {javaPath}" +
                    Environment.NewLine +

                    $"Executable: {process.StartInfo.FileName}" +
                    Environment.NewLine +

                    $"Arguments: {process.StartInfo.Arguments}" +
                    Environment.NewLine +

                    $"Working directory: {process.StartInfo.WorkingDirectory}" +
                    Environment.NewLine +

                    $"Game directory: {_gamePath}" +
                    Environment.NewLine;

                File.WriteAllText(
                    debugPath,
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
        // SANITIZE
        // =========================================================

        private static string SanitizeFileName(
            string filename)
        {
            foreach (char character in
                     Path.GetInvalidFileNameChars())
            {
                filename =
                    filename.Replace(
                        character,
                        '_');
            }

            return filename;
        }

        // =========================================================
        // WINDOW CLOSE
        // =========================================================

        protected override void OnClosed(
            EventArgs e)
        {
            try
            {
                if (_minecraftProcess != null)
                {
                    if (!_minecraftProcess.HasExited)
                    {
                        /*
                         * Do NOT kill every Java process on the PC.
                         * Only the Minecraft process started by Topu
                         * is touched.
                         */
                        try
                        {
                            _minecraftProcess.CloseMainWindow();
                        }
                        catch
                        {
                        }
                    }

                    _minecraftProcess.Dispose();
                    _minecraftProcess = null;
                }
            }
            catch
            {
            }

            base.OnClosed(e);
        }
    }
}
