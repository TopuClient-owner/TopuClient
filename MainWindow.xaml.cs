using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
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
        private static readonly HttpClient Http = CreateHttpClient();

        private MSession? _session;
        private Process? _minecraftProcess;

        private readonly string _gamePath;
        private readonly string _configPath;
        private readonly string _logPath;
        private readonly string _profilesPath;

        private readonly object _logLock = new object();

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
            ("fabric-api", "Fabric API"),
            ("sodium", "Sodium"),
            ("lithium", "Lithium"),
            ("dynamic-fps", "Dynamic FPS"),
            ("sodium-extra", "Sodium Extra"),
            ("krypton", "Krypton")
        };

        private readonly List<LauncherProfile> _profiles =
            new List<LauncherProfile>();

        private LauncherProfile? _activeProfile;

        // ============================================================
        // PROFILE MODEL
        // ============================================================

        private sealed class LauncherProfile
        {
            public string Name { get; set; } = "Default";
            public string Username { get; set; } = "TopuPlayer";
            public string Version { get; set; } = DefaultVersion;
            public int RamGb { get; set; } = 4;
            public bool PerformanceMods { get; set; } = true;

            public override string ToString()
            {
                return Name;
            }
        }

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

            _configPath = Path.Combine(
                _gamePath,
                "username.txt");

            _logPath = Path.Combine(
                _gamePath,
                "topu-minecraft.log");

            _profilesPath = Path.Combine(
                _gamePath,
                "profiles.json");

            Directory.CreateDirectory(_gamePath);

            LoadUsername();

            InitializeProfiles();

            if (RamLabel != null)
            {
                RamLabel.Text =
                    $"{(int)RamSlider.Value}GB";
            }

            WriteLog("Topu Client initialized.");
            WriteLog($"Game directory: {_gamePath}");
        }

        private static HttpClient CreateHttpClient()
        {
            HttpClient client =
                new HttpClient(
                    new HttpClientHandler
                    {
                        AllowAutoRedirect = true
                    });

            client.Timeout =
                TimeSpan.FromMinutes(30);

            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "TopuClient/1.0");

            return client;
        }

        // ============================================================
        // LOGGING
        // ============================================================

        private void WriteLog(string message)
        {
            try
            {
                Directory.CreateDirectory(_gamePath);

                lock (_logLock)
                {
                    File.AppendAllText(
                        _logPath,
                        $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
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
            WriteLog($"===== {title} =====");
            WriteLog(ex.ToString());
        }

        private void StartLaunchLog()
        {
            try
            {
                Directory.CreateDirectory(_gamePath);

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

        private void AppendGameLog(string message)
        {
            WriteLog(message);
        }

        private void AppendRawGameOutput(
            string prefix,
            string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            try
            {
                lock (_logLock)
                {
                    File.AppendAllText(
                        _logPath,
                        $"[{DateTime.Now:HH:mm:ss}] {prefix} {text}{Environment.NewLine}");
                }
            }
            catch
            {
            }
        }

        // ============================================================
        // PROFILES
        // ============================================================

        private void InitializeProfiles()
        {
            try
            {
                _profiles.Clear();

                if (File.Exists(_profilesPath))
                {
                    string json =
                        File.ReadAllText(_profilesPath);

                    List<LauncherProfile>? loaded =
                        JsonSerializer.Deserialize<
                            List<LauncherProfile>>(json);

                    if (loaded != null)
                    {
                        foreach (LauncherProfile profile in loaded)
                        {
                            if (string.IsNullOrWhiteSpace(profile.Name))
                                profile.Name = "Profile";

                            if (string.IsNullOrWhiteSpace(profile.Username))
                                profile.Username = "TopuPlayer";

                            if (string.IsNullOrWhiteSpace(profile.Version))
                                profile.Version = DefaultVersion;

                            if (profile.RamGb < 1)
                                profile.RamGb = 1;

                            _profiles.Add(profile);
                        }
                    }
                }

                if (_profiles.Count == 0)
                {
                    _profiles.Add(
                        new LauncherProfile
                        {
                            Name = "Default",
                            Username = "TopuPlayer",
                            Version = DefaultVersion,
                            RamGb = 4,
                            PerformanceMods = true
                        });

                    SaveProfiles();
                }

                RefreshProfileLists();

                SelectProfile(_profiles[0]);

                WriteLog(
                    $"Loaded {_profiles.Count} profile(s).");
            }
            catch (Exception ex)
            {
                WriteException(
                    "PROFILE INITIALIZATION ERROR",
                    ex);

                _profiles.Clear();

                LauncherProfile fallback =
                    new LauncherProfile
                    {
                        Name = "Default",
                        Username = "TopuPlayer",
                        Version = DefaultVersion,
                        RamGb = 4,
                        PerformanceMods = true
                    };

                _profiles.Add(fallback);

                RefreshProfileLists();
                SelectProfile(fallback);
            }
        }

        private void SaveProfiles()
        {
            try
            {
                Directory.CreateDirectory(_gamePath);

                JsonSerializerOptions options =
                    new JsonSerializerOptions
                    {
                        WriteIndented = true
                    };

                string json =
                    JsonSerializer.Serialize(
                        _profiles,
                        options);

                File.WriteAllText(
                    _profilesPath,
                    json);
            }
            catch (Exception ex)
            {
                WriteException(
                    "PROFILE SAVE ERROR",
                    ex);
            }
        }

        private void RefreshProfileLists()
        {
            try
            {
                if (ProfileBox != null)
                {
                    ProfileBox.Items.Clear();

                    foreach (LauncherProfile profile in _profiles)
                        ProfileBox.Items.Add(profile);
                }

                if (ProfileList != null)
                {
                    ProfileList.Items.Clear();

                    foreach (LauncherProfile profile in _profiles)
                        ProfileList.Items.Add(profile);
                }

                if (ProfilesList != null)
                {
                    ProfilesList.Items.Clear();

                    foreach (LauncherProfile profile in _profiles)
                        ProfilesList.Items.Add(profile);
                }
            }
            catch (Exception ex)
            {
                WriteException(
                    "PROFILE LIST ERROR",
                    ex);
            }
        }

        private void SelectProfile(
            LauncherProfile profile)
        {
            try
            {
                _activeProfile = profile;

                if (ProfileBox != null)
                    ProfileBox.SelectedItem = profile;

                if (ProfileList != null)
                    ProfileList.SelectedItem = profile;

                if (ProfilesList != null)
                    ProfilesList.SelectedItem = profile;

                if (ProfileUsernameInput != null)
                    ProfileUsernameInput.Text =
                        profile.Username;

                if (UsernameInput != null)
                    UsernameInput.Text =
                        profile.Username;

                SelectVersionInBox(
                    profile.Version);

                if (RamSlider != null)
                {
                    RamSlider.Value =
                        Math.Max(
                            RamSlider.Minimum,
                            Math.Min(
                                RamSlider.Maximum,
                                profile.RamGb));
                }

                if (ProfileStatusText != null)
                {
                    ProfileStatusText.Text =
                        $"Active profile: {profile.Name}";
                }

                if (SelectedProfileLabel != null)
                {
                    SelectedProfileLabel.Text =
                        $"Profile: {profile.Name}";
                }

                WriteLog(
                    $"Selected profile: {profile.Name}");
            }
            catch (Exception ex)
            {
                WriteException(
                    "PROFILE SELECTION ERROR",
                    ex);
            }
        }

        private void SelectVersionInBox(
            string version)
        {
            if (VersionBox == null)
                return;

            for (int i = 0;
                 i < VersionBox.Items.Count;
                 i++)
            {
                if (VersionBox.Items[i]
                    is ComboBoxItem item)
                {
                    string itemVersion =
                        item.Content?.ToString()?.Trim()
                        ?? "";

                    if (string.Equals(
                            itemVersion,
                            version,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        VersionBox.SelectedIndex = i;
                        return;
                    }
                }
            }
        }

        private void ApplyProfileSettings()
        {
            if (_activeProfile == null)
                return;

            string username =
                ProfileUsernameInput?.Text.Trim()
                ?? "";

            if (string.IsNullOrWhiteSpace(username))
            {
                username =
                    UsernameInput?.Text.Trim()
                    ?? "";
            }

            if (string.IsNullOrWhiteSpace(username))
                username = "TopuPlayer";

            _activeProfile.Username =
                username;

            if (VersionBox != null)
            {
                string version =
                    (VersionBox.SelectedItem as ComboBoxItem)
                        ?.Content
                        ?.ToString()
                        ?.Trim()
                    ?? DefaultVersion;

                if (!string.IsNullOrWhiteSpace(version))
                    _activeProfile.Version = version;
            }

            if (RamSlider != null)
            {
                _activeProfile.RamGb =
                    Math.Max(
                        1,
                        (int)Math.Round(
                            RamSlider.Value));
            }

            SaveProfiles();

            WriteLog(
                $"Profile settings applied: {_activeProfile.Name}");
        }

        // ============================================================
        // PROFILE EVENTS
        // ============================================================

        private void ProfileBox_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (ProfileBox.SelectedItem
                is LauncherProfile profile)
            {
                SelectProfile(profile);
            }
        }

        private void ProfilesList_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (ProfilesList.SelectedItem
                is LauncherProfile profile)
            {
                SelectProfile(profile);
            }
        }

        private void ProfileList_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (ProfileList.SelectedItem
                is LauncherProfile profile)
            {
                SelectProfile(profile);
            }
        }

        private void NewProfile_Click(
            object sender,
            RoutedEventArgs e)
        {
            try
            {
                int number = 1;

                while (_profiles.Any(
                    p => string.Equals(
                        p.Name,
                        $"Profile {number}",
                        StringComparison.OrdinalIgnoreCase)))
                {
                    number++;
                }

                LauncherProfile profile =
                    new LauncherProfile
                    {
                        Name = $"Profile {number}",
                        Username = "TopuPlayer",
                        Version = DefaultVersion,
                        RamGb = 4,
                        PerformanceMods = true
                    };

                _profiles.Add(profile);

                SaveProfiles();
                RefreshProfileLists();
                SelectProfile(profile);

                StatusText.Text =
                    $"Created profile: {profile.Name}";

                if (ProfileStatusText != null)
                    ProfileStatusText.Text =
                        $"Created profile: {profile.Name}";

                WriteLog(
                    $"Created profile: {profile.Name}");
            }
            catch (Exception ex)
            {
                WriteException(
                    "NEW PROFILE ERROR",
                    ex);

                MessageBox.Show(
                    ex.Message,
                    "Profile Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void SaveProfile_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_activeProfile == null)
                return;

            ApplyProfileSettings();

            StatusText.Text =
                $"Profile saved: {_activeProfile.Name}";

            if (ProfileStatusText != null)
                ProfileStatusText.Text =
                    $"Saved: {_activeProfile.Name}";
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

        // ============================================================
        // RAM
        // ============================================================

        private void RamSlider_ValueChanged(
            object sender,
            RoutedPropertyChangedEventArgs<double> e)
        {
            try
            {
                int ram =
                    Math.Max(
                        1,
                        (int)Math.Round(
                            e.NewValue));

                if (RamLabel != null)
                    RamLabel.Text =
                        $"{ram}GB";

                if (_activeProfile != null)
                    _activeProfile.RamGb = ram;
            }
            catch
            {
            }
        }

        // ============================================================
        // VERSION
        // ============================================================

        private string GetSelectedVersion()
        {
            if (_activeProfile != null &&
                !string.IsNullOrWhiteSpace(
                    _activeProfile.Version))
            {
                return _activeProfile.Version;
            }

            string version =
                (VersionBox.SelectedItem as ComboBoxItem)
                    ?.Content
                    ?.ToString()
                    ?.Trim()
                ?? "";

            return string.IsNullOrWhiteSpace(version)
                ? DefaultVersion
                : version;
        }

        private void VersionBox_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            try
            {
                if (_activeProfile == null ||
                    VersionBox == null)
                    return;

                string version =
                    (VersionBox.SelectedItem as ComboBoxItem)
                        ?.Content
                        ?.ToString()
                        ?.Trim()
                    ?? "";

                if (string.IsNullOrWhiteSpace(version))
                    return;

                _activeProfile.Version =
                    version;

                if (ProfileStatusText != null)
                {
                    ProfileStatusText.Text =
                        $"{_activeProfile.Name}: Minecraft {version}";
                }
            }
            catch
            {
            }
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
        // ACCOUNT
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

        private void SaveAccount_Click(
            object sender,
            RoutedEventArgs e)
        {
            try
            {
                string username =
                    ProfileUsernameInput?.Text.Trim()
                    ?? "";

                if (string.IsNullOrWhiteSpace(username))
                    username =
                        UsernameInput?.Text.Trim()
                        ?? "";

                if (string.IsNullOrWhiteSpace(username))
                    username = "TopuPlayer";

                SaveUsername(username);

                if (_activeProfile != null)
                {
                    _activeProfile.Username =
                        username;

                    SaveProfiles();
                }

                _session =
                    MSession.CreateOfflineSession(
                        username);

                UsernameInput.Text =
                    username;

                StatusText.Text =
                    $"Account saved: {username}";

                if (ProfileStatusText != null)
                    ProfileStatusText.Text =
                        $"Username: {username}";

                WriteLog(
                    $"Account saved: {username}");
            }
            catch (Exception ex)
            {
                WriteException(
                    "ACCOUNT SAVE ERROR",
                    ex);

                MessageBox.Show(
                    ex.Message,
                    "Account Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

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
                string minecraftVersion =
                    GetSelectedVersion();

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
                    await response.Content.ReadAsStringAsync();

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
                    minecraftVersion);

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
                await response.Content.ReadAsStringAsync();

            using JsonDocument doc =
                JsonDocument.Parse(json);

            JsonElement versions =
                doc.RootElement;

            JsonElement? selectedFile = null;

            foreach (JsonElement version
                     in versions.EnumerateArray())
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
                    $"No compatible JAR found for {title}.");
            }

            JsonElement file =
                selectedFile.Value;

            string downloadUrl =
                file.GetProperty("url")
                    .GetString()
                ?? throw new InvalidOperationException(
                    "Download URL missing.");

            string filename =
                file.GetProperty("filename")
                    .GetString()
                ?? $"{SanitizeFileName(title)}.jar";

            string destination =
                Path.Combine(
                    GetProfileModsFolder(),
                    SanitizeFileName(filename));

            Directory.CreateDirectory(
                GetProfileModsFolder());

            await DownloadFileAsync(
                downloadUrl,
                destination);

            WriteLog(
                $"Installed Modrinth mod: {title}");
        }

        // ============================================================
        // PERFORMANCE MODS
        // ============================================================

        private async void ApplyPerformanceMods_Click(
            object sender,
            RoutedEventArgs e)
        {
            try
            {
                if (_activeProfile == null)
                {
                    MessageBox.Show(
                        "Select a profile first.",
                        "Topu Client",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    return;
                }

                ApplyProfileSettings();

                _activeProfile.PerformanceMods =
                    true;

                SaveProfiles();

                await InstallPerformanceModsAsync(
                    _activeProfile.Version);

                ProfileStatusText.Text =
                    "Performance mods applied.";

                StatusText.Text =
                    $"Performance mods installed for {_activeProfile.Name}";
            }
            catch (Exception ex)
            {
                WriteException(
                    "PERFORMANCE MOD ERROR",
                    ex);

                MessageBox.Show(
                    ex.Message,
                    "Performance Mods",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async Task InstallPerformanceModsAsync(
            string minecraftVersion)
        {
            string modsFolder =
                GetProfileModsFolder();

            Directory.CreateDirectory(
                modsFolder);

            WriteLog(
                $"Installing performance mods for profile: {_activeProfile?.Name}");

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
                            $"Installed performance mod: {name}");
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
        }

        private async Task<bool> DownloadPerformanceModAsync(
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
                "%22%5D";

            using HttpResponseMessage response =
                await Http.GetAsync(url);

            response.EnsureSuccessStatusCode();

            string json =
                await response.Content.ReadAsStringAsync();

            using JsonDocument doc =
                JsonDocument.Parse(json);

            JsonElement versions =
                doc.RootElement;

            if (versions.ValueKind !=
                JsonValueKind.Array ||
                versions.GetArrayLength() == 0)
            {
                return false;
            }

            JsonElement? selectedFile = null;

            foreach (JsonElement version
                     in versions.EnumerateArray())
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
                    GetProfileModsFolder(),
                    SanitizeFileName(filename));

            if (File.Exists(destination) &&
                new FileInfo(destination).Length > 0)
            {
                return true;
            }

            await DownloadFileAsync(
                downloadUrl,
                destination);

            return true;
        }

        private static JsonElement? FindPrimaryJar(
            JsonElement files)
        {
            if (files.ValueKind !=
                JsonValueKind.Array)
                return null;

            JsonElement? fallback = null;

            foreach (JsonElement file
                     in files.EnumerateArray())
            {
                if (!file.TryGetProperty(
                        "filename",
                        out JsonElement filenameElement))
                    continue;

                string filename =
                    filenameElement.GetString()
                    ?? "";

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
        // PROFILE GAME DIRECTORIES
        // ============================================================

        private string GetProfileDirectory()
        {
            if (_activeProfile == null)
                return Path.Combine(
                    _gamePath,
                    "profiles",
                    "Default");

            return Path.Combine(
                _gamePath,
                "profiles",
                SanitizeDirectoryName(
                    _activeProfile.Name));
        }

        private string GetProfileModsFolder()
        {
            return Path.Combine(
                GetProfileDirectory(),
                "mods");
        }

        private string GetProfileGameDirectory()
        {
            return Path.Combine(
                GetProfileDirectory(),
                "game");
        }

        // ============================================================
        // FABRIC INSTALLATION
        // ============================================================

        private async Task<string> InstallFabricAsync(
            string minecraftVersion,
            MinecraftPath minecraftPath)
        {
            StatusText.Text =
                $"Installing Fabric {minecraftVersion}...";

            WriteLog(
                "===== FABRIC INSTALLATION =====");

            FabricInstaller installer =
                new FabricInstaller(Http);

            string fabricVersion =
                await installer.Install(
                    minecraftVersion,
                    minecraftPath);

            if (string.IsNullOrWhiteSpace(
                    fabricVersion))
            {
                throw new InvalidOperationException(
                    "Fabric installer returned an empty version.");
            }

            WriteLog(
                $"Fabric installed: {fabricVersion}");

            string fabricDirectory =
                Path.Combine(
                    _gamePath,
                    "versions",
                    fabricVersion);

            string fabricJson =
                Path.Combine(
                    fabricDirectory,
                    fabricVersion + ".json");

            if (!File.Exists(fabricJson))
            {
                throw new FileNotFoundException(
                    "Fabric JSON was not created.",
                    fabricJson);
            }

            await RepairFabricProfileAsync(
                fabricVersion,
                fabricJson);

            RemoveLegacyFabricVersionJar(
                Path.Combine(
                    fabricDirectory,
                    fabricVersion + ".jar"));

            if (!ValidateFabricInstallation(
                    fabricVersion))
            {
                throw new InvalidOperationException(
                    "Fabric validation failed.");
            }

            return fabricVersion;
        }

        private async Task RepairFabricProfileAsync(
            string fabricVersion,
            string fabricJsonPath)
        {
            string json =
                await File.ReadAllTextAsync(
                    fabricJsonPath);

            using JsonDocument document =
                JsonDocument.Parse(json);

            JsonElement root =
                document.RootElement;

            if (!root.TryGetProperty(
                    "libraries",
                    out JsonElement libraries))
            {
                throw new InvalidOperationException(
                    "Fabric profile has no libraries.");
            }

            foreach (JsonElement library
                     in libraries.EnumerateArray())
            {
                if (!library.TryGetProperty(
                        "name",
                        out JsonElement nameElement))
                    continue;

                string coordinate =
                    nameElement.GetString()
                    ?? "";

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
                        _gamePath,
                        "libraries",
                        relativePath);

                if (File.Exists(destination) &&
                    new FileInfo(destination).Length > 0)
                    continue;

                string? url =
                    GetLibraryUrl(
                        library,
                        coordinate,
                        relativePath);

                if (string.IsNullOrWhiteSpace(url))
                    continue;

                await DownloadFileAsync(
                    url,
                    destination);
            }

            string? loader =
                await EnsureFabricLoaderAsync(
                    libraries);

            if (loader == null)
            {
                throw new InvalidOperationException(
                    "Fabric Loader JAR could not be verified.");
            }
        }

        private async Task<string?> EnsureFabricLoaderAsync(
            JsonElement libraries)
        {
            foreach (JsonElement library
                     in libraries.EnumerateArray())
            {
                if (!library.TryGetProperty(
                        "name",
                        out JsonElement nameElement))
                    continue;

                string coordinate =
                    nameElement.GetString()
                    ?? "";

                if (!coordinate.StartsWith(
                        "net.fabricmc:fabric-loader:",
                        StringComparison.OrdinalIgnoreCase))
                    continue;

                string[] parts =
                    coordinate.Split(':');

                if (parts.Length < 3)
                    continue;

                string version =
                    parts[2];

                string relativePath =
                    Path.Combine(
                        "net",
                        "fabricmc",
                        "fabric-loader",
                        version,
                        $"fabric-loader-{version}.jar");

                string path =
                    Path.Combine(
                        _gamePath,
                        "libraries",
                        relativePath);

                if (File.Exists(path) &&
                    new FileInfo(path).Length > 0 &&
                    JarContainsEntry(
                        path,
                        "net/fabricmc/loader/impl/launch/knot/KnotClient.class"))
                {
                    return path;
                }

                string? url =
                    GetLibraryUrl(
                        library,
                        coordinate,
                        relativePath.Replace('\\', '/'));

                if (string.IsNullOrWhiteSpace(url))
                    continue;

                await DownloadFileAsync(
                    url,
                    path);

                if (JarContainsEntry(
                        path,
                        "net/fabricmc/loader/impl/launch/knot/KnotClient.class"))
                {
                    return path;
                }
            }

            string loaderRoot =
                Path.Combine(
                    _gamePath,
                    "libraries",
                    "net",
                    "fabricmc",
                    "fabric-loader");

            if (!Directory.Exists(loaderRoot))
                return null;

            string[] jars =
                Directory.GetFiles(
                    loaderRoot,
                    "fabric-loader-*.jar",
                    SearchOption.AllDirectories);

            foreach (string jar in jars)
            {
                if (JarContainsEntry(
                        jar,
                        "net/fabricmc/loader/impl/launch/knot/KnotClient.class"))
                {
                    return jar;
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

                return archive.Entries.Any(
                    e => string.Equals(
                        e.FullName,
                        entryName,
                        StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                return false;
            }
        }

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
                    downloads.TryGetProperty(
                        "artifact",
                        out JsonElement artifact) &&
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
                               relativePath.Replace('\\', '/');
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
                        relativePath.Replace('\\', '/');
                }

                return
                    "https://libraries.minecraft.net/" +
                    relativePath.Replace('\\', '/');
            }
            catch
            {
                return null;
            }
        }

        private bool ValidateFabricInstallation(
            string fabricVersion)
        {
            try
            {
                string directory =
                    Path.Combine(
                        _gamePath,
                        "versions",
                        fabricVersion);

                string json =
                    Path.Combine(
                        directory,
                        fabricVersion + ".json");

                if (!File.Exists(json))
                    return false;

                string loaderRoot =
                    Path.Combine(
                        _gamePath,
                        "libraries",
                        "net",
                        "fabricmc",
                        "fabric-loader");

                if (!Directory.Exists(loaderRoot))
                    return false;

                string[] jars =
                    Directory.GetFiles(
                        loaderRoot,
                        "fabric-loader-*.jar",
                        SearchOption.AllDirectories);

                return jars.Any(
                    jar => JarContainsEntry(
                        jar,
                        "net/fabricmc/loader/impl/launch/knot/KnotClient.class"));
            }
            catch
            {
                return false;
            }
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
                return javaExe;
            }

            string systemJava =
                FindSystemJava(requiredMajor);

            if (!string.IsNullOrWhiteSpace(systemJava))
                return systemJava;

            StatusText.Text =
                $"Downloading Java {requiredMajor}...";

            await DownloadAndInstallJavaAsync(
                requiredMajor,
                runtimeFolder);

            if (!File.Exists(javaExe) ||
                !IsRequiredJava(
                    javaExe,
                    requiredMajor))
            {
                throw new InvalidOperationException(
                    $"Java {requiredMajor} installation failed.");
            }

            return javaExe;
        }

        private string FindSystemJava(
            int requiredMajor)
        {
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
                    IsRequiredJava(
                        candidate,
                        requiredMajor))
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

                if (File.Exists(candidate) &&
                    IsRequiredJava(
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

                string output =
                    process.StandardOutput.ReadToEnd();

                string error =
                    process.StandardError.ReadToEnd();

                process.WaitForExit();

                string combined =
                    output +
                    Environment.NewLine +
                    error;

                WriteLog(
                    $"Java check: {combined.Trim()}");

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

            using HttpResponseMessage response =
                await Http.GetAsync(apiUrl);

            response.EnsureSuccessStatusCode();

            string json =
                await response.Content.ReadAsStringAsync();

            using JsonDocument doc =
                JsonDocument.Parse(json);

            JsonElement assets =
                doc.RootElement;

            if (assets.GetArrayLength() == 0)
                throw new InvalidOperationException(
                    $"No Java {major} runtime found.");

            JsonElement package =
                assets[0]
                    .GetProperty("binary")
                    .GetProperty("package");

            string url =
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

            string temp =
                Path.Combine(
                    Path.GetTempPath(),
                    "topu-java-" +
                    Guid.NewGuid().ToString("N") +
                    "-" +
                    SanitizeFileName(archiveName));

            string extraction =
                destination +
                ".extracting-" +
                Guid.NewGuid().ToString("N");

            try
            {
                using HttpResponseMessage javaResponse =
                    await Http.GetAsync(
                        url,
                        HttpCompletionOption.ResponseHeadersRead);

                javaResponse.EnsureSuccessStatusCode();

                using Stream input =
                    await javaResponse.Content.ReadAsStreamAsync();

                using FileStream output =
                    new FileStream(
                        temp,
                        FileMode.Create,
                        FileAccess.Write,
                        FileShare.Read);

                await input.CopyToAsync(
                    output,
                    81920,
                    CancellationToken.None);

                await output.FlushAsync(
                    CancellationToken.None);

                Directory.CreateDirectory(
                    extraction);

                ZipFile.ExtractToDirectory(
                    temp,
                    extraction,
                    true);

                string? root =
                    FindJavaRoot(extraction);

                if (root != null)
                {
                    MoveJavaRootContents(
                        root,
                        extraction);
                }

                string extractedJava =
                    Path.Combine(
                        extraction,
                        "bin",
                        "java.exe");

                if (!File.Exists(extractedJava))
                {
                    throw new InvalidOperationException(
                        "java.exe was not found.");
                }

                if (Directory.Exists(destination))
                    TryDeleteDirectory(destination);

                MoveDirectoryWithRetry(
                    extraction,
                    destination);
            }
            finally
            {
                TryDeleteFileWithRetry(temp);
                TryDeleteDirectory(extraction);
            }
        }

        private static string? FindJavaRoot(
            string destination)
        {
            foreach (string directory
                     in Directory.GetDirectories(destination))
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
            foreach (string directory
                     in Directory.GetDirectories(source))
            {
                string target =
                    Path.Combine(
                        destination,
                        Path.GetFileName(directory));

                if (Directory.Exists(target))
                    Directory.Delete(target, true);

                Directory.Move(
                    directory,
                    target);
            }

            foreach (string file
                     in Directory.GetFiles(source))
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
            Exception? last = null;

            for (int i = 0; i < 20; i++)
            {
                try
                {
                    Directory.Move(
                        source,
                        destination);

                    return;
                }
                catch (Exception ex)
                    when (ex is IOException ||
                          ex is UnauthorizedAccessException)
                {
                    last = ex;
                    Thread.Sleep(
                        250 + i * 100);
                }
            }

            throw new IOException(
                $"Could not move Java runtime to {destination}.",
                last);
        }

        // ============================================================
        // LAUNCH
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

            if (_activeProfile == null)
            {
                MessageBox.Show(
                    "Select a profile first.",
                    "Topu Client",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            LaunchBtn.IsEnabled = false;

            try
            {
                StartLaunchLog();

                ApplyProfileSettings();

                string minecraftVersion =
                    _activeProfile.Version;

                string username =
                    _activeProfile.Username;

                int ram =
                    Math.Max(
                        2048,
                        _activeProfile.RamGb * 1024);

                WriteLog(
                    $"===== LAUNCH PROFILE =====");

                WriteLog(
                    $"Profile: {_activeProfile.Name}");

                WriteLog(
                    $"Minecraft: {minecraftVersion}");

                WriteLog(
                    $"Username: {username}");

                WriteLog(
                    $"RAM: {ram} MB");

                if (AuthTypeBox.SelectedIndex != 0)
                {
                    throw new InvalidOperationException(
                        "Microsoft login is not enabled yet. Select Offline mode.");
                }

                _session =
                    MSession.CreateOfflineSession(
                        username);

                SaveUsername(username);

                int javaMajor =
                    GetRequiredJavaMajor(
                        minecraftVersion);

                string javaPath =
                    await EnsureJavaAsync(
                        javaMajor);

                WriteLog(
                    $"Java: {javaPath}");

                MinecraftPath minecraftPath =
                    new MinecraftPath(
                        _gamePath);

                MinecraftLauncher launcher =
                    new MinecraftLauncher(
                        minecraftPath);

                StatusText.Text =
                    $"Installing Minecraft {minecraftVersion}...";

                Progress<InstallerProgressChangedEventArgs>
                    fileProgress =
                    new Progress<
                        InstallerProgressChangedEventArgs>(
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

                string fabricVersion =
                    await InstallFabricAsync(
                        minecraftVersion,
                        minecraftPath);

                if (!ValidateMinecraftInstallation(
                        minecraftVersion,
                        fabricVersion))
                {
                    throw new InvalidOperationException(
                        "Minecraft/Fabric installation validation failed.");
                }

                if (_activeProfile.PerformanceMods)
                {
                    await InstallPerformanceModsAsync(
                        minecraftVersion);
                }

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
                        GameLauncherName =
                            "Topu Client",
                        GameLauncherVersion =
                            "1.0.0"
                    };

                RemoveLegacyFabricVersionJar(
                    Path.Combine(
                        _gamePath,
                        "versions",
                        fabricVersion,
                        fabricVersion + ".jar"));

                StatusText.Text =
                    "Building Minecraft process...";

                Process process =
                    await launcher.BuildProcessAsync(
                        fabricVersion,
                        options,
                        CancellationToken.None);

                if (process == null)
                {
                    throw new InvalidOperationException(
                        "CmlLib returned a null process.");
                }

                NormalizeFabricProcessArguments(
                    process,
                    minecraftVersion,
                    fabricVersion);

                ValidateFinalFabricCommand(
                    process,
                    minecraftVersion,
                    fabricVersion);

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

                WriteDebugFile(
                    process,
                    javaPath,
                    minecraftVersion,
                    fabricVersion,
                    ram);

                WriteLog(
                    $"Executable: {process.StartInfo.FileName}");

                WriteLog(
                    $"Arguments: {process.StartInfo.Arguments}");

                StatusText.Text =
                    $"Starting {_activeProfile.Name}...";

                bool started =
                    process.Start();

                if (!started)
                {
                    throw new InvalidOperationException(
                        "Windows failed to start Minecraft.");
                }

                _minecraftProcess =
                    process;

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                StatusText.Text =
                    $"Topu Client running: {_activeProfile.Name}";

                WriteLog(
                    $"Minecraft started. PID={process.Id}");

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
        // INSTALLATION VALIDATION
        // ============================================================

        private bool ValidateMinecraftInstallation(
            string minecraftVersion,
            string fabricVersion)
        {
            try
            {
                string vanillaDirectory =
                    Path.Combine(
                        _gamePath,
                        "versions",
                        minecraftVersion);

                string fabricDirectory =
                    Path.Combine(
                        _gamePath,
                        "versions",
                        fabricVersion);

                string vanillaJar =
                    Path.Combine(
                        vanillaDirectory,
                        minecraftVersion + ".jar");

                string vanillaJson =
                    Path.Combine(
                        vanillaDirectory,
                        minecraftVersion + ".json");

                string fabricJson =
                    Path.Combine(
                        fabricDirectory,
                        fabricVersion + ".json");

                if (!File.Exists(vanillaJar) ||
                    new FileInfo(vanillaJar).Length <= 0)
                    return false;

                if (!File.Exists(vanillaJson))
                    return false;

                if (!File.Exists(fabricJson))
                    return false;

                string loaderRoot =
                    Path.Combine(
                        _gamePath,
                        "libraries",
                        "net",
                        "fabricmc",
                        "fabric-loader");

                if (!Directory.Exists(loaderRoot))
                    return false;

                string[] jars =
                    Directory.GetFiles(
                        loaderRoot,
                        "fabric-loader-*.jar",
                        SearchOption.AllDirectories);

                return jars.Count(
                        JarContainsKnot)
                    == 1;
            }
            catch (Exception ex)
            {
                WriteException(
                    "INSTALLATION VALIDATION ERROR",
                    ex);

                return false;
            }
        }

        private static bool JarContainsKnot(
            string jar)
        {
            return JarContainsEntry(
                jar,
                "net/fabricmc/loader/impl/launch/knot/KnotClient.class");
        }

        // ============================================================
        // FABRIC DUPLICATE FIX
        // ============================================================

        private void NormalizeFabricProcessArguments(
            Process process,
            string minecraftVersion,
            string fabricVersion)
        {
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
                    "CmlLib generated command without a valid classpath.");
            }

            List<string> classpath =
                tokens[cpIndex + 1]
                    .Split(
                        ';',
                        StringSplitOptions.RemoveEmptyEntries)
                    .Select(
                        p => p.Trim().Trim('"'))
                    .ToList();

            string vanillaJar =
                NormalizePath(
                    Path.Combine(
                        _gamePath,
                        "versions",
                        minecraftVersion,
                        minecraftVersion + ".jar"));

            string fabricProfileJar =
                NormalizePath(
                    Path.Combine(
                        _gamePath,
                        "versions",
                        fabricVersion,
                        fabricVersion + ".jar"));

            List<string> rebuilt =
                new List<string>();

            bool vanillaPresent = false;

            foreach (string entry in classpath)
            {
                string normalized =
                    NormalizePath(entry);

                if (string.Equals(
                        normalized,
                        fabricProfileJar,
                        StringComparison.OrdinalIgnoreCase))
                {
                    WriteLog(
                        $"Removed duplicate Fabric profile JAR: {entry}");

                    continue;
                }

                if (string.Equals(
                        normalized,
                        vanillaJar,
                        StringComparison.OrdinalIgnoreCase))
                {
                    vanillaPresent = true;
                }

                rebuilt.Add(entry);
            }

            if (!vanillaPresent)
            {
                rebuilt.Add(vanillaJar);
            }

            tokens[cpIndex + 1] =
                string.Join(
                    ";",
                    rebuilt);

            for (int i =
                     tokens.Count - 1;
                 i >= 0;
                 i--)
            {
                if (tokens[i].StartsWith(
                        "-DFabricMcEmu=",
                        StringComparison.OrdinalIgnoreCase))
                {
                    tokens.RemoveAt(i);
                }
            }

            bool knot =
                tokens.Any(
                    t => string.Equals(
                        t,
                        "net.fabricmc.loader.impl.launch.knot.KnotClient",
                        StringComparison.Ordinal));

            if (!knot)
            {
                throw new InvalidOperationException(
                    "KnotClient is missing from the Minecraft command.");
            }

            process.StartInfo.Arguments =
                BuildWindowsCommandLine(tokens);
        }

        private void ValidateFinalFabricCommand(
            Process process,
            string minecraftVersion,
            string fabricVersion)
        {
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
                    "Final command has no classpath.");
            }

            List<string> classpath =
                tokens[cpIndex + 1]
                    .Split(
                        ';',
                        StringSplitOptions.RemoveEmptyEntries)
                    .Select(
                        NormalizePath)
                    .ToList();

            string vanilla =
                NormalizePath(
                    Path.Combine(
                        _gamePath,
                        "versions",
                        minecraftVersion,
                        minecraftVersion + ".jar"));

            string fabricJar =
                NormalizePath(
                    Path.Combine(
                        _gamePath,
                        "versions",
                        fabricVersion,
                        fabricVersion + ".jar"));

            bool vanillaPresent =
                classpath.Any(
                    p => string.Equals(
                        p,
                        vanilla,
                        StringComparison.OrdinalIgnoreCase));

            bool fabricPresent =
                classpath.Any(
                    p => string.Equals(
                        p,
                        fabricJar,
                        StringComparison.OrdinalIgnoreCase));

            int loaderCount =
                classpath.Count(
                    p => p.Contains(
                        "\\libraries\\net\\fabricmc\\fabric-loader\\",
                        StringComparison.OrdinalIgnoreCase) &&
                         p.EndsWith(
                             ".jar",
                             StringComparison.OrdinalIgnoreCase));

            bool knot =
                tokens.Any(
                    t => string.Equals(
                        t,
                        "net.fabricmc.loader.impl.launch.knot.KnotClient",
                        StringComparison.Ordinal));

            if (!vanillaPresent)
                throw new InvalidOperationException(
                    "Final command is missing Minecraft JAR.");

            if (fabricPresent)
                throw new InvalidOperationException(
                    "Final command still contains duplicate Fabric profile JAR.");

            if (loaderCount != 1)
                throw new InvalidOperationException(
                    $"Expected one Fabric Loader JAR, found {loaderCount}.");

            if (!knot)
                throw new InvalidOperationException(
                    "KnotClient is missing.");

            WriteLog(
                "FINAL FABRIC COMMAND CHECK PASSED.");
        }

        // ============================================================
        // COMMAND LINE
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

            bool quotes = false;
            int slashes = 0;

            foreach (char c in commandLine)
            {
                if (c == '\\')
                {
                    slashes++;
                    continue;
                }

                if (c == '"')
                {
                    if (slashes > 0)
                    {
                        current.Append(
                            new string(
                                '\\',
                                slashes / 2));

                        if (slashes % 2 == 1)
                        {
                            current.Append('"');
                        }
                        else
                        {
                            quotes = !quotes;
                        }

                        slashes = 0;
                        continue;
                    }

                    quotes = !quotes;
                    continue;
                }

                if (slashes > 0)
                {
                    current.Append(
                        new string(
                            '\\',
                            slashes));

                    slashes = 0;
                }

                if (char.IsWhiteSpace(c) &&
                    !quotes)
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

            if (slashes > 0)
            {
                current.Append(
                    new string(
                        '\\',
                        slashes));
            }

            if (current.Length > 0)
                result.Add(
                    current.ToString());

            return result;
        }

        private static string
            BuildWindowsCommandLine(
                IEnumerable<string> arguments)
        {
            return string.Join(
                " ",
                arguments.Select(
                    QuoteWindowsArgument));
        }

        private static string
            QuoteWindowsArgument(
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

            int slashes = 0;

            foreach (char c in argument)
            {
                if (c == '\\')
                {
                    slashes++;
                    continue;
                }

                if (c == '"')
                {
                    builder.Append(
                        new string(
                            '\\',
                            slashes * 2 + 1));

                    builder.Append('"');
                    slashes = 0;

                    continue;
                }

                if (slashes > 0)
                {
                    builder.Append(
                        new string(
                            '\\',
                            slashes));

                    slashes = 0;
                }

                builder.Append(c);
            }

            if (slashes > 0)
            {
                builder.Append(
                    new string(
                        '\\',
                        slashes * 2));
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
        // DOWNLOAD
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
                    Guid.NewGuid().ToString("N") +
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
                    await response.Content.ReadAsStreamAsync();

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

                if (!File.Exists(temporary) ||
                    new FileInfo(temporary).Length <= 0)
                {
                    throw new IOException(
                        "Downloaded file is empty.");
                }

                await MoveFileWithRetryAsync(
                    temporary,
                    destination);
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
            Exception? last = null;

            for (int i = 0; i < 20; i++)
            {
                try
                {
                    if (File.Exists(destination))
                    {
                        if (new FileInfo(destination).Length > 0)
                        {
                            TryDeleteFileWithRetry(source);
                            return;
                        }

                        TryDeleteFile(destination);
                    }

                    File.Move(
                        source,
                        destination);

                    return;
                }
                catch (Exception ex)
                    when (ex is IOException ||
                          ex is UnauthorizedAccessException)
                {
                    last = ex;

                    await Task.Delay(
                        300 + i * 100);
                }
            }

            throw new IOException(
                $"Could not move downloaded file to {destination}.",
                last);
        }

        // ============================================================
        // LEGACY FABRIC DUPLICATE
        // ============================================================

        private void RemoveLegacyFabricVersionJar(
            string path)
        {
            if (!File.Exists(path))
                return;

            WriteLog(
                $"Removing legacy Fabric duplicate: {path}");

            TryDeleteFileWithRetry(path);

            if (File.Exists(path))
            {
                throw new IOException(
                    $"Could not remove duplicate Fabric JAR: {path}");
            }
        }

        // ============================================================
        // MINECRAFT OUTPUT
        // ============================================================

        private void Minecraft_OutputDataReceived(
            object sender,
            DataReceivedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
                AppendRawGameOutput(
                    "[MC]",
                    e.Data);
        }

        private void Minecraft_ErrorDataReceived(
            object sender,
            DataReceivedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
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

                await Dispatcher.InvokeAsync(
                    () =>
                    {
                        StatusText.Text =
                            exitCode == 0
                                ? "Minecraft closed normally."
                                : $"Minecraft crashed (exit code {exitCode}). Check the log.";
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

                StringBuilder text =
                    new StringBuilder();

                text.AppendLine(
                    "===== TOPU CLIENT DEBUG =====");

                text.AppendLine(
                    $"Time: {DateTime.Now:O}");

                text.AppendLine(
                    $"Profile: {_activeProfile?.Name}");

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
                    "Working directory:");

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

        private static string SanitizeDirectoryName(
            string name)
        {
            foreach (char c in
                     Path.GetInvalidFileNameChars())
            {
                name =
                    name.Replace(
                        c,
                        '_');
            }

            return name.Trim();
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
