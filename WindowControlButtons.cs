using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TopuLauncher
{
    public partial class MainWindow
    {
        private Button? _maximizeButton;

        protected override void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);
            AddMaximizeButton();
            InitializeTopuLunarUi();
        }

        private void AddMaximizeButton()
        {
            if (_maximizeButton != null)
                return;

            Button? closeButton = FindCloseButton(this);
            if (closeButton?.Parent is not StackPanel buttonPanel)
                return;

            _maximizeButton = new Button
            {
                Content = "□",
                Width = 45,
                Height = 46,
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.FromRgb(133, 138, 148)),
                BorderThickness = new Thickness(0),
                FontSize = 14,
                ToolTip = "Maximize",
            };

            _maximizeButton.Click += MaximizeRestore_Click;

            int closeIndex = buttonPanel.Children.IndexOf(closeButton);
            buttonPanel.Children.Insert(Math.Max(0, closeIndex), _maximizeButton);
        }

        private static Button? FindCloseButton(DependencyObject parent)
        {
            int count = VisualTreeHelper.GetChildrenCount(parent);

            for (int i = 0; i < count; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);

                if (child is Button button &&
                    string.Equals(button.Content?.ToString(), "✕", StringComparison.Ordinal))
                {
                    return button;
                }

                Button? result = FindCloseButton(child);
                if (result != null)
                    return result;
            }

            return null;
        }

        private void MaximizeRestore_Click(object sender, RoutedEventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                WindowState = WindowState.Normal;
                _maximizeButton!.Content = "□";
                _maximizeButton.ToolTip = "Maximize";
            }
            else
            {
                WindowState = WindowState.Maximized;
                _maximizeButton!.Content = "❐";
                _maximizeButton.ToolTip = "Restore";
            }
        }
    }
}
