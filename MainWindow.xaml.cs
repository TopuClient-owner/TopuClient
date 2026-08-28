using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
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

        private bool _loadingProfile;
        private string _selectedProfileName = "";

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

            _profilesPath = Path.Combine(
                _gamePath,
                "profiles.json");

            Directory.CreateDirectory(_gamePath);

            LoadUsername();

            if (RamLabel != null)
                RamLabel.Text = $"{(int)RamSlider.Value}GB";

            if (ProfileRamLabel != null)
                ProfileRamLabel.Text = $"{(int)ProfileRamSlider.Value}GB";

            LoadProfiles();

            WriteLog("Topu Client initialized.");
            WriteLog($"Game directory: {_gamePath}");
        }

        private static HttpClient CreateHttpClient()
        {
            HttpClient client = new HttpClient(
                new HttpClientHandler
                {
                    AllowAutoRedirect = true
                });

            client.Timeout = TimeSpan.FromMinutes(30);
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

        private void WriteException(string title, Exception ex)
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
            if (string.IsNullOrEmpty(text))
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
        // USERNAME
        // ============================================================

        private void LoadUsername()
        {
            try
            {
                string username = "TopuPlayer";

                if (File.Exists(_configPath))
                {
                    string saved =
                        File.ReadAllText(_configPath).Trim();

                    if (!string.IsNullOrWhiteSpace(saved))
                        username = saved;
                }

                UsernameInput.Text = username;
                AccountUsernameInput.Text = username;

                _session =
                    MSession.CreateOfflineSession(username);

                WriteLog($"Loaded username: {username}");
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

            TabLaunch.Visibility = Visibility.Collapsed;
            TabProfiles.Visibility = Visibility.Collapsed;
            TabMods.Visibility = Visibility.Collapsed;
            TabAccounts.Visibility = Visibility.Collapsed;

            TabLaunchBtn.Foreground =
                new SolidColorBrush(Color.FromRgb(136, 136, 136));

            TabProfilesBtn.Foreground =
                new SolidColorBrush(Color.FromRgb(136, 136, 136));

            TabModsBtn.Foreground =
                new SolidColorBrush(Color.FromRgb(136, 136, 136));

            TabAccountsBtn.Foreground =
                new SolidColorBrush(Color.FromRgb(136, 136, 136));

            button.Foreground =
                new SolidColorBrush(Color.FromRgb(0, 255, 136));

            switch (tab)
            {
                case "TabLaunch":
                    TabLaunch.Visibility = Visibility.Visible;
                    break;

                case "TabProfiles":
                    TabProfiles.Visibility = Visibility.Visible;
                    break;

                case "TabMods":
                    TabMods.Visibility = Visibility.Visible;
                    UpdateModsProfileLabel();
                    break;

                case "TabAccounts":
                    TabAccounts.Visibility = Visibility.Visible;
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
                RamLabel.Text = $"{(int)e.NewValue}GB";
        }

        private void ProfileRamSlider_ValueChanged(
            object sender,
            RoutedPropertyChangedEventArgs<double> e)
        {
            if (ProfileRamLabel != null)
                ProfileRamLabel.Text = $"{(int)e.NewValue}GB";
        }

        // ============================================================
        // VERSION
        // ============================================================

        private string GetComboBoxVersion(ComboBox box)
        {
            return
                (box.SelectedItem as ComboBoxItem)
                ?.Content
                ?.ToString()
                ?.Trim()
                ?? DefaultVersion;
        }

        private string GetSelectedVersion()
        {
            string version =
                GetComboBoxVersion(VersionBox);

            if (!SupportedVersion(version))
                return DefaultVersion;

            return version;
        }

        private string GetSelectedProfileVersion()
        {
            string version =
                GetComboBoxVersion(ProfileVersionBox);

            if (!SupportedVersion(version))
                return DefaultVersion;

            return version;
        }

        private bool SupportedVersion(string version)
        {
            foreach (string supported in SupportedVersions)
            {
                if (string.Equals(
                    supported,
                    version,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private void VersionBox_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (VersionBox == null ||
                StatusText == null)
                return;

            string version = GetSelectedVersion();

            StatusText.Text =
                $"Minecraft version: {version}";
        }

        private void ProfileVersionBox_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (_loadingProfile)
                return;

            if (ProfileVersionBox == null)
                return;

            string version =
                GetSelectedProfileVersion();

            if (StatusText != null)
                StatusText.Text =
                    $"Profile version: {version}";
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
        // PROFILES
        // ============================================================

        private class ProfileData
        {
            public string Name { get; set; } = "";
            public string Version { get; set; } = DefaultVersion;
            public int Ram { get; set; } = 4;
            public string Folder { get; set; } = "";
        }

        private List<ProfileData> GetProfiles()
        {
            try
            {
                if (!File.Exists(_profilesPath))
                    return new List<ProfileData>();

                string json =
                    File.ReadAllText(_profilesPath);

                List<ProfileData>? profiles =
                    JsonSerializer.Deserialize<List<ProfileData>>(json);

                return profiles ?? new List<ProfileData>();
            }
            catch (Exception ex)
            {
                WriteException(
                    "PROFILE LOAD ERROR",
                    ex);

                return new List<ProfileData>();
            }
        }

        private void SaveProfiles(
            List<ProfileData> profiles)
        {
            try
            {
                string json =
                    JsonSerializer.Serialize(
                        profiles,
                        new JsonSerializerOptions
                        {
                            WriteIndented = true
                        });

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

        private void LoadProfiles()
        {
            try
            {
                _loadingProfile = true;

                ProfilesList.Items.Clear();
                ProfileBox.Items.Clear();

                List<ProfileData> profiles =
                    GetProfiles();

                if (profiles.Count == 0)
                {
                    ProfileData defaultProfile =
                        new ProfileData
                        {
                            Name = "Default",
                            Version = DefaultVersion,
                            Ram = 4,
                            Folder = Path.Combine(
                                _gamePath,
                                "profiles",
                                "Default")
                        };

                    profiles.Add(defaultProfile);
                    SaveProfiles(profiles);
                }

                foreach (ProfileData profile in profiles)
                {
                    ProfilesList.Items.Add(profile.Name);
                    ProfileBox.Items.Add(profile.Name);
                }

                if (ProfilesList.Items.Count > 0)
                    ProfilesList.SelectedIndex = 0;

                if (ProfileBox.Items.Count > 0)
                    ProfileBox.SelectedIndex = 0;

                UpdateProfileEditor();
                UpdateLaunchProfile();
            }
            catch (Exception ex)
            {
                WriteException(
                    "PROFILE INITIALIZATION ERROR",
                    ex);
            }
            finally
            {
                _loadingProfile = false;
            }
        }

        private ProfileData? GetCurrentProfile()
        {
            string name = "";

            if (ProfilesList.SelectedItem != null)
            {
                name =
                    ProfilesList.SelectedItem
                    .ToString() ?? "";
            }

            if (string.IsNullOrWhiteSpace(name) &&
                ProfileBox.SelectedItem != null)
            {
                name =
                    ProfileBox.SelectedItem
                    .ToString() ?? "";
            }

            if (string.IsNullOrWhiteSpace(name))
                return null;

            List<ProfileData> profiles =
                GetProfiles();

            foreach (ProfileData profile in profiles)
            {
                if (string.Equals(
                    profile.Name,
                    name,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return profile;
                }
            }

            return null;
        }

        private void ProfilesList_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (_loadingProfile)
                return;

            ProfileData? profile =
                GetCurrentProfile();

            if (profile == null)
                return;

            _loadingProfile = true;

            try
            {
                _selectedProfileName =
                    profile.Name;

                ProfileNameInput.Text =
                    profile.Name;

                SelectComboItem(
                    ProfileVersionBox,
                    profile.Version);

                ProfileRamSlider.Value =
                    Math.Clamp(
                        profile.Ram,
                        2,
                        16);

                ProfileFolderLabel.Text =
                    profile.Folder;

                ProfileModCountLabel.Text =
                    $"{CountInstalledMods(profile.Folder)} mods";

                SelectProfileBox(
                    profile.Name);

                UpdateLaunchProfile();
                UpdateModsProfileLabel();
            }
            finally
            {
                _loadingProfile = false;
            }
        }

        private void ProfileBox_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (_loadingProfile)
                return;

            if (ProfileBox.SelectedItem == null)
                return;

            string name =
                ProfileBox.SelectedItem.ToString() ?? "";

            if (string.IsNullOrWhiteSpace(name))
                return;

            List<ProfileData> profiles =
                GetProfiles();

            ProfileData? profile = null;

            foreach (ProfileData item in profiles)
            {
                if (string.Equals(
                    item.Name,
                    name,
                    StringComparison.OrdinalIgnoreCase))
                {
                    profile = item;
                    break;
                }
            }

            if (profile == null)
                return;

            _loadingProfile = true;

            try
            {
                _selectedProfileName =
                    profile.Name;

                SelectProfileList(
                    profile.Name);

                VersionBox.SelectedIndex =
                    FindComboItemIndex(
                        VersionBox,
                        profile.Version);

                if (VersionBox.SelectedIndex < 0)
                    VersionBox.SelectedIndex = 0;

                RamSlider.Value =
                    Math.Clamp(
                        profile.Ram,
                        2,
                        16);

                SelectedProfileLabel.Text =
                    profile.Name;

                FooterProfileText.Text =
                    profile.Name;

                UpdateModsProfileLabel();
            }
            finally
            {
                _loadingProfile = false;
            }
        }

        private void UpdateProfileEditor()
        {
            ProfileData? profile =
                GetCurrentProfile();

            if (profile == null)
                return;

            ProfileNameInput.Text =
                profile.Name;

            SelectComboItem(
                ProfileVersionBox,
                profile.Version);

            ProfileRamSlider.Value =
                Math.Clamp(
                    profile.Ram,
                    2,
                    16);

            ProfileFolderLabel.Text =
                profile.Folder;

            ProfileModCountLabel.Text =
                $"{CountInstalledMods(profile.Folder)} mods";
        }

        private void UpdateLaunchProfile()
        {
            ProfileData? profile =
                GetCurrentProfile();

            if (profile == null)
                return;

            _selectedProfileName =
                profile.Name;

            SelectedProfileLabel.Text =
                profile.Name;

            SelectProfileBox(
                profile.Name);

            SelectComboItem(
                VersionBox,
                profile.Version);

            RamSlider.Value =
                Math.Clamp(
                    profile.Ram,
                    2,
                    16);

            FooterProfileText.Text =
                profile.Name;
        }

        private void SelectProfileBox(string name)
        {
            for (int i = 0; i < ProfileBox.Items.Count; i++)
            {
                if (string.Equals(
                    ProfileBox.Items[i]?.ToString(),
                    name,
                    StringComparison.OrdinalIgnoreCase))
                {
                    ProfileBox.SelectedIndex = i;
                    return;
                }
            }
        }

        private void SelectProfileList(string name)
        {
            for (int i = 0; i < ProfilesList.Items.Count; i++)
            {
                if (string.Equals(
                    ProfilesList.Items[i]?.ToString(),
                    name,
                    StringComparison.OrdinalIgnoreCase))
                {
                    ProfilesList.SelectedIndex = i;
                    return;
                }
            }
        }

        private void SelectComboItem(
            ComboBox combo,
            string value)
        {
            int index =
                FindComboItemIndex(
                    combo,
                    value);

            if (index >= 0)
                combo.SelectedIndex = index;
        }

        private int FindComboItemIndex(
            ComboBox combo,
            string value)
        {
            for (int i = 0; i < combo.Items.Count; i++)
            {
                if (combo.Items[i] is ComboBoxItem item)
                {
                    string text =
                        item.Content?.ToString() ?? "";

                    if (string.Equals(
                        text,
                        value,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        return i;
                    }
                }
            }

            return -1;
        }

        private void NewProfile_Click(
            object sender,
            RoutedEventArgs e)
        {
            List<ProfileData> profiles =
                GetProfiles();

            string baseName = "New Profile";
            string name = baseName;
            int number = 2;

            while (ProfileExists(profiles, name))
            {
                name =
                    $"{baseName} {number}";
                number++;
            }

            ProfileData profile =
                new ProfileData
                {
                    Name = name,
                    Version = DefaultVersion,
                    Ram = 4,
                    Folder = Path.Combine(
                        _gamePath,
                        "profiles",
                        SanitizePathName(name))
                };

            Directory.CreateDirectory(
                profile.Folder);

            profiles.Add(profile);

            SaveProfiles(profiles);

            LoadProfiles();

            SelectProfileList(name);

            StatusText.Text =
                $"Created profile: {name}";
        }

        private void SaveProfile_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(
                ProfileNameInput.Text))
            {
                MessageBox.Show(
                    "Enter a profile name.",
                    "Topu Client",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            string oldName =
                _selectedProfileName;

            string newName =
                ProfileNameInput.Text.Trim();

            List<ProfileData> profiles =
                GetProfiles();

            ProfileData? profile = null;

            foreach (ProfileData item in profiles)
            {
                if (string.Equals(
                    item.Name,
                    oldName,
                    StringComparison.OrdinalIgnoreCase))
                {
                    profile = item;
                    break;
                }
            }

            if (profile == null)
            {
                profile =
                    new ProfileData
                    {
                        Name = newName
                    };

                profiles.Add(profile);
            }

            profile.Name = newName;
            profile.Version =
                GetSelectedProfileVersion();

            profile.Ram =
                Math.Clamp(
                    (int)ProfileRamSlider.Value,
                    2,
                    16);

            if (string.IsNullOrWhiteSpace(
                profile.Folder))
            {
                profile.Folder =
                    Path.Combine(
                        _gamePath,
                        "profiles",
                        SanitizePathName(newName));
            }

            Directory.CreateDirectory(
                profile.Folder);

            SaveProfiles(profiles);

            _selectedProfileName =
                newName;

            LoadProfiles();

            SelectProfileList(newName);

            StatusText.Text =
                $"Profile saved: {newName}";

            WriteLog(
                $"Profile saved: {newName}, Minecraft={profile.Version}, RAM={profile.Ram}GB");
        }

        private void DeleteProfile_Click(
            object sender,
            RoutedEventArgs e)
        {
            ProfileData? profile =
                GetCurrentProfile();

            if (profile == null)
                return;

            MessageBoxResult result =
                MessageBox.Show(
                    $"Delete profile '{profile.Name}'?",
                    "Delete Profile",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            List<ProfileData> profiles =
                GetProfiles();

            profiles.RemoveAll(
                p => string.Equals(
                    p.Name,
                    profile.Name,
                    StringComparison.OrdinalIgnoreCase));

            if (profiles.Count == 0)
            {
                profiles.Add(
                    new ProfileData
                    {
                        Name = "Default",
                        Version = DefaultVersion,
                        Ram = 4,
                        Folder = Path.Combine(
                            _gamePath,
                            "profiles",
                            "Default")
                    });
            }

            SaveProfiles(profiles);

            _selectedProfileName = "";

            LoadProfiles();

            StatusText.Text =
                "Profile deleted.";
        }

        private bool ProfileExists(
            List<ProfileData> profiles,
            string name)
        {
            foreach (ProfileData profile in profiles)
            {
                if (string.Equals(
                    profile.Name,
                    name,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private string SanitizePathName(string name)
        {
            foreach (char c in
                     Path.GetInvalidFileNameChars())
            {
                name =
                    name.Replace(
                        c,
                        '_');
            }

            return name;
        }

        // ============================================================
        // ACCOUNT
        // ============================================================

        private void AuthTypeBox_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (StatusText == null ||
                AuthTypeBox == null)
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

        private void SaveAccount_Click(
            object sender,
            RoutedEventArgs e)
        {
            string username =
                AccountUsernameInput.Text.Trim();

            if (string.IsNullOrWhiteSpace(username))
            {
                MessageBox.Show(
                    "Enter a username first.",
                    "Topu Client",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            UsernameInput.Text =
                username;

            SaveUsername(
                username);

            _session =
                MSession.CreateOfflineSession(
                    username);

            StatusText.Text =
                $"Account saved: {username}";

            WriteLog(
                $"Offline account saved: {username}");
        }

        private async void MsLoginBtn_Click(
            object sender,
            RoutedEventArgs e)
        {
            await Task.CompletedTask;

            MessageBox.Show(
                "Microsoft authentication is not enabled in this build yet.\n\nSelect Offline mode to launch Minecraft.",
                "Microsoft Login",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        // ============================================================
        // MODS
        // ============================================================

        private void UpdateModsProfileLabel()
        {
            string name =
                _selectedProfileName;

            if (string.IsNullOrWhiteSpace(name) &&
                ProfileBox.SelectedItem != null)
            {
                name =
                    ProfileBox.SelectedItem.ToString() ?? "";
            }

            if (string.IsNullOrWhiteSpace(name))
                name = "Default";

            if (ModsProfileLabel != null)
            {
                ModsProfileLabel.Text =
                    $"Mods for {name}";
            }

            ProfileData? profile =
                GetProfileByName(name);

            string folder =
                profile?.Folder ??
                Path.Combine(
                    _gamePath,
                    "mods");

            int count =
                CountInstalledMods(folder);

            if (InstalledModsLabel != null)
                InstalledModsLabel.Text =
                    $"{count} mods";
        }

        private ProfileData? GetProfileByName(
            string name)
        {
            List<ProfileData> profiles =
                GetProfiles();

            foreach (ProfileData profile in profiles)
            {
                if (string.Equals(
                    profile.Name,
                    name,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return profile;
                }
            }

            return null;
        }

        private int CountInstalledMods(
            string folder)
        {
            try
            {
                if (!Directory.Exists(folder))
                    return 0;

                return Directory.GetFiles(
                    folder,
                    "*.jar",
                    SearchOption.TopDirectoryOnly).Length;
            }
            catch
            {
                return 0;
            }
        }

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

                UpdateModsProfileLabel();
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

            if (versions.ValueKind != JsonValueKind.Array ||
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
                {
                    continue;
                }

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
                    $"No download URL was returned for {title}.");

            string filename =
                file.GetProperty("filename")
                    .GetString()
                ?? $"{SanitizeFileName(title)}.jar";

            string modsFolder =
                GetSelectedModsFolder();

            Directory.CreateDirectory(
                modsFolder);

            string destination =
                Path.Combine(
                    modsFolder,
                    SanitizeFileName(filename));

            StatusText.Text =
                $"Downloading {title}...";

            await DownloadFileAsync(
                downloadUrl,
                destination);

            WriteLog(
                $"Installed Modrinth mod: {title}");
        }

        private string GetSelectedModsFolder()
        {
            ProfileData? profile =
                GetCurrentProfile();

            if (profile != null &&
                !string.IsNullOrWhiteSpace(
                    profile.Folder))
            {
                string mods =
                    Path.Combine(
                        profile.Folder,
                        "mods");

                Directory.CreateDirectory(mods);

                return mods;
            }

            string defaultMods =
                Path.Combine(
                    _gamePath,
                    "mods");

            Directory.CreateDirectory(
                defaultMods);

            return defaultMods;
        }

        private async void ApplyPerformanceMods_Click(
            object sender,
            RoutedEventArgs e)
        {
            try
            {
                string version =
                    GetSelectedVersion();

                StatusText.Text =
                    "Installing performance mods...";

                await InstallSelectedPerformanceModsAsync(
                    version);

                UpdateModsProfileLabel();

                StatusText.Text =
                    "Performance pack applied.";

                MessageBox.Show(
                    "Topu performance mods have been installed.",
                    "Topu Client",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                WriteException(
                    "PERFORMANCE MOD ERROR",
                    ex);

                StatusText.Text =
                    "Performance mod installation failed.";

                MessageBox.Show(
                    ex.Message,
                    "Topu Client",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async Task InstallSelectedPerformanceModsAsync(
            string minecraftVersion)
        {
            string modsFolder =
                GetSelectedModsFolder();

            Directory.CreateDirectory(
                modsFolder);

            await InstallPerformanceModIfCheckedAsync(
                SodiumCheck,
                "sodium",
                "Sodium",
                minecraftVersion,
                modsFolder);

            await InstallPerformanceModIfCheckedAsync(
                LithiumCheck,
                "lithium",
                "Lithium",
                minecraftVersion,
                modsFolder);

            await InstallPerformanceModIfCheckedAsync(
                DynamicFpsCheck,
                "dynamic-fps",
                "Dynamic FPS",
                minecraftVersion,
                modsFolder);

            await InstallPerformanceModIfCheckedAsync(
                SodiumExtraCheck,
                "sodium-extra",
                "Sodium Extra",
                minecraftVersion,
                modsFolder);

            await InstallPerformanceModIfCheckedAsync(
                KryptonCheck,
                "krypton",
                "Krypton",
                minecraftVersion,
                modsFolder);
        }

        private async Task InstallPerformanceModIfCheckedAsync(
            CheckBox checkBox,
            string slug,
            string name,
            string minecraftVersion,
            string modsFolder)
        {
            if (checkBox.IsChecked != true)
                return;

            try
            {
                StatusText.Text =
                    $"Installing {name}...";

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

                if (versions.ValueKind != JsonValueKind.Array ||
                    versions.GetArrayLength() == 0)
                {
                    WriteLog(
                        $"No compatible {name} build for {minecraftVersion}.");

                    return;
                }

                JsonElement? selectedFile = null;

                foreach (JsonElement version in
                         versions.EnumerateArray())
                {
                    if (!version.TryGetProperty(
                        "files",
                        out JsonElement files))
                    {
                        continue;
                    }

                    selectedFile =
                        FindPrimaryJar(files);

                    if (selectedFile != null)
                        break;
                }

                if (selectedFile == null)
                {
                    WriteLog(
                        $"No JAR found for {name}.");

                    return;
                }

                JsonElement file =
                    selectedFile.Value;

                string downloadUrl =
                    file.GetProperty("url")
                        .GetString()
                    ?? "";

                string filename =
                    file.GetProperty("filename")
                        .GetString()
                    ?? $"{slug}.jar";

                if (string.IsNullOrWhiteSpace(downloadUrl))
                    return;

                string destination =
                    Path.Combine(
                        modsFolder,
                        SanitizeFileName(filename));

                if (File.Exists(destination))
                {
                    WriteLog(
                        $"Mod already installed: {name}");

                    return;
                }

                await DownloadFileAsync(
                    downloadUrl,
                    destination);

                WriteLog(
                    $"Performance mod installed: {name}");
            }
            catch (Exception ex)
            {
                WriteLog(
                    $"Optional performance mod failed: {name}");

                WriteLog(ex.Message);
            }
        }

        private static JsonElement? FindPrimaryJar(
            JsonElement files)
        {
            JsonElement? fallback = null;

            foreach (JsonElement file in
                     files.EnumerateArray())
            {
                if (!file.TryGetProperty(
                    "filename",
                    out JsonElement filenameElement))
                {
                    continue;
                }

                string filename =
                    filenameElement.GetString() ?? "";

                if (!filename.EndsWith(
                    ".jar",
                    StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

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
        // FABRIC
        // ============================================================

        private async Task<string> InstallFabricAsync(
            string minecraftVersion,
            MinecraftPath minecraftPath)
        {
            StatusText.Text =
                $"Installing Fabric for Minecraft {minecraftVersion}...";

            WriteLog(
                $"Installing Fabric for Minecraft {minecraftVersion}.");

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

            if (!IsFabricInstallationUsable(
                fabricVersionName))
            {
                WriteLog(
                    "Fabric installation incomplete. Retrying once.");

                string brokenDirectory =
                    Path.Combine(
                        _gamePath,
                        "versions",
                        fabricVersionName);

                try
                {
                    if (Directory.Exists(brokenDirectory))
                    {
                        Directory.Delete(
                            brokenDirectory,
                            true);
                    }
                }
                catch (Exception cleanupEx)
                {
                    WriteException(
                        "FABRIC CLEANUP ERROR",
                        cleanupEx);
                }

                fabricVersionName =
                    await fabricInstaller.Install(
                        minecraftVersion,
                        minecraftPath);

                if (!IsFabricInstallationUsable(
                    fabricVersionName))
                {
                    throw new InvalidOperationException(
                        "Fabric installation completed but required files are missing.");
                }
            }

            WriteLog(
                $"Fabric installed and verified: {fabricVersionName}");

            return fabricVersionName;
        }

        private bool IsFabricInstallationUsable(
            string fabricVersionName)
        {
            try
            {
                string directory =
                    Path.Combine(
                        _gamePath,
                        "versions",
                        fabricVersionName);

                string json =
                    Path.Combine(
                        directory,
                        fabricVersionName + ".json");

                if (!Directory.Exists(directory))
                    return false;

                if (!File.Exists(json))
                    return false;

                string loader =
                    FindFabricLoaderJar(
                        _gamePath,
                        fabricVersionName);

                return !string.IsNullOrWhiteSpace(loader);
            }
            catch (Exception ex)
            {
                WriteException(
                    "FABRIC VALIDATION ERROR",
                    ex);

                return false;
            }
        }

        private string FindFabricLoaderJar(
            string root,
            string fabricVersion)
        {
            try
            {
                string libraries =
                    Path.Combine(
                        root,
                        "libraries");

                if (!Directory.Exists(libraries))
                    return "";

                foreach (string file in
                         Directory.EnumerateFiles(
                             libraries,
                             "fabric-loader-*.jar",
                             SearchOption.AllDirectories))
                {
                    if (File.Exists(file))
                        return file;
                }

                string exact =
                    fabricVersion + ".jar";

                foreach (string file in
                         Directory.EnumerateFiles(
                             libraries,
                             exact,
                             SearchOption.AllDirectories))
                {
                    if (File.Exists(file))
                        return file;
                }
            }
            catch (Exception ex)
            {
                WriteException(
                    "FABRIC LOADER SEARCH ERROR",
                    ex);
            }

            return "";
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

            if (!File.Exists(javaExe))
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
                    $"No Windows x64 Java {major} runtime was found.");
            }

            JsonElement asset =
                assets[0];

            JsonElement package =
                asset
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

            string temp =
                Path.Combine(
                    Path.GetTempPath(),
                    "topu-java-" +
                    Guid.NewGuid().ToString("N") +
                    "-" +
                    SanitizeFileName(archiveName));

            try
            {
                using HttpResponseMessage javaResponse =
                    await Http.GetAsync(
                        downloadUrl,
                        HttpCompletionOption.ResponseHeadersRead);

                javaResponse.EnsureSuccessStatusCode();

                await using Stream input =
                    await javaResponse.Content.ReadAsStreamAsync();

                await using FileStream output =
                    new FileStream(
                        temp,
                        FileMode.Create,
                        FileAccess.Write,
                        FileShare.None,
                        81920,
                        FileOptions.Asynchronous);

                await input.CopyToAsync(output);
                await output.FlushAsync();

                if (Directory.Exists(destination))
                    Directory.Delete(
                        destination,
                        true);

                Directory.CreateDirectory(
                    destination);

                ZipFile.ExtractToDirectory(
                    temp,
                    destination,
                    true);

                string? javaRoot =
                    FindJavaRoot(destination);

                if (javaRoot != null &&
                    !File.Exists(
                        Path.Combine(
                            destination,
                            "bin",
                            "java.exe")))
                {
                    MoveJavaRootContents(
                        javaRoot,
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
                        $"Java {major} was extracted but java.exe was not found.");
                }

                WriteLog(
                    $"Java {major} installed: {javaExe}");
            }
            finally
            {
                try
                {
                    if (File.Exists(temp))
                        File.Delete(temp);
                }
                catch
                {
                }
            }
        }

        private static string? FindJavaRoot(
            string destination)
        {
            foreach (string directory in
                     Directory.GetDirectories(destination))
            {
                string java =
                    Path.Combine(
                        directory,
                        "bin",
                        "java.exe");

                if (File.Exists(java))
                    return directory;
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
        // MINECRAFT INSTALL
        // ============================================================

        private bool ValidateMinecraftInstallation(
            string minecraftVersion,
            MinecraftPath minecraftPath,
            string fabricVersion)
        {
            try
            {
                string assets =
                    Path.Combine(
                        _gamePath,
                        "assets");

                string libraries =
                    Path.Combine(
                        _gamePath,
                        "libraries");

                string versions =
                    Path.Combine(
                        _gamePath,
                        "versions");

                string fabricDirectory =
                    Path.Combine(
                        versions,
                        fabricVersion);

                if (!Directory.Exists(libraries))
                    return false;

                if (!Directory.Exists(versions))
                    return false;

                if (!Directory.Exists(fabricDirectory))
                    return false;

                string fabricJson =
                    Path.Combine(
                        fabricDirectory,
                        fabricVersion + ".json");

                if (!File.Exists(fabricJson))
                    return false;

                string loader =
                    FindFabricLoaderJar(
                        _gamePath,
                        fabricVersion);

                if (string.IsNullOrWhiteSpace(loader))
                    return false;

                WriteLog(
                    $"Assets exists: {Directory.Exists(assets)}");

                WriteLog(
                    $"Libraries exists: {Directory.Exists(libraries)}");

                WriteLog(
                    $"Fabric JSON: {fabricJson}");

                WriteLog(
                    $"Fabric loader: {loader}");

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

            LaunchBtn.IsEnabled = false;

            try
            {
                StartLaunchLog();

                ProfileData? profile =
                    GetCurrentProfile();

                string minecraftVersion =
                    profile?.Version ??
                    GetSelectedVersion();

                int ram =
                    Math.Max(
                        2048,
                        (profile?.Ram ??
                         (int)RamSlider.Value) *
                        1024);

                WriteLog(
                    $"Minecraft: {minecraftVersion}");

                WriteLog(
                    $"RAM: {ram} MB");

                WriteLog(
                    $"Game directory: {_gamePath}");

                // ----------------------------------------------------
                // AUTH
                // ----------------------------------------------------

                if (AuthTypeBox.SelectedIndex != 0)
                {
                    throw new InvalidOperationException(
                        "Microsoft login is not enabled in this build. Select Offline mode.");
                }

                string username =
                    UsernameInput.Text.Trim();

                if (string.IsNullOrWhiteSpace(username))
                    username = "TopuPlayer";

                _session =
                    MSession.CreateOfflineSession(
                        username);

                SaveUsername(username);

                AccountUsernameInput.Text =
                    username;

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
                    $"Java path: {javaPath}");

                // ----------------------------------------------------
                // CMLLIB
                // ----------------------------------------------------

                MinecraftPath minecraftPath =
                    new MinecraftPath(
                        _gamePath);

                MinecraftLauncher launcher =
                    new MinecraftLauncher(
                        minecraftPath);

                // ----------------------------------------------------
                // VANILLA
                // ----------------------------------------------------

                StatusText.Text =
                    $"Installing Minecraft {minecraftVersion}...";

                WriteLog(
                    "Installing vanilla Minecraft files...");

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
                    "Minecraft installation completed.");

                // ----------------------------------------------------
                // FABRIC
                // ----------------------------------------------------

                string fabricVersion =
                    await InstallFabricAsync(
                        minecraftVersion,
                        minecraftPath);

                WriteLog(
                    $"Fabric installed: {fabricVersion}");

                // ----------------------------------------------------
                // VALIDATE
                // ----------------------------------------------------

                bool valid =
                    ValidateMinecraftInstallation(
                        minecraftVersion,
                        minecraftPath,
                        fabricVersion);

                if (!valid)
                {
                    throw new InvalidOperationException(
                        "Minecraft/Fabric installation validation failed. Check topu-minecraft.log.");
                }

                // ----------------------------------------------------
                // PERFORMANCE MODS
                // ----------------------------------------------------

                StatusText.Text =
                    "Installing performance mods...";

                await InstallPerformanceModsForLaunchAsync(
                    minecraftVersion);

                // ----------------------------------------------------
                // SESSION
                // ----------------------------------------------------

                if (_session == null)
                {
                    throw new InvalidOperationException(
                        "Minecraft session was not created.");
                }

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

                        GameLauncherName =
                            "Topu Client",

                        GameLauncherVersion =
                            "1.0.0"
                    };

                // ----------------------------------------------------
                // BUILD
                // ----------------------------------------------------

                StatusText.Text =
                    "Building Minecraft process...";

                WriteLog(
                    "Building Minecraft process.");

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

                _minecraftProcess =
                    process;

                try
                {
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
                }
                catch (Exception ex)
                {
                    WriteException(
                        "PROCESS OUTPUT SETUP ERROR",
                        ex);

                    throw;
                }

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

                bool started =
                    process.Start();

                if (!started)
                {
                    throw new InvalidOperationException(
                        "Windows failed to start Minecraft.");
                }

                WriteLog(
                    $"Minecraft started. PID={process.Id}");

                try
                {
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                }
                catch (Exception ex)
                {
                    WriteException(
                        "OUTPUT REDIRECTION ERROR",
                        ex);
                }

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
                    "Minecraft failed to launch.\n\n" +
                    ex.Message +
                    "\n\nThe actual Minecraft output has been written to:\n" +
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

        private async Task InstallPerformanceModsForLaunchAsync(
            string minecraftVersion)
        {
            try
            {
                string modsFolder =
                    GetSelectedModsFolder();

                Directory.CreateDirectory(
                    modsFolder);

                await InstallPerformanceModForLaunchAsync(
                    "sodium",
                    "Sodium",
                    minecraftVersion,
                    modsFolder);

                await InstallPerformanceModForLaunchAsync(
                    "lithium",
                    "Lithium",
                    minecraftVersion,
                    modsFolder);

                await InstallPerformanceModForLaunchAsync(
                    "dynamic-fps",
                    "Dynamic FPS",
                    minecraftVersion,
                    modsFolder);

                await InstallPerformanceModForLaunchAsync(
                    "sodium-extra",
                    "Sodium Extra",
                    minecraftVersion,
                    modsFolder);

                await InstallPerformanceModForLaunchAsync(
                    "krypton",
                    "Krypton",
                    minecraftVersion,
                    modsFolder);
            }
            catch (Exception ex)
            {
                WriteException(
                    "PERFORMANCE MOD INSTALL ERROR",
                    ex);
            }
        }

        private async Task InstallPerformanceModForLaunchAsync(
            string slug,
            string name,
            string minecraftVersion,
            string modsFolder)
        {
            try
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

                if (versions.ValueKind != JsonValueKind.Array ||
                    versions.GetArrayLength() == 0)
                {
                    WriteLog(
                        $"No compatible {name} version for {minecraftVersion}.");

                    return;
                }

                JsonElement? selectedFile = null;

                foreach (JsonElement version in
                         versions.EnumerateArray())
                {
                    if (!version.TryGetProperty(
                        "files",
                        out JsonElement files))
                    {
                        continue;
                    }

                    selectedFile =
                        FindPrimaryJar(files);

                    if (selectedFile != null)
                        break;
                }

                if (selectedFile == null)
                    return;

                JsonElement file =
                    selectedFile.Value;

                string downloadUrl =
                    file.GetProperty("url")
                        .GetString()
                    ?? "";

                string filename =
                    file.GetProperty("filename")
                        .GetString()
                    ?? $"{slug}.jar";

                if (string.IsNullOrWhiteSpace(
                    downloadUrl))
                    return;

                string destination =
                    Path.Combine(
                        modsFolder,
                        SanitizeFileName(filename));

                if (File.Exists(destination))
                {
                    WriteLog(
                        $"Performance mod already installed: {name}");

                    return;
                }

                StatusText.Text =
                    $"Installing {name}...";

                await DownloadFileAsync(
                    downloadUrl,
                    destination);

                WriteLog(
                    $"Performance mod installed: {name}");
            }
            catch (Exception ex)
            {
                WriteLog(
                    $"Optional performance mod failed: {name} - {ex.Message}");
            }
        }

        // ============================================================
        // MINECRAFT OUTPUT
        // ============================================================

        private void Minecraft_OutputDataReceived(
            object sender,
            DataReceivedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(e.Data))
                return;

            AppendRawGameOutput(
                "[MC]",
                e.Data);
        }

        private void Minecraft_ErrorDataReceived(
            object sender,
            DataReceivedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(e.Data))
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

                if (exitCode != 0)
                {
                    AppendGameLog(
                        "Minecraft did not exit normally.");

                    AppendGameLog(
                        "The [MC] and [MC-ERR] lines contain the actual Minecraft output.");
                }

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
                                $"Minecraft crashed (exit code {exitCode}). Check the log.";
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

                _minecraftProcess = null;
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
                Path.GetDirectoryName(destination);

            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

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
                using HttpResponseMessage response =
                    await Http.GetAsync(
                        url,
                        HttpCompletionOption.ResponseHeadersRead);

                response.EnsureSuccessStatusCode();

                await using Stream input =
                    await response.Content.ReadAsStreamAsync();

                await using FileStream output =
                    new FileStream(
                        temporary,
                        FileMode.Create,
                        FileAccess.Write,
                        FileShare.None,
                        81920,
                        FileOptions.Asynchronous);

                await input.CopyToAsync(output);

                await output.FlushAsync();

                if (File.Exists(destination))
                    File.Delete(destination);

                File.Move(
                    temporary,
                    destination);
            }
            catch
            {
                try
                {
                    if (File.Exists(temporary))
                        File.Delete(temporary);
                }
                catch
                {
                }

                throw;
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

                text.AppendLine();

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
    }
}
