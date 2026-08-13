using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.Auth.Microsoft;

namespace TopuLauncher
{
    public partial class MainWindow : Window
    {
        private MSession? _session;

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
                // Collapse all tabs
                TabLaunch.Visibility = Visibility.Collapsed;
                TabProfiles.Visibility = Visibility.Collapsed;
                TabAccounts.Visibility = Visibility.Collapsed;

                // Reset button styles to default dark gray
                Brush defaultColor = new SolidColorBrush(Color.FromRgb(136, 136, 136));
                Thickness noBorder = new Thickness(0);

                TabLaunchBtn.Foreground = defaultColor;
                TabLaunchBtn.BorderThickness = noBorder;

                TabProfilesBtn.Foreground = defaultColor;
                TabProfilesBtn.BorderThickness = noBorder;

                TabAccountsBtn.Foreground = defaultColor;
                TabAccountsBtn.BorderThickness = noBorder;

                // Highlight clicked button in Topu Green (#00FF88)
                btn.Foreground = new SolidColorBrush(Color.FromRgb(0, 255, 136));
                btn.BorderThickness = new Thickness(0, 0, 0, 2);

                // Show active view
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
            StatusText.Text = $"Profile set to Minecraft {selectedVer} with {(int)RamSlider.Value}GB RAM";
            
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

        private async void MsLoginBtn_Click(object sender, RoutedEventArgs e)
        {
            MsLoginBtn.IsEnabled = false;
            StatusText.Text = "Initiating Microsoft Login...";

            try
            {
                var loginHandler = JELoginHandlerBuilder.BuildDefault();

                var session = await loginHandler.AuthenticateInteractively();

                if (session != null && session.CheckIsValid())
                {
                    _session = session;
                    MsAccountName.Text = session.Username;
                    AuthTypeBox.SelectedIndex = 1; // Switch dropdown to Microsoft
                    StatusText.Text = $"Signed in as {session.Username}";
                    MessageBox.Show($"Successfully authenticated as {session.Username}!", "Topu Client", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    StatusText.Text = "Microsoft Auth failed or canceled.";
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Auth Error: {ex.Message}";
                MessageBox.Show($"Microsoft Auth Exception:\n{ex.Message}", "Auth Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                MsLoginBtn.IsEnabled = true;
            }
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
        // 6. GAME LAUNCH ENGINE
        // ==========================================

        private async void LaunchBtn_Click(object sender, RoutedEventArgs e)
        {
            LaunchBtn.IsEnabled = false;

            try
            {
                // Custom Topu Client game directory (.topuclient in AppData)
                string gamePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".topuclient");
                var path = new MinecraftPath(gamePath);
                
                var launcher = new MinecraftLauncher(path);

                // Check active authentication mode
                if (AuthTypeBox.SelectedIndex == 0) // Offline / Cracked
                {
                    string username = string.IsNullOrWhiteSpace(UsernameInput.Text) ? "TopuPlayer" : UsernameInput.Text.Trim();
                    _session = MSession.CreateOfflineSession(username);
                }
                else // Microsoft Account
                {
                    if (_session == null || !_session.CheckIsValid())
                    {
                        StatusText.Text = "Please sign in with Microsoft first!";
                        MessageBox.Show("Please click 'Add Account' and sign into your Microsoft account first.", "Auth Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                        LaunchBtn.IsEnabled = true;
                        return;
                    }
                }

                StatusText.Text = "Preparing game assets and downloading files...";

                // Get selected version and RAM allocation
                string targetVer = (VersionBox.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "1.21.1";
                int allocatedRamMb = (int)RamSlider.Value * 1024;

                // Configure MLaunchOption directly from CmlLib.Core
                var launchOption = new MLaunchOption
                {
                    Session = _session,
                    MaximumRamMb = allocatedRamMb
                };

                // Create and start process
                var process = await launcher.CreateProcessAsync(targetVer, launchOption);
                process.Start();

                StatusText.Text = $"Topu Client is running ({targetVer})!";
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
    }
}
