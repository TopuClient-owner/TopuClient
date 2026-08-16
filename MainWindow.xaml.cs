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

namespace TopuLauncher
{
    public partial class MainWindow : Window
    {
        // IMPORTANT:
        // CmlLib.Core 4.0.6 BuildProcessAsync returns System.Diagnostics.Process.
        // Do NOT use ProcessWrapper here.
        private Process? _minecraftProcess;

        private static readonly HttpClient Http =
            new HttpClient(
                new HttpClientHandler
                {
                    AllowAutoRedirect = true
                });

        private readonly string _gamePath;
        private readonly string _configPath;
        private readonly string _logPath;

        private const string DefaultVersion = "1.21.1";

        private static readonly string[] SupportedVersions =
        {
            "1.21.1",
            "1.21.4",
            "1.21.8",
            "1.21.11",
            "26.1.2",
            "26.2"
        };

        private static readonly (string Slug, string Name)[] PerformanceMods =
        {
            ("sodium", "Sodium"),
            ("lithium", "Lithium"),
            ("indium", "Indium"),
            ("dynamic-fps", "Dynamic FPS"),
            ("sodium-extra", "Sodium Extra"),
            ("krypton", "Krypton")
        };

        public MainWindow()
        {
            InitializeComponent();

            _gamePath = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.ApplicationData),
                ".topuclient");

            _configPath = Path.Combine(
                _gamePath,
                "username.txt");

            _logPath = Path.Combine(
                _gamePath,
                "topu-minecraft.log");

            Directory.CreateDirectory(_gamePath);

            Http.DefaultRequestHeaders.UserAgent.ParseAdd(
                "TopuClient/1.0");

            LoadUsername();

            if (RamLabel != null)
            {
                RamLabel.Text = $"{(int)RamSlider.Value}GB";
            }

