using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.Installers;
using CmlLib.Core.ModLoaders.FabricMC;
using CmlLib.Core.ProcessBuilder;

namespace TopuLauncher
{
    public partial class MainWindow : Window
    {
        // ============================================================
        // HTTP
        // ============================================================

        private static readonly HttpClient Http = CreateHttpClient();

        private static HttpClient CreateHttpClient()
        {
            HttpClient client = new HttpClient(
                new HttpClientHandler
                {
                    AllowAutoRedirect = true
                });

            client.Timeout = TimeSpan.FromMinutes(30);
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "TopuClient/2.0");

            return client;
        }

        // ============================================================
        // CORE STATE
        // ============================================================

        private MSession? _session;
        private Process? _minecraftProcess;

        private readonly string _gamePath;
        private readonly string _profilesPath;
        private readonly string _runtimePath;
        private readonly string _configPath;
        private readonly string _logPath;

        private readonly object _logLock = new object();

        private ProfileData? _currentProfile;

        private const string DefaultVersion = "1.21.1";
        private const string DefaultProfileName = "Default";

        // ============================================================
        // SUPPORTED VERSIONS
        // ============================================================

        private static readonly string[] SupportedVersions =
        {
            "1.21.1",
            "1.21.4",
            "1.21.8",
            "1.21.11",
            "26.1.2",
            "26.2"
        };

        // ============================================================
        // PERFORMANCE MODS
        // ============================================================

        private static readonly (string Slug, string Name)[] PerformanceMods =
        {
            ("fabric-api", "Fabric API"),
            ("sodium", "Sodium"),
            ("lithium", "Lithium"),
            ("dynamic-fps", "Dynamic FPS"),
            ("sodium-extra", "Sodium Extra"),
            ("krypton", "Krypton")
        };

        // ============================================================
        // CONSTRUCTOR
        // ============================================================

