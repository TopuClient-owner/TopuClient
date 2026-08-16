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
using CmlLib.Core.ProcessBuilder;

namespace TopuLauncher
{
    public partial class MainWindow : Window
    {
        private MSession? _session;

        private ProcessWrapper? _minecraftProcessWrapper;

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
        private const string FabricLoaderVersion = "0.19.3";

        public MainWindow()
        {
            InitializeComponent();

            _gamePath = Path.Combine(
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

            if (RamLabel != null)
                RamLabel.Text = $"{(int)RamSlider.Value}GB";

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
                    _logFilePath,
                    $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
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

                if (UsernameInput != null)
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
                $"Profile saved. Minecraft={version}, RAM={ram}GB");

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
                version = DefaultMinecraftVersion;

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

                StatusText.Text =
                    "Microsoft authentication is not configured yet.";

                MessageBox.Show(
                    "Microsoft authentication is not enabled in this build yet.\n\n" +
                    "Offline mode is available.",
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

            if (button.Tag is not string server)
                return;

            StatusText.Text =
                $"Target server queued: {server}";

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
                    hits.GetArrayLength() == 0)
                {
                    ModSearchStatus.Text =
                        "No compatible mod found.";

                    return;
                }

                JsonElement hit =
                    hits[0];

                string projectId =
                    hit.TryGetProperty(
                        "project_id",
                        out JsonElement projectIdElement)
                    ? projectIdElement.GetString() ?? ""
                    : "";

                string title =
                    hit.TryGetProperty(
                        "title",
                        out JsonElement titleElement)
                    ? titleElement.GetString() ?? query
                    : query;

                if (string.IsNullOrWhiteSpace(projectId))
                    throw new InvalidOperationException(
                        "Modrinth did not return a project ID.");

                string minecraftVersion =
                    GetSelectedMinecraftVersion();

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
                        $"No Fabric version of {title} exists for {minecraftVersion}.";

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
                        "No downloadable mod file was returned.");
                }

                string downloadUrl = "";
                string filename =
                    $"{SanitizeFileName(title)}.jar";

                foreach (JsonElement file in
                         files.EnumerateArray())
                {
                    if (file.TryGetProperty(
                            "url",
                            out JsonElement urlElement))
                    {
                        downloadUrl =
                            urlElement.GetString() ?? "";
                    }

                    if (file.TryGetProperty(
                            "filename",
                            out JsonElement filenameElement))
                    {
                        filename =
                            filenameElement.GetString()
                            ?? filename;
                    }

                    bool primary =
                        file.TryGetProperty(
                            "primary",
                            out JsonElement primaryElement) &&
                        primaryElement.ValueKind ==
                            JsonValueKind.True;

                    if (primary &&
                        !string.IsNullOrWhiteSpace(downloadUrl))
                    {
                        break;
                    }
                }

                if (string.IsNullOrWhiteSpace(downloadUrl))
                    throw new InvalidOperationException(
                        "Modrinth did not return a download URL.");

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

        // =========================================================
        // MAIN LAUNCH
        // =========================================================

