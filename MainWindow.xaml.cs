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
                InitializeComponent();

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
                    return;
                }

                UsernameText.Text = $"Loading {_username}'s contributions...";

                var data = await _githubService.GetContributionsAsync();

                UsernameText.Text = $"{_username}'s GitHub Contributions";
                TotalContributionsText.Text = $"Total contributions: {data.TotalContributions}";

                // Generate month labels
                GenerateMonthLabels(data.Days);

                // Organize contribution data by weeks
                var displayData = OrganizeContributionsByWeek(data.Days);

                ContributionGrid.ItemsSource = displayData;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading contributions: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private List<ContributionDisplayItem> OrganizeContributionsByWeek(List<ContributionDay> days)
        {
            var displayData = new List<ContributionDisplayItem>();

            if (days.Count == 0) return displayData;

            // Find the first Sunday to start from
            var firstDay = days[0];
            int daysToFirstSunday = ((int)firstDay.Date.DayOfWeek + 7) % 7;

            // Add empty cells for days before the first contribution day (to align to Sunday)
            for (int i = 0; i < daysToFirstSunday; i++)
            {
                displayData.Add(new ContributionDisplayItem
                {
                    Color = new SolidColorBrush(_colorScheme[0]),
                    Tooltip = "No data"
                });
            }

            // Add all contribution days
            foreach (var day in days)
            {
                displayData.Add(new ContributionDisplayItem
                {
                    Color = GetColorForCount(day.Count),
                    Tooltip = $"{day.Date:MMM dd, yyyy}: {day.Count} contributions"
                });
            }

            return displayData;
        }

        private void GenerateMonthLabels(List<ContributionDay> days)
        {
            if (days.Count == 0 || MonthLabels == null) return;

            var monthLabels = new List<MonthLabel>();

            // Calculate padding to align first day to Sunday
            var firstDay = days[0];
            int paddingDays = ((int)firstDay.Date.DayOfWeek + 7) % 7;

            // Track current position
            int currentWeekColumn = 0;
            int dayInWeek = paddingDays;
            string lastMonthSeen = "";
            int lastMonthWeekColumn = -1;

            for (int i = 0; i < days.Count; i++)
            {
                var day = days[i];
                string currentMonth = day.Date.ToString("MMM");

                // Detect month change
                if (currentMonth != lastMonthSeen)
                {
                    // Calculate width from last month label to this one
                    if (lastMonthWeekColumn >= 0)
                    {
                        int weeksBetween = currentWeekColumn - lastMonthWeekColumn;
                        monthLabels.Add(new MonthLabel
                        {
                            MonthName = lastMonthSeen,
                            Width = weeksBetween * 14 // 14px per week (12px square + 2px margin)
                        });
                    }

                    lastMonthSeen = currentMonth;
                    lastMonthWeekColumn = currentWeekColumn;
                }

                // Move to next day position
                dayInWeek++;
                if (dayInWeek >= 7)
                {
                    dayInWeek = 0;
                    currentWeekColumn++;
                }
            }

            // Add the last month label with remaining width
            if (lastMonthWeekColumn >= 0)
            {
                int remainingWeeks = currentWeekColumn - lastMonthWeekColumn;
                if (dayInWeek > 0) remainingWeeks++; // Account for partial week

                monthLabels.Add(new MonthLabel
                {
                    MonthName = lastMonthSeen,
                    Width = remainingWeeks * 14
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
        //private void ThemeButton_Checked(object sender, RoutedEventArgs e)
        //{
        //    if (ThemePopup != null)
        //    {
        //        ThemePopup.Visibility = Visibility.Visible;
        //    }
        //}

        //private void ThemeButton_Unchecked(object sender, RoutedEventArgs e)
        //{
        //    if (ThemePopup != null)
        //    {
        //        ThemePopup.Visibility = Visibility.Collapsed;
        //    }
        //}

        //private void CloseThemePopup(object sender, RoutedEventArgs e)
        //{
        //    ThemeButton.IsChecked = false;
        //    if (ThemePopup != null)
        //    {
        //        ThemePopup.Visibility = Visibility.Collapsed;
        //    }
        //}

        // Preset theme handlers
        //private void ApplyGitHubTheme(object sender, RoutedEventArgs e)
        //{
        //    _colorScheme = new Color[]
        //    {
        //Color.FromRgb(22, 27, 34),
        //Color.FromRgb(14, 68, 41),
        //Color.FromRgb(0, 109, 50),
        //Color.FromRgb(38, 166, 65),
        //Color.FromRgb(57, 211, 83)
        //    };
        //    LoadContributions();

        //    // Auto-close popup after selection
        //    ThemeButton.IsChecked = false;
        //    ThemePopup.Visibility = Visibility.Collapsed;
        //}

        //private void ApplyOceanTheme(object sender, RoutedEventArgs e)
        //{
        //    _colorScheme = new Color[]
        //    {
        //Color.FromRgb(22, 27, 34),
        //Color.FromRgb(13, 74, 110),
        //Color.FromRgb(9, 105, 218),
        //Color.FromRgb(31, 111, 235),
        //Color.FromRgb(88, 166, 255)
        //    };
        //    LoadContributions();

        //    // Auto-close popup after selection
        //    ThemeButton.IsChecked = false;
        //    ThemePopup.Visibility = Visibility.Collapsed;
        //}

        //private void ApplySunsetTheme(object sender, RoutedEventArgs e)
        //{
        //    _colorScheme = new Color[]
        //    {
        //Color.FromRgb(22, 27, 34),
        //Color.FromRgb(77, 47, 31),
        //Color.FromRgb(188, 76, 0),
        //Color.FromRgb(251, 133, 0),
        //Color.FromRgb(255, 183, 3)
        //    };
        //    LoadContributions();

        //    // Auto-close popup after selection
        //    ThemeButton.IsChecked = false;
        //    ThemePopup.Visibility = Visibility.Collapsed;
        //}

        //private void ApplyPurpleTheme(object sender, RoutedEventArgs e)
        //{
        //    _colorScheme = new Color[]
        //    {
        //    Color.FromRgb(22, 27, 34),
        //    Color.FromRgb(61, 31, 77),
        //    Color.FromRgb(130, 80, 223),
        //    Color.FromRgb(163, 113, 247),
        //    Color.FromRgb(210, 168, 255)
        //    };
        //    LoadContributions();

        //    // Auto-close popup after selection
        //    ThemeButton.IsChecked = false;
        //    ThemePopup.Visibility = Visibility.Collapsed;
        //}
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