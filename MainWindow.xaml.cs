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
using CmlLib.Core.Installer.FabricMC;

namespace TopuLauncher
{
    public partial class MainWindow : Window
    {
        private MSession? _session;
        private static readonly HttpClient _httpClient = new HttpClient();

        public MainWindow()
        {
            InitializeComponent();
        }

        // ==========================================
        // 1. WINDOW TITLE BAR CONTROLS
        // ==========================================

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

        // ==========================================
        // 2. NAVIGATION TAB SWITCHER
        // ==========================================

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

        // ==========================================
        // 3. PROFILES & MODS MANAGEMENT
        // ==========================================

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
            SelectedProfileLabel.Text = $"Ready to launch Minecraft {selectedVer}";
            StatusText.Text = $"Profile set to Fabric {selectedVer} with {(int)RamSlider.Value}GB RAM";
            
            MessageBox.Show("Profile settings saved successfully!", "Topu Client", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ==========================================
        // 4. ACCOUNTS & AUTHENTICATION
        // ==========================================

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
            MessageBox.Show(
                "Microsoft Login requires browser authentication.\nFor now, test using Offline mode while launching!", 
                "Topu Client Auth", 
                MessageBoxButton.OK, 
                MessageBoxImage.Information
            );
        }

        // ==========================================
        // 5. SERVER DIRECT JOIN
        // ==========================================

        private void JoinServer_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string serverIp)
            {
                StatusText.Text = $"Target server queued: {serverIp}";
                MessageBox.Show($"Selected server: {serverIp}\nLaunch game to connect!", "Partnered Servers", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        // ==========================================
        // 6. FABRIC & GAME LAUNCH ENGINE
        // ==========================================

        private async void LaunchBtn_Click(object sender, RoutedEventArgs e)
        {
            LaunchBtn.IsEnabled = false;

            try
            {
                string gamePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".topuclient");
                var path = new MinecraftPath(gamePath);
                var launcher = new CMLauncher(path);

                // Setup Session
                if (AuthTypeBox.SelectedIndex == 0)
                {
                    string username = string.IsNullOrWhiteSpace(UsernameInput.Text) ? "TopuPlayer" : UsernameInput.Text.Trim();
                    _session = MSession.GetOfflineSession(username);
                }
                else
                {
                    if (_session == null || !_session.CheckIsValid())
                    {
                        StatusText.Text = "Please set an active session first!";
                        MessageBox.Show("Please select Offline mode or sign in.", "Auth Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                        LaunchBtn.IsEnabled = true;
                        return;
                    }
                }

                string targetVer = (VersionBox.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "1.21.1";

                // --- 1. INSTALL FABRIC LOADER ---
                StatusText.Text = $"Fetching Fabric Loader metadata for {targetVer}...";
                var fabricVersionLoader = new FabricVersionLoader();
                var fabricVersions = await fabricVersionLoader.GetVersionMetadatasAsync();
                var fabricVersion = fabricVersions.GetVersionMetadata(targetVer);

                StatusText.Text = $"Installing Fabric Loader for Minecraft {targetVer}...";
                string installedFabricVersion = await fabricVersion.InstallAsync(path);

                // --- 2. DOWNLOAD ESSENTIAL OPTIMIZATION MODS ---
                StatusText.Text = "Ensuring performance mods exist...";
                string modsFolder = Path.Combine(gamePath, "mods");
                Directory.CreateDirectory(modsFolder);

                await EnsureEssentialModsDownloaded(modsFolder);

                // --- 3. LAUNCH GAME ---
                StatusText.Text = "Downloading Minecraft assets & starting game...";
                int allocatedRamMb = (int)RamSlider.Value * 1024;

                var launchOption = new MLaunchOption
                {
                    Session = _session,
                    MaximumRamMb = allocatedRamMb
                };

                var process = await launcher.CreateProcessAsync(installedFabricVersion, launchOption);
                process.Start();

                StatusText.Text = $"Topu Client (Fabric {targetVer}) is running!";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Launch Error: {ex.Message}";
                MessageBox.Show($"Failed to launch Minecraft:\n{ex.Message}", "Launch Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                LaunchBtn.IsEnabled = true;
            }
        }

        private async Task EnsureEssentialModsDownloaded(string modsFolder)
        {
            // Direct CDN URLs for core optimization mods (1.21.1 Fabric)
            var coreMods = new (string Name, string Url)[]
            {
                ("sodium.jar", "https://cdn.modrinth.com/data/AANAdA4C/versions/zG94D8J5/sodium-fabric-0.5.11%2Bmc1.21.1.jar"),
                ("iris.jar", "https://cdn.modrinth.com/data/YLSE12W8/versions/J58PAnS6/iris-1.7.3%2B1.21.1-fabric.jar"),
                ("lithium.jar", "https://cdn.modrinth.com/data/gvQqBU10/versions/K3Kz5TjZ/lithium-fabric-0.13.0%2Bmc1.21.1.jar"),
                ("ferritecore.jar", "https://cdn.modrinth.com/data/u62m2qw5/versions/4U7N10wz/ferritecore-7.0.0-fabric.jar")
            };

            foreach (var mod in coreMods)
            {
                string destination = Path.Combine(modsFolder, mod.Name);
                if (!File.Exists(destination))
                {
                    try
                    {
                        byte[] data = await _httpClient.GetByteArrayAsync(mod.Url);
                        await File.WriteAllBytesAsync(destination, data);
                    }
                    catch
                    {
                        // Ignore individual mod download failures to prevent crash
                    }
                }
            }
        }
    }
}
