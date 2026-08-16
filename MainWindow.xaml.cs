using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
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

            string appFolder = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.ApplicationData),
                ".topuclient"
            );

            Directory.CreateDirectory(appFolder);

            _configFilePath = Path.Combine(
                appFolder,
                "username.txt"
            );

            LoadSavedUsername();
        }

        // =========================================================
        // BASIC PATHS
        // =========================================================

        private string GetGamePath()
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
                    File.ReadAllText(_configFilePath).Trim();

                if (string.IsNullOrWhiteSpace(savedUser))
                    return;

                if (UsernameInput != null)
                    UsernameInput.Text = savedUser;

                _session =
                    MSession.CreateOfflineSession(savedUser);
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
        // WINDOW CONTROLS
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
                    "Microsoft Login requires interactive browser flow implementation.";

                MessageBox.Show(
                    "Microsoft login integration requires the CmlLib.Core.Auth.Microsoft package.",
                    "MS Login",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
            }
            catch (Exception ex)
            {
                StatusText.Text =
                    "Microsoft Login Failed!";

                MessageBox.Show(
                    $"MS Login Error:\n{ex.Message}",
                    "Authentication Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
            finally
            {
                if (MsLoginBtn != null)
                    MsLoginBtn.IsEnabled = true;
            }

            await Task.CompletedTask;
        }

        // =========================================================
        // SERVER BUTTON
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
                    "Please enter a mod name to search on Modrinth.",
                    "Mod Search",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );

                return;
            }

            try
            {
                if (ModSearchStatus != null)
                {
                    ModSearchStatus.Text =
                        $"Searching Modrinth for '{query}'...";
                }

                string searchUrl =
                    "https://api.modrinth.com/v2/search" +
                    $"?query={Uri.EscapeDataString(query)}";

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
                    if (ModSearchStatus != null)
                    {
                        ModSearchStatus.Text =
                            "No compatible Fabric mod found.";
                    }

                    MessageBox.Show(
                        "No compatible Fabric mod was found on Modrinth.",
                        "Mod Not Found",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning
                    );

                    return;
                }

                JsonElement firstHit =
                    hits[0];

                string modTitle =
                    firstHit.TryGetProperty(
                        "title",
                        out JsonElement titleProp)
                        ? titleProp.GetString() ?? query
                        : query;

                string projectId =
                    firstHit.TryGetProperty(
                        "project_id",
                        out JsonElement idProp)
                        ? idProp.GetString() ?? ""
                        : "";

                if (string.IsNullOrWhiteSpace(projectId))
                    return;

                string targetVer =
                    (VersionBox.SelectedItem as ComboBoxItem)
                    ?.Content
                    ?.ToString()
                    ?? "1.21.1";

                string versionsUrl =
                    "https://api.modrinth.com/v2/project/" +
                    $"{projectId}/version" +
                    "?loaders=%5B%22fabric%22%5D" +
                    $"&game_versions=%5B%22{Uri.EscapeDataString(targetVer)}%22%5D";

                string versionsResponse =
                    await _httpClient.GetStringAsync(
                        versionsUrl
                    );

                using JsonDocument versionsDoc =
                    JsonDocument.Parse(
                        versionsResponse
                    );

                JsonElement versions =
                    versionsDoc.RootElement;

                if (versions.GetArrayLength() == 0)
                {
                    MessageBox.Show(
                        $"No Fabric build for Minecraft {targetVer} was found.",
                        "Mod Not Found",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning
                    );

                    return;
                }

                JsonElement latest =
                    versions[0];

                if (!latest.TryGetProperty(
                        "files",
                        out JsonElement files) ||
                    files.GetArrayLength() == 0)
                {
                    return;
                }

                string fileUrl = "";

                string fileName =
                    $"{modTitle}.jar";

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

                    if (files[0].TryGetProperty(
                            "filename",
                            out JsonElement filename))
                    {
                        fileName =
                            filename.GetString()
                            ?? fileName;
                    }
                }

                if (string.IsNullOrWhiteSpace(fileUrl))
                    return;

                string gamePath =
                    GetGamePath();

                string modsFolder =
                    Path.Combine(
                        gamePath,
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

                byte[] bytes =
                    await _httpClient.GetByteArrayAsync(
                        fileUrl
                    );

                await File.WriteAllBytesAsync(
                    destination,
                    bytes
                );

                if (ModSearchStatus != null)
                {
                    ModSearchStatus.Text =
                        $"Successfully downloaded: {modTitle}!";
                }

                MessageBox.Show(
                    $"Mod '{modTitle}' was added to your mods folder.",
                    "Modrinth",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
            }
            catch (Exception ex)
            {
                if (ModSearchStatus != null)
                {
                    ModSearchStatus.Text =
                        "Mod search failed.";
                }

                MessageBox.Show(
                    $"Mod search/download error:\n{ex.Message}",
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

                string targetVer =
                    (VersionBox.SelectedItem as ComboBoxItem)
                    ?.Content
                    ?.ToString()
                    ?? "1.21.1";

                // -------------------------------------------------
                // SESSION
                // -------------------------------------------------

                StatusText.Text =
                    "Preparing player session...";

                if (AuthTypeBox.SelectedIndex == 0 ||
                    _session == null ||
                    string.IsNullOrEmpty(
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

                    SaveUsername(
                        username
                    );
                }

                // -------------------------------------------------
                // JAVA 21
                // -------------------------------------------------

                StatusText.Text =
                    "Checking Java 21 runtime...";

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
                // MODS
                // -------------------------------------------------

                string modsFolder =
                    Path.Combine(
                        gamePath,
                        "mods"
                    );

                await EnsureEssentialModsDownloaded(
                    modsFolder,
                    targetVer
                );

                // -------------------------------------------------
                // FABRIC
                // -------------------------------------------------

                StatusText.Text =
                    $"Setting up Fabric {targetVer}...";

                string fabricVersion =
                    await InstallFabricProfileAsync(
                        gamePath,
                        targetVer
                    );

                // -------------------------------------------------
                // CMLLIB
                // -------------------------------------------------

                StatusText.Text =
                    "Preparing Minecraft launcher...";

                var minecraftPath =
                    new MinecraftPath(
                        gamePath
                    );

                var launcher =
                    new MinecraftLauncher(
                        minecraftPath
                    );

                launcher.FileProgressChanged +=
                    (senderObj, args) =>
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

                // Your CmlLib ByteProgress does not have
                // ProgressedPercentage.
                launcher.ByteProgressChanged +=
                    (senderObj, args) =>
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
                // MINECRAFT INSTALL
                // -------------------------------------------------

                StatusText.Text =
                    $"Checking Minecraft {targetVer}...";

                try
                {
                    await launcher.InstallAsync(
                        targetVer
                    );
                }
                catch (Exception installEx)
                {
                    throw new Exception(
                        "Minecraft installation failed.\n\n" +
                        $"Version: {targetVer}\n\n" +
                        installEx.Message,
                        installEx
                    );
                }

                // -------------------------------------------------
                // LAUNCH OPTIONS
                // -------------------------------------------------

                if (_session == null)
                {
                    throw new Exception(
                        "Minecraft session is null."
                    );
                }

                int ramMb =
                    Math.Max(
                        1024,
                        (int)RamSlider.Value * 1024
                    );

                StatusText.Text =
                    "Creating game process...";

                var launchOptions =
                    new MLaunchOption
                    {
                        Session = _session,
                        MaximumRamMb = ramMb,
                        JavaPath = javaPath
                    };

                // -------------------------------------------------
                // BUILD PROCESS
                // -------------------------------------------------

                Process process;

                try
                {
                    process =
                        await launcher.BuildProcessAsync(
                            fabricVersion,
                            launchOptions
                        );
                }
                catch (Exception buildEx)
                {
                    throw new Exception(
                        "CmlLib could not create the Minecraft process.\n\n" +
                        $"Minecraft: {targetVer}\n" +
                        $"Fabric: {fabricVersion}\n" +
                        $"Java: {javaPath}\n" +
                        $"Java exists: {File.Exists(javaPath)}\n" +
                        $"RAM: {ramMb} MB\n\n" +
                        buildEx.Message,
                        buildEx
                    );
                }

                if (process == null)
                {
                    throw new Exception(
                        "CmlLib returned a null Process object."
                    );
                }

                // -------------------------------------------------
                // DEBUG INFORMATION
                // -------------------------------------------------

                Debug.WriteLine(
                    "=============================="
                );

                Debug.WriteLine(
                    "TOPU CLIENT MINECRAFT PROCESS"
                );

                Debug.WriteLine(
                    "Executable: " +
                    process.StartInfo.FileName
                );

                Debug.WriteLine(
                    "Arguments: " +
                    process.StartInfo.Arguments
                );

                Debug.WriteLine(
                    "Working Directory: " +
                    process.StartInfo.WorkingDirectory
                );

                Debug.WriteLine(
                    "=============================="
                );

                // -------------------------------------------------
                // START
                // -------------------------------------------------

                StatusText.Text =
                    "Starting Minecraft...";

                bool started;

                try
                {
                    started =
                        process.Start();
                }
                catch (Exception startEx)
                {
                    throw new Exception(
                        "Windows could not start Minecraft.\n\n" +
                        $"Java:\n{javaPath}\n\n" +
                        $"Java exists: {File.Exists(javaPath)}\n\n" +
                        startEx.Message,
                        startEx
                    );
                }

                if (!started)
                {
                    throw new Exception(
                        "Process.Start() returned FALSE."
                    );
                }

                StatusText.Text =
                    $"Topu Client running as {_session.Username}";

                // -------------------------------------------------
                // MONITOR PROCESS
                // -------------------------------------------------

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await process.WaitForExitAsync();

                        int exitCode =
                            process.ExitCode;

                        Dispatcher.Invoke(() =>
                        {
                            StatusText.Text =
                                $"Minecraft exited: {exitCode}";

                            if (exitCode != 0)
                            {
                                string logsPath =
                                    Path.Combine(
                                        gamePath,
                                        "logs"
                                    );

                                MessageBox.Show(
                                    "Minecraft closed unexpectedly.\n\n" +
                                    $"Exit code: {exitCode}\n\n" +
                                    $"Minecraft logs:\n{logsPath}",
                                    "Minecraft Closed",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Warning
                                );
                            }
                        });
                    }
                    catch (Exception monitorEx)
                    {
                        Debug.WriteLine(
                            "Minecraft monitor error:"
                        );

                        Debug.WriteLine(
                            monitorEx.ToString()
                        );
                    }
                });
            }
            catch (Exception ex)
            {
                StatusText.Text =
                    "Launch Failed!";

                string details =
                    "TOPU CLIENT LAUNCH ERROR\n\n" +
                    $"Message:\n{ex.Message}\n\n" +
                    $"Exception:\n{ex.GetType().FullName}\n\n" +
                    $"Stack Trace:\n{ex.StackTrace}";

                Debug.WriteLine(details);

                MessageBox.Show(
                    details,
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

            string javaPath =
                Path.Combine(
                    runtimeRoot,
                    "bin",
                    "javaw.exe"
                );

            // Already installed.
            if (File.Exists(javaPath))
            {
                return javaPath;
            }

            StatusText.Text =
                "Java 21 not installed. Downloading...";

            Directory.CreateDirectory(
                Path.Combine(
                    gamePath,
                    "runtime"
                )
            );

            string zipPath =
                Path.Combine(
                    gamePath,
                    "runtime",
                    "java21.zip"
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
                    "Could not download Java 21.\n\n" +
                    "Check your internet connection.\n\n" +
                    ex.Message,
                    ex
                );
            }

            StatusText.Text =
                "Extracting Java 21...";

            string extractionRoot =
                Path.Combine(
                    gamePath,
                    "runtime",
                    "java21-temp"
                );

            if (Directory.Exists(
                extractionRoot))
            {
                Directory.Delete(
                    extractionRoot,
                    true
                );
            }

            Directory.CreateDirectory(
                extractionRoot
            );

            try
            {
                ZipFile.ExtractToDirectory(
                    zipPath,
                    extractionRoot
                );
            }
            catch (Exception ex)
            {
                throw new Exception(
                    "Java archive extraction failed.\n\n" +
                    ex.Message,
                    ex
                );
            }

            StatusText.Text =
                "Installing Java 21...";

            string? extractedJava =
                FindJavaExecutable(
                    extractionRoot
                );

            if (string.IsNullOrWhiteSpace(
                extractedJava))
            {
                throw new Exception(
                    "Java was downloaded, but javaw.exe could not be found."
                );
            }

            string? extractedBin =
                Path.GetDirectoryName(
                    extractedJava
                );

            if (string.IsNullOrWhiteSpace(
                extractedBin))
            {
                throw new Exception(
                    "Could not determine Java bin directory."
                );
            }

            DirectoryInfo? extractedJavaRoot =
                Directory.GetParent(
                    extractedBin
                );

            if (extractedJavaRoot == null)
            {
                throw new Exception(
                    "Could not determine Java installation root."
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
                extractedJavaRoot.FullName,
                runtimeRoot
            );

            string finalJavaPath =
                Path.Combine(
                    runtimeRoot,
                    "bin",
                    "javaw.exe"
                );

            if (!File.Exists(
                finalJavaPath))
            {
                throw new Exception(
                    "Java installation finished, but javaw.exe is missing."
                );
            }

            try
            {
                File.Delete(
                    zipPath
                );
            }
            catch
            {
            }

            try
            {
                Directory.Delete(
                    extractionRoot,
                    true
                );
            }
            catch
            {
            }

            StatusText.Text =
                "Java 21 installed successfully.";

            return finalJavaPath;
        }

        private string? FindJavaExecutable(
            string root)
        {
            try
            {
                string direct =
                    Path.Combine(
                        root,
                        "bin",
                        "javaw.exe"
                    );

                if (File.Exists(direct))
                {
                    return direct;
                }

                foreach (string directory in
                    Directory.GetDirectories(root))
                {
                    string? result =
                        FindJavaExecutable(
                            directory
                        );

                    if (!string.IsNullOrEmpty(
                        result))
                    {
                        return result;
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        private void CopyDirectory(
            string sourceDirectory,
            string destinationDirectory)
        {
            DirectoryInfo source =
                new DirectoryInfo(
                    sourceDirectory
                );

            if (!source.Exists)
            {
                throw new DirectoryNotFoundException(
                    sourceDirectory
                );
            }

            Directory.CreateDirectory(
                destinationDirectory
            );

            foreach (FileInfo file in
                source.GetFiles())
            {
                string destination =
                    Path.Combine(
                        destinationDirectory,
                        file.Name
                    );

                file.CopyTo(
                    destination,
                    true
                );
            }

            foreach (DirectoryInfo directory in
                source.GetDirectories())
            {
                string destination =
                    Path.Combine(
                        destinationDirectory,
                        directory.Name
                    );

                CopyDirectory(
                    directory.FullName,
                    destination
                );
            }
        }

        // =========================================================
        // FABRIC
        // =========================================================

        private async Task<string> InstallFabricProfileAsync(
            string gamePath,
            string mcVersion)
        {
            string loaderVersion =
                "0.19.3";

            string fabricVersionId =
                $"fabric-loader-{loaderVersion}-{mcVersion}";

            string versionFolder =
                Path.Combine(
                    gamePath,
                    "versions",
                    fabricVersionId
                );

            string jsonFile =
                Path.Combine(
                    versionFolder,
                    $"{fabricVersionId}.json"
                );

            if (!File.Exists(jsonFile))
            {
                Directory.CreateDirectory(
                    versionFolder
                );

                string apiUrl =
                    "https://meta.fabricmc.net/v2/versions/loader/" +
                    $"{mcVersion}/{loaderVersion}/profile/json";

                string jsonContent =
                    await _httpClient.GetStringAsync(
                        apiUrl
                    );

                await File.WriteAllTextAsync(
                    jsonFile,
                    jsonContent
                );
            }

            return fabricVersionId;
        }

        // =========================================================
        // MOD DOWNLOADS
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
                    new FileInfo(destination).Length >= 5000)
                {
                    continue;
                }

                try
                {
                    Dispatcher.Invoke(() =>
                    {
                        StatusText.Text =
                            $"Checking/Downloading {mod.Name}...";
                    });

                    string searchUrl =
                        "https://api.modrinth.com/v2/search" +
                        $"?query={Uri.EscapeDataString(mod.Query)}" +
                        "&facets=%5B%5B%22project_type%3Amod%22%5D%5D";

                    string searchResponse =
                        await _httpClient.GetStringAsync(
                            searchUrl
                        );

                    using JsonDocument searchDoc =
                        JsonDocument.Parse(
                            searchResponse
                        );

                    if (!searchDoc.RootElement.TryGetProperty(
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

                    using JsonDocument versionsDoc =
                        JsonDocument.Parse(
                            versionsResponse
                        );

                    JsonElement versions =
                        versionsDoc.RootElement;

                    if (versions.GetArrayLength() == 0)
                        continue;

                    JsonElement latest =
                        versions[0];

                    if (!latest.TryGetProperty(
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

                    if (string.IsNullOrWhiteSpace(
                        fileUrl))
                    {
                        if (files[0].TryGetProperty(
                                "url",
                                out JsonElement url))
                        {
                            fileUrl =
                                url.GetString() ?? "";
                        }
                    }

                    if (string.IsNullOrWhiteSpace(
                        fileUrl))
                    {
                        continue;
                    }

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
                        $"Mod download skipped for {mod.Name}: {ex.Message}"
                    );
                }
            }
        }
    }
}
