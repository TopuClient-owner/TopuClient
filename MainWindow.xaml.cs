using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
        private MSession? _session;
        private Process? _minecraftProcess;

        private static readonly HttpClient Http = new HttpClient(
            new HttpClientHandler
            {
                AllowAutoRedirect = true
            })
        {
            Timeout = TimeSpan.FromMinutes(10)
        };

        private readonly string _gamePath;
        private readonly string _usernameFile;
        private readonly string _launcherLogFile;
        private readonly string _debugFile;

        private const string DefaultMinecraftVersion = "1.21.1";
        private const string FabricLoaderVersion = "0.19.3";

        public MainWindow()
        {
            InitializeComponent();

            Http.DefaultRequestHeaders.UserAgent.ParseAdd(
                "TopuClient/1.0 MinecraftLauncher");

            _gamePath = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.ApplicationData),
                ".topuclient");

            Directory.CreateDirectory(_gamePath);

            _usernameFile = Path.Combine(
                _gamePath,
                "username.txt");

            _launcherLogFile = Path.Combine(
                _gamePath,
                "topu-minecraft.log");

            _debugFile = Path.Combine(
                _gamePath,
                "topu-launch-debug.txt");

            LoadSavedUsername();

            WriteLog("===== TOPU CLIENT STARTED =====");
            WriteLog($"Game directory: {_gamePath}");
        }

        // =========================================================
        // BASIC LOGGING
        // =========================================================

        private void WriteLog(string text)
        {
            try
            {
                Directory.CreateDirectory(_gamePath);

                File.AppendAllText(
                    _launcherLogFile,
                    $"[{DateTime.Now:HH:mm:ss}] {text}{Environment.NewLine}");
            }
            catch
            {
                // Logging must never crash the launcher.
            }
        }

        private void WriteException(string title, Exception ex)
        {
            WriteLog("");
            WriteLog($"===== {title} =====");
            WriteLog(ex.ToString());
            WriteLog("");
        }

        private void SetStatus(string text)
        {
            Dispatcher.Invoke(() =>
            {
                if (StatusText != null)
                    StatusText.Text = text;
            });
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

                if (UsernameInput != null)
                    UsernameInput.Text = username;

                _session =
                    MSession.CreateOfflineSession(username);
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
                    _usernameFile,
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
            if (e.ChangedButton == MouseButton.Left)
            {
                try
                {
                    DragMove();
                }
                catch
                {
                }
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
            try
            {
                if (_minecraftProcess != null &&
                    !_minecraftProcess.HasExited)
                {
                    // Do not kill Minecraft automatically.
                    // Let the game continue running.
                }
            }
            catch
            {
            }

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
                $"Profile saved: Fabric {version}, {ram}GB RAM";

            MessageBox.Show(
                "Profile settings saved successfully!",
                "Topu Client",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private string GetSelectedMinecraftVersion()
        {
            return
                (VersionBox.SelectedItem as ComboBoxItem)
                    ?.Content
                    ?.ToString()
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
        // SERVER BUTTONS
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
        // MODRINTH
        // =========================================================

        private async void SearchModrinth_Click(
            object sender,
            RoutedEventArgs e)
        {
            string query =
                ModSearchInput?.Text?.Trim() ?? "";

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
                    await Http.GetStringAsync(searchUrl);

                using JsonDocument document =
                    JsonDocument.Parse(response);

                JsonElement hits =
                    document.RootElement
                        .GetProperty("hits");

                if (hits.GetArrayLength() == 0)
                {
                    ModSearchStatus.Text =
                        "No mods found.";

                    return;
                }

                JsonElement hit = hits[0];

                string title =
                    hit.TryGetProperty(
                        "title",
                        out JsonElement titleElement)
                    ? titleElement.GetString() ?? query
                    : query;

                string projectId =
                    hit.TryGetProperty(
                        "project_id",
                        out JsonElement idElement)
                    ? idElement.GetString() ?? ""
                    : "";

                if (string.IsNullOrWhiteSpace(projectId))
                {
                    ModSearchStatus.Text =
                        "Mod project ID missing.";

                    return;
                }

                string mcVersion =
                    GetSelectedMinecraftVersion();

                string versionUrl =
                    $"https://api.modrinth.com/v2/project/" +
                    $"{Uri.EscapeDataString(projectId)}/version" +
                    "?loaders=%5B%22fabric%22%5D" +
                    $"&game_versions=%5B%22{Uri.EscapeDataString(mcVersion)}%22%5D";

                string versionsResponse =
                    await Http.GetStringAsync(versionUrl);

                using JsonDocument versionsDocument =
                    JsonDocument.Parse(versionsResponse);

                JsonElement versions =
                    versionsDocument.RootElement;

                if (versions.GetArrayLength() == 0)
                {
                    ModSearchStatus.Text =
                        $"No Fabric build for Minecraft {mcVersion}.";

                    return;
                }

                JsonElement version =
                    versions[0];

                JsonElement files =
                    version.GetProperty("files");

                if (files.GetArrayLength() == 0)
                {
                    ModSearchStatus.Text =
                        "No downloadable file.";

                    return;
                }

                string downloadUrl = "";
                string filename = $"{title}.jar";

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

                    break;
                }

                if (string.IsNullOrWhiteSpace(downloadUrl))
                {
                    JsonElement first =
                        files[0];

                    downloadUrl =
                        first.GetProperty("url")
                            .GetString()
                        ?? "";

                    if (first.TryGetProperty(
                            "filename",
                            out JsonElement filenameElement))
                    {
                        filename =
                            filenameElement.GetString()
                            ?? filename;
                    }
                }

                if (string.IsNullOrWhiteSpace(downloadUrl))
                {
                    ModSearchStatus.Text =
                        "Download URL missing.";

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
                    await Http.GetByteArrayAsync(
                        downloadUrl);

                await File.WriteAllBytesAsync(
                    destination,
                    data);

                ModSearchStatus.Text =
                    $"Added {title}";

                MessageBox.Show(
                    $"Installed:\n{title}\n\n{destination}",
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
                    "Modrinth search failed.";

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
                PrepareLauncherLog();

                string mcVersion =
                    GetSelectedMinecraftVersion();

                int ramMb =
                    Math.Max(
                        1024,
                        (int)RamSlider.Value * 1024);

                WriteLog(
                    "===== TOPU CLIENT MINECRAFT LAUNCH =====");

                WriteLog(
                    $"Minecraft: {mcVersion}");

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
                        UsernameInput?.Text?.Trim() ?? "";

                    if (string.IsNullOrWhiteSpace(username))
                        username = "TopuPlayer";

                    _session =
                        MSession.CreateOfflineSession(
                            username);

                    SaveUsername(username);

                    WriteLog(
                        $"Offline username: {username}");
                }

                if (_session == null)
                {
                    throw new InvalidOperationException(
                        "No Minecraft session exists.");
                }

                // -------------------------------------------------
                // JAVA
                // -------------------------------------------------

                SetStatus(
                    "Checking Java 21...");

                string java =
                    FindJava21();

                if (string.IsNullOrWhiteSpace(java))
                {
                    throw new FileNotFoundException(
                        "Java 21 was not found.");
                }

                WriteLog(
                    $"Java: {java}");

                SetStatus(
                    "Java 21 found.");

                // -------------------------------------------------
                // CMLLIB PATH
                // -------------------------------------------------

                var minecraftPath =
                    new MinecraftPath(
                        _gamePath);

                var launcher =
                    new MinecraftLauncher(
                        minecraftPath);

                // CmlLib.Core 4.0.6 exposes ByteProgress
                // with ProgressedBytes and TotalBytes.
                launcher.ByteProgressChanged +=
                    (_, args) =>
                    {
                        try
                        {
                            if (args.TotalBytes > 0)
                            {
                                double percent =
                                    args.ProgressedBytes *
                                    100.0 /
                                    args.TotalBytes;

                                SetStatus(
                                    $"Downloading Minecraft: {percent:0}%");
                            }
                            else
                            {
                                SetStatus(
                                    $"Downloading: " +
                                    $"{args.ProgressedBytes:N0} bytes");
                            }
                        }
                        catch
                        {
                        }
                    };

                // Do NOT reference FileProgressChangedEventArgs.
                // CmlLib.Core 4.0.6's example simply uses
                // the event's inferred argument type.
                launcher.FileProgressChanged +=
                    (_, args) =>
                    {
                        try
                        {
                            SetStatus(
                                $"Minecraft files: {args.Name}");
                        }
                        catch
                        {
                        }
                    };

                // -------------------------------------------------
                // VANILLA
                // -------------------------------------------------

                SetStatus(
                    $"Installing Minecraft {mcVersion}...");

                await launcher.InstallAsync(
                    mcVersion);

                WriteLog(
                    "Minecraft installation completed.");

                // -------------------------------------------------
                // FABRIC
                // -------------------------------------------------

                SetStatus(
                    $"Installing Fabric {FabricLoaderVersion}...");

                string fabricVersion =
                    await InstallFabricAsync(
                        mcVersion,
                        FabricLoaderVersion);

                WriteLog(
                    $"Fabric version: {fabricVersion}");

                // -------------------------------------------------
                // VERIFY FABRIC
                // -------------------------------------------------

                SetStatus(
                    "Verifying Fabric libraries...");

                await VerifyFabricInstallationAsync(
                    mcVersion,
                    FabricLoaderVersion);

                WriteLog(
                    "Fabric libraries verified.");

                // -------------------------------------------------
                // MODS
                // -------------------------------------------------

                await InstallOptimizationModsAsync(
                    mcVersion);

                // -------------------------------------------------
                // CMLLIB LAUNCH
                // -------------------------------------------------

                SetStatus(
                    "Building Minecraft process...");

                var options =
                    new MLaunchOption
                    {
                        Session = _session,
                        MaximumRamMb = ramMb,
                        JavaPath = java
                    };

                /*
                 * This is the actual CmlLib.Core 4.0.6 API:
                 *
                 * await launcher.InstallAsync(version);
                 * await launcher.BuildProcessAsync(version, options);
                 *
                 * CmlLib supports custom/Fabric versions.
                 */

                Process process =
                    await launcher.BuildProcessAsync(
                        fabricVersion,
                        options);

                if (process == null)
                {
                    throw new InvalidOperationException(
                        "CmlLib returned a null Process.");
                }

                _minecraftProcess =
                    process;

                // -------------------------------------------------
                // PROCESS CONFIGURATION
                // -------------------------------------------------

                try
                {
                    process.StartInfo.WorkingDirectory =
                        _gamePath;
                }
                catch
                {
                }

                SaveDebugInformation(
                    process,
                    java,
                    mcVersion,
                    fabricVersion,
                    ramMb);

                AttachMinecraftLogging(
                    process);

                // -------------------------------------------------
                // START
                // -------------------------------------------------

                SetStatus(
                    "Starting Minecraft...");

                WriteLog(
                    "Starting Minecraft process.");

                bool started =
                    process.Start();

                if (!started)
                {
                    throw new InvalidOperationException(
                        "Process.Start() returned false.");
                }

                WriteLog(
                    $"Minecraft process started. PID: {process.Id}");

                SetStatus(
                    $"Topu Client running as {_session.Username}");

                _ = MonitorMinecraftAsync(
                    process);
            }
            catch (Exception ex)
            {
                WriteException(
                    "TOPU CLIENT LAUNCH ERROR",
                    ex);

                SetStatus(
                    "Minecraft launch failed.");

                MessageBox.Show(
                    "Minecraft failed to launch.\n\n" +
                    ex.Message +
                    "\n\n" +
                    "Check:\n" +
                    _launcherLogFile,
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
        // FABRIC INSTALLATION
        // =========================================================

        private async Task<string> InstallFabricAsync(
            string minecraftVersion,
            string loaderVersion)
        {
            string fabricVersion =
                $"fabric-loader-{loaderVersion}-{minecraftVersion}";

            string versionsFolder =
                Path.Combine(
                    _gamePath,
                    "versions");

            string versionFolder =
                Path.Combine(
                    versionsFolder,
                    fabricVersion);

            Directory.CreateDirectory(
                versionFolder);

            string jsonPath =
                Path.Combine(
                    versionFolder,
                    $"{fabricVersion}.json");

            string profileUrl =
                "https://meta.fabricmc.net/v2/versions/loader/" +
                $"{Uri.EscapeDataString(minecraftVersion)}/" +
                $"{Uri.EscapeDataString(loaderVersion)}/" +
                "profile/json";

            WriteLog(
                $"Fabric profile URL: {profileUrl}");

            SetStatus(
                "Downloading Fabric profile...");

            string json =
                await Http.GetStringAsync(
                    profileUrl);

            if (string.IsNullOrWhiteSpace(json))
            {
                throw new InvalidOperationException(
                    "Fabric returned an empty profile.");
            }

            await File.WriteAllTextAsync(
                jsonPath,
                json);

            WriteLog(
                $"Fabric profile saved: {jsonPath}");

            using JsonDocument document =
                JsonDocument.Parse(json);

            JsonElement root =
                document.RootElement;

            // -----------------------------------------------------
            // Download every Fabric library listed in the profile.
            // This is the important part missing from the previous
            // implementation.
            // -----------------------------------------------------

            await DownloadFabricLibrarySetAsync(
                root,
                "common");

            await DownloadFabricLibrarySetAsync(
                root,
                "client");

            return fabricVersion;
        }

        private async Task DownloadFabricLibrarySetAsync(
            JsonElement profile,
            string section)
        {
            if (!profile.TryGetProperty(
                    "libraries",
                    out JsonElement libraries))
            {
                throw new InvalidOperationException(
                    "Fabric profile contains no libraries.");
            }

            if (!libraries.TryGetProperty(
                    section,
                    out JsonElement sectionElement))
            {
                return;
            }

            foreach (JsonElement library in
                     sectionElement.EnumerateArray())
            {
                if (!library.TryGetProperty(
                        "name",
                        out JsonElement nameElement))
                {
                    continue;
                }

                string coordinate =
                    nameElement.GetString() ?? "";

                if (string.IsNullOrWhiteSpace(coordinate))
                    continue;

                string baseUrl =
                    "https://maven.fabricmc.net/";

                if (library.TryGetProperty(
                        "url",
                        out JsonElement urlElement))
                {
                    string? customUrl =
                        urlElement.GetString();

                    if (!string.IsNullOrWhiteSpace(
                            customUrl))
                    {
                        baseUrl =
                            customUrl.TrimEnd('/') +
                            "/";
                    }
                }

                string artifactPath =
                    MavenCoordinateToPath(
                        coordinate);

                string destination =
                    Path.Combine(
                        _gamePath,
                        "libraries",
                        artifactPath.Replace(
                            '/',
                            Path.DirectorySeparatorChar));

                string? directory =
                    Path.GetDirectoryName(
                        destination);

                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(
                        directory);
                }

                if (IsValidJar(destination))
                {
                    WriteLog(
                        $"Fabric library already exists: {coordinate}");

                    continue;
                }

                string downloadUrl =
                    baseUrl + artifactPath;

                SetStatus(
                    $"Downloading Fabric library: {coordinate}");

                WriteLog(
                    $"Downloading: {downloadUrl}");

                byte[] data =
                    await Http.GetByteArrayAsync(
                        downloadUrl);

                if (data.Length < 100)
                {
                    throw new InvalidOperationException(
                        $"Fabric library download was invalid: {coordinate}");
                }

                await File.WriteAllBytesAsync(
                    destination,
                    data);

                WriteLog(
                    $"Installed Fabric library: {coordinate}");
            }
        }

        private static string MavenCoordinateToPath(
            string coordinate)
        {
            string[] parts =
                coordinate.Split(
                    ':',
                    StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 3)
            {
                throw new InvalidOperationException(
                    $"Invalid Maven coordinate: {coordinate}");
            }

            string group =
                parts[0].Replace(
                    '.',
                    '/');

            string artifact =
                parts[1];

            string version =
                parts[2];

            string classifier = "";
            string extension = "jar";

            if (parts.Length >= 4)
            {
                classifier =
                    parts[3];
            }

            if (parts.Length >= 5)
            {
                extension =
                    parts[4];
            }

            string filename =
                $"{artifact}-{version}";

            if (!string.IsNullOrWhiteSpace(
                    classifier))
            {
                filename +=
                    $"-{classifier}";
            }

            filename +=
                $".{extension}";

            return
                $"{group}/{artifact}/{version}/{filename}";
        }

        // =========================================================
        // VERIFY FABRIC
        // =========================================================

        private async Task VerifyFabricInstallationAsync(
            string minecraftVersion,
            string loaderVersion)
        {
            string loaderJar =
                Path.Combine(
                    _gamePath,
                    "libraries",
                    "net",
                    "fabricmc",
                    "fabric-loader",
                    loaderVersion,
                    $"fabric-loader-{loaderVersion}.jar");

            WriteLog(
                $"Expected Fabric loader: {loaderJar}");

            if (!IsValidJar(loaderJar))
            {
                WriteLog(
                    "Fabric loader JAR missing/invalid. Downloading directly.");

                string url =
                    "https://maven.fabricmc.net/net/fabricmc/" +
                    $"fabric-loader/{loaderVersion}/" +
                    $"fabric-loader-{loaderVersion}.jar";

                Directory.CreateDirectory(
                    Path.GetDirectoryName(loaderJar)!);

                byte[] data =
                    await Http.GetByteArrayAsync(
                        url);

                await File.WriteAllBytesAsync(
                    loaderJar,
                    data);
            }

            if (!IsValidJar(loaderJar))
            {
                throw new InvalidOperationException(
                    "Fabric loader JAR is missing or corrupted:\n" +
                    loaderJar);
            }

            // Make sure the actual KnotClient class exists.
            bool knotExists =
                JarContainsEntry(
                    loaderJar,
                    "net/fabricmc/loader/impl/launch/knot/KnotClient.class");

            WriteLog(
                $"KnotClient present: {knotExists}");

            if (!knotExists)
            {
                throw new InvalidOperationException(
                    "Fabric loader JAR does not contain KnotClient.class:\n" +
                    loaderJar);
            }

            // Check intermediary from the Fabric profile.
            string intermediaryJar =
                Path.Combine(
                    _gamePath,
                    "libraries",
                    "net",
                    "fabricmc",
                    "intermediary",
                    minecraftVersion,
                    $"intermediary-{minecraftVersion}.jar");

            if (!IsValidJar(intermediaryJar))
            {
                WriteLog(
                    "Intermediary JAR missing.");
            }
            else
            {
                WriteLog(
                    $"Intermediary verified: {intermediaryJar}");
            }
        }

        private static bool IsValidJar(
            string path)
        {
            try
            {
                if (!File.Exists(path))
                    return false;

                FileInfo info =
                    new FileInfo(path);

                return info.Length > 1000;
            }
            catch
            {
                return false;
            }
        }

        private static bool JarContainsEntry(
            string jarPath,
            string entryName)
        {
            try
            {
                using FileStream stream =
                    File.OpenRead(jarPath);

                using System.IO.Compression.ZipArchive archive =
                    new System.IO.Compression.ZipArchive(
                        stream,
                        System.IO.Compression.ZipArchiveMode.Read);

                return archive.GetEntry(entryName) != null;
            }
            catch
            {
                return false;
            }
        }

        // =========================================================
        // OPTIMIZATION MODS
        // =========================================================

        private async Task InstallOptimizationModsAsync(
            string minecraftVersion)
        {
            string modsFolder =
                Path.Combine(
                    _gamePath,
                    "mods");

            Directory.CreateDirectory(
                modsFolder);

            // These are optional. If one fails, Minecraft can
            // still launch.
            string[] projects =
            {
                "fabric-api",
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
                    WriteException(
                        $"OPTIONAL MOD FAILED: {project}",
                        ex);
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
                $"?query={Uri.EscapeDataString(project)}" +
                "&facets=%5B%5B%22project_type%3Amod%22%5D%5D";

            string response =
                await Http.GetStringAsync(
                    searchUrl);

            using JsonDocument document =
                JsonDocument.Parse(response);

            JsonElement hits =
                document.RootElement.GetProperty("hits");

            if (hits.GetArrayLength() == 0)
                return;

            JsonElement hit =
                hits[0];

            string projectId =
                hit.GetProperty("project_id")
                    .GetString()
                ?? "";

            if (string.IsNullOrWhiteSpace(projectId))
                return;

            string versionsUrl =
                $"https://api.modrinth.com/v2/project/" +
                $"{Uri.EscapeDataString(projectId)}/version" +
                "?loaders=%5B%22fabric%22%5D" +
                $"&game_versions=%5B%22{Uri.EscapeDataString(minecraftVersion)}%22%5D";

            string versionsResponse =
                await Http.GetStringAsync(
                    versionsUrl);

            using JsonDocument versionsDocument =
                JsonDocument.Parse(versionsResponse);

            JsonElement versions =
                versionsDocument.RootElement;

            if (versions.GetArrayLength() == 0)
                return;

            JsonElement version =
                versions[0];

            if (!version.TryGetProperty(
                    "files",
                    out JsonElement files))
            {
                return;
            }

            string url = "";
            string filename =
                $"{project}.jar";

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
                        out JsonElement urlElement))
                {
                    url =
                        urlElement.GetString()
                        ?? "";
                }

                if (file.TryGetProperty(
                        "filename",
                        out JsonElement filenameElement))
                {
                    filename =
                        filenameElement.GetString()
                        ?? filename;
                }

                break;
            }

            if (string.IsNullOrWhiteSpace(url) &&
                files.GetArrayLength() > 0)
            {
                JsonElement first =
                    files[0];

                url =
                    first.GetProperty("url")
                        .GetString()
                    ?? "";

                if (first.TryGetProperty(
                        "filename",
                        out JsonElement filenameElement))
                {
                    filename =
                        filenameElement.GetString()
                        ?? filename;
                }
            }

            if (string.IsNullOrWhiteSpace(url))
                return;

            string destination =
                Path.Combine(
                    modsFolder,
                    SanitizeFileName(filename));

            if (IsValidJar(destination))
            {
                WriteLog(
                    $"Mod already installed: {project}");

                return;
            }

            SetStatus(
                $"Downloading {project}...");

            byte[] data =
                await Http.GetByteArrayAsync(url);

            await File.WriteAllBytesAsync(
                destination,
                data);

            WriteLog(
                $"Installed mod: {project}");
        }

        // =========================================================
        // JAVA
        // =========================================================

        private string FindJava21()
        {
            string local =
                Path.Combine(
                    _gamePath,
                    "runtime",
                    "java21",
                    "bin",
                    "java.exe");

            if (File.Exists(local) &&
                IsJava21(local))
            {
                return local;
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

            foreach (string directory in
                     path.Split(
                         Path.PathSeparator,
                         StringSplitOptions.RemoveEmptyEntries))
            {
                string candidate =
                    Path.Combine(
                        directory.Trim(),
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

                using Process process =
                    Process.Start(info)
                    ?? throw new InvalidOperationException(
                        "Could not start Java.");

                string output =
                    process.StandardOutput.ReadToEnd();

                string error =
                    process.StandardError.ReadToEnd();

                process.WaitForExit();

                string combined =
                    output + Environment.NewLine + error;

                WriteLog(
                    $"Java version check: {combined.Trim()}");

                return combined.Contains(
                    "version \"21.",
                    StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                WriteException(
                    "JAVA CHECK ERROR",
                    ex);

                return false;
            }
        }

        // =========================================================
        // PROCESS LOGGING
        // =========================================================

        private void AttachMinecraftLogging(
            Process process)
        {
            try
            {
                process.EnableRaisingEvents = true;

                process.OutputDataReceived +=
                    MinecraftOutput;

                process.ErrorDataReceived +=
                    MinecraftError;

                // CmlLib's generated Process normally has
                // redirected streams. If it doesn't, the calls
                // simply fail and the launcher still works.
                try
                {
                    process.BeginOutputReadLine();
                }
                catch (Exception ex)
                {
                    WriteLog(
                        $"Could not begin stdout capture: {ex.Message}");
                }

                try
                {
                    process.BeginErrorReadLine();
                }
                catch (Exception ex)
                {
                    WriteLog(
                        $"Could not begin stderr capture: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                WriteException(
                    "PROCESS LOGGING ERROR",
                    ex);
            }
        }

        private void MinecraftOutput(
            object sender,
            DataReceivedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Data))
                return;

            WriteLog(
                "[STDOUT] " + e.Data);
        }

        private void MinecraftError(
            object sender,
            DataReceivedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Data))
                return;

            WriteLog(
                "[STDERR] " + e.Data);
        }

        // =========================================================
        // MONITOR
        // =========================================================

        private async Task MonitorMinecraftAsync(
            Process process)
        {
            try
            {
                await Task.Run(
                    process.WaitForExit);

                int exitCode =
                    process.ExitCode;

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

                        MessageBox.Show(
                            "Minecraft exited with code " +
                            $"{exitCode}.\n\n" +
                            "The launcher log is here:\n" +
                            _launcherLogFile,
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
        // DEBUG FILE
        // =========================================================

        private void SaveDebugInformation(
            Process process,
            string java,
            string minecraftVersion,
            string fabricVersion,
            int ramMb)
        {
            try
            {
                StringBuilder builder =
                    new StringBuilder();

                builder.AppendLine(
                    "===== TOPU CLIENT DEBUG =====");

                builder.AppendLine();

                builder.AppendLine(
                    $"Time: {DateTime.Now:O}");

                builder.AppendLine();

                builder.AppendLine(
                    "Executable:");

                builder.AppendLine(
                    java);

                builder.AppendLine();

                builder.AppendLine(
                    "Arguments:");

                try
                {
                    builder.AppendLine(
                        process.StartInfo.Arguments);
                }
                catch
                {
                    builder.AppendLine(
                        "(arguments unavailable)");
                }

                builder.AppendLine();

                builder.AppendLine(
                    "Working Directory:");

                builder.AppendLine(
                    _gamePath);

                builder.AppendLine();

                builder.AppendLine(
                    "Minecraft:");

                builder.AppendLine(
                    minecraftVersion);

                builder.AppendLine();

                builder.AppendLine(
                    "Fabric:");

                builder.AppendLine(
                    fabricVersion);

                builder.AppendLine();

                builder.AppendLine(
                    "Java:");

                builder.AppendLine(
                    java);

                builder.AppendLine();

                builder.AppendLine(
                    "RAM:");

                builder.AppendLine(
                    $"{ramMb} MB");

                builder.AppendLine();

                builder.AppendLine(
                    "Username:");

                builder.AppendLine(
                    _session?.Username ?? "(none)");

                builder.AppendLine();

                builder.AppendLine(
                    "Process ID:");

                try
                {
                    builder.AppendLine(
                        process.Id.ToString());
                }
                catch
                {
                    builder.AppendLine(
                        "(unavailable)");
                }

                File.WriteAllText(
                    _debugFile,
                    builder.ToString());
            }
            catch (Exception ex)
            {
                WriteException(
                    "DEBUG FILE ERROR",
                    ex);
            }
        }

        private void PrepareLauncherLog()
        {
            try
            {
                Directory.CreateDirectory(
                    _gamePath);

                File.WriteAllText(
                    _launcherLogFile,
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

        // =========================================================
        // HELPERS
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
    }
}
