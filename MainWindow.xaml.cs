using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
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

        private static readonly HttpClient HttpClient =
            new HttpClient(new HttpClientHandler
            {
                AllowAutoRedirect = true
            })
            {
                Timeout = TimeSpan.FromMinutes(10)
            };

        private readonly string _gamePath;
        private readonly string _usernameFile;
        private readonly string _logFile;
        private readonly string _debugFile;

        private const string DefaultMinecraftVersion = "1.21.1";

        public MainWindow()
        {
            InitializeComponent();

            _gamePath = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.ApplicationData),
                ".topuclient");

            Directory.CreateDirectory(_gamePath);

            _usernameFile =
                Path.Combine(_gamePath, "username.txt");

            _logFile =
                Path.Combine(_gamePath, "topu-minecraft.log");

            _debugFile =
                Path.Combine(_gamePath, "topu-launch-debug.txt");

            LoadSavedUsername();

            if (RamLabel != null)
                RamLabel.Text = $"{(int)RamSlider.Value}GB";

            if (StatusText != null)
                StatusText.Text = "Ready.";
        }

        // =========================================================
        // USERNAME
        // =========================================================

        private void LoadSavedUsername()
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
                if (!string.IsNullOrWhiteSpace(username))
                {
                    File.WriteAllText(
                        _usernameFile,
                        username.Trim());
                }
            }
            catch
            {
                // Non-critical.
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
                // Ignore.
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

            if (button.Tag is not string target)
                return;

            TabLaunch.Visibility = Visibility.Collapsed;
            TabProfiles.Visibility = Visibility.Collapsed;
            TabAccounts.Visibility = Visibility.Collapsed;

            Brush normal =
                new SolidColorBrush(
                    Color.FromRgb(136, 136, 136));

            Brush selected =
                new SolidColorBrush(
                    Color.FromRgb(0, 255, 136));

            TabLaunchBtn.Foreground = normal;
            TabLaunchBtn.BorderThickness =
                new Thickness(0);

            TabProfilesBtn.Foreground = normal;
            TabProfilesBtn.BorderThickness =
                new Thickness(0);

            TabAccountsBtn.Foreground = normal;
            TabAccountsBtn.BorderThickness =
                new Thickness(0);

            button.Foreground = selected;

            button.BorderThickness =
                new Thickness(0, 0, 0, 2);

            switch (target)
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

            MessageBox.Show(
                $"Profile saved successfully.\n\n" +
                $"Minecraft: {version}\n" +
                $"RAM: {ram}GB",
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
                StatusText.Text =
                    "Microsoft authentication is not enabled yet.";

                MessageBox.Show(
                    "Microsoft authentication is not enabled in this build.\n\n" +
                    "Offline mode is available.",
                    "Microsoft Login",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.ToString(),
                    "Authentication Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
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

            if (button.Tag is not string server)
                return;

            StatusText.Text =
                $"Server selected: {server}";

            MessageBox.Show(
                $"Server selected:\n{server}\n\n" +
                "Automatic server connection will be added later.",
                "Topu Client",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        // =========================================================
        // VERSION
        // =========================================================

        private string GetSelectedMinecraftVersion()
        {
            return
                (VersionBox.SelectedItem as ComboBoxItem)
                ?.Content
                ?.ToString()
                ?.Trim()
                ?? DefaultMinecraftVersion;
        }

        private static bool IsJava25Version(
            string minecraftVersion)
        {
            return minecraftVersion.StartsWith(
                       "26.",
                       StringComparison.OrdinalIgnoreCase);
        }

        // =========================================================
        // JAVA
        // =========================================================

        private string FindJava(
            string minecraftVersion)
        {
            bool needsJava25 =
                IsJava25Version(minecraftVersion);

            int requiredMajor =
                needsJava25 ? 25 : 21;

            // -----------------------------------------------------
            // Topu private runtime
            // -----------------------------------------------------

            string runtimeFolder =
                needsJava25
                    ? "java25"
                    : "java21";

            string localJava =
                Path.Combine(
                    _gamePath,
                    "runtime",
                    runtimeFolder,
                    "bin",
                    "java.exe");

            if (File.Exists(localJava) &&
                IsRequiredJavaVersion(
                    localJava,
                    requiredMajor))
            {
                return localJava;
            }

            // -----------------------------------------------------
            // JAVA_HOME
            // -----------------------------------------------------

            string javaHome =
                Environment.GetEnvironmentVariable(
                    "JAVA_HOME")
                ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(javaHome))
            {
                string candidate =
                    Path.Combine(
                        javaHome,
                        "bin",
                        "java.exe");

                if (File.Exists(candidate) &&
                    IsRequiredJavaVersion(
                        candidate,
                        requiredMajor))
                {
                    return candidate;
                }
            }

            // -----------------------------------------------------
            // PATH
            // -----------------------------------------------------

            string path =
                Environment.GetEnvironmentVariable(
                    "PATH")
                ?? string.Empty;

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

                if (IsRequiredJavaVersion(
                        candidate,
                        requiredMajor))
                {
                    return candidate;
                }
            }

            return string.Empty;
        }

        private bool IsRequiredJavaVersion(
            string javaPath,
            int requiredMajor)
        {
            try
            {
                ProcessStartInfo psi =
                    new ProcessStartInfo
                    {
                        FileName = javaPath,
                        Arguments = "-version",
                        UseShellExecute = false,
                        RedirectStandardError = true,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    };

                using Process process =
                    Process.Start(psi)
                    ?? throw new InvalidOperationException(
                        "Unable to start java.exe.");

                string stderr =
                    process.StandardError.ReadToEnd();

                string stdout =
                    process.StandardOutput.ReadToEnd();

                process.WaitForExit();

                string output =
                    stderr + "\n" + stdout;

                string expected =
                    $"version \"{requiredMajor}.";

                return output.Contains(
                    expected,
                    StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private string GetJavaVersionText(
            string javaPath)
        {
            try
            {
                ProcessStartInfo psi =
                    new ProcessStartInfo
                    {
                        FileName = javaPath,
                        Arguments = "-version",
                        UseShellExecute = false,
                        RedirectStandardError = true,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    };

                using Process process =
                    Process.Start(psi)
                    ?? throw new InvalidOperationException(
                        "Unable to start Java.");

                string stderr =
                    process.StandardError.ReadToEnd();

                string stdout =
                    process.StandardOutput.ReadToEnd();

                process.WaitForExit();

                return stderr + stdout;
            }
            catch (Exception ex)
            {
                return ex.ToString();
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
                    // Continue.
                }

                _minecraftProcess = null;
            }

            LaunchBtn.IsEnabled = false;

            string minecraftVersion =
                GetSelectedMinecraftVersion();

            int ramMb =
                Math.Max(
                    1024,
                    (int)RamSlider.Value * 1024);

            try
            {
                StartFreshLog(
                    minecraftVersion,
                    ramMb);

                WriteLog(
                    "Starting CmlLib.Core launch pipeline.");

                // -------------------------------------------------
                // SESSION
                // -------------------------------------------------

                if (AuthTypeBox.SelectedIndex == 0)
                {
                    string username =
                        UsernameInput.Text?.Trim()
                        ?? string.Empty;

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
                        "Microsoft authentication is not implemented " +
                        "in this build. Select Offline / Cracked Mode.");
                }

                // -------------------------------------------------
                // JAVA
                // -------------------------------------------------

                StatusText.Text =
                    "Checking Java...";

                string javaPath =
                    FindJava(minecraftVersion);

                if (string.IsNullOrWhiteSpace(javaPath))
                {
                    int required =
                        IsJava25Version(minecraftVersion)
                            ? 25
                            : 21;

                    throw new InvalidOperationException(
                        $"Java {required} was not found.\n\n" +
                        $"Minecraft {minecraftVersion} requires " +
                        $"Java {required}.\n\n" +
                        "Topu expected it at:\n" +
                        Path.Combine(
                            _gamePath,
                            "runtime",
                            required == 25
                                ? "java25"
                                : "java21",
                            "bin",
                            "java.exe"));
                }

                string javaVersion =
                    GetJavaVersionText(javaPath);

                WriteLog(
                    $"Java: {javaPath}");

                WriteLog(
                    "Java version:\n" +
                    javaVersion);

                // -------------------------------------------------
                // MINECRAFT PATH
                // -------------------------------------------------

                MinecraftPath minecraftPath =
                    new MinecraftPath(_gamePath);

                Directory.CreateDirectory(
                    _gamePath);

                // -------------------------------------------------
                // CMLLIB LAUNCHER
                // -------------------------------------------------

                MinecraftLauncher launcher =
                    new MinecraftLauncher(
                        minecraftPath);

                launcher.ByteProgressChanged +=
                    Launcher_ByteProgressChanged;

                // -------------------------------------------------
                // INSTALL VANILLA
                // -------------------------------------------------

                StatusText.Text =
                    $"Installing Minecraft {minecraftVersion}...";

                WriteLog(
                    $"Installing Minecraft {minecraftVersion}...");

                await launcher.InstallAsync(
                    minecraftVersion);

                WriteLog(
                    "Minecraft installation completed.");

                // -------------------------------------------------
                // INSTALL FABRIC USING CMLLIB
                // -------------------------------------------------

                StatusText.Text =
                    "Finding compatible Fabric loader...";

                WriteLog(
                    "Requesting compatible Fabric loader...");

                FabricInstaller fabricInstaller =
                    new FabricInstaller(
                        HttpClient);

                FabricLoader? fabricLoader =
                    await fabricInstaller.GetFirstLoader(
                        minecraftVersion);

                if (fabricLoader == null)
                {
                    throw new InvalidOperationException(
                        $"Fabric does not currently provide a loader " +
                        $"compatible with Minecraft {minecraftVersion}.");
                }

                if (string.IsNullOrWhiteSpace(
                        fabricLoader.Version))
                {
                    throw new InvalidOperationException(
                        "Fabric returned a loader without a version.");
                }

                string loaderVersion =
                    fabricLoader.Version;

                WriteLog(
                    $"Selected Fabric loader: {loaderVersion}");

                StatusText.Text =
                    $"Installing Fabric {loaderVersion}...";

                WriteLog(
                    $"Installing Fabric {loaderVersion}...");

                string fabricVersionName =
                    await fabricInstaller.Install(
                        minecraftVersion,
                        loaderVersion,
                        minecraftPath);

                WriteLog(
                    $"Fabric installed: {fabricVersionName}");

                // -------------------------------------------------
                // MODS
                // -------------------------------------------------

                StatusText.Text =
                    "Checking Fabric mods...";

                await InstallCompatibleModsAsync(
                    minecraftVersion);

                // -------------------------------------------------
                // BUILD PROCESS
                // -------------------------------------------------

                StatusText.Text =
                    "Creating Minecraft process...";

                WriteLog(
                    "Building Minecraft process with CmlLib.");

                MLaunchOption launchOptions =
                    new MLaunchOption
                    {
                        Session = _session,
                        MaximumRamMb = ramMb,
                        MinimumRamMb = Math.Min(
                            1024,
                            ramMb),
                        JavaPath = javaPath,
                        GameLauncherName = "TopuClient",
                        GameLauncherVersion = "1.0.0"
                    };

                ProcessWrapper process =
                    await launcher.BuildProcessAsync(
                        fabricVersionName,
                        launchOptions);

                if (process == null)
                {
                    throw new InvalidOperationException(
                        "CmlLib returned a null ProcessWrapper.");
                }

                _minecraftProcess = process;

                WriteDebugFile(
                    minecraftVersion,
                    loaderVersion,
                    fabricVersionName,
                    javaPath,
                    ramMb,
                    process);

                // -------------------------------------------------
                // OUTPUT
                // -------------------------------------------------

                process.OutputReceived +=
                    Minecraft_OutputReceived;

                process.Exited +=
                    Minecraft_Exited;

                // -------------------------------------------------
                // START
                // -------------------------------------------------

                StatusText.Text =
                    $"Starting Fabric {minecraftVersion}...";

                WriteLog(
                    "Starting Minecraft process.");

                process.StartWithEvents();

                WriteLog(
                    $"Minecraft process started. PID: " +
                    $"{process.Process.Id}");

                StatusText.Text =
                    $"Topu Client running as " +
                    $"{_session.Username}";

                _ = MonitorMinecraftAsync(
                    process);
            }
            catch (Exception ex)
            {
                WriteException(
                    ex);

                StatusText.Text =
                    "Launch failed.";

                MessageBox.Show(
                    "Minecraft failed to launch.\n\n" +
                    ex.Message +
                    "\n\nDetailed log:\n" +
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
        // CMLLIB BYTE PROGRESS
        // =========================================================

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
                        double percentage =
                            args.ProgressedBytes *
                            100.0 /
                            args.TotalBytes;

                        StatusText.Text =
                            $"Downloading: {percentage:0}%";
                    }
                    else
                    {
                        StatusText.Text =
                            $"Downloading: " +
                            $"{args.ProgressedBytes:N0} bytes";
                    }
                });
            }
            catch
            {
                // UI may be closing.
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

            WriteLog(
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

                WriteLog(
                    $"===== MINECRAFT EXITED: {exitCode} =====");

                Dispatcher.Invoke(() =>
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
                WriteLog(
                    "Exit event error: " +
                    ex);
            }
        }

        private async Task MonitorMinecraftAsync(
            ProcessWrapper process)
        {
            try
            {
                int exitCode =
                    await process.WaitForExitTaskAsync();

                WriteLog(
                    $"Minecraft process finished with exit code " +
                    $"{exitCode}.");

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
                            $"Minecraft exited with code {exitCode}";
                    }
                });
            }
            catch (Exception ex)
            {
                WriteLog(
                    "Process monitor error:\n" +
                    ex);
            }
            finally
            {
                _minecraftProcess = null;
            }
        }

        // =========================================================
        // FABRIC MODS
        // =========================================================

        private async Task InstallCompatibleModsAsync(
            string minecraftVersion)
        {
            string modsFolder =
                Path.Combine(
                    _gamePath,
                    "mods");

            Directory.CreateDirectory(
                modsFolder);

            // These are optional optimization mods.
            // If a mod doesn't have a compatible release,
            // Topu simply skips it.
            string[] projects =
            {
                "fabric-api",
                "sodium",
                "lithium",
                "ferrite-core",
                "sodium-extra",
                "dynamic-fps"
            };

            foreach (string project in projects)
            {
                try
                {
                    await InstallModrinthProjectAsync(
                        project,
                        minecraftVersion,
                        modsFolder);
                }
                catch (Exception ex)
                {
                    WriteLog(
                        $"Optional mod skipped: {project}");
                    WriteLog(
                        ex.ToString());
                }
            }
        }

        private async Task InstallModrinthProjectAsync(
            string project,
            string minecraftVersion,
            string modsFolder)
        {
            string searchUrl =
                "https://api.modrinth.com/v2/search" +
                "?query=" +
                Uri.EscapeDataString(project) +
                "&facets=%5B%5B%22project_type%3Amod%22%5D%5D";

            string searchResponse =
                await HttpClient.GetStringAsync(
                    searchUrl);

            using JsonDocument searchDocument =
                JsonDocument.Parse(
                    searchResponse);

            JsonElement root =
                searchDocument.RootElement;

            if (!root.TryGetProperty(
                    "hits",
                    out JsonElement hits) ||
                hits.ValueKind != JsonValueKind.Array ||
                hits.GetArrayLength() == 0)
            {
                WriteLog(
                    $"Modrinth project not found: {project}");

                return;
            }

            string? projectId = null;

            foreach (JsonElement hit in
                     hits.EnumerateArray())
            {
                if (!hit.TryGetProperty(
                        "project_id",
                        out JsonElement id))
                    continue;

                string? idText =
                    id.GetString();

                if (!string.IsNullOrWhiteSpace(idText))
                {
                    projectId = idText;
                    break;
                }
            }

            if (string.IsNullOrWhiteSpace(projectId))
                return;

            string versionsUrl =
                "https://api.modrinth.com/v2/project/" +
                Uri.EscapeDataString(projectId) +
                "/version" +
                "?loaders=%5B%22fabric%22%5D" +
                "&game_versions=%5B%22" +
                Uri.EscapeDataString(minecraftVersion) +
                "%22%5D";

            string versionsResponse =
                await HttpClient.GetStringAsync(
                    versionsUrl);

            using JsonDocument versionsDocument =
                JsonDocument.Parse(
                    versionsResponse);

            JsonElement versions =
                versionsDocument.RootElement;

            if (versions.ValueKind !=
                JsonValueKind.Array)
            {
                return;
            }

            if (versions.GetArrayLength() == 0)
            {
                WriteLog(
                    $"No compatible version: {project} " +
                    $"for Minecraft {minecraftVersion}");

                return;
            }

            JsonElement selectedVersion =
                versions[0];

            if (!selectedVersion.TryGetProperty(
                    "files",
                    out JsonElement files) ||
                files.ValueKind != JsonValueKind.Array ||
                files.GetArrayLength() == 0)
            {
                return;
            }

            string? downloadUrl = null;
            string? filename = null;

            foreach (JsonElement file in
                     files.EnumerateArray())
            {
                bool primary = false;

                if (file.TryGetProperty(
                        "primary",
                        out JsonElement primaryProperty))
                {
                    primary =
                        primaryProperty.ValueKind ==
                        JsonValueKind.True;
                }

                if (!file.TryGetProperty(
                        "url",
                        out JsonElement urlProperty))
                {
                    continue;
                }

                string? url =
                    urlProperty.GetString();

                if (string.IsNullOrWhiteSpace(url))
                    continue;

                downloadUrl = url;

                if (file.TryGetProperty(
                        "filename",
                        out JsonElement filenameProperty))
                {
                    filename =
                        filenameProperty.GetString();
                }

                if (primary)
                    break;
            }

            if (string.IsNullOrWhiteSpace(downloadUrl))
                return;

            filename =
                SanitizeFileName(
                    filename ??
                    $"{project}.jar");

            string destination =
                Path.Combine(
                    modsFolder,
                    filename);

            if (File.Exists(destination) &&
                new FileInfo(destination).Length > 1024)
            {
                WriteLog(
                    $"Mod already installed: {filename}");

                return;
            }

            StatusText.Text =
                $"Downloading {project}...";

            WriteLog(
                $"Downloading mod: {project}");

            byte[] data =
                await HttpClient.GetByteArrayAsync(
                    downloadUrl);

            if (data.Length < 1024)
            {
                WriteLog(
                    $"Rejected suspiciously small mod file: " +
                    $"{filename}");

                return;
            }

            await File.WriteAllBytesAsync(
                destination,
                data);

            WriteLog(
                $"Installed mod: {filename}");
        }

        // =========================================================
        // MODRINTH MANUAL SEARCH
        // =========================================================

        private async void SearchModrinth_Click(
            object sender,
            RoutedEventArgs e)
        {
            string query =
                ModSearchInput.Text?.Trim()
                ?? string.Empty;

            if (string.IsNullOrWhiteSpace(query))
            {
                MessageBox.Show(
                    "Enter a mod name first.",
                    "Modrinth",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            string minecraftVersion =
                GetSelectedMinecraftVersion();

            try
            {
                ModSearchStatus.Text =
                    $"Searching Modrinth for {query}...";

                string searchUrl =
                    "https://api.modrinth.com/v2/search" +
                    "?query=" +
                    Uri.EscapeDataString(query) +
                    "&facets=%5B%5B%22project_type%3Amod%22%5D%5D";

                string response =
                    await HttpClient.GetStringAsync(
                        searchUrl);

                using JsonDocument document =
                    JsonDocument.Parse(response);

                JsonElement root =
                    document.RootElement;

                if (!root.TryGetProperty(
                        "hits",
                        out JsonElement hits) ||
                    hits.ValueKind != JsonValueKind.Array ||
                    hits.GetArrayLength() == 0)
                {
                    ModSearchStatus.Text =
                        "No compatible mod found.";

                    return;
                }

                JsonElement first =
                    hits[0];

                string title =
                    first.TryGetProperty(
                        "title",
                        out JsonElement titleProperty)
                        ? titleProperty.GetString()
                          ?? query
                        : query;

                string projectId =
                    first.TryGetProperty(
                        "project_id",
                        out JsonElement idProperty)
                        ? idProperty.GetString()
                          ?? string.Empty
                        : string.Empty;

                if (string.IsNullOrWhiteSpace(projectId))
                {
                    ModSearchStatus.Text =
                        "Project ID was missing.";

                    return;
                }

                string versionsUrl =
                    "https://api.modrinth.com/v2/project/" +
                    Uri.EscapeDataString(projectId) +
                    "/version" +
                    "?loaders=%5B%22fabric%22%5D" +
                    "&game_versions=%5B%22" +
                    Uri.EscapeDataString(minecraftVersion) +
                    "%22%5D";

                string versionsResponse =
                    await HttpClient.GetStringAsync(
                        versionsUrl);

                using JsonDocument versionDocument =
                    JsonDocument.Parse(
                        versionsResponse);

                JsonElement versions =
                    versionDocument.RootElement;

                if (versions.ValueKind !=
                    JsonValueKind.Array ||
                    versions.GetArrayLength() == 0)
                {
                    ModSearchStatus.Text =
                        $"No Fabric version of {title} " +
                        $"supports Minecraft {minecraftVersion}.";

                    return;
                }

                JsonElement version =
                    versions[0];

                if (!version.TryGetProperty(
                        "files",
                        out JsonElement files) ||
                    files.ValueKind != JsonValueKind.Array ||
                    files.GetArrayLength() == 0)
                {
                    ModSearchStatus.Text =
                        "No downloadable file found.";

                    return;
                }

                string? url = null;
                string filename =
                    $"{SanitizeFileName(title)}.jar";

                foreach (JsonElement file in
                         files.EnumerateArray())
                {
                    bool primary =
                        file.TryGetProperty(
                            "primary",
                            out JsonElement primaryProperty) &&
                        primaryProperty.ValueKind ==
                        JsonValueKind.True;

                    if (!file.TryGetProperty(
                            "url",
                            out JsonElement urlProperty))
                        continue;

                    string? candidate =
                        urlProperty.GetString();

                    if (string.IsNullOrWhiteSpace(candidate))
                        continue;

                    url = candidate;

                    if (file.TryGetProperty(
                            "filename",
                            out JsonElement filenameProperty))
                    {
                        filename =
                            filenameProperty.GetString()
                            ?? filename;
                    }

                    if (primary)
                        break;
                }

                if (string.IsNullOrWhiteSpace(url))
                {
                    ModSearchStatus.Text =
                        "Download URL was missing.";

                    return;
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
                    await HttpClient.GetByteArrayAsync(
                        url);

                await File.WriteAllBytesAsync(
                    destination,
                    data);

                ModSearchStatus.Text =
                    $"Installed {title}.";

                MessageBox.Show(
                    $"Successfully installed:\n\n" +
                    $"{title}\n\n" +
                    $"Minecraft: {minecraftVersion}",
                    "Modrinth",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ModSearchStatus.Text =
                    "Modrinth search failed.";

                WriteLog(
                    "Modrinth error:\n" +
                    ex);

                MessageBox.Show(
                    ex.Message,
                    "Modrinth Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // =========================================================
        // LOGGING
        // =========================================================

        private void StartFreshLog(
            string minecraftVersion,
            int ramMb)
        {
            try
            {
                Directory.CreateDirectory(
                    _gamePath);

                File.WriteAllText(
                    _logFile,
                    "===== TOPU CLIENT MINECRAFT LOG =====\r\n" +
                    $"Started: {DateTime.Now:O}\r\n\r\n" +
                    "===== TOPU CLIENT MINECRAFT LAUNCH =====\r\n" +
                    $"Minecraft: {minecraftVersion}\r\n" +
                    $"RAM: {ramMb} MB\r\n" +
                    $"Game directory: {_gamePath}\r\n");
            }
            catch
            {
                // Logging must never break launching.
            }
        }

        private void WriteLog(string text)
        {
            try
            {
                File.AppendAllText(
                    _logFile,
                    $"[{DateTime.Now:HH:mm:ss}] " +
                    $"{text}\r\n");
            }
            catch
            {
                // Ignore logging failures.
            }
        }

        private void WriteException(
            Exception ex)
        {
            WriteLog(
                "\r\n===== TOPU CLIENT LAUNCH ERROR =====");

            WriteLog(
                ex.ToString());
        }

        // =========================================================
        // DEBUG FILE
        // =========================================================

        private void WriteDebugFile(
            string minecraftVersion,
            string loaderVersion,
            string fabricVersionName,
            string javaPath,
            int ramMb,
            ProcessWrapper process)
        {
            try
            {
                string arguments =
                    process.Process.StartInfo.Arguments;

                string executable =
                    process.Process.StartInfo.FileName;

                string workingDirectory =
                    process.Process.StartInfo.WorkingDirectory;

                string debug =
                    "===== TOPU CLIENT DEBUG =====\r\n\r\n" +
                    $"Executable:\r\n{executable}\r\n\r\n" +
                    $"Arguments:\r\n{arguments}\r\n\r\n" +
                    $"Working Directory:\r\n" +
                    $"{workingDirectory}\r\n\r\n" +
                    $"Java:\r\n{javaPath}\r\n\r\n" +
                    $"Minecraft:\r\n" +
                    $"{minecraftVersion}\r\n\r\n" +
                    $"Fabric Loader:\r\n" +
                    $"{loaderVersion}\r\n\r\n" +
                    $"Fabric Version:\r\n" +
                    $"{fabricVersionName}\r\n\r\n" +
                    $"RAM:\r\n{ramMb} MB\r\n\r\n" +
                    $"PID:\r\n{process.Process.Id}\r\n\r\n";

                File.WriteAllText(
                    _debugFile,
                    debug);

                WriteLog(
                    $"Debug file written: {_debugFile}");
            }
            catch (Exception ex)
            {
                WriteLog(
                    "Could not write debug file:\n" +
                    ex);
            }
        }

        // =========================================================
        // FILENAME
        // =========================================================

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

        // =========================================================
        // WINDOW CLOSE
        // =========================================================

        protected override void OnClosed(
            EventArgs e)
        {
            // Do NOT kill every java.exe on the machine.
            // Topu only tracks the Minecraft process it started.

            base.OnClosed(e);
        }
    }
}
