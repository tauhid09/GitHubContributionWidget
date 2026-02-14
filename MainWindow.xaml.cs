using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace GitHubContributionWidget
{
    public partial class MainWindow : Window
    {
        private GitHubService _githubService;
        private string _username;
        private string _token;

        // Color scheme storage
        private Color[] _colorScheme = new Color[]
        {
            Color.FromRgb(22, 27, 34),    // Level 0
            Color.FromRgb(14, 68, 41),    // Level 1
            Color.FromRgb(0, 109, 50),    // Level 2
            Color.FromRgb(38, 166, 65),   // Level 3
            Color.FromRgb(57, 211, 83)    // Level 4
        };

        public MainWindow(string username, string token)
        {
            try
            {
                InitializeComponent(); // MUST be first!

                _username = username;
                _token = token;
                _githubService = new GitHubService(_username, _token);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"MainWindow Constructor Error: {ex.Message}\n\n{ex.StackTrace}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Enable blur effect
            WindowBlur.EnableBlur(this);

            // Load contributions after a small delay to ensure UI is ready
            Dispatcher.BeginInvoke(new Action(() =>
            {
                LoadContributions();
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private async void LoadContributions()
        {
            try
            {
                if (UsernameText == null || TotalContributionsText == null || ContributionGrid == null)
                {
                    // UI elements not ready yet, skip
                    return;
                }

                UsernameText.Text = $"Loading {_username}'s contributions...";

                var data = await _githubService.GetContributionsAsync();

                UsernameText.Text = $"{_username}'s GitHub Contributions";
                TotalContributionsText.Text = $"Total contributions: {data.TotalContributions}";

                // Generate month labels
                GenerateMonthLabels(data.Days);

                // Generate contribution display
                var displayData = new List<ContributionDisplayItem>();

                foreach (var day in data.Days)
                {
                    displayData.Add(new ContributionDisplayItem
                    {
                        Color = GetColorForCount(day.Count),
                        Tooltip = $"{day.Date:MMM dd, yyyy}: {day.Count} contributions"
                    });
                }

                ContributionGrid.ItemsSource = displayData;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading contributions: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void GenerateMonthLabels(List<ContributionDay> days)
        {
            if (days.Count == 0 || MonthLabels == null) return;

            var monthLabels = new List<MonthLabel>();
            var weekGroups = new List<List<ContributionDay>>();
            var currentWeek = new List<ContributionDay>();

            foreach (var day in days)
            {
                currentWeek.Add(day);
                if (day.Date.DayOfWeek == DayOfWeek.Saturday || day == days.Last())
                {
                    weekGroups.Add(new List<ContributionDay>(currentWeek));
                    currentWeek.Clear();
                }
            }

            string currentMonth = "";
            int weeksInCurrentMonth = 0;

            foreach (var week in weekGroups)
            {
                var firstDayOfWeek = week.First().Date;
                string monthName = firstDayOfWeek.ToString("MMM");

                if (monthName != currentMonth)
                {
                    if (!string.IsNullOrEmpty(currentMonth) && weeksInCurrentMonth > 0)
                    {
                        monthLabels.Add(new MonthLabel
                        {
                            MonthName = currentMonth,
                            Width = weeksInCurrentMonth * 14
                        });
                    }

                    currentMonth = monthName;
                    weeksInCurrentMonth = 1;
                }
                else
                {
                    weeksInCurrentMonth++;
                }
            }

            if (weeksInCurrentMonth > 0)
            {
                monthLabels.Add(new MonthLabel
                {
                    MonthName = currentMonth,
                    Width = weeksInCurrentMonth * 14
                });
            }

            MonthLabels.ItemsSource = monthLabels;
        }

        private SolidColorBrush GetColorForCount(int count)
        {
            if (count == 0) return new SolidColorBrush(_colorScheme[0]);
            if (count <= 3) return new SolidColorBrush(_colorScheme[1]);
            if (count <= 6) return new SolidColorBrush(_colorScheme[2]);
            if (count <= 9) return new SolidColorBrush(_colorScheme[3]);
            return new SolidColorBrush(_colorScheme[4]);
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            LoadContributions();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }

        private void PinButton_Checked(object sender, RoutedEventArgs e)
        {
            this.Topmost = true;
            CloseButton.IsEnabled = false;
        }

        private void PinButton_Unchecked(object sender, RoutedEventArgs e)
        {
            this.Topmost = false;
            CloseButton.IsEnabled = true;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        // Theme button handlers
        private void ThemeButton_Checked(object sender, RoutedEventArgs e)
        {
            if (ThemePopup != null)
            {
                ThemePopup.Visibility = Visibility.Visible;
            }
        }

        private void ThemeButton_Unchecked(object sender, RoutedEventArgs e)
        {
            if (ThemePopup != null)
            {
                ThemePopup.Visibility = Visibility.Collapsed;
            }
        }

        private void ColorPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            if (comboBox == null || comboBox.SelectedItem == null) return;

            var selectedItem = comboBox.SelectedItem as ComboBoxItem;
            var colorHex = selectedItem.Tag as string;

            if (string.IsNullOrEmpty(colorHex)) return;

            // Determine which color level was changed
            int level = -1;
            Border previewBorder = null;

            if (comboBox.Name == "ColorPicker0") { level = 0; previewBorder = ColorPreview0; }
            else if (comboBox.Name == "ColorPicker1") { level = 1; previewBorder = ColorPreview1; }
            else if (comboBox.Name == "ColorPicker2") { level = 2; previewBorder = ColorPreview2; }
            else if (comboBox.Name == "ColorPicker3") { level = 3; previewBorder = ColorPreview3; }
            else if (comboBox.Name == "ColorPicker4") { level = 4; previewBorder = ColorPreview4; }

            if (level >= 0 && level < 5)
            {
                var color = (Color)ColorConverter.ConvertFromString(colorHex);
                _colorScheme[level] = color;

                // Update the preview border
                if (previewBorder != null)
                {
                    previewBorder.Background = new SolidColorBrush(color);
                }

                LoadContributions(); // Refresh the grid with new colors
            }
        }

        private void CloseThemePopup(object sender, RoutedEventArgs e)
        {
            ThemeButton.IsChecked = false;
            if (ThemePopup != null)
            {
                ThemePopup.Visibility = Visibility.Collapsed;
            }
        }

        // Preset theme handlers
        private void ApplyGitHubTheme(object sender, RoutedEventArgs e)
        {
            _colorScheme = new Color[]
            {
                Color.FromRgb(22, 27, 34),
                Color.FromRgb(14, 68, 41),
                Color.FromRgb(0, 109, 50),
                Color.FromRgb(38, 166, 65),
                Color.FromRgb(57, 211, 83)
            };
            UpdateColorPickers(0, 0, 0, 0, 0);
            LoadContributions();
        }

        private void ApplyOceanTheme(object sender, RoutedEventArgs e)
        {
            _colorScheme = new Color[]
            {
                Color.FromRgb(22, 27, 34),
                Color.FromRgb(13, 74, 110),
                Color.FromRgb(9, 105, 218),
                Color.FromRgb(31, 111, 235),
                Color.FromRgb(88, 166, 255)
            };
            UpdateColorPickers(0, 1, 1, 1, 1);
            LoadContributions();
        }

        private void ApplySunsetTheme(object sender, RoutedEventArgs e)
        {
            _colorScheme = new Color[]
            {
                Color.FromRgb(22, 27, 34),
                Color.FromRgb(77, 47, 31),
                Color.FromRgb(188, 76, 0),
                Color.FromRgb(251, 133, 0),
                Color.FromRgb(255, 183, 3)
            };
            UpdateColorPickers(0, 4, 4, 4, 4);
            LoadContributions();
        }

        private void ApplyPurpleTheme(object sender, RoutedEventArgs e)
        {
            _colorScheme = new Color[]
            {
                Color.FromRgb(22, 27, 34),
                Color.FromRgb(61, 31, 77),
                Color.FromRgb(130, 80, 223),
                Color.FromRgb(163, 113, 247),
                Color.FromRgb(210, 168, 255)
            };
            UpdateColorPickers(0, 2, 2, 2, 2);
            LoadContributions();
        }

        private void UpdateColorPickers(int idx0, int idx1, int idx2, int idx3, int idx4)
        {
            if (ColorPicker0 != null) ColorPicker0.SelectedIndex = idx0;
            if (ColorPicker1 != null) ColorPicker1.SelectedIndex = idx1;
            if (ColorPicker2 != null) ColorPicker2.SelectedIndex = idx2;
            if (ColorPicker3 != null) ColorPicker3.SelectedIndex = idx3;
            if (ColorPicker4 != null) ColorPicker4.SelectedIndex = idx4;

            // Update preview borders
            if (ColorPreview0 != null) UpdatePreviewBorder(ColorPreview0, ColorPicker0);
            if (ColorPreview1 != null) UpdatePreviewBorder(ColorPreview1, ColorPicker1);
            if (ColorPreview2 != null) UpdatePreviewBorder(ColorPreview2, ColorPicker2);
            if (ColorPreview3 != null) UpdatePreviewBorder(ColorPreview3, ColorPicker3);
            if (ColorPreview4 != null) UpdatePreviewBorder(ColorPreview4, ColorPicker4);
        }

        private void UpdatePreviewBorder(Border border, ComboBox comboBox)
        {
            if (border == null || comboBox == null) return;

            if (comboBox.SelectedItem is ComboBoxItem item && item.Tag is string colorHex)
            {
                var color = (Color)ColorConverter.ConvertFromString(colorHex);
                border.Background = new SolidColorBrush(color);
            }
        }
    }

    public class ContributionDisplayItem
    {
        public SolidColorBrush Color { get; set; }
        public string Tooltip { get; set; }
    }

    public class MonthLabel
    {
        public string MonthName { get; set; }
        public double Width { get; set; }
    }
}