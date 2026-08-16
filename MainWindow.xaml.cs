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

        private static readonly HttpClient _httpClient =
            new HttpClient(new HttpClientHandler
            {
                AllowAutoRedirect = true
            })
            {
                DefaultRequestHeaders =
                {
                    {
                        "User-Agent",
                        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) TopuClient/1.0"
                    }
                }
            };

        private readonly string _configFilePath;

        public MainWindow()
        {
            InitializeComponent();

            string appFolder = GetGamePath();

            Directory.CreateDirectory(appFolder);

            _configFilePath =
                Path.Combine(
                    appFolder,
                    "username.txt"
                );

            LoadSavedUsername();
        }

        // =========================================================
        // PATHS
        // =========================================================

        private static string GetGamePath()
        {
            return Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.ApplicationData),
                ".topuclient"
            );
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

                string savedUser =
                    File.ReadAllText(
                        _configFilePath
                    ).Trim();

                if (string.IsNullOrWhiteSpace(savedUser))
                    return;

                if (UsernameInput != null)
                    UsernameInput.Text = savedUser;

                _session =
                    MSession.CreateOfflineSession(
                        savedUser
                    );
            }
            catch
            {
            }
        }

        private void SaveUsername(string? username)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(username))
                {
                    File.WriteAllText(
                        _configFilePath,
                        username
                    );
                }
            }
            catch
            {
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
                DragMove();
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
            if (sender is not Button btn ||
                btn.Tag is not string targetTab)
            {
                return;
            }

            TabLaunch.Visibility =
                Visibility.Collapsed;

            TabProfiles.Visibility =
                Visibility.Collapsed;

            TabAccounts.Visibility =
                Visibility.Collapsed;

            Brush defaultColor =
                new SolidColorBrush(
                    Color.FromRgb(
                        136,
                        136,
                        136
                    )
                );

            Thickness noBorder =
                new Thickness(0);

            TabLaunchBtn.Foreground =
                defaultColor;

            TabLaunchBtn.BorderThickness =
                noBorder;

            TabProfilesBtn.Foreground =
                defaultColor;

            TabProfilesBtn.BorderThickness =
                noBorder;

            TabAccountsBtn.Foreground =
                defaultColor;

            TabAccountsBtn.BorderThickness =
                noBorder;

            btn.Foreground =
                new SolidColorBrush(
                    Color.FromRgb(
                        0,
                        255,
                        136
                    )
                );

            btn.BorderThickness =
                new Thickness(
                    0,
                    0,
                    0,
                    2
                );

            switch (targetTab)
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
            string selectedVer =
                (VersionBox.SelectedItem as ComboBoxItem)
                ?.Content
                ?.ToString()
                ?? "1.21.1";

            if (SelectedProfileLabel != null)
            {
                SelectedProfileLabel.Text =
                    $"Ready to launch Fabric {selectedVer}";
            }

            if (StatusText != null)
            {
                StatusText.Text =
                    $"Profile saved: Fabric {selectedVer} with {(int)RamSlider.Value}GB RAM";
            }

            MessageBox.Show(
                "Profile settings saved successfully!",
                "Topu Client",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
        }

        // =========================================================
        // AUTH MODE
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
                    "Auth Mode: Offline / Local";
            }
            else
            {
                StatusText.Text =
                    "Auth Mode: Microsoft Official";
            }
        }

        // =========================================================
        // MICROSOFT LOGIN
        // =========================================================

        private async void MsLoginBtn_Click(
            object sender,
            RoutedEventArgs e)
        {
            try
            {
                StatusText.Text =
                    "Microsoft Login requires interactive authentication.";

                MessageBox.Show(
                    "Microsoft authentication requires the CmlLib Microsoft authentication flow.",
                    "Microsoft Login",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
            }
            catch (Exception ex)
            {
                StatusText.Text =
                    "Microsoft Login Failed!";

                MessageBox.Show(
                    $"Microsoft Login Error:\n\n{ex.Message}",
                    "Authentication Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
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
            if (sender is Button btn &&
                btn.Tag is string serverIp)
            {
                StatusText.Text =
                    $"Target server queued: {serverIp}";
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
                ModSearchInput?.Text?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(query))
            {
                MessageBox.Show(
                    "Please enter a mod name.",
                    "Mod Search",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );

                return;
            }

            try
            {
                ModSearchStatus.Text =
                    $"Searching Modrinth for '{query}'...";

                string searchUrl =
                    "https://api.modrinth.com/v2/search" +
                    $"?query={Uri.EscapeDataString(query)}" +
                    "&facets=%5B%5B%22project_type%3Amod%22%5D%5D";

                string response =
                    await _httpClient.GetStringAsync(
                        searchUrl
                    );

                using JsonDocument doc =
                    JsonDocument.Parse(response);

                if (!doc.RootElement.TryGetProperty(
                        "hits",
                        out JsonElement hits) ||
                    hits.GetArrayLength() == 0)
                {
                    ModSearchStatus.Text =
                        "No compatible Fabric mod found.";

                    return;
                }

                JsonElement first =
                    hits[0];

                string title =
                    first.TryGetProperty(
                        "title",
                        out JsonElement titleProp)
                        ? titleProp.GetString() ?? query
                        : query;

                string projectId =
                    first.TryGetProperty(
                        "project_id",
                        out JsonElement idProp)
                        ? idProp.GetString() ?? ""
                        : "";

                if (string.IsNullOrWhiteSpace(projectId))
                    return;

                string mcVersion =
                    (VersionBox.SelectedItem as ComboBoxItem)
                    ?.Content
                    ?.ToString()
                    ?? "1.21.1";

                string versionsUrl =
                    "https://api.modrinth.com/v2/project/" +
                    $"{projectId}/version" +
                    "?loaders=%5B%22fabric%22%5D" +
                    $"&game_versions=%5B%22{Uri.EscapeDataString(mcVersion)}%22%5D";

                string versionsResponse =
                    await _httpClient.GetStringAsync(
                        versionsUrl
                    );

                using JsonDocument versionDoc =
                    JsonDocument.Parse(
                        versionsResponse
                    );

                JsonElement versions =
                    versionDoc.RootElement;

                if (versions.GetArrayLength() == 0)
                {
                    ModSearchStatus.Text =
                        "No compatible Fabric version found.";

                    return;
                }

                JsonElement version =
                    versions[0];

                if (!version.TryGetProperty(
                        "files",
                        out JsonElement files) ||
                    files.GetArrayLength() == 0)
                {
                    return;
                }

                string fileUrl = "";
                string fileName =
                    $"{title}.jar";

                foreach (JsonElement file in
                    files.EnumerateArray())
                {
                    if (file.TryGetProperty(
                            "primary",
                            out JsonElement primary) &&
                        primary.GetBoolean())
                    {
                        if (file.TryGetProperty(
                                "url",
                                out JsonElement url))
                        {
                            fileUrl =
                                url.GetString() ?? "";
                        }

                        if (file.TryGetProperty(
                                "filename",
                                out JsonElement filename))
                        {
                            fileName =
                                filename.GetString()
                                ?? fileName;
                        }

                        break;
                    }
                }

                if (string.IsNullOrWhiteSpace(fileUrl))
                {
                    if (files[0].TryGetProperty(
                            "url",
                            out JsonElement url))
                    {
                        fileUrl =
                            url.GetString() ?? "";
                    }
                }

                if (string.IsNullOrWhiteSpace(fileUrl))
                    return;

                string modsFolder =
                    Path.Combine(
                        GetGamePath(),
                        "mods"
                    );

                Directory.CreateDirectory(
                    modsFolder
                );

                string destination =
                    Path.Combine(
                        modsFolder,
                        fileName
                    );

                byte[] data =
                    await _httpClient.GetByteArrayAsync(
                        fileUrl
                    );

                await File.WriteAllBytesAsync(
                    destination,
                    data
                );

                ModSearchStatus.Text =
                    $"Successfully downloaded: {title}!";

                MessageBox.Show(
                    $"Mod '{title}' was added to your mods folder.",
                    "Modrinth",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
            }
            catch (Exception ex)
            {
                ModSearchStatus.Text =
                    "Mod search failed.";

                MessageBox.Show(
                    $"Modrinth Error:\n\n{ex.Message}",
                    "Modrinth Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        // =========================================================
        // LAUNCH
        // =========================================================

        private async void LaunchBtn_Click(
            object sender,
            RoutedEventArgs e)
        {
            LaunchBtn.IsEnabled = false;

            try
            {
                string gamePath =
                    GetGamePath();

                Directory.CreateDirectory(
                    gamePath
                );

                string mcVersion =
                    (VersionBox.SelectedItem as ComboBoxItem)
                    ?.Content
                    ?.ToString()
                    ?? "1.21.1";

                // -------------------------------------------------
                // SESSION
                // -------------------------------------------------

                StatusText.Text =
                    "Preparing player...";

                if (AuthTypeBox.SelectedIndex == 0 ||
                    _session == null ||
                    string.IsNullOrWhiteSpace(
                        _session.AccessToken) ||
                    _session.AccessToken == "0")
                {
                    string username =
                        string.IsNullOrWhiteSpace(
                            UsernameInput.Text)
                            ? "TopuPlayer"
                            : UsernameInput.Text.Trim();

                    _session =
                        MSession.CreateOfflineSession(
                            username
                        );

                    SaveUsername(username);
                }

                // -------------------------------------------------
                // JAVA
                // -------------------------------------------------

                StatusText.Text =
                    "Checking Java 21...";

                string javaPath =
                    await EnsureJava21Async(
                        gamePath
                    );

                if (!File.Exists(javaPath))
                {
                    throw new Exception(
                        "Java 21 was not found:\n\n" +
                        javaPath
                    );
                }

                StatusText.Text =
                    "Java 21 ready.";

                // -------------------------------------------------
                // MINECRAFT PATH
                // -------------------------------------------------

                var minecraftPath =
                    new MinecraftPath(
                        gamePath
                    );

                var launcher =
                    new MinecraftLauncher(
                        minecraftPath
                    );

                // -------------------------------------------------
                // PROGRESS
                // -------------------------------------------------

                launcher.FileProgressChanged +=
                    (s, args) =>
                    {
                        try
                        {
                            Dispatcher.Invoke(() =>
                            {
                                StatusText.Text =
                                    $"Checking: {args.Name}";
                            });
                        }
                        catch
                        {
                        }
                    };

                launcher.ByteProgressChanged +=
                    (s, args) =>
                    {
                        try
                        {
                            Dispatcher.Invoke(() =>
                            {
                                StatusText.Text =
                                    "Downloading Minecraft files...";
                            });
                        }
                        catch
                        {
                        }
                    };

                // -------------------------------------------------
                // INSTALL MINECRAFT
                // -------------------------------------------------

                StatusText.Text =
                    $"Installing Minecraft {mcVersion}...";

                await launcher.InstallAsync(
                    mcVersion
                );

                // -------------------------------------------------
                // VERIFY MINECRAFT
                // -------------------------------------------------

                string vanillaJar =
                    Path.Combine(
                        gamePath,
                        "versions",
                        mcVersion,
                        $"{mcVersion}.jar"
                    );

                if (!File.Exists(vanillaJar))
                {
                    throw new Exception(
                        "Minecraft JAR was not installed:\n\n" +
                        vanillaJar
                    );
                }

                // -------------------------------------------------
                // FABRIC
                // -------------------------------------------------

                StatusText.Text =
                    "Checking Fabric...";

                string fabricVersion =
                    await EnsureFabricProfileAsync(
                        gamePath,
                        mcVersion
                    );

                StatusText.Text =
                    $"Fabric ready: {fabricVersion}";

                // -------------------------------------------------
                // MODS
                // -------------------------------------------------

                string modsFolder =
                    Path.Combine(
                        gamePath,
                        "mods"
                    );

                await EnsureEssentialModsDownloaded(
                    modsFolder,
                    mcVersion
                );

                // -------------------------------------------------
                // RAM
                // -------------------------------------------------

                int ramMb =
                    Math.Max(
                        1024,
                        (int)RamSlider.Value * 1024
                    );

                // -------------------------------------------------
                // SESSION CHECK
                // -------------------------------------------------

                if (_session == null)
                {
                    throw new Exception(
                        "Minecraft session is null."
                    );
                }

                // -------------------------------------------------
                // LAUNCH OPTIONS
                // -------------------------------------------------

                var options =
                    new MLaunchOption
                    {
                        Session = _session,

                        MaximumRamMb =
                            ramMb,

                        JavaPath =
                            javaPath,

                        Path =
                            minecraftPath
                    };

                // -------------------------------------------------
                // BUILD
                // -------------------------------------------------

                StatusText.Text =
                    "Creating game process...";

                Process process =
                    await launcher.BuildProcessAsync(
                        fabricVersion,
                        options
                    );

                if (process == null)
                {
                    throw new Exception(
                        "CmlLib returned no process."
                    );
                }

                // -------------------------------------------------
                // SAVE GENERATED COMMAND
                // -------------------------------------------------

                string commandFile =
                    Path.Combine(
                        gamePath,
                        "launch-command.txt"
                    );

                string commandText =
                    "===== TOPU CLIENT DEBUG =====\r\n\r\n" +
                    "Executable:\r\n" +
                    process.StartInfo.FileName +
                    "\r\n\r\n" +
                    "Arguments:\r\n" +
                    process.StartInfo.Arguments +
                    "\r\n\r\n" +
                    "Working Directory:\r\n" +
                    process.StartInfo.WorkingDirectory +
                    "\r\n\r\n" +
                    "Java:\r\n" +
                    javaPath +
                    "\r\n\r\n" +
                    "Minecraft:\r\n" +
                    mcVersion +
                    "\r\n\r\n" +
                    "Fabric:\r\n" +
                    fabricVersion +
                    "\r\n\r\n" +
                    "RAM:\r\n" +
                    ramMb +
                    " MB\r\n";

                await File.WriteAllTextAsync(
                    commandFile,
                    commandText
                );

                Debug.WriteLine(
                    commandText
                );

                // -------------------------------------------------
                // NORMAL MINECRAFT LAUNCH
                // -------------------------------------------------

                StatusText.Text =
                    "Starting Minecraft...";

                bool started =
                    process.Start();

                if (!started)
                {
                    throw new Exception(
                        "Process.Start() returned false."
                    );
                }

                StatusText.Text =
                    $"Topu Client running as {_session.Username}";

                // -------------------------------------------------
                // WAIT FOR MINECRAFT
                // -------------------------------------------------

                _ = MonitorMinecraftAsync(
                    process,
                    gamePath,
                    commandFile
                );
            }
            catch (Exception ex)
            {
                StatusText.Text =
                    "Launch Failed!";

                string error =
                    "TOPU CLIENT LAUNCH ERROR\r\n\r\n" +
                    $"Message:\r\n{ex.Message}\r\n\r\n" +
                    $"Type:\r\n{ex.GetType().FullName}\r\n\r\n" +
                    $"Stack Trace:\r\n{ex.StackTrace}";

                try
                {
                    string errorFile =
                        Path.Combine(
                            GetGamePath(),
                            "launcher-error.txt"
                        );

                    await File.WriteAllTextAsync(
                        errorFile,
                        error
                    );
                }
                catch
                {
                }

                MessageBox.Show(
                    error,
                    "Topu Client - Launch Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
            finally
            {
                LaunchBtn.IsEnabled = true;
            }
        }

        // =========================================================
        // FABRIC
        // =========================================================

        private async Task<string> EnsureFabricProfileAsync(
            string gamePath,
            string mcVersion)
        {
            /*
             * The launcher expects a Fabric profile to already
             * exist in the Minecraft versions directory.
             *
             * We do not hard-code a random Fabric loader version.
             *
             * This method first searches existing Fabric profiles.
             */

            string versionsFolder =
                Path.Combine(
                    gamePath,
                    "versions"
                );

            if (!Directory.Exists(
                    versionsFolder))
            {
                throw new Exception(
                    "Minecraft versions directory does not exist."
                );
            }

            string? found =
                FindFabricVersion(
                    versionsFolder,
                    mcVersion
                );

            if (!string.IsNullOrWhiteSpace(
                found))
            {
                return found;
            }

            throw new Exception(
                $"No installed Fabric profile was found for Minecraft {mcVersion}.\n\n" +
                "The Minecraft installation exists, but Fabric does not.\n\n" +
                "Check the versions folder."
            );
        }

        private string? FindFabricVersion(
            string versionsFolder,
            string mcVersion)
        {
            try
            {
                foreach (string directory in
                    Directory.GetDirectories(
                        versionsFolder))
                {
                    string name =
                        Path.GetFileName(
                            directory
                        );

                    if (!name.StartsWith(
                            "fabric-loader-",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (!name.EndsWith(
                            "-" + mcVersion,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string json =
                        Path.Combine(
                            directory,
                            name + ".json"
                        );

                    if (File.Exists(json))
                    {
                        return name;
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        // =========================================================
        // JAVA 21
        // =========================================================

        private async Task<string> EnsureJava21Async(
            string gamePath)
        {
            string runtimeRoot =
                Path.Combine(
                    gamePath,
                    "runtime",
                    "java21"
                );

            string javaExe =
                Path.Combine(
                    runtimeRoot,
                    "bin",
                    "java.exe"
                );

            if (File.Exists(javaExe))
            {
                return javaExe;
            }

            StatusText.Text =
                "Java 21 not found. Downloading...";

            string runtimeFolder =
                Path.Combine(
                    gamePath,
                    "runtime"
                );

            Directory.CreateDirectory(
                runtimeFolder
            );

            string zipPath =
                Path.Combine(
                    runtimeFolder,
                    "java21.zip"
                );

            string tempFolder =
                Path.Combine(
                    runtimeFolder,
                    "java21-temp"
                );

            string downloadUrl =
                "https://api.adoptium.net/v3/binary/latest/" +
                "21/ga/windows/x64/jre/hotspot/normal/eclipse";

            try
            {
                using HttpResponseMessage response =
                    await _httpClient.GetAsync(
                        downloadUrl,
                        HttpCompletionOption.ResponseHeadersRead
                    );

                response.EnsureSuccessStatusCode();

                using Stream input =
                    await response.Content.ReadAsStreamAsync();

                using FileStream output =
                    new FileStream(
                        zipPath,
                        FileMode.Create,
                        FileAccess.Write,
                        FileShare.None
                    );

                await input.CopyToAsync(
                    output
                );
            }
            catch (Exception ex)
            {
                throw new Exception(
                    "Java 21 download failed.\n\n" +
                    ex.Message,
                    ex
                );
            }

            StatusText.Text =
                "Extracting Java 21...";

            if (Directory.Exists(
                    tempFolder))
            {
                Directory.Delete(
                    tempFolder,
                    true
                );
            }

            Directory.CreateDirectory(
                tempFolder
            );

            try
            {
                System.IO.Compression.ZipFile
                    .ExtractToDirectory(
                        zipPath,
                        tempFolder
                    );
            }
            catch (Exception ex)
            {
                throw new Exception(
                    "Java 21 extraction failed.\n\n" +
                    ex.Message,
                    ex
                );
            }

            string? extractedJava =
                FindFile(
                    tempFolder,
                    "java.exe"
                );

            if (string.IsNullOrWhiteSpace(
                extractedJava))
            {
                throw new Exception(
                    "java.exe was not found in the Java archive."
                );
            }

            string? bin =
                Path.GetDirectoryName(
                    extractedJava
                );

            if (bin == null)
            {
                throw new Exception(
                    "Could not locate Java bin folder."
                );
            }

            DirectoryInfo? javaRoot =
                Directory.GetParent(
                    bin
                );

            if (javaRoot == null)
            {
                throw new Exception(
                    "Could not locate Java root."
                );
            }

            if (Directory.Exists(
                    runtimeRoot))
            {
                Directory.Delete(
                    runtimeRoot,
                    true
                );
            }

            CopyDirectory(
                javaRoot.FullName,
                runtimeRoot
            );

            string finalJava =
                Path.Combine(
                    runtimeRoot,
                    "bin",
                    "java.exe"
                );

            if (!File.Exists(finalJava))
            {
                throw new Exception(
                    "Java installation finished but java.exe is missing."
                );
            }

            try
            {
                File.Delete(zipPath);
            }
            catch
            {
            }

            try
            {
                Directory.Delete(
                    tempFolder,
                    true
                );
            }
            catch
            {
            }

            return finalJava;
        }

        private string? FindFile(
            string root,
            string fileName)
        {
            try
            {
                string direct =
                    Path.Combine(
                        root,
                        fileName
                    );

                if (File.Exists(direct))
                    return direct;

                foreach (string directory in
                    Directory.GetDirectories(root))
                {
                    string? result =
                        FindFile(
                            directory,
                            fileName
                        );

                    if (result != null)
                        return result;
                }
            }
            catch
            {
            }

            return null;
        }

        private void CopyDirectory(
            string source,
            string destination)
        {
            DirectoryInfo sourceInfo =
                new DirectoryInfo(
                    source
                );

            if (!sourceInfo.Exists)
            {
                throw new DirectoryNotFoundException(
                    source
                );
            }

            Directory.CreateDirectory(
                destination
            );

            foreach (FileInfo file in
                sourceInfo.GetFiles())
            {
                file.CopyTo(
                    Path.Combine(
                        destination,
                        file.Name
                    ),
                    true
                );
            }

            foreach (DirectoryInfo directory in
                sourceInfo.GetDirectories())
            {
                CopyDirectory(
                    directory.FullName,
                    Path.Combine(
                        destination,
                        directory.Name
                    )
                );
            }
        }

        // =========================================================
        // PROCESS MONITOR
        // =========================================================

        private async Task MonitorMinecraftAsync(
            Process process,
            string gamePath,
            string commandFile)
        {
            try
            {
                await process.WaitForExitAsync();

                int exitCode =
                    process.ExitCode;

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
                            exitCode +
                            ".\n\n" +
                            "The generated launch command was saved to:\n\n" +
                            commandFile +
                            "\n\n" +
                            "Send me that file so we can diagnose the exact launch command.",
                            "Minecraft Exit",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning
                        );
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine(
                    "Minecraft monitor error: " +
                    ex.Message
                );
            }
        }

        // =========================================================
        // ESSENTIAL MODS
        // =========================================================

        private async Task EnsureEssentialModsDownloaded(
            string modsFolder,
            string mcVersion)
        {
            Directory.CreateDirectory(
                modsFolder
            );

            var coreMods =
                new (string Name, string Query)[]
                {
                    ("fabric-api.jar", "fabric-api"),
                    ("sodium.jar", "sodium"),
                    ("lithium.jar", "lithium"),
                    ("ferritecore.jar", "ferritecore"),
                    ("sodium-extra.jar", "sodium-extra"),
                    ("dynamic-fps.jar", "dynamic-fps")
                };

            foreach (var mod in coreMods)
            {
                string destination =
                    Path.Combine(
                        modsFolder,
                        mod.Name
                    );

                if (File.Exists(destination) &&
                    new FileInfo(destination).Length > 5000)
                {
                    continue;
                }

                try
                {
                    Dispatcher.Invoke(() =>
                    {
                        StatusText.Text =
                            $"Downloading {mod.Name}...";
                    });

                    string searchUrl =
                        "https://api.modrinth.com/v2/search" +
                        $"?query={Uri.EscapeDataString(mod.Query)}" +
                        "&facets=%5B%5B%22project_type%3Amod%22%5D%5D";

                    string response =
                        await _httpClient.GetStringAsync(
                            searchUrl
                        );

                    using JsonDocument doc =
                        JsonDocument.Parse(response);

                    if (!doc.RootElement.TryGetProperty(
                            "hits",
                            out JsonElement hits) ||
                        hits.GetArrayLength() == 0)
                    {
                        continue;
                    }

                    string projectId =
                        hits[0].TryGetProperty(
                            "project_id",
                            out JsonElement idProp)
                            ? idProp.GetString() ?? ""
                            : "";

                    if (string.IsNullOrWhiteSpace(
                        projectId))
                    {
                        continue;
                    }

                    string versionsUrl =
                        "https://api.modrinth.com/v2/project/" +
                        $"{projectId}/version" +
                        "?loaders=%5B%22fabric%22%5D" +
                        $"&game_versions=%5B%22{Uri.EscapeDataString(mcVersion)}%22%5D";

                    string versionsResponse =
                        await _httpClient.GetStringAsync(
                            versionsUrl
                        );

                    using JsonDocument versionDoc =
                        JsonDocument.Parse(
                            versionsResponse
                        );

                    JsonElement versions =
                        versionDoc.RootElement;

                    if (versions.GetArrayLength() == 0)
                        continue;

                    JsonElement version =
                        versions[0];

                    if (!version.TryGetProperty(
                            "files",
                            out JsonElement files) ||
                        files.GetArrayLength() == 0)
                    {
                        continue;
                    }

                    string fileUrl = "";

                    foreach (JsonElement file in
                        files.EnumerateArray())
                    {
                        if (file.TryGetProperty(
                                "primary",
                                out JsonElement primary) &&
                            primary.GetBoolean())
                        {
                            if (file.TryGetProperty(
                                    "url",
                                    out JsonElement url))
                            {
                                fileUrl =
                                    url.GetString() ?? "";
                            }

                            break;
                        }
                    }

                    if (string.IsNullOrWhiteSpace(fileUrl))
                    {
                        if (files[0].TryGetProperty(
                                "url",
                                out JsonElement url))
                        {
                            fileUrl =
                                url.GetString() ?? "";
                        }
                    }

                    if (string.IsNullOrWhiteSpace(fileUrl))
                        continue;

                    byte[] data =
                        await _httpClient.GetByteArrayAsync(
                            fileUrl
                        );

                    await File.WriteAllBytesAsync(
                        destination,
                        data
                    );
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(
                        $"Mod skipped: {mod.Name}: {ex.Message}"
                    );
                }
            }
        }
    }
}
