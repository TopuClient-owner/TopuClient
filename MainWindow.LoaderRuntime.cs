using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CmlLib.Core;
using CmlLib.Core.Installer.Forge;
using CmlLib.Core.ProcessBuilder;

namespace TopuLauncher
{
    public partial class MainWindow
    {
        private static readonly string[] RuntimeVanillaVersions =
        {
            "1.8.9", "1.20.1", "1.21.1", "1.21.2", "1.21.4", "1.21.5", "1.21.8", "1.21.11", "26.1.2", "26.2"
        };

        private static readonly string[] RuntimeFabricVersions =
        {
            "1.21.1", "1.21.4", "1.21.8", "1.21.11", "26.1.2", "26.2"
        };

        private static readonly string[] RuntimeForgeVersions =
        {
            "1.20.1", "1.8.9"
        };

        private static readonly string[] RuntimeQuiltVersions =
        {
            "1.20.6", "1.21"
        };

        private static readonly string[] RuntimeNeoForgeVersions =
        {
            "1.21.1", "1.21.4", "1.21.8", "1.21.11", "26.1.2", "26.2"
        };

        private ComboBox? _loaderBox;
        private bool _runtimeUiReady;

        private sealed class RuntimeProfileSettings
        {
            public string Loader { get; set; } = "Vanilla";
            public string Version { get; set; } = "1.21.1";
            public int RamGb { get; set; } = 4;
            public string ForgeVersion { get; set; } = "";
        }

        static MainWindow()
        {
            EventManager.RegisterClassHandler(typeof(MainWindow), FrameworkElement.LoadedEvent, new RoutedEventHandler(RuntimeLoaded));
        }

