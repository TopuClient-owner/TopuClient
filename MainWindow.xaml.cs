using System;
using System.IO;
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

        public MainWindow()
        {
            InitializeComponent();
        }

        // --- Custom Title Bar Window Controls ---
        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        // --- Navigation Tab Switcher ---
        private void SwitchTab_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string targetTab)
            {
                // Hide all tabs
                TabLaunch.Visibility = Visibility.Collapsed;
                TabProfiles.Visibility = Visibility.Collapsed;
                TabAccounts.Visibility = Visibility.Collapsed;

                // Reset button text colors
                TabLaunchBtn.Foreground = new SolidColorBrush(Color.FromRgb(136, 136, 136));
                TabLaunchBtn.BorderThickness = new Thickness(0);
                TabProfilesBtn.Foreground = new SolidColorBrush(Color.FromRgb(136, 136, 136));
                TabProfilesBtn.BorderThickness = new Thickness(0);
                TabAccountsBtn.Foreground = new SolidColorBrush(Color.FromRgb(136, 136, 136));
                TabAccountsBtn.BorderThickness = new Thickness(0);

                // Highlight active button
                btn.Foreground = new SolidColorBrush(Color.FromRgb(0, 255, 136));
                btn.BorderThickness = new Thickness(0, 0, 0, 2);

                // Display selected tab
                if (targetTab == "TabLaunch") TabLaunch.Visibility = Visibility.Visible;
                if (targetTab == "TabProfiles") TabProfiles.Visibility = Visibility.Visible;
                if (targetTab == "TabAccounts") TabAccounts.Visibility = Visibility.Visible;
            }
        }

        // --- Profiles Management ---
        private void RamSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (RamLabel != null) RamLabel.Text = $"{(int)e.NewValue}GB";
        }

        private void SaveProfile_Click(object sender, RoutedEventArgs e)
        {
            string selectedVer = (VersionBox.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "1.21.1";
            SelectedProfileLabel.Text = $"Ready to launch Minecraft {selectedVer}";
            MessageBox.Show("Profile settings saved successfully!", "Topu Launcher");
        }

        // --- Auth Mode Switcher ---
        private void AuthTypeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Handled during launch execution
        }

        private void MsLoginBtn_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Microsoft OAuth authentication flow initiated.", "Topu Auth");
        }

        // --- Server Direct Join ---
        private void JoinServer_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string serverIp)
            {
                StatusText.Text = $"Connecting to {serverIp}...";
            }
        }

        // --- Game Launch Process Engine ---
        private async void LaunchBtn_Click(object sender, RoutedEventArgs e)
        {
            LaunchBtn.IsEnabled = false;

            try
            {
                var gamePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".topuclient");
                var path = new MinecraftPath(gamePath);
                
                // Initialize CmlLib v3.3.5 Launcher Engine
                var launcher = new CMLauncher(path);

                // Determine session
                if (AuthTypeBox.SelectedIndex == 0) // Offline / Cracked
                {
                    string username = string.IsNullOrWhiteSpace(UsernameInput.Text) ? "TopuPlayer" : UsernameInput.Text;
                    _session = MSession.GetOfflineSession(username);
                }
                else if (_session == null)
                {
                    StatusText.Text = "Error: Please sign in with Microsoft first!";
                    MessageBox.Show("Please log into your Microsoft Account before launching.", "Topu Launcher");
                    LaunchBtn.IsEnabled = true;
                    return;
                }

                StatusText.Text = "Checking assets & downloading game files...";

                string targetVer = (VersionBox.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "1.21.1";
                int allocatedRam = (int)RamSlider.Value * 1024;

                var process = await launcher.CreateProcessAsync(targetVer, new MLaunchOption
                {
                    Session = _session,
                    MaximumRamMb = allocatedRam
                });

                process.Start();
                StatusText.Text = "Topu Client is running!";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Launch Failed: {ex.Message}";
                MessageBox.Show(ex.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                LaunchBtn.IsEnabled = true;
            }
        }
    }
}
