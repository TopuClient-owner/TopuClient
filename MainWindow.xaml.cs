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
        private Process? _minecraftProcess;

        private static readonly HttpClient _httpClient =
            new HttpClient(
                new HttpClientHandler
                {
                    AllowAutoRedirect = true
                })
            {
                Timeout = TimeSpan.FromMinutes(10)
            };

        private readonly string _gamePath;
        private readonly string _configFilePath;
        private readonly string _logFilePath;
        private readonly string _debugFilePath;

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

            _configFilePath = Path.Combine(
                _gamePath,
                "username.txt");

            _logFilePath = Path.Combine(
                _gamePath,
                "topu-minecraft.log");

            _debugFilePath = Path.Combine(
                _gamePath,
                "topu-launch-debug.txt");

            LoadSavedUsername();

            WriteLauncherLog(
                "===== TOPU CLIENT STARTED =====");
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

                if (UsernameInput != null)
                    UsernameInput.Text = username;

                _session =
                    MSession.CreateOfflineSession(username);
            }
            catch (Exception ex)
            {
                WriteLauncherLog(
                    "Username load failed: " + ex);
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
                WriteLauncherLog(
                    "Username save failed: " + ex);
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

            Brush inactive =
                new SolidColorBrush(
                    Color.FromRgb(136, 136, 136));

            TabLaunchBtn.Foreground = inactive;
            TabProfilesBtn.Foreground = inactive;
            TabAccountsBtn.Foreground = inactive;

            TabLaunchBtn.BorderThickness =
                new Thickness(0);

            TabProfilesBtn.BorderThickness =
                new Thickness(0);

            TabAccountsBtn.BorderThickness =
                new Thickness(0);

            button.Foreground =
                new SolidColorBrush(
                    Color.FromRgb(0, 255, 136));

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

            SelectedProfileLabel.Text =
                $"Ready to launch Fabric {version}";

            StatusText.Text =
                $"Profile saved: Fabric {version} " +
                $"with {(int)RamSlider.Value}GB RAM";
        }

        private string GetSelectedMinecraftVersion()
        {
            return
                (VersionBox.SelectedItem as ComboBoxItem)
                ?.Content?
                .ToString()
                ?? DefaultMinecraftVersion;
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
                    "Microsoft authentication is not configured yet.";

                MessageBox.Show(
                    "Microsoft authentication is not enabled in this build.\n\n" +
                    "Offline mode remains available.",
                    "Microsoft Login",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                WriteLauncherException(ex);

                MessageBox.Show(
                    ex.Message,
                    "Authentication Error",
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
            if (sender is Button button &&
                button.Tag is string server)
            {
                StatusText.Text =
                    $"Server selected: {server}";
            }
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
                ModSearchStatus.Text =
                    "Enter a mod name first.";

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

                using JsonDocument search =
                    JsonDocument.Parse(
                        await _httpClient.GetStringAsync(
                            searchUrl));

                if (!search.RootElement.TryGetProperty(
                        "hits",
                        out JsonElement hits) ||
                    hits.GetArrayLength() == 0)
                {
                    ModSearchStatus.Text =
                        "No mod found.";

                    return;
                }

                JsonElement hit = hits[0];

                string projectId =
                    hit.TryGetProperty(
                        "project_id",
                        out JsonElement id)
                    ? id.GetString() ?? ""
                    : "";

                string title =
                    hit.TryGetProperty(
                        "title",
                        out JsonElement titleElement)
                    ? titleElement.GetString() ?? query
                    : query;

                if (string.IsNullOrWhiteSpace(projectId))
                    throw new Exception(
                        "Modrinth did not return a project ID.");

                string mcVersion =
                    GetSelectedMinecraftVersion();

                string versionsUrl =
                    "https://api.modrinth.com/v2/project/" +
                    Uri.EscapeDataString(projectId) +
                    "/version" +
                    "?loaders=%5B%22fabric%22%5D" +
                    $"&game_versions=%5B%22{Uri.EscapeDataString(mcVersion)}%22%5D";

                using JsonDocument versions =
                    JsonDocument.Parse(
                        await _httpClient.GetStringAsync(
                            versionsUrl));

                if (versions.RootElement.GetArrayLength() == 0)
                {
                    ModSearchStatus.Text =
                        $"No Fabric version of {title} supports {mcVersion}.";

                    return;
                }

                JsonElement version =
                    versions.RootElement[0];

                if (!version.TryGetProperty(
                        "files",
                        out JsonElement files) ||
                    files.GetArrayLength() == 0)
                {
                    throw new Exception(
                        "Modrinth returned no mod files.");
                }

                string downloadUrl = "";
                string filename = title + ".jar";

                foreach (JsonElement file in
                         files.EnumerateArray())
                {
                    bool primary =
                        file.TryGetProperty(
                            "primary",
                            out JsonElement primaryElement) &&
                        primaryElement.ValueKind ==
                            JsonValueKind.True;

                    if (!primary)
                        continue;

                    if (file.TryGetProperty(
                            "url",
                            out JsonElement url))
                    {
                        downloadUrl =
                            url.GetString() ?? "";
                    }

                    if (file.TryGetProperty(
                            "filename",
                            out JsonElement name))
                    {
                        filename =
                            name.GetString() ?? filename;
                    }

                    break;
                }

                if (string.IsNullOrWhiteSpace(downloadUrl))
                {
                    downloadUrl =
                        files[0]
                            .GetProperty("url")
                            .GetString()
                            ?? "";

                    filename =
                        files[0]
                            .TryGetProperty(
                                "filename",
                                out JsonElement name)
                        ? name.GetString() ?? filename
                        : filename;
                }

                if (string.IsNullOrWhiteSpace(downloadUrl))
                    throw new Exception(
                        "Modrinth download URL is empty.");

                string mods =
                    Path.Combine(
                        _gamePath,
                        "mods");

                Directory.CreateDirectory(mods);

                string destination =
                    Path.Combine(
                        mods,
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
                    $"Added {title} successfully.";

                WriteLauncherLog(
                    $"Mod installed: {title} -> {destination}");
            }
            catch (Exception ex)
            {
                WriteLauncherException(ex);

                ModSearchStatus.Text =
                    "Mod download failed.";

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
            }

            LaunchBtn.IsEnabled = false;

            try
            {
                string minecraftVersion =
                    GetSelectedMinecraftVersion();

                WriteLauncherLog(
                    "===== LAUNCH REQUEST =====");

                WriteLauncherLog(
                    $"Minecraft: {minecraftVersion}");

                WriteLauncherLog(
                    $"Fabric: {FabricLoaderVersion}");

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

                    WriteLauncherLog(
                        $"Offline username: {username}");
                }
                else
                {
                    throw new InvalidOperationException(
                        "Microsoft login is not implemented in this build.");
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
                        "Java 21 was not found.\n\n" +
                        "Expected location:\n" +
                        Path.Combine(
                            _gamePath,
                            "runtime",
                            "java21",
                            "bin",
                            "java.exe"));
                }

                WriteLauncherLog(
                    $"Java: {javaPath}");

                StatusText.Text =
                    "Java 21 found.";

                // -------------------------------------------------
                // CMLLIB
                // -------------------------------------------------

                MinecraftPath minecraftPath =
                    new MinecraftPath(_gamePath);

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

                StatusText.Text =
                    $"Downloading Minecraft {minecraftVersion}...";

                WriteLauncherLog(
                    "Installing vanilla Minecraft...");

                await launcher.InstallAsync(
                    minecraftVersion);

                WriteLauncherLog(
                    "Vanilla installation finished.");

                // -------------------------------------------------
                // INSTALL FABRIC PROFILE
                // -------------------------------------------------

                StatusText.Text =
                    $"Downloading Fabric {FabricLoaderVersion}...";

                string fabricVersion =
                    await InstallFabricAsync(
                        minecraftVersion,
                        FabricLoaderVersion);

                WriteLauncherLog(
                    $"Fabric version: {fabricVersion}");

                // -------------------------------------------------
                // INSTALL FABRIC LIBRARIES
                // -------------------------------------------------

                StatusText.Text =
                    "Downloading Fabric libraries...";

                await DownloadFabricLibrariesAsync(
                    fabricVersion);

                WriteLauncherLog(
                    "Fabric libraries downloaded.");

                // -------------------------------------------------
                // RAM
                // -------------------------------------------------

                int ramMb =
                    Math.Max(
                        2048,
                        (int)RamSlider.Value * 1024);

                // -------------------------------------------------
                // LAUNCH OPTIONS
                // -------------------------------------------------

                MLaunchOption options =
                    new MLaunchOption
                    {
                        Session = _session,
                        MaximumRamMb = ramMb,
                        JavaPath = javaPath
                    };

                // -------------------------------------------------
                // BUILD
                // -------------------------------------------------

                StatusText.Text =
                    "Creating Minecraft process...";

                WriteLauncherLog(
                    "Calling BuildProcessAsync...");

                Process process =
                    await launcher.BuildProcessAsync(
                        fabricVersion,
                        options);

                if (process == null)
                {
                    throw new Exception(
                        "CmlLib returned a null process.");
                }

                _minecraftProcess = process;

                // -------------------------------------------------
                // DEBUG
                // -------------------------------------------------

                WriteDebugInformation(
                    process,
                    javaPath,
                    minecraftVersion,
                    fabricVersion,
                    ramMb);

                // -------------------------------------------------
                // PROCESS OUTPUT
                // -------------------------------------------------

                ConfigureProcessLogging(process);

                StatusText.Text =
                    "Starting Minecraft...";

                WriteLauncherLog(
                    "Calling process.Start()...");

                bool started =
                    process.Start();

                if (!started)
                {
                    throw new Exception(
                        "Process.Start() returned false.");
                }

                WriteLauncherLog(
                    $"Minecraft process started. PID={process.Id}");

                StatusText.Text =
                    $"Minecraft running as {_session.Username}";

                _ = MonitorMinecraftAsync(process);
            }
            catch (Exception ex)
            {
                WriteLauncherException(ex);

                StatusText.Text =
                    "Launch Failed!";

                MessageBox.Show(
                    "Minecraft failed to launch.\n\n" +
                    ex.Message +
                    "\n\n" +
                    "Debug file:\n" +
                    _debugFilePath +
                    "\n\n" +
                    "Minecraft log:\n" +
                    _logFilePath,
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
        // FABRIC INSTALL
        // =========================================================

        private async Task<string> InstallFabricAsync(
            string minecraftVersion,
            string loaderVersion)
        {
            string versionId =
                $"fabric-loader-{loaderVersion}-{minecraftVersion}";

            string versionsFolder =
                Path.Combine(
                    _gamePath,
                    "versions");

            string versionFolder =
                Path.Combine(
                    versionsFolder,
                    versionId);

            Directory.CreateDirectory(
                versionFolder);

            string jsonFile =
                Path.Combine(
                    versionFolder,
                    $"{versionId}.json");

            string url =
                "https://meta.fabricmc.net/v2/versions/loader/" +
                Uri.EscapeDataString(minecraftVersion) +
                "/" +
                Uri.EscapeDataString(loaderVersion) +
                "/profile/json";

            WriteLauncherLog(
                $"Fabric profile URL: {url}");

            string json =
                await _httpClient.GetStringAsync(url);

            if (string.IsNullOrWhiteSpace(json))
                throw new Exception(
                    "Fabric returned an empty profile.");

            await File.WriteAllTextAsync(
                jsonFile,
                json);

            WriteLauncherLog(
                $"Fabric profile saved: {jsonFile}");

            return versionId;
        }

        // =========================================================
        // FABRIC LIBRARIES
        // =========================================================

        private async Task DownloadFabricLibrariesAsync(
            string fabricVersion)
        {
            string jsonPath =
                Path.Combine(
                    _gamePath,
                    "versions",
                    fabricVersion,
                    $"{fabricVersion}.json");

            if (!File.Exists(jsonPath))
                throw new FileNotFoundException(
                    "Fabric profile JSON was not found.",
                    jsonPath);

            using JsonDocument document =
                JsonDocument.Parse(
                    await File.ReadAllTextAsync(jsonPath));

            JsonElement root =
                document.RootElement;

            if (!root.TryGetProperty(
                    "libraries",
                    out JsonElement libraries))
            {
                WriteLauncherLog(
                    "Fabric profile has no libraries section.");

                return;
            }

            foreach (JsonElement library in
                     libraries.EnumerateArray())
            {
                if (!library.TryGetProperty(
                        "name",
                        out JsonElement nameElement))
                    continue;

                string name =
                    nameElement.GetString() ?? "";

                if (string.IsNullOrWhiteSpace(name))
                    continue;

                string? url = null;

                if (library.TryGetProperty(
                        "url",
                        out JsonElement urlElement))
                {
                    url =
                        urlElement.GetString();
                }

                if (string.IsNullOrWhiteSpace(url))
                {
                    url =
                        "https://maven.fabricmc.net/";
                }

                string relative =
                    MavenNameToPath(name);

                string destination =
                    Path.Combine(
                        _gamePath,
                        "libraries",
                        relative);

                if (File.Exists(destination) &&
                    new FileInfo(destination).Length > 0)
                {
                    continue;
                }

                string downloadUrl =
                    CombineUrl(
                        url,
                        relative.Replace(
                            '\\',
                            '/'));

                Directory.CreateDirectory(
                    Path.GetDirectoryName(
                        destination)!);

                StatusText.Text =
                    $"Downloading {Path.GetFileName(destination)}...";

                WriteLauncherLog(
                    $"Fabric library: {downloadUrl}");

                byte[] data =
                    await _httpClient.GetByteArrayAsync(
                        downloadUrl);

                await File.WriteAllBytesAsync(
                    destination,
                    data);
            }
        }

        private static string MavenNameToPath(
            string name)
        {
            string[] parts =
                name.Split(':');

            if (parts.Length < 3)
                throw new Exception(
                    $"Invalid Maven library name: {name}");

            string group =
                parts[0].Replace('.', Path.DirectorySeparatorChar);

            string artifact =
                parts[1];

            string version =
                parts[2];

            string classifier = "";
            string extension = "jar";

            if (parts.Length >= 4)
            {
                string fourth =
                    parts[3];

                if (fourth.Contains('.'))
                    extension =
                        fourth;
                else
                    classifier =
                        fourth;
            }

            string filename =
                $"{artifact}-{version}";

            if (!string.IsNullOrWhiteSpace(classifier))
                filename +=
                    $"-{classifier}";

            filename +=
                $".{extension}";

            return Path.Combine(
                group,
                artifact,
                version,
                filename);
        }

        private static string CombineUrl(
            string baseUrl,
            string relative)
        {
            if (!baseUrl.EndsWith("/"))
                baseUrl += "/";

            return baseUrl + relative;
        }

        // =========================================================
        // JAVA
        // =========================================================

        private string FindJava21()
        {
            string bundled =
                Path.Combine(
                    _gamePath,
                    "runtime",
                    "java21",
                    "bin",
                    "java.exe");

            if (File.Exists(bundled) &&
                IsJava21(bundled))
            {
                return bundled;
            }

            string javaHome =
                Environment.GetEnvironmentVariable(
                    "JAVA_HOME")
                ?? "";

            if (!string.IsNullOrWhiteSpace(javaHome))
            {
                string candidate =
                    Path.Combine(
                        javaHome,
                        "bin",
                        "java.exe");

                if (File.Exists(candidate) &&
                    IsJava21(candidate))
                {
                    return candidate;
                }
            }

            string path =
                Environment.GetEnvironmentVariable(
                    "PATH")
                ?? "";

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

                if (IsJava21(candidate))
                    return candidate;
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

                using Process? process =
                    Process.Start(info);

                if (process == null)
                    return false;

                string output =
                    process.StandardOutput.ReadToEnd();

                string error =
                    process.StandardError.ReadToEnd();

                process.WaitForExit();

                string combined =
                    output + "\n" + error;

                WriteLauncherLog(
                    $"Java version check:\n{combined}");

                return combined.Contains(
                    "version \"21.",
                    StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                WriteLauncherLog(
                    $"Java check failed: {ex.Message}");

                return false;
            }
        }

        // =========================================================
        // CMLLIB PROGRESS
        // =========================================================

        private void Launcher_FileProgressChanged(
            object? sender,
            FileProgressChangedEventArgs args)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    StatusText.Text =
                        $"Installing {args.Name} " +
                        $"({args.EventType})";
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
            }
        }

        // =========================================================
        // PROCESS LOGGING
        // =========================================================

        private void ConfigureProcessLogging(
            Process process)
        {
            try
            {
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;

                process.OutputDataReceived +=
                    Minecraft_OutputDataReceived;

                process.ErrorDataReceived +=
                    Minecraft_ErrorDataReceived;

                process.EnableRaisingEvents = true;

                File.WriteAllText(
                    _logFilePath,
                    "===== TOPU CLIENT MINECRAFT LOG =====\r\n" +
                    $"Started: {DateTime.Now:O}\r\n\r\n");
            }
            catch (Exception ex)
            {
                WriteLauncherLog(
                    "Process logging setup failed: " +
                    ex);
            }
        }

        private void Minecraft_OutputDataReceived(
            object? sender,
            DataReceivedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                AppendMinecraftLog(
                    "[STDOUT] " + e.Data);
            }
        }

        private void Minecraft_ErrorDataReceived(
            object? sender,
            DataReceivedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                AppendMinecraftLog(
                    "[STDERR] " + e.Data);
            }
        }

        private void AppendMinecraftLog(
            string text)
        {
            try
            {
                File.AppendAllText(
                    _logFilePath,
                    $"[{DateTime.Now:HH:mm:ss}] {text}\r\n");
            }
            catch
            {
            }
        }

        // =========================================================
        // PROCESS MONITOR
        // =========================================================

        private async Task MonitorMinecraftAsync(
            Process process)
        {
            try
            {
                try
                {
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                }
                catch (Exception ex)
                {
                    WriteLauncherLog(
                        "Could not begin async output reading: " +
                        ex.Message);
                }

                await Task.Run(
                    () => process.WaitForExit());

                int exitCode =
                    process.ExitCode;

                AppendMinecraftLog(
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

                        MessageBox.Show(
                            "Minecraft exited with an error.\n\n" +
                            $"Exit code: {exitCode}\n\n" +
                            $"Minecraft log:\n{_logFilePath}",
                            "Topu Client",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                });
            }
            catch (Exception ex)
            {
                WriteLauncherException(ex);
            }
            finally
            {
                _minecraftProcess = null;
            }
        }

        // =========================================================
        // DEBUG FILE
        // =========================================================

        private void WriteDebugInformation(
            Process process,
            string javaPath,
            string minecraftVersion,
            string fabricVersion,
            int ramMb)
        {
            try
            {
                string arguments =
                    process.StartInfo.Arguments;

                string executable =
                    process.StartInfo.FileName;

                string workingDirectory =
                    process.StartInfo.WorkingDirectory;

                string text =
                    "===== TOPU CLIENT DEBUG =====\r\n\r\n" +
                    $"Time:\r\n{DateTime.Now:O}\r\n\r\n" +
                    $"Executable:\r\n{executable}\r\n\r\n" +
                    $"Arguments:\r\n{arguments}\r\n\r\n" +
                    $"Working Directory:\r\n{workingDirectory}\r\n\r\n" +
                    $"Java:\r\n{javaPath}\r\n\r\n" +
                    $"Minecraft:\r\n{minecraftVersion}\r\n\r\n" +
                    $"Fabric:\r\n{fabricVersion}\r\n\r\n" +
                    $"RAM:\r\n{ramMb} MB\r\n\r\n" +
                    $"Username:\r\n{_session?.Username}\r\n";

                File.WriteAllText(
                    _debugFilePath,
                    text);
            }
            catch (Exception ex)
            {
                WriteLauncherLog(
                    "Debug file failed: " + ex);
            }
        }

        // =========================================================
        // LOGGING
        // =========================================================

        private void WriteLauncherLog(
            string text)
        {
            try
            {
                File.AppendAllText(
                    _logFilePath,
                    $"[{DateTime.Now:HH:mm:ss}] {text}\r\n");
            }
            catch
            {
            }
        }

        private void WriteLauncherException(
            Exception ex)
        {
            try
            {
                File.AppendAllText(
                    _logFilePath,
                    "\r\n===== LAUNCHER EXCEPTION =====\r\n" +
                    $"Time: {DateTime.Now:O}\r\n" +
                    ex +
                    "\r\n");
            }
            catch
            {
            }
        }

        // =========================================================
        // FILENAME
        // =========================================================

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
                        // Do NOT kill unrelated Java processes.
                        // Only the process created by TopuClient
                        // is considered here.
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