        private static void RuntimeLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is not MainWindow window || window._runtimeUiReady) return;
            window._runtimeUiReady = true;
            window.InitializeLoaderRuntimeUi();
        }

        private void InitializeLoaderRuntimeUi()
        {
            try
            {
                AddLoaderSelectorCard();
                HookProfileCreateButton();
                HookSaveProfileButton();
                HookLaunchButton();
                HookProfileSelection();
                RefreshLoaderUiFromProfile();
                UpdateLaunchSummary();
            }
            catch (Exception ex) { WriteException("LOADER UI INITIALIZATION ERROR", ex); }
        }

        private void AddLoaderSelectorCard()
        {
            if (_loaderBox != null || TabProfiles == null) return;
            _loaderBox = new ComboBox { Height = 36, HorizontalAlignment = HorizontalAlignment.Stretch, Style = FindResource("ModernComboBox") as Style };
            _loaderBox.Items.Add("Vanilla");
            _loaderBox.Items.Add("Fabric");
            _loaderBox.Items.Add("Forge");
            _loaderBox.Items.Add("Quilt");
            _loaderBox.Items.Add("NeoForge");
            _loaderBox.SelectionChanged += LoaderBox_SelectionChanged;

            Border card = new Border
            {
                Background = (Brush)FindResource("CardBackground"), BorderBrush = (Brush)FindResource("CardBorder"),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(16), Margin = new Thickness(0, 0, 0, 14)
            };
            StackPanel stack = new StackPanel();
            stack.Children.Add(new TextBlock { Text = "MOD LOADER", Foreground = new SolidColorBrush(Color.FromRgb(102,108,118)), FontSize = 10, FontWeight = FontWeights.Bold, Margin = new Thickness(0,0,0,8) });
            stack.Children.Add(_loaderBox);
            stack.Children.Add(new TextBlock { Text = "Choose the loader for this profile. The Minecraft version list changes automatically.", Foreground = (Brush)FindResource("MutedText"), FontSize = 10, Margin = new Thickness(0,8,0,0), TextWrapping = TextWrapping.Wrap });
            card.Child = stack;
            int insertIndex = Math.Min(2, TabProfiles.Children.Count);
            TabProfiles.Children.Insert(insertIndex, card);
        }

        private void HookProfileCreateButton()
        {
            Button? create = FindButtonByContent(TabProfiles, "+ New");
            if (create != null) create.PreviewMouseLeftButtonDown += CreateProfilePreview;
        }

        private void HookSaveProfileButton()
        {
            TabProfiles.AddHandler(Button.ClickEvent, new RoutedEventHandler(ProfileAreaButtonClicked), true);
        }

        private void HookLaunchButton() => LaunchBtn.PreviewMouseLeftButtonDown += LaunchPreview;
        private void HookProfileSelection() => ProfileSelector.SelectionChanged += RuntimeProfileSelectionChanged;

        private void ProfileAreaButtonClicked(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is not Button button || !string.Equals(button.Content?.ToString(), "Save Profile Settings", StringComparison.OrdinalIgnoreCase)) return;
            Dispatcher.BeginInvoke(new Action(() => { try { SaveRuntimeLoaderSetting(); UpdateLaunchSummary(); } catch (Exception ex) { WriteException("LOADER PROFILE SAVE ERROR", ex); } }));
        }

        private void RuntimeProfileSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_runtimeUiReady || e.OriginalSource != ProfileSelector) return;
            Dispatcher.BeginInvoke(new Action(() => { RefreshLoaderUiFromProfile(); UpdateLaunchSummary(); }));
        }

        private void CreateProfilePreview(object sender, MouseButtonEventArgs e) { e.Handled = true; ShowCreateProfileDialog(); }

        private void LaunchPreview(object sender, MouseButtonEventArgs e)
        {
            string loader = GetRuntimeProfile().Loader;
            if (loader.Equals("Fabric", StringComparison.OrdinalIgnoreCase)) return;
            if (loader.Equals("NeoForge", StringComparison.OrdinalIgnoreCase)) return;
            e.Handled = true;
            _ = LaunchNonFabricProfileAsync();
        }

        private void LoaderBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (!_runtimeUiReady || _loaderBox?.SelectedItem == null) return;
            SetVersionChoices(_loaderBox.SelectedItem.ToString() ?? "Vanilla");
        }

        private void SetVersionChoices(string loader, string? preferred = null)
        {
            string[] versions = loader switch
            {
                "Vanilla" => RuntimeVanillaVersions,
                "Forge" => RuntimeForgeVersions,
                "Quilt" => RuntimeQuiltVersions,
                "NeoForge" => RuntimeNeoForgeVersions,
                _ => RuntimeFabricVersions
            };
            string current = preferred ?? GetSelectedVersion();
            VersionBox.Items.Clear();
            foreach (string version in versions) VersionBox.Items.Add(new ComboBoxItem { Content = version });
            int index = Array.IndexOf(versions, current);
            if (index < 0) index = 0;
            VersionBox.SelectedIndex = index;
            UpdateProfileCard();
            UpdateLaunchSummary();
        }

        private RuntimeProfileSettings GetRuntimeProfile()
        {
            try
            {
                string path = GetProfileSettingsPath(_gamePath);
                if (File.Exists(path))
                {
                    RuntimeProfileSettings? value = JsonSerializer.Deserialize<RuntimeProfileSettings>(File.ReadAllText(path));
                    if (value != null) return value;
                }
            }
            catch (Exception ex) { WriteException("RUNTIME PROFILE READ ERROR", ex); }
            return new RuntimeProfileSettings();
        }

        private void SaveRuntimeLoaderSetting()
        {
            RuntimeProfileSettings current = GetRuntimeProfile();
            current.Loader = _loaderBox?.SelectedItem?.ToString() ?? current.Loader;
            current.Version = GetSelectedVersion();
            current.RamGb = Math.Clamp((int)RamSlider.Value, 2, 12);
            WriteRuntimeProfile(current);
        }

        private void WriteRuntimeProfile(RuntimeProfileSettings settings)
        {
            Directory.CreateDirectory(_gamePath);
            File.WriteAllText(GetProfileSettingsPath(_gamePath), JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        }

        private void RefreshLoaderUiFromProfile()
        {
            if (_loaderBox == null) return;
            RuntimeProfileSettings settings = GetRuntimeProfile();
            string loader = settings.Loader;
            if (!loader.Equals("Vanilla", StringComparison.OrdinalIgnoreCase) && !loader.Equals("Fabric", StringComparison.OrdinalIgnoreCase) && !loader.Equals("Forge", StringComparison.OrdinalIgnoreCase) && !loader.Equals("Quilt", StringComparison.OrdinalIgnoreCase) && !loader.Equals("NeoForge", StringComparison.OrdinalIgnoreCase)) loader = "Vanilla";
            _loaderBox.SelectedItem = loader;
            SetVersionChoices(loader, settings.Version);
            int ram = Math.Clamp(settings.RamGb, 2, 12);
            RamSlider.Value = ram;
            RamLabel.Text = $"{ram}GB";
            UpdateLaunchSummary();
        }

        private void ShowCreateProfileDialog()
        {
            Window dialog = new Window { Title = "Create New Topu Profile", Width = 480, Height = 430, WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this, ResizeMode = ResizeMode.NoResize, Background = (Brush)FindResource("WindowBackground"), Foreground = Brushes.White, WindowStyle = WindowStyle.SingleBorderWindow };
            Grid root = new Grid { Margin = new Thickness(24) };
            for (int i = 0; i < 6; i++) root.RowDefinitions.Add(new RowDefinition { Height = i == 4 ? new GridLength(1, GridUnitType.Star) : GridLength.Auto });
            TextBlock title = new TextBlock { Text = "Create New Profile", FontSize = 24, FontWeight = FontWeights.Bold, Foreground = Brushes.White, Margin = new Thickness(0,0,0,18) }; Grid.SetRow(title,0); root.Children.Add(title);
            TextBox nameBox = new TextBox { Height = 36, Padding = new Thickness(10,6,10,6), Text = "pvp" }; AddDialogField(root,1,"Profile name",nameBox);
            ComboBox loaderBox = new ComboBox { Height = 36, ItemsSource = new[] { "Vanilla", "Fabric", "Forge", "Quilt", "NeoForge" }, SelectedIndex = 0 }; AddDialogField(root,2,"Loader",loaderBox);
            ComboBox versionBox = new ComboBox { Height = 36, ItemsSource = RuntimeVanillaVersions, SelectedIndex = 0 }; AddDialogField(root,3,"Minecraft version",versionBox);
            Slider ram = new Slider { Minimum = 2, Maximum = 12, Value = 4, TickFrequency = 1, IsSnapToTickEnabled = true };
            TextBlock ramValue = new TextBlock { Text = "4GB", Foreground = (Brush)FindResource("TopuGreen"), FontWeight = FontWeights.Bold, Margin = new Thickness(10,0,0,0) };
            ram.ValueChanged += (_, args) => ramValue.Text = $"{(int)args.NewValue}GB";
            StackPanel ramPanel = new StackPanel { Orientation = Orientation.Horizontal }; ramPanel.Children.Add(ram); ramPanel.Children.Add(ramValue); AddDialogField(root,4,"Allocated RAM",ramPanel);
            loaderBox.SelectionChanged += (_, _) =>
            {
                string loader = loaderBox.SelectedItem?.ToString() ?? "Vanilla";
                string[] choices = loader switch { "Vanilla" => RuntimeVanillaVersions, "Forge" => RuntimeForgeVersions, "Quilt" => RuntimeQuiltVersions, "NeoForge" => RuntimeNeoForgeVersions, _ => RuntimeFabricVersions };
                versionBox.ItemsSource = choices; versionBox.SelectedIndex = 0;
            };
            StackPanel buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            Button cancel = new Button { Content = "Cancel", Width = 100, Height = 38, Margin = new Thickness(0,0,10,0), Style = FindResource("ModernButton") as Style };
            Button create = new Button { Content = "Create Profile", Width = 130, Height = 38, Style = FindResource("GreenButton") as Style };
            cancel.Click += (_, _) => dialog.Close();
            create.Click += (_, _) =>
            {
                string name = nameBox.Text.Trim(); string loader = loaderBox.SelectedItem?.ToString() ?? "Vanilla"; string version = versionBox.SelectedItem?.ToString() ?? "";
                if (string.IsNullOrWhiteSpace(name)) { MessageBox.Show(dialog,"Enter a profile name.","Topu Client",MessageBoxButton.OK,MessageBoxImage.Warning); return; }
                if (string.IsNullOrWhiteSpace(version)) return;
                try
                {
                    string normalized = NormalizeProfileName(name); string path = Path.Combine(_profilesPath, normalized);
                    if (Directory.Exists(path)) { MessageBox.Show(dialog,"That profile already exists.","Topu Client",MessageBoxButton.OK,MessageBoxImage.Warning); return; }
                    Directory.CreateDirectory(path); string oldPath = _gamePath; _gamePath = path;
                    try { WriteRuntimeProfile(new RuntimeProfileSettings { Loader=loader, Version=version, RamGb=(int)ram.Value }); }
                    finally { _gamePath = oldPath; }
                    LoadProfilesIntoSelector(); ProfileSelector.SelectedItem = GetDisplayProfileName(normalized); SetActiveProfile(GetDisplayProfileName(normalized)); RefreshLoaderUiFromProfile(); StatusText.Text = $"Created {loader} profile: {GetDisplayProfileName(normalized)}"; WriteLog($"Created {loader} profile {normalized} for Minecraft {version} with {(int)ram.Value}GB RAM."); dialog.Close();
                }
                catch (Exception ex) { WriteException("CUSTOM PROFILE CREATE ERROR", ex); MessageBox.Show(dialog,ex.Message,"Profile Error",MessageBoxButton.OK,MessageBoxImage.Error); }
            };
            buttons.Children.Add(cancel); buttons.Children.Add(create); Grid.SetRow(buttons,5); root.Children.Add(buttons);
            dialog.Content = root; dialog.ShowDialog();
        }

        private void AddDialogField(Panel root, int row, string label, UIElement control)
        {
            StackPanel stack = new StackPanel { Margin = new Thickness(0,0,0,12) };
            stack.Children.Add(new TextBlock { Text=label, Foreground=new SolidColorBrush(Color.FromRgb(200,205,213)), FontSize=11, Margin=new Thickness(0,0,0,5) });
            stack.Children.Add(control); Grid.SetRow(stack,row); root.Children.Add(stack);
        }

        private Button? FindButtonByContent(Panel parent, string content)
        {
            foreach (UIElement child in parent.Children)
            {
                if (child is Button button && string.Equals(button.Content?.ToString(), content, StringComparison.OrdinalIgnoreCase)) return button;
                if (child is Panel panel) { Button? result=FindButtonByContent(panel,content); if(result!=null)return result; }
                else if (child is Border border && border.Child is Panel borderPanel) { Button? result=FindButtonByContent(borderPanel,content); if(result!=null)return result; }
            }
            return null;
        }

        private int RuntimeJavaMajor(string loader, string minecraftVersion)
        {
            if (minecraftVersion == "1.8.9") return 8;
            if (minecraftVersion == "1.20.1") return 17;
            if (minecraftVersion.StartsWith("26.", StringComparison.OrdinalIgnoreCase)) return 25;
            return 21;
        }

        private async Task LaunchNonFabricProfileAsync()
        {
            if (_minecraftProcess != null)
            {
                try { if (!_minecraftProcess.HasExited) { MessageBox.Show("Minecraft is already running.","Topu Client",MessageBoxButton.OK,MessageBoxImage.Information); return; } } catch { }
                _minecraftProcess=null;
            }
            LaunchBtn.IsEnabled=false;
            try
            {
                RuntimeProfileSettings profile=GetRuntimeProfile(); string loaderType=profile.Loader; string minecraftVersion=profile.Version; int ram=Math.Max(2048,profile.RamGb*1024);
                StartLaunchLog(); WriteLog("===== TOPU MULTI-LOADER LAUNCH ====="); WriteLog($"Loader: {loaderType}"); WriteLog($"Minecraft: {minecraftVersion}"); WriteLog($"Profile: {_gamePath}"); WriteLog($"RAM: {ram} MB");
                _session=await AuthenticateSelectedAccountAsync(); if(_session==null) throw new InvalidOperationException("Could not create a Minecraft session.");
                int javaMajor=RuntimeJavaMajor(loaderType,minecraftVersion); string javaPath=await EnsureJavaAsync(javaMajor); MinecraftPath minecraftPath=new MinecraftPath(_gamePath); MinecraftLauncher launcher=new MinecraftLauncher(minecraftPath);
                StatusText.Text=$"Installing Minecraft {minecraftVersion}..."; await launcher.InstallAsync(minecraftVersion,CancellationToken.None);
                string loaderVersionName;
                if(loaderType.Equals("Forge",StringComparison.OrdinalIgnoreCase))
                {
                    StatusText.Text=$"Installing Forge for {minecraftVersion}..."; ForgeInstaller forge=new ForgeInstaller(launcher,Http); IEnumerable<ForgeVersion> versions=await forge.GetForgeVersions(minecraftVersion); ForgeVersion? selected=versions.FirstOrDefault(); if(selected==null) throw new InvalidOperationException($"No Forge build was found for Minecraft {minecraftVersion}."); WriteLog($"Selected Forge build: {selected.ForgeVersionName}"); loaderVersionName=await forge.Install(selected);
                }
                else if(loaderType.Equals("Quilt",StringComparison.OrdinalIgnoreCase)) loaderVersionName=await InstallQuiltRuntimeAsync(minecraftVersion);
                else loaderVersionName=minecraftVersion;

                MLaunchOption options=new MLaunchOption {... (truncated)