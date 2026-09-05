using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace TopuLauncher
{
    public partial class MainWindow
    {
        private static readonly object LunarRuntimeFixRegistration = RegisterLunarRuntimeFix();
        private Button? _lunarMaximizeButton;
        private Grid? _lunarLaunchHost;

        private static object RegisterLunarRuntimeFix()
        {
            EventManager.RegisterClassHandler(
                typeof(MainWindow),
                Window.ContentRenderedEvent,
                new RoutedEventHandler(OnLunarContentRendered));
            return new object();
        }

        private static void OnLunarContentRendered(object sender, RoutedEventArgs e)
        {
            if (sender is MainWindow window)
            {
                window.Dispatcher.BeginInvoke(
                    DispatcherPriority.ApplicationIdle,
                    new Action(window.FixLunarRuntimeUi));
            }
        }

        private void FixLunarRuntimeUi()
        {
            try
            {
                KeepLaunchButtonInVisualTree();
                WireLunarPlayButton();
                AddLunarMaximizeButton();
            }
            catch (Exception ex)
            {
                try { WriteException("LUNAR UI RUNTIME FIX ERROR", ex); } catch { }
            }
        }

        private void KeepLaunchButtonInVisualTree()
        {
            if (LaunchBtn == null || LaunchBtn.Parent != null)
                return;

            if (Content is not Border shell || shell.Child is not Grid grid)
                return;

            _lunarLaunchHost = new Grid
            {
                Width = 1,
                Height = 1,
                Opacity = 0,
                IsHitTestVisible = false,
                Visibility = Visibility.Visible
            };

            _lunarLaunchHost.Children.Add(LaunchBtn);
            grid.Children.Add(_lunarLaunchHost);
        }

        private void WireLunarPlayButton()
        {
            if (Content is not DependencyObject root)
                return;

            Button? play = FindButtonByContent(root, "PLAY NOW  ›");
            if (play == null || Equals(play.Tag, "TOPU_LUNAR_PLAY_WIRED"))
                return;

            play.Tag = "TOPU_LUNAR_PLAY_WIRED";
            play.PreviewMouseLeftButtonDown += LunarPlayNow_Click;
        }

        private void LunarPlayNow_Click(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;

            try
            {
                if (LaunchBtn == null)
                    return;

                LaunchBtn.IsEnabled = true;

                RuntimeProfileSettings profile = GetRuntimeProfile();

                if (profile.Loader.Equals("Vanilla", StringComparison.OrdinalIgnoreCase) &&
                    profile.Version.Equals("1.8.9", StringComparison.OrdinalIgnoreCase))
                {
                    _ = LaunchJava8ProfileAsync();
                    return;
                }

                LaunchBtn_Click(LaunchBtn, new RoutedEventArgs(Button.ClickEvent));
            }
            catch (Exception ex)
            {
                try
                {
                    WriteException("LUNAR PLAY NOW ERROR", ex);
                    MessageBox.Show(
                        ex.Message,
                        "Topu Client - Launch Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
                catch { }
            }
        }

        private void AddLunarMaximizeButton()
        {
            if (_lunarMaximizeButton != null)
                return;

            if (Content is not DependencyObject root)
                return;

            Button? close = FindButtonByContent(root, "×");
            if (close?.Parent is not Panel panel)
                return;

            _lunarMaximizeButton = new Button
            {
                Content = "□",
                Width = 34,
                Height = 34,
                Margin = new Thickness(2, 0, 0, 0),
                ToolTip = "Maximize",
                Style = FindButtonStyle(close)
            };

            _lunarMaximizeButton.Click += LunarMaximizeRestore_Click;
            int index = panel.Children.IndexOf(close);
            panel.Children.Insert(Math.Max(0, index), _lunarMaximizeButton);
        }

        private static Style? FindButtonStyle(Button source)
        {
            return source.Style;
        }

        private void LunarMaximizeRestore_Click(object sender, RoutedEventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                WindowState = WindowState.Normal;
                if (sender is Button button)
                {
                    button.Content = "□";
                    button.ToolTip = "Maximize";
                }
            }
            else
            {
                WindowState = WindowState.Maximized;
                if (sender is Button button)
                {
                    button.Content = "❐";
                    button.ToolTip = "Restore";
                }
            }
        }

        private static Button? FindButtonByContent(DependencyObject parent, string content)
        {
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                if (child is Button button && string.Equals(button.Content?.ToString(), content, StringComparison.Ordinal))
                    return button;

                Button? result = FindButtonByContent(child, content);
                if (result != null)
                    return result;
            }

            return null;
        }
    }
}
