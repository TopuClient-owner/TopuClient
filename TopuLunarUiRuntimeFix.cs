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

        private static object RegisterLunarRuntimeFix()
        {
            EventManager.RegisterClassHandler(
                typeof(MainWindow),
                FrameworkElement.LoadedEvent,
                new RoutedEventHandler(OnLunarLoaded));
            return new object();
        }

        private static void OnLunarLoaded(object sender, RoutedEventArgs e)
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
                // IMPORTANT: LaunchBtn is part of the real profile/launcher logic.
                // Never move it into an invisible/non-hit-testable container.
                // Doing so makes the button permanently unclickable.
                WireLunarPlayButton();
                AddLunarMaximizeButton();
            }
            catch (Exception ex)
            {
                try { WriteException("LUNAR UI RUNTIME FIX ERROR", ex); } catch { }
            }
        }

        private void WireLunarPlayButton()
        {
            if (Content is not DependencyObject root)
                return;

            Button? play = FindButtonByContent(root, "PLAY NOW  ›");
            if (play == null || Equals(play.Tag, "TOPU_LUNAR_PLAY_WIRED"))
                return;

            play.Tag = "TOPU_LUNAR_PLAY_WIRED";
            play.Click += LunarPlayNow_Click;
        }

        private void LunarPlayNow_Click(object sender, RoutedEventArgs e)
        {
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

                LaunchBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
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
                Style = close.Style
            };

            _lunarMaximizeButton.Click += LunarMaximizeRestore_Click;
            int index = panel.Children.IndexOf(close);
            panel.Children.Insert(Math.Max(0, index), _lunarMaximizeButton);
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
                if (child is Button button &&
                    string.Equals(button.Content?.ToString(), content, StringComparison.Ordinal))
                    return button;

                Button? result = FindButtonByContent(child, content);
                if (result != null)
                    return result;
            }

            return null;
        }
    }
}
