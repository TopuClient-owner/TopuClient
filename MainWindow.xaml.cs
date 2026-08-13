using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.Version;

namespace TopuLauncher
{
    public partial class MainWindow : Window
    {
        private MSession? _session;
        private static readonly HttpClient _httpClient = new HttpClient
        {
            DefaultRequestHeaders = { { "User-Agent", "TopuClient/1.0 (Windows NT 10.0; Win64; x64)" } }
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

        private void LoadSavedUsername()
        {
            try
            {
                if (File.Exists(_configFilePath))
                {
                    string savedUser = File.ReadAllText(_configFilePath).Trim();
                    if (!string.IsNullOrEmpty(savedUser) && UsernameInput != null)
                    {
                        UsernameInput.Text = savedUser;
                    }
                }
            }
            catch { }
        }

        private void SaveUsername(string username)
        {
            try
            {
                File.WriteAllText(_configFilePath, username);
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
            string selectedVer = (VersionBox.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "1.21.1";
            SelectedProfileLabel.Text = $"Ready to launch Fabric {selectedVer}";
            StatusText.Text = $"Profile saved: Fabric {selectedVer} with {(int)RamSlider.Value}GB RAM";
            
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
            MessageBox.Show("For now, test using Offline / Cracked mode!", "Topu Client Auth", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void JoinServer_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string serverIp)
            {
                StatusText.Text = $"Target server queued: {serverIp}";
            }
        }

        private async void LaunchBtn_Click(object sender, RoutedEventArgs e)
        {
            LaunchBtn.IsEnabled = false;

            try
            {
                string gamePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".topuclient");
                var path = new MinecraftPath(gamePath);
                
                string targetVer = (VersionBox.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "1.21.1";

                string username = string.IsNullOrWhiteSpace(UsernameInput.Text) ? "TopuPlayer" : UsernameInput.Text.Trim();
                _session = MSession.GetOfflineSession(username);
                SaveUsername(username);

                StatusText.Text = "Downloading performance mods...";
                string modsFolder = Path.Combine(gamePath, "mods");
                await EnsureEssentialModsDownloaded(modsFolder);

                StatusText.Text = $"Setting up Fabric for {targetVer}...";
                string fabricVersionName = await InstallFabricProfileAsync(gamePath, targetVer);

                var launcher = new CMLauncher(path);

                // Fetch full MVersion object cleanly
                StatusText.Text = $"Ensuring base Minecraft {targetVer} is installed...";
                var vanillaVersion = await launcher.GetVersionAsync(targetVer);
                if (vanillaVersion != null)
                {
                    await launcher.CheckAndDownloadAsync(vanillaVersion);
                }

                StatusText.Text = "Starting game process...";
                int allocatedRamMb = (int)RamSlider.Value * 1024;

                var launchOption = new MLaunchOption
                {
                    Session = _session,
                    MaximumRamMb = allocatedRamMb
                };

                var process = await launcher.CreateProcessAsync(fabricVersionName, launchOption);

                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.UseShellExecute = false;

                process.Start();

                string errorOutput = await process.StandardError.ReadToEndAsync();
                
                if (!string.IsNullOrEmpty(errorOutput) && errorOutput.Contains("Exception"))
                {
                    MessageBox.Show($"Minecraft Error Output:\n\n{errorOutput}", "Game Crashed", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                StatusText.Text = $"Topu Client ({fabricVersionName}) running as {username}!";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Launch Error: {ex.Message}";
                MessageBox.Show($"Failed to launch:\n\n{ex.ToString()}", "Topu Client Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                LaunchBtn.IsEnabled = true;
            }
        }

        private async Task<string> InstallFabricProfileAsync(string gamePath, string mcVersion)
        {
            string fabricVersionId = $"fabric-loader-0.16.0-{mcVersion}";
            string versionFolder = Path.Combine(gamePath, "versions", fabricVersionId);
            string jsonFile = Path.Combine(versionFolder, $"{fabricVersionId}.json");

            if (!File.Exists(jsonFile))
            {
                Directory.CreateDirectory(versionFolder);
                string apiUrl = $"https://meta.fabricmc.net/v2/versions/loader/{mcVersion}/0.16.0/profile/json";
                string jsonContent = await _httpClient.GetStringAsync(apiUrl);
                await File.WriteAllTextAsync(jsonFile, jsonContent);
            }

            return fabricVersionId;
        }

        private async Task EnsureEssentialModsDownloaded(string modsFolder)
        {
            Directory.CreateDirectory(modsFolder);

            var coreMods = new (string Name, string Url)[]
            {
                ("fabric-api.jar", "https://cdn.modrinth.com/data/P7R216yC/versions/zG4CqI6T/fabric-api-0.102.0%2B1.21.1.jar"),
                ("sodium.jar", "https://cdn.modrinth.com/data/AANAdA4C/versions/zG94D8J5/sodium-fabric-0.5.11%2Bmc1.21.1.jar"),
                ("lithium.jar", "https://cdn.modrinth.com/data/gvQqBU10/versions/K3Kz5TjZ/lithium-fabric-0.13.0%2Bmc1.21.1.jar"),
                ("ferritecore.jar", "https://cdn.modrinth.com/data/u62m2qw5/versions/4U7N10wz/ferritecore-7.0.0-fabric.jar"),
                ("sodium-extra.jar", "https://cdn.modrinth.com/data/1eAoo2A1/versions/0.5.7%2Bmc1.21.1/sodium-extra-0.5.7%2Bmc1.21.1.jar"),
                ("dynamic-fps.jar", "https://cdn.modrinth.com/data/10000001/versions/3.6.3%2B1.21.1/dynamic_fps-3.6.3%2B1.21.1-fabric.jar")
            };

            foreach (var mod in coreMods)
            {
                string destination = Path.Combine(modsFolder, mod.Name);
                if (!File.Exists(destination) || new FileInfo(destination).Length == 0)
                {
                    try
                    {
                        using (var response = await _httpClient.GetAsync(mod.Url, HttpCompletionOption.ResponseHeadersRead))
                        {
                            response.EnsureSuccessStatusCode();
                            byte[] data = await response.Content.ReadAsByteArrayAsync();
                            await File.WriteAllBytesAsync(destination, data);
                        }
                    }
                    catch { }
                }
            }
        }
    }
}
