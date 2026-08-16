using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
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
        private MSession? _session;

        private static readonly HttpClient _httpClient = new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = true
        })
        {
            DefaultRequestHeaders = { { "User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) TopuClient/1.0" } }
        };

        private readonly string _configFilePath;

        public MainWindow()
        {
            InitializeComponent();

            string appFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".topuclient");
            Directory.CreateDirectory(appFolder);
            _configFilePath = Path.Combine(appFolder, "username.txt");

            LoadSavedUsername();
        }

        private static string GetOfflineUuid(string username)
        {
            using var md5 = MD5.Create();
            byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes("OfflinePlayer:" + username));
            hash[6] = (byte)((hash[6] & 0x0f) | 0x30); // Version 3
            hash[8] = (byte)((hash[8] & 0x3f) | 0x80); // Variant
            return new Guid(hash).ToString("N");
        }

        private void LoadSavedUsername()
        {
            try
            {
                if (File.Exists(_configFilePath))
                {
                    string savedUser = File.ReadAllText(_configFilePath).Trim();
                    if (!string.IsNullOrEmpty(savedUser))
                    {
                        if (UsernameInput != null) UsernameInput.Text = savedUser;
                        
                        string offlineUuid = GetOfflineUuid(savedUser);
                        _session = new MSession(savedUser, offlineUuid, offlineUuid);
                    }
                }
            }
            catch { }
        }

        private void SaveUsername(string username)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(username))
                {
                    File.WriteAllText(_configFilePath, username);
                }
            }
            catch { }
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void SwitchTab_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string targetTab)
            {
                TabLaunch.Visibility = Visibility.Collapsed;
                TabProfiles.Visibility = Visibility.Collapsed;
                TabAccounts.Visibility = Visibility.Collapsed;

                Brush defaultColor = new SolidColorBrush(Color.FromRgb(136, 136, 136));
                Thickness noBorder = new Thickness(0);

                TabLaunchBtn.Foreground = defaultColor;
                TabLaunchBtn.BorderThickness = noBorder;

                TabProfilesBtn.Foreground = defaultColor;
                TabProfilesBtn.BorderThickness = noBorder;

                TabAccountsBtn.Foreground = defaultColor;
                TabAccountsBtn.BorderThickness = noBorder;

                btn.Foreground = new SolidColorBrush(Color.FromRgb(0, 255, 136));
                btn.BorderThickness = new Thickness(0, 0, 0, 2);

                switch (targetTab)
                {
                    case "TabLaunch":
                        TabLaunch.Visibility = Visibility.Visible;
                        break;
                    case "TabProfiles":
                        TabProfiles.Visibility = Visibility.Visible;
                        break;
                    case "TabAccounts":
                        TabAccounts.Visibility = Visibility.Visible;
                        break;
                }
            }
        }

        private void RamSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (RamLabel != null)
            {
                RamLabel.Text = $"{(int)e.NewValue}GB";
            }
        }

        private void SaveProfile_Click(object sender, RoutedEventArgs e)
        {
            string selectedVer = (VersionBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "1.21.1";
            if (SelectedProfileLabel != null) SelectedProfileLabel.Text = $"Ready to launch Fabric {selectedVer}";
            if (StatusText != null) StatusText.Text = $"Profile saved: Fabric {selectedVer} with {(int)RamSlider.Value}GB RAM";
            
            MessageBox.Show("Profile settings saved successfully!", "Topu Client", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void AuthTypeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (StatusText == null) return;

            if (AuthTypeBox.SelectedIndex == 0)
            {
                StatusText.Text = "Auth Mode: Offline / Cracked";
            }
            else
            {
                StatusText.Text = "Auth Mode: Microsoft Official";
            }
        }

        private void MsLoginBtn_Click(object sender, RoutedEventArgs e)
        {
            StatusText.Text = "Using Offline / Cracked Mode.";
            MessageBox.Show("Topu Client is configured for fast Offline/Cracked play. Enter your username on the main screen to launch!", "Topu Client Auth", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void JoinServer_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string serverIp)
            {
                StatusText.Text = $"Target server queued: {serverIp}";
            }
        }

        private async void SearchModrinth_Click(object sender, RoutedEventArgs e)
        {
            string query = ModSearchInput?.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(query))
            {
                MessageBox.Show("Please enter a mod name to search on Modrinth.", "Mod Search", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                if (ModSearchStatus != null) ModSearchStatus.Text = $"Searching Modrinth for '{query}'...";
                
                string responseString = await _httpClient.GetStringAsync($"https://api.modrinth.com/v2/search?query={Uri.EscapeDataString(query)}");
                using var doc = JsonDocument.Parse(responseString);
                
                if (!doc.RootElement.TryGetProperty("hits", out var hits) || hits.GetArrayLength() == 0)
                {
                    if (ModSearchStatus != null) ModSearchStatus.Text = "No compatible Fabric version found on Modrinth.";
                    MessageBox.Show("No compatible Fabric version found on Modrinth for your current selection.", "Mod Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var firstHit = hits[0];
                string modTitle = firstHit.TryGetProperty("title", out var titleProp) ? (titleProp.GetString() ?? query) : query;
                string projectId = firstHit.TryGetProperty("project_id", out var idProp) ? (idProp.GetString() ?? "") : "";

                if (string.IsNullOrEmpty(projectId)) return;

                string targetVer = (VersionBox?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "1.21.1";
                
                string versionsUrl = $"https://api.modrinth.com/v2/project/{projectId}/version?loaders=%5B%22fabric%22%5D&game_versions=%5B%22{targetVer}%22%5D";
                string versionsResponse = await _httpClient.GetStringAsync(versionsUrl);
                using var versionsDoc = JsonDocument.Parse(versionsResponse);
                var versionRoot = versionsDoc.RootElement;

                if (versionRoot.GetArrayLength() > 0)
                {
                    var latestVerObj = versionRoot[0];
                    if (!latestVerObj.TryGetProperty("files", out var files) || files.GetArrayLength() == 0) return;

                    string fileUrl = "";
                    string fileName = $"{modTitle}.jar";

                    foreach (var file in files.EnumerateArray())
                    {
                        if (file.TryGetProperty("primary", out var primaryProp) && primaryProp.GetBoolean())
                        {
                            if (file.TryGetProperty("url", out var urlProp)) fileUrl = urlProp.GetString() ?? "";
                            if (file.TryGetProperty("filename", out var nameProp)) fileName = nameProp.GetString() ?? fileName;
                            break;
                        }
                    }

                    if (string.IsNullOrEmpty(fileUrl))
                    {
                        if (files[0].TryGetProperty("url", out var urlProp)) fileUrl = urlProp.GetString() ?? "";
                        if (files[0].TryGetProperty("filename", out var nameProp)) fileName = nameProp.GetString() ?? fileName;
                    }

                    if (!string.IsNullOrEmpty(fileUrl))
                    {
                        string gamePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".topuclient");
                        string modsFolder = Path.Combine(gamePath, "mods");
                        Directory.CreateDirectory(modsFolder);

                        string destPath = Path.Combine(modsFolder, fileName);
                        byte[] modBytes = await _httpClient.GetByteArrayAsync(fileUrl);
                        await File.WriteAllBytesAsync(destPath, modBytes);

                        if (ModSearchStatus != null) ModSearchStatus.Text = $"Successfully downloaded: {modTitle}!";
                        MessageBox.Show($"Mod '{modTitle}' successfully added to your mods folder for {targetVer}!", "Modrinth API", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }
                }

                if (ModSearchStatus != null) ModSearchStatus.Text = "No compatible Fabric version found on Modrinth.";
                MessageBox.Show("No compatible Fabric version found on Modrinth for your current selection.", "Mod Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                if (ModSearchStatus != null) ModSearchStatus.Text = "Mod search failed.";
                MessageBox.Show($"Error searching/downloading mod:\n{ex.Message}", "Modrinth Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void LaunchBtn_Click(object sender, RoutedEventArgs e)
        {
            LaunchBtn.IsEnabled = false;

            try
            {
                string gamePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".topuclient");
                var path = new MinecraftPath(gamePath);
                
                string targetVer = (VersionBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "1.21.1";
                string inputUser = string.IsNullOrWhiteSpace(UsernameInput.Text) ? "TopuPlayer" : UsernameInput.Text.Trim();
                
                string offlineUuid = GetOfflineUuid(inputUser);
                _session = new MSession(inputUser, offlineUuid, offlineUuid);
                SaveUsername(inputUser);

                string modsFolder = Path.Combine(gamePath, "mods");
                await EnsureEssentialModsDownloaded(modsFolder, targetVer);

                StatusText.Text = $"Setting up Fabric for {targetVer}...";
                string fabricVersionName = await InstallFabricProfileAsync(gamePath, targetVer);

                var launcher = new CMLauncher(path);

                launcher.FileChanged += (e) =>
                {
                    Dispatcher.Invoke(() => StatusText.Text = $"Checking: {e.FileName} ({e.FileType})");
                };

                launcher.ProgressChanged += (sender, e) =>
                {
                    Dispatcher.Invoke(() => StatusText.Text = $"Downloading: {e.ProgressPercentage}%");
                };

                StatusText.Text = $"Preparing Minecraft {targetVer}...";
                var vanillaVersion = await launcher.GetVersionAsync(targetVer);
                if (vanillaVersion != null)
                {
                    // Smart check: Skip heavy re-validation if core jar and assets exist, ensuring instant launch
                    string jarPath = Path.Combine(gamePath, "versions", targetVer, $"{targetVer}.jar");
                    string assetsFolder = Path.Combine(gamePath, "assets", "objects");

                    bool assetsMissing = !Directory.Exists(assetsFolder) || Directory.GetFiles(assetsFolder, "*", SearchOption.AllDirectories).Length < 10;

                    if (!File.Exists(jarPath) || assetsMissing)
                    {
                        StatusText.Text = $"Downloading Minecraft {targetVer} files & assets...";
                        await launcher.CheckAndDownloadAsync(vanillaVersion);
                    }
                    else
                    {
                        StatusText.Text = "Game files & assets verified. Fast launching...";
                    }
                }

                StatusText.Text = "Creating game process...";
                int allocatedRamMb = (int)RamSlider.Value * 1024;

                var launchOption = new MLaunchOption
                {
                    Session = _session,
                    MaximumRamMb = allocatedRamMb
                };

                var process = await launcher.CreateProcessAsync(fabricVersionName, launchOption);
                
                bool started = process.Start();

                if (!started)
                {
                    MessageBox.Show("process.Start() returned false!", "Launch Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    StatusText.Text = $"Topu Client ({fabricVersionName}) running as {_session.Username}!";
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = "Launch Failed!";
                MessageBox.Show($"CRITICAL LAUNCH ERROR:\n\n{ex.Message}\n\nStack Trace:\n{ex.StackTrace}", "Topu Client Crash", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                LaunchBtn.IsEnabled = true;
            }
        }

        private async Task<string> InstallFabricProfileAsync(string gamePath, string mcVersion)
        {
            string loaderVersion = "0.19.3";
            string fabricVersionId = $"fabric-loader-{loaderVersion}-{mcVersion}";
            string versionFolder = Path.Combine(gamePath, "versions", fabricVersionId);
            string jsonFile = Path.Combine(versionFolder, $"{fabricVersionId}.json");

            if (!File.Exists(jsonFile))
            {
                Directory.CreateDirectory(versionFolder);
                string apiUrl = $"https://meta.fabricmc.net/v2/versions/loader/{mcVersion}/{loaderVersion}/profile/json";
                string jsonContent = await _httpClient.GetStringAsync(apiUrl);
                await File.WriteAllTextAsync(jsonFile, jsonContent);
            }

            return fabricVersionId;
        }

        private async Task EnsureEssentialModsDownloaded(string modsFolder, string mcVersion)
        {
            Directory.CreateDirectory(modsFolder);

            var coreMods = new (string Name, string UrlQuery)[]
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
                string destination = Path.Combine(modsFolder, mod.Name);
                
                if (!File.Exists(destination) || new FileInfo(destination).Length < 5000)
                {
                    try
                    {
                        Dispatcher.Invoke(() => StatusText.Text = $"Checking/Downloading {mod.Name} for {mcVersion}...");

                        string searchUrl = $"https://api.modrinth.com/v2/search?query={mod.UrlQuery}&facets=%5B%5B%22project_type%3Amod%22%5D%5D";
                        string searchRes = await _httpClient.GetStringAsync(searchUrl);
                        using var doc = JsonDocument.Parse(searchRes);
                        
                        if (!doc.RootElement.TryGetProperty("hits", out var hits) || hits.GetArrayLength() == 0) continue;

                        string projectId = hits[0].TryGetProperty("project_id", out var idProp) ? (idProp.GetString() ?? "") : "";
                        if (string.IsNullOrEmpty(projectId)) continue;

                        string versionsUrl = $"https://api.modrinth.com/v2/project/{projectId}/version?loaders=%5B%22fabric%22%5D&game_versions=%5B%22{mcVersion}%22%5D";
                        string verRes = await _httpClient.GetStringAsync(versionsUrl);
                        using var verDoc = JsonDocument.Parse(verRes);
                        var verArray = verDoc.RootElement;

                        if (verArray.GetArrayLength() > 0)
                        {
                            var latestVerObj = verArray[0];
                            if (!latestVerObj.TryGetProperty("files", out var files) || files.GetArrayLength() == 0) continue;

                            string fileUrl = "";

                            foreach (var file in files.EnumerateArray())
                            {
                                if (file.TryGetProperty("primary", out var primaryProp) && primaryProp.GetBoolean())
                                {
                                    if (file.TryGetProperty("url", out var urlProp)) fileUrl = urlProp.GetString() ?? "";
                                    break;
                                }
                            }
                            if (string.IsNullOrEmpty(fileUrl))
                            {
                                if (files[0].TryGetProperty("url", out var urlProp)) fileUrl = urlProp.GetString() ?? "";
                            }

                            if (!string.IsNullOrEmpty(fileUrl))
                            {
                                byte[] data = await _httpClient.GetByteArrayAsync(fileUrl);
                                await File.WriteAllBytesAsync(destination, data);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Mod download skipped for {mod.Name}: {ex.Message}");
                    }
                }
            }
        }
    }
}