        public MainWindow()
        {
            InitializeComponent();

            _gamePath = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.ApplicationData),
                ".topuclient");

            _profilesPath = Path.Combine(
                _gamePath,
                "profiles");

            _runtimePath = Path.Combine(
                _gamePath,
                "runtime");

            _configPath = Path.Combine(
                _gamePath,
                "username.txt");

            _logPath = Path.Combine(
                _gamePath,
                "topu-minecraft.log");

            Directory.CreateDirectory(_gamePath);
            Directory.CreateDirectory(_profilesPath);
            Directory.CreateDirectory(_runtimePath);

            EnsureDefaultProfile();
            LoadUsername();

            WriteLog("==========================================");
            WriteLog("Topu Client Multi-Profile initialized.");
            WriteLog($"Root directory: {_gamePath}");
            WriteLog($"Profiles directory: {_profilesPath}");
            WriteLog("==========================================");

            RefreshProfileList();
        }

        // ============================================================
        // PROFILE MODEL
        // ============================================================

        public sealed class ProfileData
        {
            [JsonPropertyName("name")]
            public string Name { get; set; } = "Default";

            [JsonPropertyName("minecraftVersion")]
            public string MinecraftVersion { get; set; } = "1.21.1";

            [JsonPropertyName("ramGb")]
            public int RamGb { get; set; } = 4;

            [JsonPropertyName("username")]
            public string Username { get; set; } = "TopuPlayer";

            [JsonPropertyName("loader")]
            public string Loader { get; set; } = "Fabric";

            [JsonPropertyName("created")]
            public DateTime Created { get; set; } = DateTime.Now;

            [JsonPropertyName("lastPlayed")]
            public DateTime LastPlayed { get; set; } = DateTime.MinValue;

            [JsonPropertyName("mods")]
            public List<string> Mods { get; set; } = new List<string>();
        }

        private static readonly JsonSerializerOptions ProfileJsonOptions =
            new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true
            };

        // ============================================================
        // PROFILE PATHS
        // ============================================================

        private string GetProfileDirectory(string profileName)
        {
            return Path.Combine(
                _profilesPath,
                SanitizeProfileName(profileName));
        }

        private string GetProfileJsonPath(string profileName)
        {
            return Path.Combine(
                GetProfileDirectory(profileName),
                "profile.json");
        }

        private string GetProfileModsPath(string profileName)
        {
            return Path.Combine(
                GetProfileDirectory(profileName),
                "mods");
        }

        private string GetProfileVersionsPath(string profileName)
        {
            return Path.Combine(
                GetProfileDirectory(profileName),
                "versions");
        }

        private string GetProfileLibrariesPath(string profileName)
        {
            return Path.Combine(
                GetProfileDirectory(profileName),
                "libraries");
        }

        // ============================================================
        // PROFILE NAME SANITIZATION
        // ============================================================

        private static string SanitizeProfileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "Default";

            StringBuilder result = new StringBuilder();

            foreach (char c in name.Trim())
            {
                if (char.IsLetterOrDigit(c) ||
                    c == ' ' ||
                    c == '-' ||
                    c == '_' ||
                    c == '.')
                {
                    result.Append(c);
                }
            }

            string value = result.ToString().Trim();

            if (string.IsNullOrWhiteSpace(value))
                return "Default";

            return value;
        }

        // ============================================================
        // DEFAULT PROFILE
        // ============================================================

        private void EnsureDefaultProfile()
        {
            string directory =
                GetProfileDirectory(DefaultProfileName);

            Directory.CreateDirectory(directory);

            Directory.CreateDirectory(
                GetProfileModsPath(DefaultProfileName));

            Directory.CreateDirectory(
                GetProfileVersionsPath(DefaultProfileName));

            Directory.CreateDirectory(
                GetProfileLibrariesPath(DefaultProfileName));

            string json =
                GetProfileJsonPath(DefaultProfileName);

            if (!File.Exists(json))
            {
                ProfileData profile = new ProfileData
                {
                    Name = DefaultProfileName,
                    MinecraftVersion = DefaultVersion,
                    RamGb = 4,
                    Username = "TopuPlayer",
                    Loader = "Fabric",
                    Created = DateTime.Now,
                    LastPlayed = DateTime.MinValue
                };

                SaveProfile(profile);
            }
        }

        // ============================================================
        // PROFILE SAVE
        // ============================================================

        private void SaveProfile(ProfileData profile)
        {
            string directory =
                GetProfileDirectory(profile.Name);

            Directory.CreateDirectory(directory);

            Directory.CreateDirectory(
                GetProfileModsPath(profile.Name));

            Directory.CreateDirectory(
                GetProfileVersionsPath(profile.Name));

            Directory.CreateDirectory(
                GetProfileLibrariesPath(profile.Name));

            string json =
                GetProfileJsonPath(profile.Name);

            string content =
                JsonSerializer.Serialize(
                    profile,
                    ProfileJsonOptions);

            File.WriteAllText(
                json,
                content);

            WriteLog(
                $"Saved profile: {profile.Name}");
        }

        // ============================================================
        // PROFILE LOAD
        // ============================================================

        private ProfileData? LoadProfile(string profileName)
        {
            try
            {
                string json =
                    GetProfileJsonPath(profileName);

                if (!File.Exists(json))
                    return null;

                string content =
                    File.ReadAllText(json);

                ProfileData? profile =
                    JsonSerializer.Deserialize<ProfileData>(
                        content,
                        ProfileJsonOptions);

                if (profile == null)
                    return null;

                profile.Name =
                    SanitizeProfileName(profile.Name);

                if (string.IsNullOrWhiteSpace(
                        profile.MinecraftVersion))
                {
                    profile.MinecraftVersion =
                        DefaultVersion;
                }

                if (profile.RamGb < 1)
                    profile.RamGb = 1;

                if (string.IsNullOrWhiteSpace(profile.Loader))
                    profile.Loader = "Fabric";

                return profile;
            }
            catch (Exception ex)
            {
                WriteException(
                    $"PROFILE LOAD ERROR: {profileName}",
                    ex);

                return null;
            }
        }

        // ============================================================
        // ALL PROFILES
        // ============================================================

        private List<ProfileData> GetAllProfiles()
        {
            List<ProfileData> profiles =
                new List<ProfileData>();

            Directory.CreateDirectory(_profilesPath);

            foreach (string directory in
                     Directory.GetDirectories(_profilesPath))
            {
                string name =
                    Path.GetFileName(directory);

                ProfileData? profile =
                    LoadProfile(name);

                if (profile != null)
                    profiles.Add(profile);
            }

            profiles = profiles
                .OrderBy(
                    p => p.Name.Equals(
                        DefaultProfileName,
                        StringComparison.OrdinalIgnoreCase)
                        ? 0
                        : 1)
                .ThenBy(
                    p => p.Name,
                    StringComparer.OrdinalIgnoreCase)
                .ToList();

            return profiles;
        }

        // ============================================================
        // REFRESH PROFILE UI
        // ============================================================

        private void RefreshProfileList()
        {
            try
            {
                List<ProfileData> profiles =
                    GetAllProfiles();

                // These controls belong to the NEW profile XAML.
                // We intentionally don't rely on the old XAML layout.

                if (ProfileList != null)
                {
                    ProfileList.Items.Clear();

                    foreach (ProfileData profile in profiles)
                    {
                        ProfileList.Items.Add(profile.Name);
                    }
                }

                if (_currentProfile != null &&
                    ProfileList != null)
                {
                    int index =
                        ProfileList.Items.IndexOf(
                            _currentProfile.Name);

                    if (index >= 0)
                        ProfileList.SelectedIndex = index;
                }

                WriteLog(
                    $"Profile list refreshed: {profiles.Count} profiles.");
            }
            catch (Exception ex)
            {
                WriteException(
                    "PROFILE LIST REFRESH ERROR",
                    ex);
            }
        }

        // ============================================================
        // SELECT PROFILE
        // ============================================================

        private void ProfileList_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            try
            {
                if (ProfileList?.SelectedItem == null)
                    return;

                string name =
                    ProfileList.SelectedItem.ToString() ?? "";

                if (string.IsNullOrWhiteSpace(name))
                    return;

                SelectProfile(name);
            }
            catch (Exception ex)
            {
                WriteException(
                    "PROFILE SELECTION ERROR",
                    ex);
            }
        }

        private void SelectProfile(string profileName)
        {
            ProfileData? profile =
                LoadProfile(profileName);

            if (profile == null)
            {
                WriteLog(
                    $"Could not load profile: {profileName}");
                return;
            }

            _currentProfile = profile;

            WriteLog(
                $"Selected profile: {profile.Name}");

            UpdateProfileControls(profile);
        }

        // ============================================================
        // PROFILE CONTROLS
        // ============================================================

        private void UpdateProfileControls(
            ProfileData profile)
        {
            try
            {
                if (ProfileNameInput != null)
                    ProfileNameInput.Text = profile.Name;

                if (ProfileVersionBox != null)
                {
                    SelectVersionInComboBox(
                        ProfileVersionBox,
                        profile.MinecraftVersion);
                }

                if (ProfileRamSlider != null)
                {
                    ProfileRamSlider.Value =
                        Math.Max(
                            ProfileRamSlider.Minimum,
                            Math.Min(
                                ProfileRamSlider.Maximum,
                                profile.RamGb));
                }

                if (ProfileUsernameInput != null)
                    ProfileUsernameInput.Text =
                        profile.Username;

                if (ProfileStatusText != null)
                {
                    ProfileStatusText.Text =
                        $"{profile.Name} • Fabric {profile.MinecraftVersion} • {profile.RamGb}GB";
                }

                if (StatusText != null)
                {
                    StatusText.Text =
                        $"Profile: {profile.Name}";
                }
            }
            catch (Exception ex)
            {
                WriteException(
                    "PROFILE CONTROL UPDATE ERROR",
                    ex);
            }
        }

        private static void SelectVersionInComboBox(
            ComboBox box,
            string version)
        {
            for (int i = 0; i < box.Items.Count; i++)
            {
                if (box.Items[i] is ComboBoxItem item)
                {
                    string value =
                        item.Content?.ToString() ?? "";

                    if (string.Equals(
                            value,
                            version,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        box.SelectedIndex = i;
                        return;
                    }
                }
                else
                {
                    string value =
                        box.Items[i]?.ToString() ?? "";

                    if (string.Equals(
                            value,
                            version,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        box.SelectedIndex = i;
                        return;
                    }
                }
            }
        }

        // ============================================================
        // CREATE PROFILE
        // ============================================================

        private void CreateProfile_Click(
            object sender,
            RoutedEventArgs e)
        {
            try
            {
                string requestedName =
                    ProfileNameInput?.Text.Trim() ?? "";

                if (string.IsNullOrWhiteSpace(requestedName))
                {
                    requestedName = "New Profile";
                }

                string profileName =
                    MakeUniqueProfileName(
                        requestedName);

                ProfileData profile =
                    new ProfileData
                    {
                        Name = profileName,
                        MinecraftVersion = DefaultVersion,
                        RamGb = 4,
                        Username =
                            !string.IsNullOrWhiteSpace(
                                UsernameInput?.Text)
                                ? UsernameInput.Text.Trim()
                                : "TopuPlayer",
                        Loader = "Fabric",
                        Created = DateTime.Now
                    };

                SaveProfile(profile);

                _currentProfile = profile;

                RefreshProfileList();

                if (ProfileList != null)
                {
                    int index =
                        ProfileList.Items.IndexOf(
                            profile.Name);

                    if (index >= 0)
                        ProfileList.SelectedIndex = index;
                }

                UpdateProfileControls(profile);

                WriteLog(
                    $"Created new profile: {profile.Name}");

                StatusText.Text =
                    $"Created profile: {profile.Name}";
            }
            catch (Exception ex)
            {
                WriteException(
                    "CREATE PROFILE ERROR",
                    ex);

                MessageBox.Show(
                    ex.Message,
                    "Profile Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private string MakeUniqueProfileName(string requested)
        {
            string baseName =
                SanitizeProfileName(requested);

            string name = baseName;
            int number = 2;

            while (
                Directory.Exists(
                    GetProfileDirectory(name)))
            {
                name =
                    $"{baseName} {number}";
                number++;
            }

            return name;
        }

        // ============================================================
        // SAVE CURRENT PROFILE
        // ============================================================

        private void SaveProfile_Click(
            object sender,
            RoutedEventArgs e)
        {
            try
            {
                if (_currentProfile == null)
                {
                    MessageBox.Show(
                        "Select a profile first.",
                        "Topu Client",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    return;
                }

                string newName =
                    ProfileNameInput?.Text.Trim()
                    ?? _currentProfile.Name;

                if (string.IsNullOrWhiteSpace(newName))
                    newName = _currentProfile.Name;

                newName =
                    SanitizeProfileName(newName);

                if (!string.Equals(
                        newName,
                        _currentProfile.Name,
                        StringComparison.OrdinalIgnoreCase))
                {
                    RenameProfileDirectory(
                        _currentProfile.Name,
                        newName);

                    _currentProfile.Name = newName;
                }

                string version =
                    GetProfileSelectedVersion();

                int ram =
                    GetProfileRam();

                string username =
                    ProfileUsernameInput?.Text.Trim()
                    ?? _currentProfile.Username;

                if (string.IsNullOrWhiteSpace(username))
                    username = "TopuPlayer";

                _currentProfile.MinecraftVersion =
                    version;

                _currentProfile.RamGb =
                    ram;

                _currentProfile.Username =
                    username;

                SaveProfile(_currentProfile);

                RefreshProfileList();
                UpdateProfileControls(_currentProfile);

                StatusText.Text =
                    $"Profile saved: {_currentProfile.Name}";

                WriteLog(
                    $"Profile settings saved: {_currentProfile.Name}");
            }
            catch (Exception ex)
            {
                WriteException(
                    "SAVE PROFILE ERROR",
                    ex);

                MessageBox.Show(
                    ex.Message,
                    "Profile Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // ============================================================
        // DELETE PROFILE
        // ============================================================

        private void DeleteProfile_Click(
            object sender,
            RoutedEventArgs e)
        {
            try
            {
                if (_currentProfile == null)
                {
                    MessageBox.Show(
                        "Select a profile first.",
                        "Topu Client",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    return;
                }

                if (_currentProfile.Name.Equals(
                        DefaultProfileName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show(
                        "The Default profile cannot be deleted.",
                        "Topu Client",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    return;
                }

                MessageBoxResult result =
                    MessageBox.Show(
                        $"Delete profile '{_currentProfile.Name}'?\n\n" +
                        "This deletes the profile's Minecraft files, mods and versions.",
                        "Delete Profile",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes)
                    return;

                string directory =
                    GetProfileDirectory(
                        _currentProfile.Name);

                TryDeleteDirectory(directory);

                WriteLog(
                    $"Deleted profile: {_currentProfile.Name}");

                _currentProfile = null;

                RefreshProfileList();

                if (ProfileList != null &&
                    ProfileList.Items.Count > 0)
                {
                    ProfileList.SelectedIndex = 0;
                }

                StatusText.Text =
                    "Profile deleted.";
            }
            catch (Exception ex)
            {
                WriteException(
                    "DELETE PROFILE ERROR",
                    ex);

                MessageBox.Show(
                    ex.Message,
                    "Profile Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // ============================================================
        // RENAME PROFILE DIRECTORY
        // ============================================================

        private void RenameProfileDirectory(
            string oldName,
            string newName)
        {
            string oldPath =
                GetProfileDirectory(oldName);

            string newPath =
                GetProfileDirectory(newName);

            if (Directory.Exists(newPath))
            {
                throw new InvalidOperationException(
                    $"A profile named '{newName}' already exists.");
            }

            if (!Directory.Exists(oldPath))
            {
                Directory.CreateDirectory(newPath);
                return;
            }

            Directory.Move(
                oldPath,
                newPath);

            WriteLog(
                $"Renamed profile '{oldName}' -> '{newName}'");
        }

        // ============================================================
        // PROFILE VERSION
        // ============================================================

        private string GetProfileSelectedVersion()
        {
            if (ProfileVersionBox?.SelectedItem is ComboBoxItem item)
            {
                string value =
                    item.Content?.ToString()?.Trim() ?? "";

                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }

            if (ProfileVersionBox?.SelectedItem != null)
            {
                string value =
                    ProfileVersionBox.SelectedItem
                        .ToString()
                        ?.Trim() ?? "";

                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }

            return DefaultVersion;
        }

        private int GetProfileRam()
        {
            if (ProfileRamSlider == null)
                return 4;

            return Math.Max(
                1,
                (int)ProfileRamSlider.Value);
        }

        // ============================================================
        // PROFILE RAM DISPLAY
        // ============================================================

        private void ProfileRamSlider_ValueChanged(
            object sender,
            RoutedPropertyChangedEventArgs<double> e)
        {
            if (ProfileRamLabel != null)
            {
                ProfileRamLabel.Text =
                    $"{(int)e.NewValue}GB";
            }
        }

        // ============================================================
        // PROFILE VERSION CHANGE
        // ============================================================

        private void ProfileVersionBox_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (_currentProfile == null)
                return;

            try
            {
                string version =
                    GetProfileSelectedVersion();

                if (ProfileStatusText != null)
                {
                    ProfileStatusText.Text =
                        $"{_currentProfile.Name} • Fabric {version} • {_currentProfile.RamGb}GB";
                }
            }
            catch
            {
            }
        }

        // ============================================================
        // OLD VERSION SUPPORT
        // ============================================================

        private string GetSelectedVersion()
        {
            if (_currentProfile != null)
                return _currentProfile.MinecraftVersion;

            return GetProfileSelectedVersion();
        }

        // ============================================================
        // JAVA
        // ============================================================

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

        private async Task<string> EnsureJavaAsync(
            int requiredMajor)
        {
            string runtimeFolder =
                Path.Combine(
                    _runtimePath,
                    $"java{requiredMajor}");

            string javaExe =
                Path.Combine(
                    runtimeFolder,
                    "bin",
                    "java.exe");

            if (File.Exists(javaExe))
            {
                WriteLog(
                    $"Checking Topu Java {requiredMajor}: {javaExe}");

                if (IsRequiredJava(
                        javaExe,
                        requiredMajor))
                {
                    WriteLog(
                        $"Using Topu Java {requiredMajor}: {javaExe}");

                    return javaExe;
                }
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

            await DownloadAndInstallJavaAsync(
                requiredMajor,
                runtimeFolder);

            if (!File.Exists(javaExe))
            {
                throw new InvalidOperationException(
                    $"Java {requiredMajor} installation failed.");
            }

            if (!IsRequiredJava(
                    javaExe,
                    requiredMajor))
            {
                throw new InvalidOperationException(
                    $"Installed runtime is not Java {requiredMajor}.");
            }

            return javaExe;
        }

        private string FindSystemJava(int requiredMajor)
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
                        "Could not start Java.");

                string stdout =
                    process.StandardOutput.ReadToEnd();

                string stderr =
                    process.StandardError.ReadToEnd();

                process.WaitForExit();

                string combined =
                    stdout +
                    Environment.NewLine +
                    stderr;

                WriteLog(
                    $"Java check [{javaPath}]: {combined.Trim()}");

                return combined.Contains(
                    $"version \"{requiredMajor}.",
                    StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                WriteLog(
                    $"Java check failed: {ex.Message}");

                return false;
            }
        }

        // ============================================================
        // JAVA DOWNLOAD
        // ============================================================

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

            using HttpResponseMessage response =
                await Http.GetAsync(apiUrl);

            response.EnsureSuccessStatusCode();

            string json =
                await response.Content.ReadAsStringAsync();

            using JsonDocument doc =
                JsonDocument.Parse(json);

            JsonElement assets =
                doc.RootElement;

            if (assets.ValueKind != JsonValueKind.Array ||
                assets.GetArrayLength() == 0)
            {
                throw new InvalidOperationException(
                    $"No Java {major} Windows x64 runtime found.");
            }

            JsonElement package =
                assets[0]
                    .GetProperty("binary")
                    .GetProperty("package");

            string downloadUrl =
                package.GetProperty("link")
                    .GetString()
                ?? throw new InvalidOperationException(
                    "Java download URL missing.");

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
                    "topu-java-" +
                    Guid.NewGuid().ToString("N") +
                    "-" +
                    SanitizeFileName(archiveName));

            try
            {
                await DownloadFileAsync(
                    downloadUrl,
                    tempArchive);

                string extractionDirectory =
                    destination +
                    ".extracting-" +
                    Guid.NewGuid().ToString("N");

                try
                {
                    TryDeleteDirectory(
                        extractionDirectory);

                    Directory.CreateDirectory(
                        extractionDirectory);

                    ZipFile.ExtractToDirectory(
                        tempArchive,
                        extractionDirectory,
                        true);

                    string? javaRoot =
                        FindJavaRoot(
                            extractionDirectory);

                    if (javaRoot != null &&
                        !File.Exists(
                            Path.Combine(
                                extractionDirectory,
                                "bin",
                                "java.exe")))
                    {
                        MoveJavaRootContents(
                            javaRoot,
                            extractionDirectory);
                    }

                    string extractedJava =
                        Path.Combine(
                            extractionDirectory,
                            "bin",
                            "java.exe");

                    if (!File.Exists(extractedJava))
                    {
                        throw new InvalidOperationException(
                            "java.exe was not found after extraction.");
                    }

                    if (Directory.Exists(destination))
                        TryDeleteDirectory(destination);

                    MoveDirectoryWithRetry(
                        extractionDirectory,
                        destination);
                }
                catch
                {
                    TryDeleteDirectory(
                        extractionDirectory);

                    throw;
                }
            }
            finally
            {
                TryDeleteFileWithRetry(
                    tempArchive);
            }
        }

        private static string? FindJavaRoot(
            string destination)
        {
            foreach (string directory in
                     Directory.GetDirectories(destination))
            {
                if (File.Exists(
                        Path.Combine(
                            directory,
                            "bin",
                            "java.exe")))
                {
                    return directory;
                }
            }

            return null;
        }

        private static void MoveJavaRootContents(
            string source,
            string destination)
        {
            foreach (string directory in
                     Directory.GetDirectories(source))
            {
                string target =
                    Path.Combine(
                        destination,
                        Path.GetFileName(directory));

                if (Directory.Exists(target))
                    Directory.Delete(
                        target,
                        true);

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

            TryDeleteDirectory(source);
        }

        private static void MoveDirectoryWithRetry(
            string source,
            string destination)
        {
            Exception? lastException = null;

            for (int i = 0; i < 20; i++)
            {
                try
                {
                    Directory.Move(
                        source,
                        destination);

                    return;
                }
                catch (IOException ex)
                {
                    lastException = ex;
                    Thread.Sleep(
                        250 + i * 100);
                }
                catch (UnauthorizedAccessException ex)
                {
                    lastException = ex;
                    Thread.Sleep(
                        250 + i * 100);
                }
            }

            throw new IOException(
                $"Could not move Java runtime into {destination}.",
                lastException);
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
                    File.ReadAllText(
                        _configPath).Trim();

                if (string.IsNullOrWhiteSpace(username))
                    return;

                if (UsernameInput != null)
                    UsernameInput.Text = username;

                _session =
                    MSession.CreateOfflineSession(
                        username);

                WriteLog(
                    $"Loaded username: {username}");
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
        // AUTH
        // ============================================================

        private void AuthTypeBox_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (StatusText == null)
                return;

            if (AuthTypeBox.SelectedIndex == 0)
                StatusText.Text = "Auth Mode: Offline";
            else
                StatusText.Text =
                    "Auth Mode: Microsoft Official";
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

            TabLaunch.Visibility =
                Visibility.Collapsed;

            TabProfiles.Visibility =
                Visibility.Collapsed;

            TabAccounts.Visibility =
                Visibility.Collapsed;

            if (TabLaunchBtn != null)
                TabLaunchBtn.Foreground =
                    InactiveBrush();

            if (TabProfilesBtn != null)
                TabProfilesBtn.Foreground =
                    InactiveBrush();

            if (TabAccountsBtn != null)
                TabAccountsBtn.Foreground =
                    InactiveBrush();

            if (TabLaunchBtn != null)
                TabLaunchBtn.BorderThickness =
                    new Thickness(0);

            if (TabProfilesBtn != null)
                TabProfilesBtn.BorderThickness =
                    new Thickness(0);

            if (TabAccountsBtn != null)
                TabAccountsBtn.BorderThickness =
                    new Thickness(0);

            button.Foreground =
                ActiveBrush();

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

        private static Brush ActiveBrush()
        {
            return new SolidColorBrush(
                Color.FromRgb(
                    0,
                    255,
                    136));
        }

        private static Brush InactiveBrush()
        {
            return new SolidColorBrush(
                Color.FromRgb(
                    136,
                    136,
                    136));
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
            WindowState =
                WindowState.Minimized;
        }

        private void Close_Click(
            object sender,
            RoutedEventArgs e)
        {
            try
            {
                if (_minecraftProcess != null)
                {
                    try
                    {
                        if (!_minecraftProcess.HasExited)
                            _minecraftProcess.CloseMainWindow();
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }

            Close();
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

            if (_currentProfile == null)
            {
                MessageBox.Show(
                    "Select a profile first.",
                    "Modrinth",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            try
            {
                string version =
                    _currentProfile.MinecraftVersion;

                ModSearchStatus.Text =
                    $"Searching Modrinth for {query}...";

                string url =
                    "https://api.modrinth.com/v2/search" +
                    "?query=" +
                    Uri.EscapeDataString(query) +
                    "&facets=%5B%5B%22project_type%3Amod%22%5D%5D";

                using HttpResponseMessage response =
                    await Http.GetAsync(url);

                response.EnsureSuccessStatusCode();

                string json =
                    await response.Content
                        .ReadAsStringAsync();

                using JsonDocument doc =
                    JsonDocument.Parse(json);

                JsonElement hits =
                    doc.RootElement
                        .GetProperty("hits");

                if (hits.GetArrayLength() == 0)
                {
                    ModSearchStatus.Text =
                        "No mod found.";

                    return;
                }

                JsonElement hit = hits[0];

                string projectId =
                    hit.GetProperty("project_id")
                        .GetString() ?? "";

                string title =
                    hit.GetProperty("title")
                        .GetString() ?? query;

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

        // ============================================================
        // DOWNLOAD MOD INTO CURRENT PROFILE
        // ============================================================

        private async Task DownloadModByProjectIdAsync(
            string projectId,
            string title,
            string minecraftVersion)
        {
            if (_currentProfile == null)
                throw new InvalidOperationException(
                    "No profile is selected.");

            string url =
                "https://api.modrinth.com/v2/project/" +
                Uri.EscapeDataString(projectId) +
                "/version" +
                "?loaders=%5B%22fabric%22%5D" +
                "&game_versions=%5B%22" +
                Uri.EscapeDataString(minecraftVersion) +
                "%22%5D";

            using HttpResponseMessage response =
                await Http.GetAsync(url);

            response.EnsureSuccessStatusCode();

            string json =
                await response.Content
                    .ReadAsStringAsync();

            using JsonDocument doc =
                JsonDocument.Parse(json);

            JsonElement versions =
                doc.RootElement;

            if (versions.ValueKind !=
                    JsonValueKind.Array ||
                versions.GetArrayLength() == 0)
            {
                throw new InvalidOperationException(
                    $"{title} has no compatible Fabric build for {minecraftVersion}.");
            }

            JsonElement? selectedFile = null;

            foreach (JsonElement version in
                     versions.EnumerateArray())
            {
                if (!version.TryGetProperty(
                        "files",
                        out JsonElement files))
                    continue;

                selectedFile =
                    FindPrimaryJar(files);

                if (selectedFile != null)
                    break;
            }

            if (selectedFile == null)
            {
                throw new InvalidOperationException(
                    $"No JAR file was returned for {title}.");
            }

            JsonElement file =
                selectedFile.Value;

            string downloadUrl =
                file.GetProperty("url")
                    .GetString()
                ?? throw new InvalidOperationException(
                    $"No download URL for {title}.");

            string filename =
                file.GetProperty("filename")
                    .GetString()
                ?? $"{SanitizeFileName(title)}.jar";

            string modsFolder =
                GetProfileModsPath(
                    _currentProfile.Name);

            Directory.CreateDirectory(
                modsFolder);

            string destination =
                Path.Combine(
                    modsFolder,
                    SanitizeFileName(filename));

            StatusText.Text =
                $"Downloading {title} to {_currentProfile.Name}...";

            await DownloadFileAsync(
                downloadUrl,
                destination);

            if (!_currentProfile.Mods.Contains(
                    filename,
                    StringComparer.OrdinalIgnoreCase))
            {
                _currentProfile.Mods.Add(
                    filename);
            }

            SaveProfile(_currentProfile);

            WriteLog(
                $"Installed mod '{title}' into profile '{_currentProfile.Name}'.");
        }

        // ============================================================
        // PERFORMANCE MODS
        // ============================================================

        private async Task InstallPerformanceModsAsync(
            string minecraftVersion)
        {
            if (_currentProfile == null)
                throw new InvalidOperationException(
                    "No profile selected.");

            string modsFolder =
                GetProfileModsPath(
                    _currentProfile.Name);

            Directory.CreateDirectory(
                modsFolder);

            WriteLog(
                $"===== PERFORMANCE MOD INSTALL: {_currentProfile.Name} =====");

            foreach ((string slug, string name)
                     in PerformanceMods)
            {
                try
                {
                    StatusText.Text =
                        $"Installing {name}...";

                    bool installed =
                        await DownloadPerformanceModAsync(
                            slug,
                            name,
                            minecraftVersion);

                    if (installed)
                    {
                        WriteLog(
                            $"Installed {name} into {_currentProfile.Name}");
                    }
                }
                catch (Exception ex)
                {
                    WriteLog(
                        $"Optional mod failed: {name}");

                    WriteLog(
                        ex.Message);
                }
            }

            WriteLog(
                "===== PERFORMANCE MOD INSTALL COMPLETE =====");
        }

        private async Task<bool> DownloadPerformanceModAsync(
            string slug,
            string name,
            string minecraftVersion)
        {
            if (_currentProfile == null)
                return false;

            string url =
                "https://api.modrinth.com/v2/project/" +
                Uri.EscapeDataString(slug) +
                "/version" +
                "?loaders=%5B%22fabric%22%5D" +
                "&game_versions=%5B%22" +
                Uri.EscapeDataString(minecraftVersion) +
                "%22%5D";

            using HttpResponseMessage response =
                await Http.GetAsync(url);

            response.EnsureSuccessStatusCode();

            string json =
                await response.Content
                    .ReadAsStringAsync();

            using JsonDocument doc =
                JsonDocument.Parse(json);

            JsonElement versions =
                doc.RootElement;

            if (versions.ValueKind !=
                    JsonValueKind.Array ||
                versions.GetArrayLength() == 0)
            {
                WriteLog(
                    $"No compatible {name} for {minecraftVersion}");

                return false;
            }

            JsonElement? selectedFile = null;

            foreach (JsonElement version in
                     versions.EnumerateArray())
            {
                if (!version.TryGetProperty(
                        "files",
                        out JsonElement files))
                    continue;

                selectedFile =
                    FindPrimaryJar(files);

                if (selectedFile != null)
                    break;
            }

            if (selectedFile == null)
                return false;

            JsonElement file =
                selectedFile.Value;

            string downloadUrl =
                file.GetProperty("url")
                    .GetString()
                ?? throw new InvalidOperationException(
                    $"No download URL for {name}.");

            string filename =
                file.GetProperty("filename")
                    .GetString()
                ?? $"{slug}.jar";

            string destination =
                Path.Combine(
                    GetProfileModsPath(
                        _currentProfile.Name),
                    SanitizeFileName(filename));

            if (File.Exists(destination) &&
                new FileInfo(destination).Length > 0)
            {
                if (!_currentProfile.Mods.Contains(
                        filename,
                        StringComparer.OrdinalIgnoreCase))
                {
                    _currentProfile.Mods.Add(filename);
                    SaveProfile(_currentProfile);
                }

                return true;
            }

            await DownloadFileAsync(
                downloadUrl,
                destination);

            if (!File.Exists(destination) ||
                new FileInfo(destination).Length <= 0)
            {
                throw new IOException(
                    $"Mod file failed verification: {destination}");
            }

            if (!_currentProfile.Mods.Contains(
                    filename,
                    StringComparer.OrdinalIgnoreCase))
            {
                _currentProfile.Mods.Add(filename);
            }

            SaveProfile(_currentProfile);

            return true;
        }

        private static JsonElement? FindPrimaryJar(
            JsonElement files)
        {
            if (files.ValueKind !=
                JsonValueKind.Array)
                return null;

            JsonElement? fallback = null;

            foreach (JsonElement file in
                     files.EnumerateArray())
            {
                if (!file.TryGetProperty(
                        "filename",
                        out JsonElement filenameElement))
                    continue;

                string filename =
                    filenameElement.GetString() ?? "";

                if (!filename.EndsWith(
                        ".jar",
                        StringComparison.OrdinalIgnoreCase))
                    continue;

                bool primary =
                    file.TryGetProperty(
                        "primary",
                        out JsonElement primaryElement) &&
                    primaryElement.ValueKind ==
                        JsonValueKind.True;

                if (primary)
                    return file;

                fallback ??= file;
            }

            return fallback;
        }

        // ============================================================
        // FABRIC INSTALLATION
        // ============================================================

        private async Task<string> InstallFabricAsync(
            string minecraftVersion,
            MinecraftPath minecraftPath)
        {
            if (_currentProfile == null)
                throw new InvalidOperationException(
                    "No profile selected.");

            StatusText.Text =
                $"Installing Fabric for {_currentProfile.Name}...";

            WriteLog(
                "===== FABRIC INSTALLATION START =====");

            WriteLog(
                $"Profile: {_currentProfile.Name}");

            WriteLog(
                $"Minecraft: {minecraftVersion}");

            FabricInstaller fabricInstaller =
                new FabricInstaller(Http);

            string fabricVersionName =
                await fabricInstaller.Install(
                    minecraftVersion,
                    minecraftPath);

            if (string.IsNullOrWhiteSpace(
                    fabricVersionName))
            {
                throw new InvalidOperationException(
                    "Fabric installer returned an empty version name.");
            }

            WriteLog(
                $"Fabric installer returned: {fabricVersionName}");

            string fabricDirectory =
                Path.Combine(
                    minecraftPath.BasePath,
                    "versions",
                    fabricVersionName);

            Directory.CreateDirectory(
                fabricDirectory);

            string fabricJson =
                Path.Combine(
                    fabricDirectory,
                    fabricVersionName + ".json");

            if (!File.Exists(fabricJson))
            {
                throw new FileNotFoundException(
                    "Fabric installer did not create Fabric JSON.",
                    fabricJson);
            }

            await RepairFabricProfileAsync(
                fabricVersionName,
                fabricJson,
                minecraftPath);

            if (!ValidateFabricInstallation(
                    fabricVersionName,
                    minecraftPath))
            {
                throw new InvalidOperationException(
                    "Fabric installation is incomplete. Check topu-minecraft.log.");
            }

            WriteLog(
                "===== FABRIC INSTALLATION COMPLETE =====");

            return fabricVersionName;
        }

        // ============================================================
        // FABRIC PROFILE REPAIR
        // ============================================================

        private async Task RepairFabricProfileAsync(
            string fabricVersionName,
            string fabricJsonPath,
            MinecraftPath minecraftPath)
        {
            WriteLog(
                "===== FABRIC PROFILE CHECK =====");

            string json =
                await File.ReadAllTextAsync(
                    fabricJsonPath);

            using JsonDocument document =
                JsonDocument.Parse(json);

            JsonElement root =
                document.RootElement;

            if (!root.TryGetProperty(
                    "libraries",
                    out JsonElement libraries) ||
                libraries.ValueKind !=
                    JsonValueKind.Array)
            {
                throw new InvalidOperationException(
                    "Fabric profile has no libraries array.");
            }

            int checkedLibraries = 0;
            int downloadedLibraries = 0;

            foreach (JsonElement library in
                     libraries.EnumerateArray())
            {
                if (!library.TryGetProperty(
                        "name",
                        out JsonElement nameElement))
                    continue;

                string coordinate =
                    nameElement.GetString() ?? "";

                if (string.IsNullOrWhiteSpace(
                        coordinate))
                    continue;

                string[] parts =
                    coordinate.Split(':');

                if (parts.Length < 3)
                    continue;

                string group = parts[0];
                string artifact = parts[1];
                string version = parts[2];

                string relativePath =
                    group.Replace(
                        '.',
                        Path.DirectorySeparatorChar) +
                    Path.DirectorySeparatorChar +
                    artifact +
                    Path.DirectorySeparatorChar +
                    version +
                    Path.DirectorySeparatorChar +
                    artifact +
                    "-" +
                    version +
                    ".jar";

                string destination =
                    Path.Combine(
                        minecraftPath.BasePath,
                        "libraries",
                        relativePath);

                checkedLibraries++;

                if (File.Exists(destination) &&
                    new FileInfo(destination).Length > 0)
                {
                    continue;
                }

                string? url =
                    GetLibraryUrl(
                        library,
                        coordinate,
                        relativePath);

                if (string.IsNullOrWhiteSpace(url))
                {
                    WriteLog(
                        $"WARNING: Cannot determine URL for {coordinate}");

                    continue;
                }

                StatusText.Text =
                    $"Downloading {artifact}-{version}.jar";

                await DownloadFileAsync(
                    url,
                    destination);

                if (!File.Exists(destination) ||
                    new FileInfo(destination).Length <= 0)
                {
                    throw new IOException(
                        $"Fabric library failed: {destination}");
                }

                downloadedLibraries++;

                WriteLog(
                    $"Installed Fabric library: {destination}");
            }

            WriteLog(
                $"Fabric libraries checked: {checkedLibraries}");

            WriteLog(
                $"Fabric libraries downloaded: {downloadedLibraries}");

            string? loaderJar =
                await EnsureFabricLoaderAsync(
                    libraries,
                    minecraftPath);

            if (loaderJar == null)
            {
                throw new FileNotFoundException(
                    "Fabric Loader JAR could not be installed.");
            }

            WriteLog(
                $"Fabric Loader verified: {loaderJar}");

            string legacyFabricVersionJar =
                Path.Combine(
                    minecraftPath.BasePath,
                    "versions",
                    fabricVersionName,
                    fabricVersionName + ".jar");

            RemoveLegacyFabricVersionJar(
                legacyFabricVersionJar);
        }

        // ============================================================
        // FABRIC LOADER
        // ============================================================

        private async Task<string?> EnsureFabricLoaderAsync(
            JsonElement libraries,
            MinecraftPath minecraftPath)
        {
            foreach (JsonElement library in
                     libraries.EnumerateArray())
            {
                if (!library.TryGetProperty(
                        "name",
                        out JsonElement nameElement))
                    continue;

                string coordinate =
                    nameElement.GetString() ?? "";

                if (!coordinate.StartsWith(
                        "net.fabricmc:fabric-loader:",
                        StringComparison.OrdinalIgnoreCase))
                    continue;

                string[] parts =
                    coordinate.Split(':');

                if (parts.Length < 3)
                    continue;

                string version = parts[2];

                string relativePath =
                    Path.Combine(
                        "net",
                        "fabricmc",
                        "fabric-loader",
                        version,
                        $"fabric-loader-{version}.jar");

                string path =
                    Path.Combine(
                        minecraftPath.BasePath,
                        "libraries",
                        relativePath);

                if (File.Exists(path) &&
                    new FileInfo(path).Length > 0)
                {
                    if (JarContainsEntry(
                            path,
                            "net/fabricmc/loader/impl/launch/knot/KnotClient.class"))
                    {
                        return path;
                    }
                }

                string? url =
                    GetLibraryUrl(
                        library,
                        coordinate,
                        relativePath.Replace(
                            Path.DirectorySeparatorChar,
                            '/'));

                if (string.IsNullOrWhiteSpace(url))
                    continue;

                StatusText.Text =
                    "Downloading Fabric Loader...";

                await DownloadFileAsync(
                    url,
                    path);

                if (!JarContainsEntry(
                        path,
                        "net/fabricmc/loader/impl/launch/knot/KnotClient.class"))
                {
                    throw new InvalidDataException(
                        "Downloaded Fabric Loader does not contain KnotClient.");
                }

                return path;
            }

            string loaderRoot =
                Path.Combine(
                    minecraftPath.BasePath,
                    "libraries",
                    "net",
                    "fabricmc",
                    "fabric-loader");

            if (Directory.Exists(loaderRoot))
            {
                string[] jars =
                    Directory.GetFiles(
                        loaderRoot,
                        "fabric-loader-*.jar",
                        SearchOption.AllDirectories);

                foreach (string jar in jars)
                {
                    try
                    {
                        if (File.Exists(jar) &&
                            new FileInfo(jar).Length > 0 &&
                            JarContainsEntry(
                                jar,
                                "net/fabricmc/loader/impl/launch/knot/KnotClient.class"))
                        {
                            return jar;
                        }
                    }
                    catch
                    {
                    }
                }
            }

            return null;
        }

        private static bool JarContainsEntry(
            string jarPath,
            string entryName)
        {
            try
            {
                using FileStream stream =
                    new FileStream(
                        jarPath,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read);

                using ZipArchive archive =
                    new ZipArchive(
                        stream,
                        ZipArchiveMode.Read,
                        false);

                foreach (ZipArchiveEntry entry in
                         archive.Entries)
                {
                    if (string.Equals(
                            entry.FullName,
                            entryName,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        // ============================================================
        // LIBRARY URL
        // ============================================================

        private string? GetLibraryUrl(
            JsonElement library,
            string coordinate,
            string relativePath)
        {
            try
            {
                if (library.TryGetProperty(
                        "downloads",
                        out JsonElement downloads) &&
                    downloads.ValueKind ==
                        JsonValueKind.Object &&
                    downloads.TryGetProperty(
                        "artifact",
                        out JsonElement artifact) &&
                    artifact.ValueKind ==
                        JsonValueKind.Object &&
                    artifact.TryGetProperty(
                        "url",
                        out JsonElement artifactUrl))
                {
                    string? url =
                        artifactUrl.GetString();

                    if (!string.IsNullOrWhiteSpace(url))
                        return url;
                }

                if (library.TryGetProperty(
                        "url",
                        out JsonElement urlElement))
                {
                    string? baseUrl =
                        urlElement.GetString();

                    if (!string.IsNullOrWhiteSpace(baseUrl))
                    {
                        return baseUrl.TrimEnd('/') +
                               "/" +
                               relativePath.Replace(
                                   '\\',
                                   '/');
                    }
                }

                if (coordinate.StartsWith(
                        "net.fabricmc:",
                        StringComparison.OrdinalIgnoreCase) ||
                    coordinate.StartsWith(
                        "org.ow2.asm:",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return
                        "https://maven.fabricmc.net/" +
                        relativePath.Replace(
                            '\\',
                            '/');
                }

                return
                    "https://libraries.minecraft.net/" +
                    relativePath.Replace(
                        '\\',
                        '/');
            }
            catch
            {
                return null;
            }
        }

        // ============================================================
        // FABRIC VALIDATION
        // ============================================================

        private bool ValidateFabricInstallation(
            string fabricVersionName,
            MinecraftPath minecraftPath)
        {
            try
            {
                string fabricDirectory =
                    Path.Combine(
                        minecraftPath.BasePath,
                        "versions",
                        fabricVersionName);

                string fabricJson =
                    Path.Combine(
                        fabricDirectory,
                        fabricVersionName + ".json");

                string legacyFabricJar =
                    Path.Combine(
                        fabricDirectory,
                        fabricVersionName + ".jar");

                RemoveLegacyFabricVersionJar(
                    legacyFabricJar);

                if (!Directory.Exists(
                        fabricDirectory))
                    return false;

                if (!File.Exists(
                        fabricJson))
                    return false;

                using JsonDocument document =
                    JsonDocument.Parse(
                        File.ReadAllText(
                            fabricJson));

                if (document.RootElement.ValueKind !=
                    JsonValueKind.Object)
                    return false;

                string loaderRoot =
                    Path.Combine(
                        minecraftPath.BasePath,
                        "libraries",
                        "net",
                        "fabricmc",
                        "fabric-loader");

                if (!Directory.Exists(loaderRoot))
                    return false;

                string[] loaderJars =
                    Directory.GetFiles(
                        loaderRoot,
                        "fabric-loader-*.jar",
                        SearchOption.AllDirectories);

                if (loaderJars.Length == 0)
                    return false;

                bool validLoader = false;

                foreach (string jar in loaderJars)
                {
                    if (JarContainsEntry(
                            jar,
                            "net/fabricmc/loader/impl/launch/knot/KnotClient.class"))
                    {
                        validLoader = true;
                    }
                }

                WriteLog(
                    $"Fabric validation: {validLoader}");

                return validLoader;
            }
            catch (Exception ex)
            {
                WriteException(
                    "FABRIC VALIDATION ERROR",
                    ex);

                return false;
            }
        }

        // ============================================================
        // MINECRAFT INSTALLATION VALIDATION
        // ============================================================

        private bool ValidateMinecraftInstallation(
            string minecraftVersion,
            MinecraftPath minecraftPath,
            string fabricVersion)
        {
            try
            {
                string vanillaDirectory =
                    Path.Combine(
                        minecraftPath.BasePath,
                        "versions",
                        minecraftVersion);

                string fabricDirectory =
                    Path.Combine(
                        minecraftPath.BasePath,
                        "versions",
                        fabricVersion);

                string vanillaJson =
                    Path.Combine(
                        vanillaDirectory,
                        minecraftVersion + ".json");

                string vanillaJar =
                    Path.Combine(
                        vanillaDirectory,
                        minecraftVersion + ".jar");

                string fabricJson =
                    Path.Combine(
                        fabricDirectory,
                        fabricVersion + ".json");

                if (!File.Exists(vanillaJson))
                    return false;

                if (!File.Exists(vanillaJar))
                    return false;

                if (new FileInfo(vanillaJar).Length <= 0)
                    return false;

                if (!File.Exists(fabricJson))
                    return false;

                if (!ValidateFabricInstallation(
                        fabricVersion,
                        minecraftPath))
                {
                    return false;
                }

                WriteLog(
                    "Minecraft/Fabric installation validation passed.");

                return true;
            }
            catch (Exception ex)
            {
                WriteException(
                    "INSTALLATION VALIDATION ERROR",
                    ex);

                return false;
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
                }

                _minecraftProcess = null;
            }

            if (_currentProfile == null)
            {
                MessageBox.Show(
                    "Select a profile before launching.",
                    "Topu Client",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            LaunchBtn.IsEnabled = false;

            try
            {
                StartLaunchLog();

                ProfileData profile =
                    _currentProfile;

                string minecraftVersion =
                    profile.MinecraftVersion;

                int ram =
                    Math.Max(
                        1024,
                        profile.RamGb * 1024);

                string profilePath =
                    GetProfileDirectory(
                        profile.Name);

                WriteLog(
                    "==========================================");

                WriteLog(
                    "TOPU CLIENT MULTI-PROFILE LAUNCH");

                WriteLog(
                    $"Profile: {profile.Name}");

                WriteLog(
                    $"Minecraft: {minecraftVersion}");

                WriteLog(
                    $"RAM: {ram} MB");

                WriteLog(
                    $"Profile directory: {profilePath}");

                WriteLog(
                    "==========================================");

                // ----------------------------------------------------
                // AUTH
                // ----------------------------------------------------

                if (AuthTypeBox.SelectedIndex != 0)
                {
                    throw new InvalidOperationException(
                        "Microsoft login is not enabled yet. Select Offline mode.");
                }

                string username =
                    profile.Username.Trim();

                if (string.IsNullOrWhiteSpace(username))
                    username = "TopuPlayer";

                _session =
                    MSession.CreateOfflineSession(
                        username);

                SaveUsername(username);

                profile.Username =
                    username;

                // ----------------------------------------------------
                // JAVA
                // ----------------------------------------------------

                int javaMajor =
                    GetRequiredJavaMajor(
                        minecraftVersion);

                WriteLog(
                    $"Required Java: {javaMajor}");

                string javaPath =
                    await EnsureJavaAsync(
                        javaMajor);

                WriteLog(
                    $"Java path: {javaPath}");

                // ----------------------------------------------------
                // PROFILE GAME DIRECTORY
                // ----------------------------------------------------

                Directory.CreateDirectory(
                    profilePath);

                Directory.CreateDirectory(
                    GetProfileModsPath(
                        profile.Name));

                Directory.CreateDirectory(
                    GetProfileVersionsPath(
                        profile.Name));

                Directory.CreateDirectory(
                    GetProfileLibrariesPath(
                        profile.Name));

                // IMPORTANT:
                // CmlLib now works INSIDE THE PROFILE DIRECTORY.
                MinecraftPath minecraftPath =
                    new MinecraftPath(
                        profilePath);

                MinecraftLauncher launcher =
                    new MinecraftLauncher(
                        minecraftPath);

                // ----------------------------------------------------
                // VANILLA
                // ----------------------------------------------------

                StatusText.Text =
                    $"Installing Minecraft {minecraftVersion}...";

                Progress<InstallerProgressChangedEventArgs>
                    fileProgress =
                        new Progress<InstallerProgressChangedEventArgs>(
                            args =>
                            {
                                try
                                {
                                    StatusText.Text =
                                        $"Downloading {args.Name} " +
                                        $"({args.ProgressedTasks}/{args.TotalTasks})";
                                }
                                catch
                                {
                                }
                            });

                Progress<ByteProgress>
                    byteProgress =
                        new Progress<ByteProgress>(
                            args =>
                            {
                                try
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
                                }
                                catch
                                {
                                }
                            });

                await launcher.InstallAsync(
                    minecraftVersion,
                    fileProgress,
                    byteProgress,
                    CancellationToken.None);

                WriteLog(
                    "Vanilla Minecraft installation complete.");

                // ----------------------------------------------------
                // FABRIC
                // ----------------------------------------------------

                string fabricVersion =
                    await InstallFabricAsync(
                        minecraftVersion,
                        minecraftPath);

                WriteLog(
                    $"Fabric profile: {fabricVersion}");

                // ----------------------------------------------------
                // VALIDATE
                // ----------------------------------------------------

                if (!ValidateMinecraftInstallation(
                        minecraftVersion,
                        minecraftPath,
                        fabricVersion))
                {
                    throw new InvalidOperationException(
                        "Minecraft/Fabric installation validation failed.");
                }

                // ----------------------------------------------------
                // PERFORMANCE MODS
                // ----------------------------------------------------

                StatusText.Text =
                    "Installing performance mods...";

                await InstallPerformanceModsAsync(
                    minecraftVersion);

                // ----------------------------------------------------
                // PROFILE LAST PLAYED
                // ----------------------------------------------------

                profile.LastPlayed =
                    DateTime.Now;

                SaveProfile(profile);

                // ----------------------------------------------------
                // OPTIONS
                // ----------------------------------------------------

                MLaunchOption options =
                    new MLaunchOption
                    {
                        Session = _session,
                        MaximumRamMb = ram,
                        MinimumRamMb =
                            Math.Min(
                                1024,
                                ram),
                        JavaPath = javaPath,
                        GameLauncherName = "Topu Client",
                        GameLauncherVersion = "2.0.0"
                    };

                // ----------------------------------------------------
                // REMOVE LEGACY FABRIC DUPLICATE
                // ----------------------------------------------------

                RemoveLegacyFabricVersionJar(
                    Path.Combine(
                        profilePath,
                        "versions",
                        fabricVersion,
                        fabricVersion + ".jar"));

                // ----------------------------------------------------
                // BUILD
                // ----------------------------------------------------

                StatusText.Text =
                    "Building Minecraft process...";

                WriteLog(
                    "Calling CmlLib BuildProcessAsync.");

                Process process =
                    await launcher.BuildProcessAsync(
                        fabricVersion,
                        options,
                        CancellationToken.None);

                if (process == null)
                {
                    throw new InvalidOperationException(
                        "CmlLib returned a null Minecraft process.");
                }

                // ----------------------------------------------------
                // NORMALIZE FABRIC
                // ----------------------------------------------------

                NormalizeFabricProcessArguments(
                    process,
                    minecraftVersion,
                    fabricVersion,
                    profilePath);

                ValidateFinalFabricCommand(
                    process,
                    minecraftVersion,
                    fabricVersion,
                    profilePath);

                process.StartInfo.RedirectStandardOutput =
                    true;

                process.StartInfo.RedirectStandardError =
                    true;

                process.StartInfo.UseShellExecute =
                    false;

                process.StartInfo.CreateNoWindow =
                    true;

                process.OutputDataReceived +=
                    Minecraft_OutputDataReceived;

                process.ErrorDataReceived +=
                    Minecraft_ErrorDataReceived;

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
                    ram,
                    profile);

                // ----------------------------------------------------
                // START
                // ----------------------------------------------------

                StatusText.Text =
                    $"Starting {profile.Name}...";

                bool started =
                    process.Start();

                if (!started)
                {
                    throw new InvalidOperationException(
                        "Windows failed to start Minecraft.");
                }

                _minecraftProcess =
                    process;

                WriteLog(
                    $"Minecraft started successfully. PID={process.Id}");

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                StatusText.Text =
                    $"{profile.Name} running as {username}";

                _ = MonitorMinecraftAsync(
                    process);
            }
            catch (Exception ex)
            {
                StatusText.Text =
                    "Launch failed.";

                WriteException(
                    "TOPU MULTI-PROFILE LAUNCH ERROR",
                    ex);

                MessageBox.Show(
                    "Minecraft failed to launch.\n\n" +
                    ex.Message +
                    "\n\nLog:\n" +
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
        // FABRIC ARGUMENT NORMALIZATION
        // ============================================================

        private void NormalizeFabricProcessArguments(
            Process process,
            string minecraftVersion,
            string fabricVersion,
            string profilePath)
        {
            WriteLog(
                "===== FABRIC PROCESS NORMALIZATION =====");

            string original =
                process.StartInfo.Arguments;

            List<string> tokens =
                TokenizeWindowsCommandLine(
                    original);

            if (tokens.Count == 0)
            {
                throw new InvalidOperationException(
                    "CmlLib generated an empty Java command line.");
            }

            int cpIndex =
                FindArgumentIndex(
                    tokens,
                    "-cp",
                    "--class-path",
                    "-classpath");

            if (cpIndex < 0)
            {
                throw new InvalidOperationException(
                    "Could not find -cp in generated arguments.");
            }

            if (cpIndex + 1 >= tokens.Count)
            {
                throw new InvalidOperationException(
                    "Generated -cp has no classpath.");
            }

            string classpathToken =
                tokens[cpIndex + 1];

            List<string> classpathEntries =
                classpathToken
                    .Split(
                        ';',
                        StringSplitOptions.RemoveEmptyEntries)
                    .Select(
                        p => p.Trim().Trim('"'))
                    .Where(
                        p => !string.IsNullOrWhiteSpace(p))
                    .ToList();

            string vanillaJar =
                Path.GetFullPath(
                    Path.Combine(
                        profilePath,
                        "versions",
                        minecraftVersion,
                        minecraftVersion + ".jar"));

            string fabricProfileJar =
                Path.GetFullPath(
                    Path.Combine(
                        profilePath,
                        "versions",
                        fabricVersion,
                        fabricVersion + ".jar"));

            if (!File.Exists(vanillaJar))
            {
                throw new FileNotFoundException(
                    "Minecraft JAR is missing.",
                    vanillaJar);
            }

            string vanillaNormalized =
                NormalizePath(vanillaJar);

            string fabricNormalized =
                NormalizePath(
                    fabricProfileJar);

            List<string> rebuilt =
                new List<string>();

            bool vanillaPresent = false;

            foreach (string entry in
                     classpathEntries)
            {
                string normalized =
                    NormalizePath(entry);

                // NEVER put the Fabric profile JAR into
                // the classpath.
                if (string.Equals(
                        normalized,
                        fabricNormalized,
                        StringComparison.OrdinalIgnoreCase))
                {
                    WriteLog(
                        $"REMOVED Fabric profile duplicate: {entry}");

                    continue;
                }

                if (string.Equals(
                        normalized,
                        vanillaNormalized,
                        StringComparison.OrdinalIgnoreCase))
                {
                    vanillaPresent = true;
                }

                rebuilt.Add(entry);
            }

            if (!vanillaPresent)
            {
                rebuilt.Add(
                    vanillaNormalized);

                WriteLog(
                    $"ADDED Minecraft JAR: {vanillaNormalized}");
            }

            tokens[cpIndex + 1] =
                string.Join(
                    ";",
                    rebuilt);

            // Remove malformed FabricMcEmu argument.
            for (int i = tokens.Count - 1;
                 i >= 0;
                 i--)
            {
                if (tokens[i].StartsWith(
                        "-DFabricMcEmu=",
                        StringComparison.OrdinalIgnoreCase))
                {
                    WriteLog(
                        $"REMOVED malformed FabricMcEmu: {tokens[i]}");

                    tokens.RemoveAt(i);
                }
            }

            bool knotFound =
                tokens.Any(
                    t => string.Equals(
                        t,
                        "net.fabricmc.loader.impl.launch.knot.KnotClient",
                        StringComparison.Ordinal));

            if (!knotFound)
            {
                throw new InvalidOperationException(
                    "KnotClient main class is missing.");
            }

            process.StartInfo.Arguments =
                BuildWindowsCommandLine(
                    tokens);

            WriteLog(
                "Fabric process arguments rebuilt.");

            WriteLog(
                "===== FABRIC PROCESS NORMALIZATION COMPLETE =====");
        }

        // ============================================================
        // FINAL FABRIC COMMAND VALIDATION
        // ============================================================

        private void ValidateFinalFabricCommand(
            Process process,
            string minecraftVersion,
            string fabricVersion,
            string profilePath)
        {
            WriteLog(
                "===== FINAL FABRIC COMMAND CHECK =====");

            List<string> tokens =
                TokenizeWindowsCommandLine(
                    process.StartInfo.Arguments);

            int cpIndex =
                FindArgumentIndex(
                    tokens,
                    "-cp",
                    "--class-path",
                    "-classpath");

            if (cpIndex < 0 ||
                cpIndex + 1 >= tokens.Count)
            {
                throw new InvalidOperationException(
                    "Final command has no valid classpath.");
            }

            List<string> classpath =
                tokens[cpIndex + 1]
                    .Split(
                        ';',
                        StringSplitOptions.RemoveEmptyEntries)
                    .Select(
                        NormalizePath)
                    .ToList();

            string vanillaJar =
                NormalizePath(
                    Path.Combine(
                        profilePath,
                        "versions",
                        minecraftVersion,
                        minecraftVersion + ".jar"));

            string fabricJar =
                NormalizePath(
                    Path.Combine(
                        profilePath,
                        "versions",
                        fabricVersion,
                        fabricVersion + ".jar"));

            bool vanillaPresent =
                classpath.Any(
                    p => string.Equals(
                        p,
                        vanillaJar,
                        StringComparison.OrdinalIgnoreCase));

            bool fabricPresent =
                classpath.Any(
                    p => string.Equals(
                        p,
                        fabricJar,
                        StringComparison.OrdinalIgnoreCase));

            int loaderCopies =
                classpath.Count(
                    p =>
                        p.Contains(
                            "\\libraries\\net\\fabricmc\\fabric-loader\\",
                            StringComparison.OrdinalIgnoreCase) &&
                        p.EndsWith(
                            ".jar",
                            StringComparison.OrdinalIgnoreCase));

            bool knotPresent =
                tokens.Any(
                    t => string.Equals(
                        t,
                        "net.fabricmc.loader.impl.launch.knot.KnotClient",
                        StringComparison.Ordinal));

            WriteLog(
                $"Minecraft JAR present: {vanillaPresent}");

            WriteLog(
                $"Fabric profile JAR present: {fabricPresent}");

            WriteLog(
                $"Fabric Loader copies: {loaderCopies}");

            WriteLog(
                $"KnotClient present: {knotPresent}");

            if (!vanillaPresent)
            {
                throw new InvalidOperationException(
                    "FINAL COMMAND INVALID: Minecraft JAR missing.");
            }

            if (fabricPresent)
            {
                throw new InvalidOperationException(
                    "FINAL COMMAND INVALID: Fabric profile JAR remains in classpath.");
            }

            if (loaderCopies != 1)
            {
                throw new InvalidOperationException(
                    $"FINAL COMMAND INVALID: expected exactly one Fabric Loader, found {loaderCopies}.");
            }

            if (!knotPresent)
            {
                throw new InvalidOperationException(
                    "FINAL COMMAND INVALID: KnotClient missing.");
            }

            WriteLog(
                "FINAL FABRIC COMMAND CHECK PASSED.");
        }

        // ============================================================
        // COMMAND LINE PARSER
        // ============================================================

        private static int FindArgumentIndex(
            List<string> arguments,
            params string[] names)
        {
            for (int i = 0;
                 i < arguments.Count;
                 i++)
            {
                foreach (string name in names)
                {
                    if (string.Equals(
                            arguments[i],
                            name,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return i;
                    }
                }
            }

            return -1;
        }

        private static List<string>
            TokenizeWindowsCommandLine(
                string commandLine)
        {
            List<string> result =
                new List<string>();

            StringBuilder current =
                new StringBuilder();

            bool inQuotes = false;
            int backslashes = 0;

            for (int i = 0;
                 i < commandLine.Length;
                 i++)
            {
                char c =
                    commandLine[i];

                if (c == '\\')
                {
                    backslashes++;
                    continue;
                }

                if (c == '"')
                {
                    if (backslashes > 0)
                    {
                        int pairs =
                            backslashes / 2;

                        current.Append(
                            new string(
                                '\\',
                                pairs));

                        if ((backslashes % 2) == 1)
                        {
                            current.Append('"');
                        }
                        else
                        {
                            inQuotes =
                                !inQuotes;
                        }

                        backslashes = 0;
                        continue;
                    }

                    inQuotes =
                        !inQuotes;

                    continue;
                }

                if (backslashes > 0)
                {
                    current.Append(
                        new string(
                            '\\',
                            backslashes));

                    backslashes = 0;
                }

                if (char.IsWhiteSpace(c) &&
                    !inQuotes)
                {
                    if (current.Length > 0)
                    {
                        result.Add(
                            current.ToString());

                        current.Clear();
                    }

                    continue;
                }

                current.Append(c);
            }

            if (backslashes > 0)
            {
                current.Append(
                    new string(
                        '\\',
                        backslashes));
            }

            if (current.Length > 0)
            {
                result.Add(
                    current.ToString());
            }

            return result;
        }

        private static string BuildWindowsCommandLine(
            IEnumerable<string> arguments)
        {
            return string.Join(
                " ",
                arguments.Select(
                    QuoteWindowsArgument));
        }

        private static string QuoteWindowsArgument(
            string argument)
        {
            if (argument.Length == 0)
                return "\"\"";

            if (!argument.Any(
                    char.IsWhiteSpace) &&
                !argument.Contains('"'))
            {
                return argument;
            }

            StringBuilder builder =
                new StringBuilder();

            builder.Append('"');

            int backslashes = 0;

            foreach (char c in argument)
            {
                if (c == '\\')
                {
                    backslashes++;
                    continue;
                }

                if (c == '"')
                {
                    builder.Append(
                        new string(
                            '\\',
                            backslashes * 2 + 1));

                    builder.Append('"');
                    backslashes = 0;
                    continue;
                }

                if (backslashes > 0)
                {
                    builder.Append(
                        new string(
                            '\\',
                            backslashes));

                    backslashes = 0;
                }

                builder.Append(c);
            }

            if (backslashes > 0)
            {
                builder.Append(
                    new string(
                        '\\',
                        backslashes * 2));
            }

            builder.Append('"');

            return builder.ToString();
        }

        private static string NormalizePath(
            string path)
        {
            string cleaned =
                path.Trim().Trim('"');

            try
            {
                return Path.GetFullPath(
                        cleaned)
                    .Replace(
                        '/',
                        '\\')
                    .TrimEnd('\\');
            }
            catch
            {
                return cleaned
                    .Replace(
                        '/',
                        '\\')
                    .TrimEnd('\\');
            }
        }

        // ============================================================
        // DOWNLOADS
        // ============================================================

        private async Task DownloadFileAsync(
            string url,
            string destination)
        {
            string? directory =
                Path.GetDirectoryName(
                    destination);

            if (!string.IsNullOrWhiteSpace(
                    directory))
            {
                Directory.CreateDirectory(
                    directory);
            }

            string temporary =
                Path.Combine(
                    directory ?? _gamePath,
                    "." +
                    Path.GetFileName(destination) +
                    "." +
                    Guid.NewGuid()
                        .ToString("N") +
                    ".download");

            try
            {
                WriteLog(
                    $"Downloading: {url}");

                using HttpResponseMessage response =
                    await Http.GetAsync(
                        url,
                        HttpCompletionOption.ResponseHeadersRead);

                response.EnsureSuccessStatusCode();

                using Stream input =
                    await response.Content
                        .ReadAsStreamAsync();

                using FileStream output =
                    new FileStream(
                        temporary,
                        FileMode.Create,
                        FileAccess.Write,
                        FileShare.Read,
                        81920,
                        FileOptions.SequentialScan);

                await input.CopyToAsync(
                    output,
                    81920,
                    CancellationToken.None);

                await output.FlushAsync(
                    CancellationToken.None);

                if (!File.Exists(
                        temporary))
                {
                    throw new IOException(
                        "Temporary download file was not created.");
                }

                long size =
                    new FileInfo(
                        temporary).Length;

                if (size <= 0)
                {
                    throw new IOException(
                        "Downloaded file is empty.");
                }

                await Task.Delay(150);

                await MoveFileWithRetryAsync(
                    temporary,
                    destination);

                if (!File.Exists(
                        destination))
                {
                    throw new IOException(
                        "Final downloaded file does not exist.");
                }

                long finalSize =
                    new FileInfo(
                        destination).Length;

                if (finalSize <= 0)
                {
                    throw new IOException(
                        "Final downloaded file is empty.");
                }

                WriteLog(
                    $"Download complete: {destination} ({finalSize:N0} bytes)");
            }
            catch
            {
                TryDeleteFileWithRetry(
                    temporary);

                throw;
            }
            finally
            {
                TryDeleteFileWithRetry(
                    temporary);
            }
        }

        private async Task MoveFileWithRetryAsync(
            string source,
            string destination)
        {
            const int attempts = 20;

            Exception? lastException = null;

            for (int attempt = 1;
                 attempt <= attempts;
                 attempt++)
            {
                try
                {
                    if (File.Exists(
                            destination))
                    {
                        try
                        {
                            if (new FileInfo(
                                    destination).Length > 0)
                            {
                                TryDeleteFileWithRetry(
                                    source);

                                return;
                            }
                        }
                        catch
                        {
                        }

                        TryDeleteFile(
                            destination);
                    }

                    File.Move(
                        source,
                        destination);

                    return;
                }
                catch (IOException ex)
                {
                    lastException = ex;

                    if (attempt == attempts)
                        break;

                    await Task.Delay(
                        300 + attempt * 150);
                }
                catch (UnauthorizedAccessException ex)
                {
                    lastException = ex;

                    if (attempt == attempts)
                        break;

                    await Task.Delay(
                        300 + attempt * 150);
                }
            }

            throw new IOException(
                $"Could not move downloaded file to '{destination}'.",
                lastException);
        }

        // ============================================================
        // LEGACY FABRIC DUPLICATE
        // ============================================================

        private void RemoveLegacyFabricVersionJar(
            string legacyJarPath)
        {
            try
            {
                if (!File.Exists(
                        legacyJarPath))
                    return;

                WriteLog(
                    $"Removing legacy Fabric duplicate: {legacyJarPath}");

                TryDeleteFileWithRetry(
                    legacyJarPath);

                if (File.Exists(
                        legacyJarPath))
                {
                    throw new IOException(
                        $"Could not remove Fabric duplicate: {legacyJarPath}");
                }
            }
            catch (Exception ex)
            {
                WriteException(
                    "LEGACY FABRIC DUPLICATE CLEANUP ERROR",
                    ex);

                throw;
            }
        }

        // ============================================================
        // PROCESS OUTPUT
        // ============================================================

        private void Minecraft_OutputDataReceived(
            object sender,
            DataReceivedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(
                    e.Data))
                return;

            AppendRawGameOutput(
                "[MC]",
                e.Data);
        }

        private void Minecraft_ErrorDataReceived(
            object sender,
            DataReceivedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(
                    e.Data))
                return;

            AppendRawGameOutput(
                "[MC-ERR]",
                e.Data);
        }

        // ============================================================
        // PROCESS MONITOR
        // ============================================================

        private async Task MonitorMinecraftAsync(
            Process process)
        {
            try
            {
                await Task.Run(
                    () =>
                    {
                        try
                        {
                            process.WaitForExit();
                        }
                        catch
                        {
                        }
                    });

                await Task.Delay(500);

                int exitCode = 0;

                try
                {
                    exitCode =
                        process.ExitCode;
                }
                catch
                {
                }

                AppendGameLog(
                    $"===== MINECRAFT EXITED: {exitCode} =====");

                if (exitCode == 0)
                {
                    AppendGameLog(
                        "Minecraft exited normally.");
                }
                else
                {
                    AppendGameLog(
                        "Minecraft did not exit normally.");
                }

                await Dispatcher.InvokeAsync(
                    () =>
                    {
                        if (StatusText != null)
                        {
                            StatusText.Text =
                                exitCode == 0
                                    ? "Minecraft closed normally."
                                    : $"Minecraft crashed (exit code {exitCode}).";
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
                    process.OutputDataReceived -=
                        Minecraft_OutputDataReceived;

                    process.ErrorDataReceived -=
                        Minecraft_ErrorDataReceived;
                }
                catch
                {
                }

                _minecraftProcess =
                    null;
            }
        }

        // ============================================================
        // LOGGING
        // ============================================================

        private void StartLaunchLog()
        {
            try
            {
                Directory.CreateDirectory(
                    _gamePath);

                lock (_logLock)
                {
                    File.WriteAllText(
                        _logPath,
                        "===== TOPU CLIENT MINECRAFT LOG =====" +
                        Environment.NewLine +
                        $"Started: {DateTime.Now:O}" +
                        Environment.NewLine +
                        Environment.NewLine);
                }
            }
            catch
            {
            }
        }

        private void WriteLog(
            string message)
        {
            try
            {
                Directory.CreateDirectory(
                    _gamePath);

                lock (_logLock)
                {
                    File.AppendAllText(
                        _logPath,
                        $"[{DateTime.Now:HH:mm:ss}] {message}" +
                        Environment.NewLine);
                }
            }
            catch
            {
            }
        }

        private void AppendGameLog(
            string message)
        {
            WriteLog(message);
        }

        private void AppendRawGameOutput(
            string prefix,
            string? text)
        {
            if (string.IsNullOrWhiteSpace(
                    text))
                return;

            try
            {
                lock (_logLock)
                {
                    File.AppendAllText(
                        _logPath,
                        $"[{DateTime.Now:HH:mm:ss}] {prefix} {text}" +
                        Environment.NewLine);
                }
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
            WriteLog(
                $"===== {title} =====");
            WriteLog(
                ex.ToString());
        }

        // ============================================================
        // DEBUG FILE
        // ============================================================

        private void WriteDebugFile(
            Process process,
            string javaPath,
            string minecraftVersion,
            string fabricVersion,
            int ram,
            ProfileData profile)
        {
            try
            {
                string path =
                    Path.Combine(
                        _gamePath,
                        "topu-launch-debug.txt");

                StringBuilder text =
                    new StringBuilder();

                text.AppendLine(
                    "===== TOPU CLIENT MULTI-PROFILE DEBUG =====");

                text.AppendLine(
                    $"Time: {DateTime.Now:O}");

                text.AppendLine();

                text.AppendLine(
                    $"Profile: {profile.Name}");

                text.AppendLine(
                    $"Profile Directory: {GetProfileDirectory(profile.Name)}");

                text.AppendLine(
                    $"Minecraft: {minecraftVersion}");

                text.AppendLine(
                    $"Fabric: {fabricVersion}");

                text.AppendLine(
                    $"Java: {javaPath}");

                text.AppendLine(
                    $"RAM: {ram} MB");

                text.AppendLine();

                text.AppendLine(
                    "Executable:");

                text.AppendLine(
                    process.StartInfo.FileName);

                text.AppendLine();

                text.AppendLine(
                    "Arguments:");

                text.AppendLine(
                    process.StartInfo.Arguments);

                text.AppendLine();

                text.AppendLine(
                    "Working Directory:");

                text.AppendLine(
                    process.StartInfo.WorkingDirectory);

                File.WriteAllText(
                    path,
                    text.ToString());
            }
            catch (Exception ex)
            {
                WriteException(
                    "DEBUG FILE ERROR",
                    ex);
            }
        }

        // ============================================================
        // PROFILE INFO HELPERS
        // ============================================================

        public ProfileData? GetCurrentProfile()
        {
            return _currentProfile;
        }

        public IReadOnlyList<ProfileData>
            GetProfilesForUi()
        {
            return GetAllProfiles();
        }

        public string GetCurrentProfileDirectory()
        {
            if (_currentProfile == null)
                return "";

            return GetProfileDirectory(
                _currentProfile.Name);
        }

        public string GetCurrentProfileModsDirectory()
        {
            if (_currentProfile == null)
                return "";

            return GetProfileModsPath(
                _currentProfile.Name);
        }

        // ============================================================
        // UTILITY
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

        private static void TryDeleteFile(
            string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
            }
        }

        private static void TryDeleteFileWithRetry(
            string path)
        {
            if (!File.Exists(path))
                return;

            for (int i = 0; i < 8; i++)
            {
                try
                {
                    if (!File.Exists(path))
                        return;

                    File.Delete(path);
                    return;
                }
                catch
                {
                    Thread.Sleep(150);
                }
            }
        }

        private static void TryDeleteDirectory(
            string path)
        {
            try
            {
                if (Directory.Exists(path))
                    Directory.Delete(
                        path,
                        true);
            }
            catch
            {
            }
        }
    }
}
