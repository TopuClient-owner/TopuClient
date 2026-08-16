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
using CmlLib.Core.ModLoaders.FabricMC;
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
        // PATH
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
        // AUTH TYPE
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
                    "Microsoft Login requires interactive browser flow.";

                MessageBox.Show(
                    "Microsoft login requires the CmlLib.Core.Auth.Microsoft package and interactive authentication.",
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
                if (ModSearchStatus != null)
                {
                    ModSearchStatus.Text =
                        $"Searching Modrinth for '{query}'...";
                }

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
                    if (ModSearchStatus != null)
                    {
                        ModSearchStatus.Text =
                            "No mod found.";
                    }

                    MessageBox.Show(
                        "No compatible Fabric mod was found.",
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
                        $"No Fabric version for Minecraft {targetVer} was found.",
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

                if (ModSearchStatus != null)
                {
                    ModSearchStatus.Text =
                        $"Successfully downloaded: {modTitle}!";
                }

                MessageBox.Show(
                    $"Mod '{modTitle}' successfully added.",
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
                    $"Modrinth error:\n\n{ex.Message}",
                    "Modrinth Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        // =========================================================
        // MAIN LAUNCH
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
                        "Java 21 executable was not found:\n\n" +
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
                // DOWNLOAD PROGRESS
                // -------------------------------------------------

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
                // VANILLA INSTALL
                // -------------------------------------------------

                StatusText.Text =
                    $"Installing Minecraft {targetVer}...";

                await launcher.InstallAsync(
                    targetVer
                );

                // -------------------------------------------------
                // FABRIC INSTALL
                // -------------------------------------------------

                StatusText.Text =
                    $"Installing Fabric for {targetVer}...";

                string fabricVersion =
                    await InstallFabricAsync(
                        targetVer,
                        minecraftPath
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
                    targetVer
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
                // OPTIONS
                // -------------------------------------------------

                if (_session == null)
                {
                    throw new Exception(
                        "Minecraft session is null."
                    );
                }

                var launchOptions =
                    new MLaunchOption
                    {
                        Session = _session,

                        MaximumRamMb =
                            ramMb,

                        MinimumRamMb =
                            Math.Min(
                                1024,
                                ramMb
                            ),

                        JavaPath =
                            javaPath,

                        Path =
                            minecraftPath
                    };

                // -------------------------------------------------
                // BUILD PROCESS
                // -------------------------------------------------

                StatusText.Text =
                    "Creating game process...";

                Process process =
                    await launcher.BuildProcessAsync(
                        fabricVersion,
                        launchOptions
                    );

                if (process == null)
                {
                    throw new Exception(
                        "CmlLib returned a null process."
                    );
                }

                // -------------------------------------------------
                // CAPTURE JAVA OUTPUT
                // -------------------------------------------------

                process.StartInfo.UseShellExecute =
                    false;

                process.StartInfo.RedirectStandardOutput =
                    true;

                process.StartInfo.RedirectStandardError =
                    true;

                process.StartInfo.CreateNoWindow =
                    true;

                string outputLog =
                    Path.Combine(
                        gamePath,
                        "topu-launch-output.log"
                    );

                using StreamWriter outputWriter =
                    new StreamWriter(
                        outputLog,
                        false
                    );

                process.OutputDataReceived +=
                    (s, args) =>
                    {
                        if (!string.IsNullOrEmpty(
                            args.Data))
                        {
                            try
                            {
                                lock (outputWriter)
                                {
                                    outputWriter.WriteLine(
                                        args.Data
                                    );

                                    outputWriter.Flush();
                                }

                                Debug.WriteLine(
                                    "[MC] " +
                                    args.Data
                                );
                            }
                            catch
                            {
                            }
                        }
                    };

                process.ErrorDataReceived +=
                    (s, args) =>
                    {
                        if (!string.IsNullOrEmpty(
                            args.Data))
                        {
                            try
                            {
                                lock (outputWriter)
                                {
                                    outputWriter.WriteLine(
                                        "[ERROR] " +
                                        args.Data
                                    );

                                    outputWriter.Flush();
                                }

                                Debug.WriteLine(
                                    "[MC ERROR] " +
                                    args.Data
                                );
                            }
                            catch
                            {
                            }
                        }
                    };

                // -------------------------------------------------
                // DEBUG
                // -------------------------------------------------

                Debug.WriteLine(
                    "========== TOPU LAUNCH =========="
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
                    "Working directory: " +
                    process.StartInfo.WorkingDirectory
                );

                Debug.WriteLine(
                    "Java: " +
                    javaPath
                );

                Debug.WriteLine(
                    "Fabric: " +
                    fabricVersion
                );

                Debug.WriteLine(
                    "Minecraft: " +
                    targetVer
                );

                Debug.WriteLine(
                    "================================="
                );

                // -------------------------------------------------
                // START
                // -------------------------------------------------

                StatusText.Text =
                    "Starting Minecraft...";

                bool started =
                    process.Start();

                if (!started)
                {
                    throw new Exception(
                        "Process.Start() returned FALSE."
                    );
                }

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                StatusText.Text =
                    $"Topu Client running as {_session.Username}";

                // -------------------------------------------------
                // PROCESS MONITOR
                // -------------------------------------------------

                _ = MonitorMinecraftProcessAsync(
                    process,
                    gamePath,
                    outputLog
                );
            }
            catch (Exception ex)
            {
                StatusText.Text =
                    "Launch Failed!";

                string details =
                    "TOPU CLIENT LAUNCH ERROR\n\n" +
                    $"Message:\n{ex.Message}\n\n" +
                    $"Type:\n{ex.GetType().FullName}\n\n" +
                    $"Stack:\n{ex.StackTrace}";

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
        // FABRIC INSTALLER
        // =========================================================

        private async Task<string> InstallFabricAsync(
            string minecraftVersion,
            MinecraftPath minecraftPath)
        {
            try
            {
                var fabricInstaller =
                    new FabricInstaller(
                        _httpClient
                    );

                // Let CmlLib select a compatible Fabric
                // loader instead of hard-coding 0.19.3.
                FabricLoader? loader =
                    await fabricInstaller.GetFirstLoader(
                        minecraftVersion
                    );

                if (loader == null ||
                    string.IsNullOrWhiteSpace(
                        loader.Version))
                {
                    throw new Exception(
                        $"No Fabric loader was found for Minecraft {minecraftVersion}."
                    );
                }

                StatusText.Text =
                    $"Installing Fabric Loader {loader.Version}...";

                string versionName =
                    await fabricInstaller.Install(
                        minecraftVersion,
                        loader.Version,
                        minecraftPath
                    );

                if (string.IsNullOrWhiteSpace(
                    versionName))
                {
                    throw new Exception(
                        "FabricInstaller returned an empty version name."
                    );
                }

                return versionName;
            }
            catch (Exception ex)
            {
                throw new Exception(
                    $"Fabric installation failed for Minecraft {minecraftVersion}.\n\n" +
                    ex.Message,
                    ex
                );
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

            if (File.Exists(javaPath))
            {
                return javaPath;
            }

            StatusText.Text =
                "Java 21 not installed. Downloading...";

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

            if (Directory.Exists(tempFolder))
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
                ZipFile.ExtractToDirectory(
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
                FindJavaExecutable(
                    tempFolder
                );

            if (string.IsNullOrWhiteSpace(
                extractedJava))
            {
                throw new Exception(
                    "javaw.exe was not found inside the Java 21 archive."
                );
            }

            string? binFolder =
                Path.GetDirectoryName(
                    extractedJava
                );

            if (string.IsNullOrWhiteSpace(
                binFolder))
            {
                throw new Exception(
                    "Could not determine Java bin directory."
                );
            }

            DirectoryInfo? javaRoot =
                Directory.GetParent(
                    binFolder
                );

            if (javaRoot == null)
            {
                throw new Exception(
                    "Could not determine Java installation directory."
                );
            }

            if (Directory.Exists(runtimeRoot))
            {
                Directory.Delete(
                    runtimeRoot,
                    true
                );
            }

            StatusText.Text =
                "Installing Java 21...";

            CopyDirectory(
                javaRoot.FullName,
                runtimeRoot
            );

            string finalJava =
                Path.Combine(
                    runtimeRoot,
                    "bin",
                    "javaw.exe"
                );

            if (!File.Exists(finalJava))
            {
                throw new Exception(
                    "Java installation completed, but javaw.exe is missing."
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
                    return direct;

                foreach (string directory in
                    Directory.GetDirectories(root))
                {
                    string? result =
                        FindJavaExecutable(
                            directory
                        );

                    if (!string.IsNullOrEmpty(result))
                        return result;
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
        // MONITOR MINECRAFT
        // =========================================================

        private async Task MonitorMinecraftProcessAsync(
            Process process,
            string gamePath,
            string outputLog)
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
                            $"{exitCode}.\n\n" +
                            "The launcher saved the Java/Minecraft output here:\n\n" +
                            outputLog +
                            "\n\n" +
                            "Send me that file's last lines if it still crashes.",
                            "Minecraft Closed",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning
                        );
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine(
                    "Process monitor error: " +
                    ex
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
                        $"Skipped {mod.Name}: {ex.Message}"
                    );
                }
            }
        }
    }
}
