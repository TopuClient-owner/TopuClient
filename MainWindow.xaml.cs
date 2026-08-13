using System;
using System.Collections.ObjectModel;
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

        // --- Auth Mode Switcher (Cracked vs Microsoft) ---
        private void AuthTypeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CrackedPanel == null || PremiumPanel == null) return;

            if (AuthTypeBox.SelectedIndex == 0) // Cracked
            {
                CrackedPanel.Visibility = Visibility.Visible;
                PremiumPanel.Visibility = Visibility.Collapsed;
            }
            else // Microsoft Premium
            {
                CrackedPanel.Visibility = Visibility.Collapsed;
                PremiumPanel.Visibility = Visibility.Visible;
            }
        }

        // --- Microsoft Login Action ---
        private void MsLoginBtn_Click(object sender, RoutedEventArgs e)
        {
            StatusText.Text = "Microsoft OAuth setup: Redirecting to browser...";
            MessageBox.Show("Microsoft OAuth authentication step.", "Topu Launcher");
        }

        // --- Game Launch Process Engine ---
        private async void LaunchBtn_Click(object sender, RoutedEventArgs e)
        {
            LaunchBtn.IsEnabled = false;

            try
            {
                // Custom Application directory in AppData (.topuclient)
                var gamePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".topuclient");
                var path = new MinecraftPath(gamePath);
                
                // Initialize v4 Launcher Engine
                var launcher = new MinecraftLauncher(path);

                // Check Session Type
                if (AuthTypeBox.SelectedIndex == 0)
                {
                    // Generate Offline / Cracked Session
                    string username = string.IsNullOrWhiteSpace(UsernameInput.Text) ? "TopuPlayer" : UsernameInput.Text;
                    _session = MSession.CreateOfflineSession(username);
                }
                else if (_session == null)
                {
                    StatusText.Text = "Error: Please sign in with Microsoft first!";
                    MessageBox.Show("Please log into your Microsoft Account before launching.", "Topu Launcher");
                    LaunchBtn.IsEnabled = true;
                    return;
                }

                StatusText.Text = "Checking assets & downloading game files...";

                // Install and build process for target version 1.21.1
                var process = await launcher.CreateProcessAsync("1.21.1", new MLaunchOption
                {
                    Session = _session,
                    MaximumRamMb = 4096 // Allocates 4GB RAM to game process
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