            WriteLog("Topu Client initialized.");
        }

        // ============================================================
        // LOGGING
        // ============================================================

        private void WriteLog(string message)
        {
            try
            {
                Directory.CreateDirectory(_gamePath);

                File.AppendAllText(
                    _logPath,
                    $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
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

        private void StartLaunchLog()
        {
            try
            {
                File.WriteAllText(
                    _logPath,
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
            try
            {
                File.AppendAllText(
                    _logPath,
                    $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
            }
            catch
            {
            }
        }

        // ============================================================
        // USERNAME
        // ============================================================

        private void LoadUsername()
        {
            try
            {
                if (!File.Exists(_configPath))
                    return;

                string username =
                    File.ReadAllText(_configPath).Trim();

                if (string.IsNullOrWhiteSpace(username))
                    return;

                UsernameInput.Text = username;
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
                    _configPath,
                    username);
            }
            catch (Exception ex)
            {
                WriteException(
                    "USERNAME SAVE ERROR",
                    ex);
            }
        }

        // ============================================================
        // WINDOW
        // ============================================================

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

        // ============================================================
        // TABS
        // ============================================================

        private void SwitchTab_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (sender is not Button button)
                return;

            string tab =
                button.Tag?.ToString() ?? "";

            TabLaunch.Visibility = Visibility.Collapsed;
            TabProfiles.Visibility = Visibility.Collapsed;
            TabAccounts.Visibility = Visibility.Collapsed;

            Brush inactive =
                new SolidColorBrush(
                    Color.FromRgb(136, 136, 136));

            Brush active =
                new SolidColorBrush(
                    Color.FromRgb(0, 255, 136));

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

        // ============================================================
        // RAM
        // ============================================================

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

        // ============================================================
        // VERSION
        // ============================================================

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

        private int GetRequiredJavaMajor(
            string minecraftVersion)
        {
            if (minecraftVersion.StartsWith(
                    "26.",
                    StringComparison.OrdinalIgnoreCase))
            {
                return 25;
            }

            return 21;
        }

        // ============================================================
        // PROFILE
        // ============================================================

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
                $"Profile saved: Minecraft={version}, RAM={ram}GB");

            MessageBox.Show(
                "Profile settings saved.",
                "Topu Client",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        // ============================================================
        // AUTH
        // ============================================================

        private void AuthTypeBox_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (StatusText == null)
                return;

            if (AuthTypeBox.SelectedIndex == 0)
            {
                StatusText.Text =
                    "Auth Mode: Offline";
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
            await Task.CompletedTask;

            MessageBox.Show(
                "Microsoft authentication is not enabled in this build yet.",
                "Microsoft Login",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        // ============================================================
        // SERVER
        // ============================================================

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

        // ============================================================
        // MODRINTH SEARCH
        // ============================================================

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
                    GetSelectedVersion();

                ModSearchStatus.Text =
                    $"Searching Modrinth for {query}...";

                string url =
                    "https://api.modrinth.com/v2/search" +
                    "?query=" +
                    Uri.EscapeDataString(query) +
                    "&facets=%5B%5B%22project_type%3Amod%22%5D%5D";

                string json =
                    await Http.GetStringAsync(url);

                using JsonDocument doc =
                    JsonDocument.Parse(json);

                JsonElement hits =
                    doc.RootElement.GetProperty("hits");

                if (hits.GetArrayLength() == 0)
                {
                    ModSearchStatus.Text =
                        "No mod found.";

                    return;
                }

                JsonElement hit =
                    hits[0];

                string projectId =
                    hit.GetProperty("project_id")
                        .GetString()
                    ?? "";

                string title =
                    hit.GetProperty("title")
                        .GetString()
                    ?? query;

                await DownloadModByProjectIdAsync(
                    projectId,
                    title,
                    version);

                ModSearchStatus.Text =
                    $"Installed: {title}";
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

        private async Task DownloadModByProjectIdAsync(
            string projectId,
            string title,
            string minecraftVersion)
        {
            string versionsUrl =
                "https://api.modrinth.com/v2/project/" +
                Uri.EscapeDataString(projectId) +
                "/version" +
                "?loaders=%5B%22fabric%22%5D" +
                "&game_versions=%5B%22" +
                Uri.EscapeDataString(minecraftVersion) +
                "%22%5D" +
                "&featured=true";

            string json =
                await Http.GetStringAsync(versionsUrl);

            using JsonDocument doc =
                JsonDocument.Parse(json);

            JsonElement versions =
                doc.RootElement;

            if (versions.ValueKind != JsonValueKind.Array ||
                versions.GetArrayLength() == 0)
            {
                throw new InvalidOperationException(
                    $"{title} has no compatible Fabric build for {minecraftVersion}.");
            }

            JsonElement version =
                versions[0];

            JsonElement files =
                version.GetProperty("files");

            string downloadUrl = "";
            string filename = "";

            foreach (JsonElement file in
                     files.EnumerateArray())
            {
                if (file.TryGetProperty(
                        "url",
                        out JsonElement urlElement))
                {
                    string? candidate =
                        urlElement.GetString();

                    if (!string.IsNullOrWhiteSpace(candidate))
                    {
                        downloadUrl = candidate;
                    }
                }

                if (file.TryGetProperty(
                        "filename",
                        out JsonElement filenameElement))
                {
                    string? candidate =
                        filenameElement.GetString();

                    if (!string.IsNullOrWhiteSpace(candidate))
                    {
                        filename = candidate;
                    }
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
            {
                throw new InvalidOperationException(
                    $"No download file was returned for {title}.");
            }

            if (string.IsNullOrWhiteSpace(filename))
            {
                filename =
                    $"{SanitizeFileName(title)}.jar";
            }

            string modsFolder =
                Path.Combine(
                    _gamePath,
                    "mods");

            Directory.CreateDirectory(modsFolder);

            string destination =
                Path.Combine(
                    modsFolder,
                    SanitizeFileName(filename));

            ModSearchStatus.Text =
                $"Downloading {title}...";

            byte[] bytes =
                await Http.GetByteArrayAsync(
                    downloadUrl);

            await File.WriteAllBytesAsync(
                destination,
                bytes);

            WriteLog(
                $"Installed mod: {title} -> {destination}");
        }

        // ============================================================
        // FABRIC INSTALLATION
        // ============================================================

        private async Task<string> InstallFabricAsync(
            string minecraftVersion)
        {
            StatusText.Text =
                $"Finding Fabric for Minecraft {minecraftVersion}...";

            string loaderUrl =
                "https://meta.fabricmc.net/v2/versions/loader/" +
                Uri.EscapeDataString(minecraftVersion);

            string json =
                await Http.GetStringAsync(loaderUrl);

            using JsonDocument doc =
                JsonDocument.Parse(json);

            JsonElement loaders =
                doc.RootElement;

            if (loaders.ValueKind != JsonValueKind.Array ||
                loaders.GetArrayLength() == 0)
            {
                throw new InvalidOperationException(
                    $"Fabric does not currently provide a loader for Minecraft {minecraftVersion}.");
            }

            JsonElement selected =
                loaders[0];

            JsonElement loader =
                selected.GetProperty("loader");

            string loaderVersion =
                loader.GetProperty("version")
                    .GetString()
                ?? throw new InvalidOperationException(
                    "Fabric loader version was missing.");

            string fabricId =
                $"fabric-loader-{loaderVersion}-{minecraftVersion}";

            string versionFolder =
                Path.Combine(
                    _gamePath,
                    "versions",
                    fabricId);

            Directory.CreateDirectory(
                versionFolder);

            string profileUrl =
                "https://meta.fabricmc.net/v2/versions/loader/" +
                Uri.EscapeDataString(minecraftVersion) +
                "/" +
                Uri.EscapeDataString(loaderVersion) +
                "/profile/json";

            StatusText.Text =
                $"Downloading Fabric {loaderVersion}...";

            string profileJson =
                await Http.GetStringAsync(profileUrl);

            if (string.IsNullOrWhiteSpace(profileJson))
            {
                throw new InvalidOperationException(
                    "Fabric returned an empty profile.");
            }

            string profilePath =
                Path.Combine(
                    versionFolder,
                    $"{fabricId}.json");

            await File.WriteAllTextAsync(
                profilePath,
                profileJson);

            WriteLog(
                $"Fabric profile saved: {profilePath}");

            using JsonDocument profileDoc =
                JsonDocument.Parse(profileJson);

            await DownloadFabricLibrariesAsync(
                profileDoc.RootElement);

            return fabricId;
        }

        private async Task DownloadFabricLibrariesAsync(
            JsonElement profile)
        {
            if (!profile.TryGetProperty(
                    "libraries",
                    out JsonElement libraries))
            {
                WriteLog(
                    "Fabric profile contains no additional libraries.");

                return;
            }

            if (libraries.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidOperationException(
                    "Fabric profile libraries is not an array.");
            }

            int total =
                libraries.GetArrayLength();

            int current = 0;

            foreach (JsonElement library in
                     libraries.EnumerateArray())
            {
                current++;

                if (!library.TryGetProperty(
                        "downloads",
                        out JsonElement downloads))
                {
                    continue;
                }

                if (!downloads.TryGetProperty(
                        "artifact",
                        out JsonElement artifact))
                {
                    continue;
                }

                if (!artifact.TryGetProperty(
                        "url",
                        out JsonElement urlElement))
                {
                    continue;
                }

                string? url =
                    urlElement.GetString();

                if (string.IsNullOrWhiteSpace(url))
                    continue;

                string relativePath;

                if (artifact.TryGetProperty(
                        "path",
                        out JsonElement pathElement))
                {
                    relativePath =
                        pathElement.GetString() ?? "";
                }
                else
                {
                    relativePath =
                        GetMavenPathFromUrl(url);
                }

                if (string.IsNullOrWhiteSpace(relativePath))
                    continue;

                string destination =
                    Path.Combine(
                        _gamePath,
                        "libraries",
                        relativePath.Replace(
                            '/',
                            Path.DirectorySeparatorChar));

                string? parent =
                    Path.GetDirectoryName(destination);

                if (!string.IsNullOrWhiteSpace(parent))
                {
                    Directory.CreateDirectory(parent);
                }

                if (File.Exists(destination))
                    continue;

                StatusText.Text =
                    $"Fabric libraries {current}/{total}...";

                try
                {
                    byte[] data =
                        await Http.GetByteArrayAsync(url);

                    await File.WriteAllBytesAsync(
                        destination,
                        data);

                    WriteLog(
                        $"Fabric library downloaded: {relativePath}");
                }
                catch (Exception ex)
                {
                    WriteLog(
                        $"Fabric library failed: {relativePath}");

                    WriteLog(ex.Message);

                    throw new InvalidOperationException(
                        $"Failed downloading Fabric library:{Environment.NewLine}{relativePath}",
                        ex);
                }
            }
        }

        private static string GetMavenPathFromUrl(
            string url)
        {
            Uri uri =
                new Uri(url);

            return uri.AbsolutePath.TrimStart('/');
        }

        // ============================================================
        // PERFORMANCE MODS
        // ============================================================

        private async Task InstallPerformanceModsAsync(
            string minecraftVersion)
        {
            string modsFolder =
                Path.Combine(
                    _gamePath,
                    "mods");

            Directory.CreateDirectory(modsFolder);

            int installed = 0;

            foreach ((string slug, string name) in
                     PerformanceMods)
            {
                try
                {
                    StatusText.Text =
                        $"Installing {name}...";

                    await DownloadModBySlugAsync(
                        slug,
                        name,
                        minecraftVersion);

                    installed++;
                }
                catch (Exception ex)
                {
                    WriteLog(
                        $"Optional mod skipped: {name}");

                    WriteLog(
                        ex.Message);
                }
            }

            WriteLog(
                $"Performance mods installed/skipped: {installed}/{PerformanceMods.Length}");
        }

        private async Task DownloadModBySlugAsync(
            string slug,
            string name,
            string minecraftVersion)
        {
            string url =
                "https://api.modrinth.com/v2/project/" +
                Uri.EscapeDataString(slug) +
                "/version" +
                "?loaders=%5B%22fabric%22%5D" +
                "&game_versions=%5B%22" +
                Uri.EscapeDataString(minecraftVersion) +
                "%22%5D" +
                "&featured=true";

            string json =
                await Http.GetStringAsync(url);

            using JsonDocument doc =
                JsonDocument.Parse(json);

            JsonElement versions =
                doc.RootElement;

            if (versions.ValueKind != JsonValueKind.Array ||
                versions.GetArrayLength() == 0)
            {
                throw new InvalidOperationException(
                    $"No compatible {name} version exists for {minecraftVersion}.");
            }

            JsonElement selected =
                versions[0];

            JsonElement files =
                selected.GetProperty("files");

            JsonElement? selectedFile = null;

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
                    selectedFile = file;
                    break;
                }

                if (selectedFile == null)
                {
                    selectedFile = file;
                }
            }

            if (selectedFile == null)
            {
                throw new InvalidOperationException(
                    $"No file was returned for {name}.");
            }

            JsonElement fileElement =
                selectedFile.Value;

            string downloadUrl =
                fileElement.GetProperty("url")
                    .GetString()
                ?? throw new InvalidOperationException(
                    $"No download URL for {name}.");

            string filename =
                fileElement.GetProperty("filename")
                    .GetString()
                ?? $"{slug}.jar";

            string destination =
                Path.Combine(
                    _gamePath,
                    "mods",
                    SanitizeFileName(filename));

            if (File.Exists(destination))
            {
                WriteLog(
                    $"Mod already installed: {name}");

                return;
            }

            byte[] data =
                await Http.GetByteArrayAsync(
                    downloadUrl);

            await File.WriteAllBytesAsync(
                destination,
                data);

            WriteLog(
                $"Installed performance mod: {name}");
        }

        // ============================================================
        // JAVA
        // ============================================================

        private async Task<string> EnsureJavaAsync(
            int requiredMajor)
        {
            string runtimeFolder =
                Path.Combine(
                    _gamePath,
                    "runtime",
                    $"java{requiredMajor}");

            string javaExe =
                Path.Combine(
                    runtimeFolder,
                    "bin",
                    "java.exe");

            if (File.Exists(javaExe) &&
                IsRequiredJava(
                    javaExe,
                    requiredMajor))
            {
                WriteLog(
                    $"Using existing Java {requiredMajor}: {javaExe}");

                return javaExe;
            }

            string systemJava =
                FindSystemJava(requiredMajor);

            if (!string.IsNullOrWhiteSpace(systemJava))
            {
                WriteLog(
                    $"Using system Java {requiredMajor}: {systemJava}");

                return systemJava;
            }

            StatusText.Text =
                $"Downloading Java {requiredMajor}...";

            WriteLog(
                $"Java {requiredMajor} not found. Installing automatically.");

            await DownloadAndInstallJavaAsync(
                requiredMajor,
                runtimeFolder);

            if (!File.Exists(javaExe))
            {
                throw new InvalidOperationException(
                    $"Java {requiredMajor} installation completed but java.exe was not found.");
            }

            return javaExe;
        }

        private string FindSystemJava(
            int requiredMajor)
        {
            string javaHome =
                Environment.GetEnvironmentVariable(
                    "JAVA_HOME") ?? "";

            if (!string.IsNullOrWhiteSpace(javaHome))
            {
                string candidate =
                    Path.Combine(
                        javaHome,
                        "bin",
                        "java.exe");

                if (File.Exists(candidate) &&
                    IsRequiredJava(
                        candidate,
                        requiredMajor))
                {
                    return candidate;
                }
            }

            string path =
                Environment.GetEnvironmentVariable(
                    "PATH") ?? "";

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

                if (IsRequiredJava(
                        candidate,
                        requiredMajor))
                {
                    return candidate;
                }
            }

            return "";
        }

        private bool IsRequiredJava(
            string javaPath,
            int requiredMajor)
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
                        "Could not start java.exe.");

                string output =
                    process.StandardOutput.ReadToEnd();

                string error =
                    process.StandardError.ReadToEnd();

                process.WaitForExit();

                string combined =
                    output +
                    Environment.NewLine +
                    error;

                return combined.Contains(
                    $"version \"{requiredMajor}.",
                    StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private async Task DownloadAndInstallJavaAsync(
            int major,
            string destination)
        {
            string apiUrl =
                "https://api.adoptium.net/v3/assets/latest/" +
                major +
                "/hotspot" +
                "?architecture=x64" +
                "&image_type=jre" +
                "&os=windows" +
                "&vendor=eclipse";

            string json =
                await Http.GetStringAsync(apiUrl);

            using JsonDocument doc =
                JsonDocument.Parse(json);

            JsonElement assets =
                doc.RootElement;

            if (assets.ValueKind != JsonValueKind.Array ||
                assets.GetArrayLength() == 0)
            {
                throw new InvalidOperationException(
                    $"Could not find a Windows x64 Java {major} runtime.");
            }

            JsonElement asset =
                assets[0];

            JsonElement binary =
                asset.GetProperty("binary");

            JsonElement package =
                binary.GetProperty("package");

            string downloadUrl =
                package.GetProperty("link")
                    .GetString()
                ?? throw new InvalidOperationException(
                    "Java download URL was missing.");

            string archiveName =
                package.TryGetProperty(
                    "name",
                    out JsonElement nameElement)
                    ? nameElement.GetString()
                        ?? $"java{major}.zip"
                    : $"java{major}.zip";

            string tempArchive =
                Path.Combine(
                    Path.GetTempPath(),
                    $"topu-java-{major}-{Guid.NewGuid():N}-{archiveName}");

            try
            {
                WriteLog(
                    $"Java download: {downloadUrl}");

                // ----------------------------------------------------
                // Download
                // ----------------------------------------------------

                using (HttpResponseMessage response =
                       await Http.GetAsync(
                           downloadUrl,
                           HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();

                    await using Stream input =
                        await response.Content.ReadAsStreamAsync();

                    await using (FileStream output =
                                 new FileStream(
                                     tempArchive,
                                     FileMode.Create,
                                     FileAccess.Write,
                                     FileShare.None,
                                     1024 * 64,
                                     FileOptions.SequentialScan))
                    {
                        await input.CopyToAsync(output);
                    }
                }

                // IMPORTANT:
                // Every HTTP/file stream is now disposed.
                // Only AFTER this point do we open the ZIP.
                if (!File.Exists(tempArchive))
                {
                    throw new IOException(
                        "Java archive was not created.");
                }

                if (Directory.Exists(destination))
                {
                    Directory.Delete(
                        destination,
                        true);
                }

                Directory.CreateDirectory(
                    destination);

                // ----------------------------------------------------
                // Extract
                // ----------------------------------------------------

                using (FileStream zipStream =
                       new FileStream(
                           tempArchive,
                           FileMode.Open,
                           FileAccess.Read,
                           FileShare.Read))
                using (ZipArchive archive =
                       new ZipArchive(
                           zipStream,
                           ZipArchiveMode.Read))
                {
                    archive.ExtractToDirectory(
                        destination,
                        true);
                }

                // ----------------------------------------------------
                // Flatten Adoptium root directory
                // ----------------------------------------------------

                string? nested =
                    FindJavaRoot(destination);

                if (nested != null &&
                    !File.Exists(
                        Path.Combine(
                            destination,
                            "bin",
                            "java.exe")))
                {
                    MoveJavaRootContents(
                        nested,
                        destination);
                }

                string javaExe =
                    Path.Combine(
                        destination,
                        "bin",
                        "java.exe");

                if (!File.Exists(javaExe))
                {
                    throw new InvalidOperationException(
                        $"Java {major} archive was extracted, but java.exe could not be located.");
                }

                WriteLog(
                    $"Java {major} installed: {javaExe}");
            }
            finally
            {
                // At this point the ZIP stream is guaranteed to be closed.
                try
                {
                    if (File.Exists(tempArchive))
                    {
                        File.Delete(tempArchive);
                    }
                }
                catch (Exception cleanupEx)
                {
                    WriteLog(
                        $"Java archive cleanup failed: {cleanupEx.Message}");
                }
            }
        }

        private string? FindJavaRoot(
            string destination)
        {
            foreach (string directory in
                     Directory.GetDirectories(
                         destination))
            {
                string javaExe =
                    Path.Combine(
                        directory,
                        "bin",
                        "java.exe");

                if (File.Exists(javaExe))
                    return directory;
            }

            return null;
        }

        private void MoveJavaRootContents(
            string source,
            string destination)
        {
            foreach (string directory in
                     Directory.GetDirectories(
                         source))
            {
                string target =
                    Path.Combine(
                        destination,
                        Path.GetFileName(directory));

                if (Directory.Exists(target))
                {
                    Directory.Delete(
                        target,
                        true);
                }

                Directory.Move(
                    directory,
                    target);
            }

            foreach (string file in
                     Directory.GetFiles(source))
            {
                string target =
                    Path.Combine(
                        destination,
                        Path.GetFileName(file));

                File.Move(
                    file,
                    target,
                    true);
            }

            try
            {
                Directory.Delete(
                    source,
                    true);
            }
            catch
            {
            }
        }

        // ============================================================
        // MAIN LAUNCH
        // ============================================================

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
                    _minecraftProcess = null;
                }
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
                    $"Minecraft: {minecraftVersion}");

                WriteLog(
                    $"RAM: {ram} MB");

                WriteLog(
                    $"Game directory: {_gamePath}");

                // ----------------------------------------------------
                // OFFLINE SESSION
                // ----------------------------------------------------

                if (AuthTypeBox.SelectedIndex != 0)
                {
                    throw new InvalidOperationException(
                        "Microsoft login is not configured yet. Select Offline Mode.");
                }

                string username =
                    UsernameInput.Text.Trim();

                if (string.IsNullOrWhiteSpace(username))
                {
                    username =
                        "TopuPlayer";
                }

                MSession session =
                    MSession.CreateOfflineSession(
                        username);

                SaveUsername(username);

                WriteLog(
                    $"Offline username: {username}");

                // ----------------------------------------------------
                // JAVA
                // ----------------------------------------------------

                int javaMajor =
                    GetRequiredJavaMajor(
                        minecraftVersion);

                WriteLog(
                    $"Required Java major: {javaMajor}");

                string javaPath =
                    await EnsureJavaAsync(
                        javaMajor);

                WriteLog(
                    $"Java executable: {javaPath}");

                // ----------------------------------------------------
                // CMLLIB
                // ----------------------------------------------------

                MinecraftPath minecraftPath =
                    new MinecraftPath(
                        _gamePath);

                MinecraftLauncher launcher =
                    new MinecraftLauncher(
                        minecraftPath);

                launcher.FileProgressChanged +=
                    (sender2, args) =>
                    {
                        Dispatcher.Invoke(
                            () =>
                            {
                                StatusText.Text =
                                    $"Downloading: {args.Name} " +
                                    $"({args.ProgressedTasks}/{args.TotalTasks})";
                            });
                    };

                launcher.ByteProgressChanged +=
                    (sender2, args) =>
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
                                        $"Downloading: {percent:0}%";
                                }
                            });
                    };

                // ----------------------------------------------------
                // VANILLA MINECRAFT
                // ----------------------------------------------------

                StatusText.Text =
                    $"Installing Minecraft {minecraftVersion}...";

                await launcher.InstallAsync(
                    minecraftVersion);

                WriteLog(
                    "Minecraft vanilla files installed.");

                // ----------------------------------------------------
                // FABRIC
                // ----------------------------------------------------

                StatusText.Text =
                    "Installing Fabric...";

                string fabricVersion =
                    await InstallFabricAsync(
                        minecraftVersion);

                WriteLog(
                    $"Fabric installed: {fabricVersion}");

                // ----------------------------------------------------
                // PERFORMANCE MODS
                // ----------------------------------------------------

                StatusText.Text =
                    "Installing performance mods...";

                await InstallPerformanceModsAsync(
                    minecraftVersion);

                WriteLog(
                    "Performance mod setup completed.");

                // ----------------------------------------------------
                // BUILD PROCESS
                // ----------------------------------------------------

                StatusText.Text =
                    "Building Minecraft process...";

                MLaunchOption options =
                    new MLaunchOption
                    {
                        Session = session,
                        MaximumRamMb = ram,
                        JavaPath = javaPath
                    };

                /*
                 * CmlLib.Core 4.0.6:
                 *
                 * BuildProcessAsync returns System.Diagnostics.Process.
                 *
                 * Therefore:
                 *
                 *     Process process =
                 *         await launcher.BuildProcessAsync(...);
                 *
                 * NOT:
                 *
                 *     ProcessWrapper processWrapper = ...
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
                    fabricVersion,
                    ram);

                // ----------------------------------------------------
                // START
                // ----------------------------------------------------

                StatusText.Text =
                    $"Starting Fabric {minecraftVersion}...";

                WriteLog(
                    "Starting Minecraft.");

                // This is System.Diagnostics.Process.Start().
                process.Start();

                WriteLog(
                    $"Minecraft started. PID={process.Id}");

                StatusText.Text =
                    $"Topu Client running as {username}";

                _ = MonitorMinecraftAsync(
                    process);
            }
            catch (Exception ex)
            {
                StatusText.Text =
                    "Launch failed.";

                WriteException(
                    "TOPU LAUNCH ERROR",
                    ex);

                MessageBox.Show(
                    "Minecraft failed to launch." +
                    Environment.NewLine +
                    Environment.NewLine +
                    ex.Message +
                    Environment.NewLine +
                    Environment.NewLine +
                    "Log file:" +
                    Environment.NewLine +
                    _logPath,
                    "Topu Client",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                LaunchBtn.IsEnabled = true;
            }
        }

        // ============================================================
        // PROCESS MONITOR
        // ============================================================

        private async Task MonitorMinecraftAsync(
            Process process)
        {
            try
            {
                while (!process.HasExited)
                {
                    await Task.Delay(1000);
                }

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

        // ============================================================
        // DEBUG
        // ============================================================

        private void WriteDebugFile(
            Process process,
            string javaPath,
            string minecraftVersion,
            string fabricVersion,
            int ram)
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
                    $"Time: {DateTime.Now:O}" +
                    Environment.NewLine +
                    Environment.NewLine +
                    $"Minecraft: {minecraftVersion}" +
                    Environment.NewLine +
                    $"Fabric: {fabricVersion}" +
                    Environment.NewLine +
                    $"Java: {javaPath}" +
                    Environment.NewLine +
                    $"RAM: {ram} MB" +
                    Environment.NewLine +
                    Environment.NewLine +
                    "Executable:" +
                    Environment.NewLine +
                    process.StartInfo.FileName +
                    Environment.NewLine +
                    Environment.NewLine +
                    "Arguments:" +
                    Environment.NewLine +
                    process.StartInfo.Arguments +
                    Environment.NewLine +
                    Environment.NewLine +
                    "Working directory:" +
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

        // ============================================================
        // UTILITIES
        // ============================================================

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