        private async void LaunchBtn_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_minecraftProcessWrapper != null)
            {
                try
                {
                    if (!_minecraftProcessWrapper.Process.HasExited)
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
                    _minecraftProcessWrapper = null;
                }
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
                    $"Minecraft: {minecraftVersion}");

                WriteLog(
                    $"Fabric Loader: {FabricLoaderVersion}");

                WriteLog(
                    $"RAM: {ramMb} MB");

                WriteLog(
                    $"Game directory: {_gamePath}");

                // -------------------------------------------------
                // SESSION
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
                        "Microsoft authentication has not been configured yet.");
                }

                // -------------------------------------------------
                // JAVA
                // -------------------------------------------------

                StatusText.Text =
                    "Checking Java 21...";

                string javaPath =
                    FindJava21();

                if (string.IsNullOrWhiteSpace(javaPath))
                {
                    throw new FileNotFoundException(
                        "Java 21 was not found.",
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
                // CMLLIB
                // -------------------------------------------------

                StatusText.Text =
                    $"Installing Minecraft {minecraftVersion}...";

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
                // INSTALL VANILLA
                // -------------------------------------------------

                await launcher.InstallAsync(
                    minecraftVersion);

                WriteLog(
                    "Minecraft installation completed.");

                // -------------------------------------------------
                // FABRIC
                // -------------------------------------------------

                StatusText.Text =
                    $"Installing Fabric {FabricLoaderVersion}...";

                string fabricVersion =
                    await InstallFabricWithCmlLibAsync(
                        launcher,
                        minecraftVersion);

                WriteLog(
                    $"Fabric version: {fabricVersion}");

                // -------------------------------------------------
                // LAUNCH OPTIONS
                // -------------------------------------------------

                MLaunchOption launchOptions =
                    new MLaunchOption
                    {
                        Session = _session,
                        MaximumRamMb = ramMb,
                        JavaPath = javaPath
                    };

                WriteLog(
                    "Building Minecraft process...");

                StatusText.Text =
                    "Creating Minecraft process...";

                // IMPORTANT:
                // CmlLib.Core 4.0.6 returns ProcessWrapper.
                ProcessWrapper wrapper =
                    await launcher.BuildProcessAsync(
                        fabricVersion,
                        launchOptions);

                if (wrapper == null)
                {
                    throw new InvalidOperationException(
                        "CmlLib returned a null ProcessWrapper.");
                }

                _minecraftProcessWrapper =
                    wrapper;

                Process process =
                    wrapper.Process;

                WriteLog(
                    $"Minecraft executable: {process.StartInfo.FileName}");

                WriteLog(
                    $"Minecraft arguments: {process.StartInfo.Arguments}");

                WriteLog(
                    $"Working directory: {process.StartInfo.WorkingDirectory}");

                // -------------------------------------------------
                // EVENTS
                // -------------------------------------------------

                wrapper.OutputReceived +=
                    Minecraft_OutputReceived;

                wrapper.Exited +=
                    Minecraft_Exited;

                // -------------------------------------------------
                // DEBUG FILE
                // -------------------------------------------------

                WriteDebugFile(
                    process,
                    javaPath,
                    minecraftVersion,
                    fabricVersion,
                    ramMb);

                // -------------------------------------------------
                // START
                // -------------------------------------------------

                StatusText.Text =
                    $"Starting Fabric {minecraftVersion}...";

                WriteLog(
                    "Starting Minecraft process.");

                // This is the CmlLib 4.x way.
                wrapper.StartWithEvents();

                WriteLog(
                    $"Minecraft process started. PID: {process.Id}");

                StatusText.Text =
                    $"Topu Client running as {_session.Username}";

                _ = MonitorProcessAsync(
                    wrapper);
            }
            catch (Exception ex)
            {
                StatusText.Text =
                    "Launch Failed!";

                WriteException(
                    "TOPU LAUNCH ERROR",
                    ex);

                MessageBox.Show(
                    "Minecraft failed to start.\n\n" +
                    ex.Message +
                    "\n\n" +
                    "Detailed log:\n" +
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
        // CMLLIB FABRIC
        // =========================================================

        private async Task<string>
            InstallFabricWithCmlLibAsync(
                MinecraftLauncher launcher,
                string minecraftVersion)
        {
            /*
             * CmlLib 4.x has Fabric support through its
             * ModLoaders.FabricMC API.
             *
             * We use reflection here so this launcher remains
             * compatible with the exact 4.0.6 Fabric installer
             * surface without manually creating Fabric JSON files.
             */

            Type? installerType =
                Type.GetType(
                    "CmlLib.Core.ModLoaders.FabricMC.FabricInstaller, CmlLib.Core");

            if (installerType == null)
            {
                throw new InvalidOperationException(
                    "CmlLib Fabric installer was not found in CmlLib.Core 4.0.6.");
            }

            object? installer =
                Activator.CreateInstance(
                    installerType);

            if (installer == null)
            {
                throw new InvalidOperationException(
                    "Could not create the CmlLib Fabric installer.");
            }

            // Find an Install method that accepts the
            // MinecraftLauncher/path information.
            var methods =
                installerType.GetMethods();

            foreach (var method in methods)
            {
                if (!string.Equals(
                        method.Name,
                        "Install",
                        StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(
                        method.Name,
                        "InstallAsync",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var parameters =
                    method.GetParameters();

                try
                {
                    // Most CmlLib 4.x Fabric installer APIs expose
                    // version + loader version information.
                    if (parameters.Length == 3)
                    {
                        object?[] args =
                        {
                            launcher,
                            minecraftVersion,
                            FabricLoaderVersion
                        };

                        object? result =
                            method.Invoke(
                                installer,
                                args);

                        if (result is Task task)
                        {
                            await task;
                        }

                        return
                            $"fabric-loader-{FabricLoaderVersion}-{minecraftVersion}";
                    }

                    if (parameters.Length == 2)
                    {
                        object?[] args =
                        {
                            minecraftVersion,
                            FabricLoaderVersion
                        };

                        object? result =
                            method.Invoke(
                                installer,
                                args);

                        if (result is Task task)
                        {
                            await task;
                        }

                        return
                            $"fabric-loader-{FabricLoaderVersion}-{minecraftVersion}";
                    }
                }
                catch
                {
                    // Try the next compatible overload.
                }
            }

            /*
             * Fallback:
             * If the installed CmlLib build doesn't expose the
             * Fabric installer overload above, download the official
             * Fabric profile and save it.
             *
             * This is only a compatibility fallback.
             */

            string fabricId =
                $"fabric-loader-{FabricLoaderVersion}-{minecraftVersion}";

            string versionsFolder =
                Path.Combine(
                    _gamePath,
                    "versions");

            string versionFolder =
                Path.Combine(
                    versionsFolder,
                    fabricId);

            Directory.CreateDirectory(
                versionFolder);

            string profileUrl =
                "https://meta.fabricmc.net/v2/versions/loader/" +
                Uri.EscapeDataString(minecraftVersion) +
                "/" +
                Uri.EscapeDataString(FabricLoaderVersion) +
                "/profile/json";

            string json =
                await _httpClient.GetStringAsync(
                    profileUrl);

            if (string.IsNullOrWhiteSpace(json))
            {
                throw new InvalidOperationException(
                    "Fabric returned an empty profile.");
            }

            string profileFile =
                Path.Combine(
                    versionFolder,
                    $"{fabricId}.json");

            await File.WriteAllTextAsync(
                profileFile,
                json);

            WriteLog(
                $"Fabric profile saved: {profileFile}");

            return fabricId;
        }

        // =========================================================
        // JAVA
        // =========================================================

        private string FindJava21()
        {
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

                try
                {
                    if (IsJava21(java))
                        return java;
                }
                catch
                {
                }
            }

            return "";
        }

        private bool IsJava21(
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

                using Process process =
                    Process.Start(info)
                    ?? throw new InvalidOperationException(
                        "Could not start Java.");

                string output =
                    process.StandardError.ReadToEnd();

                process.WaitForExit();

                return output.Contains(
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
                    (stdout + Environment.NewLine + stderr)
                    .Trim();
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // =========================================================
        // CMLLIB PROGRESS
        // =========================================================

        private void Launcher_FileProgressChanged(
            object? sender,
            InstallerProgressChangedEventArgs args)
        {
            Dispatcher.Invoke(
                () =>
                {
                    StatusText.Text =
                        $"{args.Name} ({args.EventType})";
                });
        }

        private void Launcher_ByteProgressChanged(
            object? sender,
            ByteProgress args)
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
        }

        // =========================================================
        // PROCESS EVENTS
        // =========================================================

        private void Minecraft_OutputReceived(
            object? sender,
            string output)
        {
            if (string.IsNullOrWhiteSpace(output))
                return;

            AppendMinecraftLog(
                "[MINECRAFT] " + output);
        }

        private void Minecraft_Exited(
            object? sender,
            EventArgs e)
        {
            try
            {
                if (_minecraftProcessWrapper == null)
                    return;

                int exitCode =
                    _minecraftProcessWrapper.Process.ExitCode;

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

        private async Task MonitorProcessAsync(
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
                _minecraftProcessWrapper = null;
            }
        }

        // =========================================================
        // LOGGING
        // =========================================================

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

        private void AppendMinecraftLog(
            string message)
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
        // DEBUG FILE
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
                string debugPath =
                    Path.Combine(
                        _gamePath,
                        "topu-launch-debug.txt");

                string text =
                    "===== TOPU CLIENT DEBUG =====" +
                    Environment.NewLine +
                    Environment.NewLine +
                    $"Executable:{Environment.NewLine}" +
                    $"{process.StartInfo.FileName}" +
                    Environment.NewLine +
                    Environment.NewLine +
                    $"Arguments:{Environment.NewLine}" +
                    $"{process.StartInfo.Arguments}" +
                    Environment.NewLine +
                    Environment.NewLine +
                    $"Working Directory:{Environment.NewLine}" +
                    $"{process.StartInfo.WorkingDirectory}" +
                    Environment.NewLine +
                    Environment.NewLine +
                    $"Java:{Environment.NewLine}" +
                    $"{javaPath}" +
                    Environment.NewLine +
                    Environment.NewLine +
                    $"Minecraft:{Environment.NewLine}" +
                    $"{minecraftVersion}" +
                    Environment.NewLine +
                    Environment.NewLine +
                    $"Fabric:{Environment.NewLine}" +
                    $"{fabricVersion}" +
                    Environment.NewLine +
                    Environment.NewLine +
                    $"RAM:{Environment.NewLine}" +
                    $"{ramMb} MB" +
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
        // PROCESS CLEANUP
        // =========================================================

        private void KillOldMinecraftProcesses()
        {
            try
            {
                foreach (string processName in
                         new[] { "java", "javaw" })
                {
                    foreach (Process process in
                             Process.GetProcessesByName(
                                 processName))
                    {
                        try
                        {
                            process.Kill();
                        }
                        catch
                        {
                        }

                        process.Dispose();
                    }
                }
            }
            catch
            {
            }
        }

        // =========================================================
        // SANITIZE
        // =========================================================

        private static string
            SanitizeFileName(
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
    }
}
