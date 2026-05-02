using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Microsoft.Win32;
using AutoUpdaterDotNET;

namespace GitHubContributionWidget
{
    public partial class MainWindow : Window
    {
        private GitHubService _githubService;
        private string _username;
        private string _token;
        private ContributionData _lastData;

        private bool _allowClose = false;
        private bool _isPinned   = true;   // true = widget is locked, cannot be dragged
        private int _currentYear = DateTime.Now.Year;
        private string _currentView = "Heatmap";
        private string _currentTimeframe = "1Y";

        // Registry key for saving window position
        private const string RegKey    = @"SOFTWARE\GitHubContributionWidget";

        // Win32 message constants — block Win+D / Show Desktop
        private const int WM_SYSCOMMAND = 0x0112;
        private const int SC_MINIMIZE   = 0xF020;

        // Y-axis tick values the user requested
        private static readonly int[] YTicks = { 0, 5, 10, 15, 20, 25, 31 };
        private const int YMax = 31; // max active days in any month

        // GitHub-style green palette (mapped to activity level)
        private static readonly Color[] GreenScale =
        {
            Color.FromRgb(22,  27,  34),   // 0 days
            Color.FromRgb(14,  68,  41),   // 1–6 days
            Color.FromRgb(0,   109, 50),   // 7–13 days
            Color.FromRgb(38,  166, 65),   // 14–20 days
            Color.FromRgb(57,  211, 83)    // 21–31 days
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

        // ---------------------------------------------------------------
        // WndProc hook — block SC_MINIMIZE (Win+D / Show Desktop)
        // ---------------------------------------------------------------
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            source?.AddHook(WndProc);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            // Block SC_MINIMIZE sent via Alt+Space menu or keyboard shortcuts.
            // Note: touchpad gesture / Win+D use ShowWindow() directly, so they
            // are caught by OnStateChanged below instead.
            if (msg == WM_SYSCOMMAND && (wParam.ToInt32() & 0xFFF0) == SC_MINIMIZE)
                handled = true;
            return IntPtr.Zero;
        }

        // ---------------------------------------------------------------
        // Window position persistence
        // ---------------------------------------------------------------
        private void LoadViewPreference()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegKey))
                {
                    if (key != null)
                    {
                        var view = key.GetValue("ViewMode") as string;
                        if (!string.IsNullOrEmpty(view))
                        {
                            _currentView = view;
                            CurrentViewText.Text = view;
                        }
                    }
                }
            }
            catch { }
        }

        private void LoadWindowPosition()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RegKey))
                {
                    if (key != null)
                    {
                        double left = Convert.ToDouble(key.GetValue("Left", "-1"));
                        double top  = Convert.ToDouble(key.GetValue("Top",  "-1"));

                        if (left >= 0 && top >= 0)
                        {
                            // Make sure it's still on screen after a resolution change
                            var wa = SystemParameters.WorkArea;
                            this.Left = Math.Max(0, Math.Min(left, wa.Right  - this.Width));
                            this.Top  = Math.Max(0, Math.Min(top,  wa.Bottom - this.Height));
                            return;
                        }
                    }
                }
            }
            catch { }

            // Default position: top-right corner with a small margin
            var workArea  = SystemParameters.WorkArea;
            this.Left = workArea.Right  - this.Width  - 20;
            this.Top  = workArea.Top + 20;
        }

        private void SaveWindowPosition()
        {
            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(RegKey))
                {
                    key?.SetValue("Left", this.Left.ToString("F0"));
                    key?.SetValue("Top",  this.Top.ToString("F0"));
                }
            }
            catch { }
        }

        private void Window_LocationChanged(object sender, EventArgs e)
        {
            SaveWindowPosition();
        }

        // ---------------------------------------------------------------
        // Window loaded
        // ---------------------------------------------------------------
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Apply acrylic blur
            WindowBlur.EnableBlur(this);

            // Smooth window fade-in
            this.Opacity = 0;
            var windowFadeIn = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(500))
            {
                EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut }
            };
            this.BeginAnimation(UIElement.OpacityProperty, windowFadeIn);

            // Restore saved position and view
            LoadWindowPosition();
            LoadViewPreference();

            // Always ensure the app is registered to run at Windows startup
            StartupManager.SetStartup(true);
            SyncMenuToggles();

            Dispatcher.BeginInvoke(new Action(() =>
            {
                LoadContributions();
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        // ---------------------------------------------------------------
        // Block ALL close attempts except our intentional menu "Close"
        // ---------------------------------------------------------------
        protected override void OnClosing(CancelEventArgs e)
        {
            if (!_allowClose)
            {
                e.Cancel = true;
                return;
            }
            base.OnClosing(e);
        }

        // ---------------------------------------------------------------
        // Block ALL minimize attempts — including three-finger swipe gesture,
        // Win+D, Show Desktop button, and any ShowWindow(SW_MINIMIZE) API call.
        // OnStateChanged fires for every minimize path that OnClosing/WndProc miss.
        // ---------------------------------------------------------------
        protected override void OnStateChanged(EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                // Restore immediately — no flicker path goes unblocked
                WindowState = WindowState.Normal;
                return;
            }
            base.OnStateChanged(e);
        }

        // ---------------------------------------------------------------
        // Three-dot menu
        // ---------------------------------------------------------------
        private void MenuButton_Click(object sender, RoutedEventArgs e)
        {
            SyncMenuToggles();
            OptionsPopup.IsOpen = true;
        }

        private void SyncMenuToggles()
        {
            MenuPinToggle.Checked   -= MenuPin_Checked;
            MenuPinToggle.Unchecked -= MenuPin_Unchecked;
            MenuStartupToggle.Checked   -= MenuStartup_Checked;
            MenuStartupToggle.Unchecked -= MenuStartup_Unchecked;

            MenuPinToggle.IsChecked     = _isPinned;
            MenuStartupToggle.IsChecked = StartupManager.IsStartupEnabled();

            MenuPinToggle.Checked   += MenuPin_Checked;
            MenuPinToggle.Unchecked += MenuPin_Unchecked;
            MenuStartupToggle.Checked   += MenuStartup_Checked;
            MenuStartupToggle.Unchecked += MenuStartup_Unchecked;

            UpdateStartupSubtext();
        }

        private void MenuPin_Checked(object sender, RoutedEventArgs e)
        {
            _isPinned = true;              // lock: title bar drag is disabled
            UpdatePinSubtext();
            OptionsPopup.IsOpen = false;
        }

        private void MenuPin_Unchecked(object sender, RoutedEventArgs e)
        {
            _isPinned = false;             // unlock: widget can be dragged
            UpdatePinSubtext();
            OptionsPopup.IsOpen = false;
        }

        private void UpdatePinSubtext()
        {
            if (PinLabel == null) return;
            PinLabel.Text = _isPinned ? "Locked (drag disabled)" : "Unlocked (drag enabled)";
        }

        private void MenuStartup_Checked(object sender, RoutedEventArgs e)
        {
            bool ok = StartupManager.SetStartup(true);
            UpdateStartupSubtext();
            if (!ok) { MessageBox.Show("Could not register startup entry.", "Startup", MessageBoxButton.OK, MessageBoxImage.Warning); MenuStartupToggle.IsChecked = false; }
            OptionsPopup.IsOpen = false;
        }

        private void MenuStartup_Unchecked(object sender, RoutedEventArgs e)
        {
            bool ok = StartupManager.SetStartup(false);
            UpdateStartupSubtext();
            if (!ok) { MessageBox.Show("Could not remove startup entry.", "Startup", MessageBoxButton.OK, MessageBoxImage.Warning); MenuStartupToggle.IsChecked = true; }
            OptionsPopup.IsOpen = false;
        }

        private void UpdateStartupSubtext()
        {
            if (StartupSubtext == null) return;
            bool isOn = StartupManager.IsStartupEnabled();
            StartupSubtext.Text = isOn
                ? "✓ Enabled — opens after every login"
                : "Opens automatically after login";
            StartupSubtext.Foreground = isOn
                ? new SolidColorBrush(Color.FromRgb(57, 211, 83))
                : new SolidColorBrush(Color.FromArgb(0x80, 0xFF, 0xFF, 0xFF));
        }

        private void MenuRefresh_Click(object sender, RoutedEventArgs e) { OptionsPopup.IsOpen = false; LoadContributions(); }

        private void MenuLogout_Click(object sender, RoutedEventArgs e)
        {
            OptionsPopup.IsOpen = false;
            try
            {
                // Use the same safe LocalAppData path as LoginWindow
                string credFile = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "GitHubContributionWidget",
                    "github_credentials.dat");
                if (System.IO.File.Exists(credFile))
                    System.IO.File.Delete(credFile);
            }
            catch { }

            // Restart app to show login window again
            System.Diagnostics.Process.Start(Application.ResourceAssembly.Location);
            _allowClose = true;
            this.Close();
        }

        private void MenuClose_Click(object sender, RoutedEventArgs e)
        {
            OptionsPopup.IsOpen = false;
            _allowClose = true;
            this.Close();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (!_allowClose) e.Cancel = true;
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Only allow dragging when the widget is NOT pinned (locked)
            if (!_isPinned && e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        // ---------------------------------------------------------------
        // Canvas SizeChanged — redraw if we already have data
        // ---------------------------------------------------------------
        private void ChartCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_lastData != null && _lastData.Days.Count > 0)
                DrawCurrentView();
        }

        // ---------------------------------------------------------------
        // GitHub data loading
        // ---------------------------------------------------------------
        private async void LoadContributions()
        {
            try
            {
                if (UsernameText == null || TotalContributionsText == null || ChartCanvas == null)
                    return;

                CurrentYearText.Text = _currentYear.ToString();

                UsernameText.Text = $"Loading {_username}'s contributions...";

                // Soft fade-out while loading
                var fadeOut = new DoubleAnimation(0.4, TimeSpan.FromMilliseconds(250));
                ChartCanvas.BeginAnimation(UIElement.OpacityProperty, fadeOut);

                var data = await _githubService.GetContributionsAsync(_currentYear);

                UsernameText.Text = $"{_username}'s GitHub Contributions";
                TotalContributionsText.Text = $"Total contributions: {data.TotalContributions}";

                if (!string.IsNullOrEmpty(data.AvatarUrl))
                {
                    try
                    {
                        var bitmap = new System.Windows.Media.Imaging.BitmapImage(new Uri(data.AvatarUrl));
                        UserAvatarImage.ImageSource = bitmap;
                    }
                    catch { } // fallback if invalid
                }

                // Populate Year Dropdown dynamically based on actual GitHub contribution years
                YearPopupList.Children.Clear();
                foreach (int yr in data.Years)
                {
                    var btn = new Button
                    {
                        Style = (Style)FindResource("MenuItemStyle"),
                        Content = new TextBlock { Text = yr.ToString(), FontWeight = FontWeights.SemiBold }
                    };
                    btn.Click += (s, ev) =>
                    {
                        YearPopup.IsOpen = false;
                        if (_currentYear != yr)
                        {
                            _currentYear = yr;
                            LoadContributions();
                        }
                    };
                    YearPopupList.Children.Add(btn);
                }

                _lastData = data;
                DrawCurrentView();

                // Soft fade-in when data arrives
                var fadeIn = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(400));
                ChartCanvas.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading contributions: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ---------------------------------------------------------------
        // Timeframe Segment Handlers
        // ---------------------------------------------------------------
        private void TimeframeBtn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tf)
            {
                if (_currentTimeframe != tf)
                {
                    _currentTimeframe = tf;

                    // Update UI styling for segment buttons
                    foreach (var child in TimeframePanel.Children)
                    {
                        if (child is Button b)
                        {
                            if (b == btn)
                            {
                                b.Foreground = new SolidColorBrush(Color.FromRgb(57, 211, 83)); // #39D353
                                b.Background = new SolidColorBrush(Color.FromArgb(0x1A, 0x23, 0x86, 0x36)); // #1A238636
                                b.FontWeight = FontWeights.SemiBold;
                            }
                            else
                            {
                                b.Foreground = new SolidColorBrush(Color.FromRgb(201, 209, 217)); // #C9D1D9
                                b.Background = Brushes.Transparent;
                                b.FontWeight = FontWeights.Normal;
                            }
                        }
                    }

                    if (_lastData != null)
                    {
                        DrawCurrentView();
                    }
                }
            }
        }

        // ---------------------------------------------------------------
        // Year Dropdown Handlers
        // ---------------------------------------------------------------
        private void YearSelectorBtn_Click(object sender, RoutedEventArgs e)
        {
            YearPopup.IsOpen = !YearPopup.IsOpen;
        }

        // ---------------------------------------------------------------
        // View Dropdown Handlers
        // ---------------------------------------------------------------
        private void ViewSelectorBtn_Click(object sender, RoutedEventArgs e)
        {
            ViewPopup.IsOpen = !ViewPopup.IsOpen;
        }

        private void ViewOption_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string viewName)
            {
                ViewPopup.IsOpen = false;
                
                if (_currentView != viewName)
                {
                    _currentView = viewName;
                    CurrentViewText.Text = viewName;

                    // Swap the button icon to match the selected view
                    BtnIconHeatmap.Visibility   = viewName == "Heatmap"     ? Visibility.Visible : Visibility.Collapsed;
                    BtnIconLineChart.Visibility = viewName == "Line Chart"  ? Visibility.Visible : Visibility.Collapsed;
                    BtnIconBarChart.Visibility  = viewName == "Bar Chart"   ? Visibility.Visible : Visibility.Collapsed;
                    BtnIconPieChart.Visibility  = viewName == "Pie Chart"   ? Visibility.Visible : Visibility.Collapsed;

                    SaveViewPreference();

                    if (_lastData != null && _lastData.Days.Count > 0)
                    {
                        // Soft fade for view change
                        var fadeOut = new DoubleAnimation(0, TimeSpan.FromMilliseconds(200));
                        fadeOut.Completed += (s, ev) =>
                        {
                            DrawCurrentView();
                            var fadeIn = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(300));
                            ChartCanvas.BeginAnimation(UIElement.OpacityProperty, fadeIn);
                        };
                        ChartCanvas.BeginAnimation(UIElement.OpacityProperty, fadeOut);
                    }
                }
            }
        }

        private void SaveViewPreference()
        {
            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(RegKey))
                {
                    key?.SetValue("ViewMode", _currentView);
                }
            }
            catch { }
        }

        // ---------------------------------------------------------------
        // Chart Drawing Delegation
        // ---------------------------------------------------------------
        private void DrawCurrentView()
        {
            if (_lastData == null) return;

            var filteredData = FilterDataByTimeframe(_lastData);

            if (_currentView == "Bar Chart")
                DrawBarChart(filteredData.Days);
            else if (_currentView == "Line Chart")
                DrawTrendChart(filteredData.Days);
            else if (_currentView == "Heatmap")
                DrawHeatmap(filteredData.Days);
            else if (_currentView == "Pie Chart")
                DrawPieChart(filteredData);
        }

        private ContributionData FilterDataByTimeframe(ContributionData original)
        {
            if (_currentTimeframe == "1Y" || original.Days.Count == 0)
                return original;

            DateTime referenceDate = _currentYear == DateTime.Now.Year ? DateTime.Now : new DateTime(_currentYear, 12, 31);
            DateTime startDate;

            if (_currentTimeframe == "6M") startDate = referenceDate.AddMonths(-6);
            else if (_currentTimeframe == "3M") startDate = referenceDate.AddMonths(-3);
            else if (_currentTimeframe == "1M") startDate = referenceDate.AddMonths(-1);
            else startDate = referenceDate.AddYears(-1);

            var filteredDays = new List<ContributionDay>();
            var originalDict = original.Days.ToDictionary(d => d.Date.Date);

            for (DateTime d = startDate.Date; d <= referenceDate.Date; d = d.AddDays(1))
            {
                if (originalDict.TryGetValue(d, out var existing))
                {
                    filteredDays.Add(existing);
                }
                else
                {
                    filteredDays.Add(new ContributionDay { Date = d, Count = 0 });
                }
            }

            // Calculate proportionally scaled breakdown metrics for Pie Chart accuracy
            int newTotal = filteredDays.Sum(d => d.Count);
            double ratio = original.TotalContributions > 0 ? (double)newTotal / original.TotalContributions : 0;

            return new ContributionData
            {
                TotalContributions = newTotal,
                Commits = (int)(original.Commits * ratio),
                PullRequests = (int)(original.PullRequests * ratio),
                Issues = (int)(original.Issues * ratio),
                Reviews = (int)(original.Reviews * ratio),
                AvatarUrl = original.AvatarUrl,
                Years = original.Years,
                Days = filteredDays
            };
        }

        // ---------------------------------------------------------------
        // Data aggregation
        // ---------------------------------------------------------------
        private List<MonthBarData> AggregateByMonth(List<ContributionDay> days)
        {
            return days
                .GroupBy(d => new { d.Date.Year, d.Date.Month })
                .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
                .Select(g => new MonthBarData
                {
                    MonthName          = g.First().Date.ToString("MMM").ToUpper(),
                    ActiveDays         = g.Count(d => d.Count > 0),
                    TotalContributions = g.Sum(d => d.Count)
                })
                .ToList();
        }

        // ---------------------------------------------------------------
        // Bar chart drawing
        // ---------------------------------------------------------------
        private void DrawBarChart(List<ContributionDay> days)
        {
            ChartCanvas.Children.Clear();
            if (days == null || days.Count == 0) return;

            double W = ChartCanvas.ActualWidth;
            double H = ChartCanvas.ActualHeight;
            if (W < 10 || H < 10) return;

            var months = AggregateByMonth(days);
            if (months.Count == 0) return;

            // Layout margins
            const double leftM   = 42;  // space for Y labels
            const double bottomM = 28;  // space for X labels
            const double topM    = 12;
            const double rightM  = 10;

            double chartW = W - leftM - rightM;
            double chartH = H - topM  - bottomM;

            // ── Y-axis grid lines + labels ────────────────────────────
            foreach (int tick in YTicks)
            {
                double yPos = topM + chartH - (tick / (double)YMax) * chartH;

                // Horizontal grid line
                var line = new Line
                {
                    X1 = leftM, X2 = leftM + chartW,
                    Y1 = yPos,  Y2 = yPos,
                    Stroke          = new SolidColorBrush(tick == 0
                                        ? Color.FromArgb(0x60, 0xFF, 0xFF, 0xFF)
                                        : Color.FromArgb(0x20, 0xFF, 0xFF, 0xFF)),
                    StrokeThickness = tick == 0 ? 1.5 : 1,
                    StrokeDashArray = tick == 0 ? null : new DoubleCollection { 3, 3 }
                };
                ChartCanvas.Children.Add(line);

                // Y label (right-aligned to the margin)
                var lbl = new TextBlock
                {
                    Text       = tick.ToString(),
                    Foreground = new SolidColorBrush(Color.FromArgb(0xA0, 0xFF, 0xFF, 0xFF)),
                    FontSize   = 9.5
                };
                lbl.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetLeft(lbl, leftM - lbl.DesiredSize.Width - 5);
                Canvas.SetTop(lbl,  yPos - lbl.DesiredSize.Height / 2);
                ChartCanvas.Children.Add(lbl);
            }

            // ── Vertical axis line ────────────────────────────────────
            var yAxis = new Line
            {
                X1 = leftM, X2 = leftM,
                Y1 = topM,  Y2 = topM + chartH,
                Stroke          = new SolidColorBrush(Color.FromArgb(0x60, 0xFF, 0xFF, 0xFF)),
                StrokeThickness = 1.5
            };
            ChartCanvas.Children.Add(yAxis);

            // ── Bars ──────────────────────────────────────────────────
            int   n          = months.Count;
            double groupW    = chartW / n;
            double barW      = Math.Max(groupW * 0.55, 8);
            double barGap    = (groupW - barW) / 2.0;

            for (int i = 0; i < n; i++)
            {
                var m       = months[i];
                double barH = (m.ActiveDays / (double)YMax) * chartH;
                double barX = leftM + i * groupW + barGap;
                double barY = topM  + chartH - barH;

                // Gradient fill
                var grad = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 1),
                    EndPoint   = new Point(0, 0)
                };
                Color barColor = GetBarColor(m.ActiveDays);
                grad.GradientStops.Add(new GradientStop(
                    Color.FromRgb((byte)(barColor.R / 2), (byte)(barColor.G / 2), (byte)(barColor.B / 2)), 0));
                grad.GradientStops.Add(new GradientStop(barColor, 1));

                // Bar rectangle
                var rect = new Rectangle
                {
                    Width    = barW,
                    Height   = 0, // start at 0 for animation
                    Fill     = grad,
                    RadiusX  = 3,
                    RadiusY  = 3
                };
                Canvas.SetLeft(rect, barX);
                Canvas.SetTop(rect, topM + chartH); // start from bottom

                // Animate Height (growing up)
                double targetHeight = Math.Max(barH, 2);
                var heightAnim = new DoubleAnimation
                {
                    From = 0,
                    To = targetHeight,
                    Duration = TimeSpan.FromMilliseconds(600 + (i * 30)), // staggered effect
                    EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut }
                };
                rect.BeginAnimation(Rectangle.HeightProperty, heightAnim);

                // Animate Y Position (moving up to match height)
                double targetTop = barH < 2 ? topM + chartH - 2 : barY;
                var topAnim = new DoubleAnimation
                {
                    From = topM + chartH,
                    To = targetTop,
                    Duration = TimeSpan.FromMilliseconds(600 + (i * 30)),
                    EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut }
                };
                rect.BeginAnimation(Canvas.TopProperty, topAnim);

                // Tooltip
                ToolTipService.SetToolTip(rect,
                    $"{m.MonthName}  |  Active days: {m.ActiveDays}  |  Total contributions: {m.TotalContributions}");

                ChartCanvas.Children.Add(rect);

                // Value label on top of bar (show active days count)
                if (m.ActiveDays > 0)
                {
                    var valLbl = new TextBlock
                    {
                        Text       = m.ActiveDays.ToString(),
                        Foreground = new SolidColorBrush(Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF)),
                        FontSize   = 9,
                        FontWeight = FontWeights.SemiBold,
                        Opacity    = 0 // start hidden
                    };
                    valLbl.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    Canvas.SetLeft(valLbl, barX + barW / 2 - valLbl.DesiredSize.Width / 2);
                    Canvas.SetTop(valLbl,  targetTop - valLbl.DesiredSize.Height - 2);
                    
                    ChartCanvas.Children.Add(valLbl);

                    // Fade in label with a delay so it appears after the bar grows
                    var lblFade = new DoubleAnimation
                    {
                        From = 0,
                        To = 1,
                        BeginTime = TimeSpan.FromMilliseconds(400 + (i * 30)),
                        Duration = TimeSpan.FromMilliseconds(300)
                    };
                    valLbl.BeginAnimation(UIElement.OpacityProperty, lblFade);
                }

                // X-axis month label
                var xLbl = new TextBlock
                {
                    Text       = m.MonthName,
                    Foreground = new SolidColorBrush(Color.FromArgb(0xB0, 0xFF, 0xFF, 0xFF)),
                    FontSize   = 9.5
                };
                xLbl.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetLeft(xLbl, barX + barW / 2 - xLbl.DesiredSize.Width / 2);
                Canvas.SetTop(xLbl,  topM + chartH + 5);
                ChartCanvas.Children.Add(xLbl);
            }
        }

        private void DrawTrendChart(List<ContributionDay> days)
        {
            ChartCanvas.Children.Clear();
            if (days == null || days.Count == 0) return;

            double W = ChartCanvas.ActualWidth;
            double H = ChartCanvas.ActualHeight;
            if (W < 10 || H < 10) return;

            var months = AggregateByMonth(days);
            if (months.Count == 0) return;

            const double leftM   = 40;
            const double bottomM = 30;
            const double topM    = 12;
            const double rightM  = 10;

            double chartW = W - leftM - rightM;
            double chartH = H - topM  - bottomM;

            // Y-axis grid lines + labels
            foreach (int tick in YTicks)
            {
                double yPos = topM + chartH - (tick / (double)YMax) * chartH;

                var line = new Line
                {
                    X1 = leftM, X2 = leftM + chartW,
                    Y1 = yPos,  Y2 = yPos,
                    Stroke          = new SolidColorBrush(tick == 0 ? Color.FromArgb(0x60, 0xFF, 0xFF, 0xFF) : Color.FromArgb(0x20, 0xFF, 0xFF, 0xFF)),
                    StrokeThickness = tick == 0 ? 1.5 : 1,
                    StrokeDashArray = tick == 0 ? null : new DoubleCollection { 3, 3 }
                };
                ChartCanvas.Children.Add(line);

                var lbl = new TextBlock
                {
                    Text       = tick.ToString(),
                    Foreground = new SolidColorBrush(Color.FromArgb(0xA0, 0xFF, 0xFF, 0xFF)),
                    FontSize   = 9.5
                };
                lbl.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetLeft(lbl, leftM - lbl.DesiredSize.Width - 5);
                Canvas.SetTop(lbl,  yPos - lbl.DesiredSize.Height / 2);
                ChartCanvas.Children.Add(lbl);
            }

            var yAxis = new Line { X1 = leftM, X2 = leftM, Y1 = topM, Y2 = topM + chartH, Stroke = new SolidColorBrush(Color.FromArgb(0x60, 0xFF, 0xFF, 0xFF)), StrokeThickness = 1.5 };
            ChartCanvas.Children.Add(yAxis);

            // Draw Line
            int n = months.Count;
            double groupW = chartW / n;
            
            var points = new PointCollection();
            for (int i = 0; i < n; i++)
            {
                double x = leftM + i * groupW + groupW / 2;
                double y = topM + chartH - (months[i].ActiveDays / (double)YMax) * chartH;
                points.Add(new Point(x, y));
            }

            var polyline = new Polyline
            {
                Points = points,
                Stroke = new SolidColorBrush(Color.FromRgb(57, 211, 83)), // GitHub bright green
                StrokeThickness = 3,
                StrokeLineJoin = PenLineJoin.Round,
                Opacity = 0 // for animation
            };
            ChartCanvas.Children.Add(polyline);

            var lineFade = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(600)) { EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut } };
            polyline.BeginAnimation(UIElement.OpacityProperty, lineFade);

            // Data Points & X-labels
            for (int i = 0; i < n; i++)
            {
                var m = months[i];
                double x = points[i].X;
                double y = points[i].Y;

                var dot = new Ellipse
                {
                    Width = 8, Height = 8,
                    Fill = new SolidColorBrush(Color.FromRgb(35, 134, 54)), // darker green center
                    Stroke = new SolidColorBrush(Color.FromRgb(57, 211, 83)), // bright outline
                    StrokeThickness = 2,
                    Opacity = 0
                };
                ToolTipService.SetToolTip(dot, $"{m.MonthName} | Active days: {m.ActiveDays}");
                Canvas.SetLeft(dot, x - 4);
                Canvas.SetTop(dot, y - 4);
                ChartCanvas.Children.Add(dot);

                var dotFade = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(400)) { BeginTime = TimeSpan.FromMilliseconds(200 + i * 40) };
                dot.BeginAnimation(UIElement.OpacityProperty, dotFade);

                var xLbl = new TextBlock
                {
                    Text = m.MonthName,
                    Foreground = new SolidColorBrush(Color.FromArgb(0xB0, 0xFF, 0xFF, 0xFF)),
                    FontSize = 9.5
                };
                xLbl.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetLeft(xLbl, x - xLbl.DesiredSize.Width / 2);
                Canvas.SetTop(xLbl, topM + chartH + 5);
                ChartCanvas.Children.Add(xLbl);
            }
        }

        private void DrawHeatmap(List<ContributionDay> days)
        {
            ChartCanvas.Children.Clear();
            if (days == null || days.Count == 0) return;

            double W = ChartCanvas.ActualWidth;
            double H = ChartCanvas.ActualHeight;
            if (W < 10 || H < 10) return;

            const double leftM   = 35;
            const double bottomM = 25;
            const double topM    = 30; // Space for Month labels
            const double rightM  = 10;

            double chartW = W - leftM - rightM;
            double chartH = H - topM  - bottomM;

            // GitHub has 53 weeks max, 7 days a week.
            // Estimate max weeks in the dataset.
            DateTime minDate = days.Min(d => d.Date);
            DateTime maxDate = days.Max(d => d.Date);
            int totalDays = (int)(maxDate - minDate).TotalDays + 1;
            int totalCols = (totalDays / 7) + 2; // rough estimate

            // Fully utilize available space, but cap to prevent comical sizes on small timeframes
            double boxSize = Math.Min(chartW / totalCols, chartH / 7);
            boxSize = Math.Min(boxSize, 35);
            
            // Tight gap layout
            double spacing = Math.Max(2.0, boxSize * 0.15);
            double actualBoxSize = boxSize - spacing;

            // Center the grid if there is leftover space
            double gridWidth = totalCols * boxSize;
            double gridHeight = 7 * boxSize;
            double offsetX = leftM + (chartW - gridWidth) / 2.0;
            double offsetY = topM + (chartH - gridHeight) / 2.0;

            // Add subtle background glow effect to fill ALL empty space in the canvas
            var glow = new Rectangle
            {
                Width = W,
                Height = H,
                Fill = new RadialGradientBrush(Color.FromArgb(0x18, 0x39, 0xD3, 0x53), Color.FromArgb(0x00, 0x39, 0xD3, 0x53)),
                IsHitTestVisible = false
            };
            Canvas.SetLeft(glow, 0);
            Canvas.SetTop(glow, 0);
            ChartCanvas.Children.Add(glow);

            // Calculate exact starting column
            int currentCol = 0;

            // Day labels
            string[] dayLabels = { "MON", "TUE", "WED", "THU", "FRI", "SAT", "SUN" };
            for(int i=0; i<7; i++)
            {
                var lbl = new TextBlock
                {
                    Text = dayLabels[i],
                    Foreground = new SolidColorBrush(Color.FromArgb(0xA0, 0xFF, 0xFF, 0xFF)),
                    FontSize = Math.Max(9.5, boxSize * 0.45) // Scale font slightly with box size
                };
                lbl.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetLeft(lbl, offsetX - lbl.DesiredSize.Width - 8);
                Canvas.SetTop(lbl, offsetY + (i * boxSize) + (actualBoxSize - lbl.DesiredSize.Height) / 2);
                ChartCanvas.Children.Add(lbl);
            }

            int lastMonth = -1;

            for (int i = 0; i < days.Count; i++)
            {
                var d = days[i];
                int dayOfWeek = (int)d.Date.DayOfWeek;
                int currentRow = (dayOfWeek + 6) % 7; // Map Mon=0, Tue=1, ..., Sun=6
                
                // Draw Month Label if new month
                if (d.Date.Month != lastMonth && currentRow == 0)
                {
                    lastMonth = d.Date.Month;
                    var mLbl = new TextBlock
                    {
                        Text = d.Date.ToString("MMM"),
                        Foreground = new SolidColorBrush(Color.FromArgb(0xA0, 0xFF, 0xFF, 0xFF)),
                        FontSize = Math.Max(9.5, boxSize * 0.55)
                    };
                    Canvas.SetLeft(mLbl, offsetX + currentCol * boxSize);
                    Canvas.SetTop(mLbl, offsetY - 20); // Fixed 20px above the grid
                    ChartCanvas.Children.Add(mLbl);
                }

                var rect = new Rectangle
                {
                    Width = actualBoxSize,
                    Height = actualBoxSize,
                    Fill = new SolidColorBrush(GetBarColor(d.Count > 0 ? d.Count : 0)), // Map strictly using existing color scale
                    RadiusX = 2, RadiusY = 2,
                    Opacity = 0
                };
                
                // Fine-tuning color thresholds since GetBarColor was for monthly active days, not daily counts.
                // Re-calculating properly:
                Color c = GreenScale[0];
                if (d.Count == 1) c = GreenScale[1];
                else if (d.Count >= 2 && d.Count <= 4) c = GreenScale[2];
                else if (d.Count >= 5 && d.Count <= 8) c = GreenScale[3];
                else if (d.Count > 8) c = GreenScale[4];
                rect.Fill = new SolidColorBrush(c);

                ToolTipService.SetToolTip(rect, $"{d.Count} contributions on {d.Date.ToString("MMM dd, yyyy")}");

                Canvas.SetLeft(rect, offsetX + currentCol * boxSize);
                Canvas.SetTop(rect, offsetY + currentRow * boxSize);
                ChartCanvas.Children.Add(rect);

                var fade = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(300)) { BeginTime = TimeSpan.FromMilliseconds((currentCol * 10) + (currentRow * 5)) };
                rect.BeginAnimation(UIElement.OpacityProperty, fade);

                if (currentRow == 6) currentCol++;
            }

            // Legend
            double legendY = offsetY + gridHeight + 10;
            var lessLbl = new TextBlock { Text = "Less", Foreground = new SolidColorBrush(Color.FromArgb(0xA0, 0xFF, 0xFF, 0xFF)), FontSize = Math.Max(9.5, boxSize * 0.45) };
            lessLbl.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(lessLbl, offsetX);
            Canvas.SetTop(lessLbl, legendY + (actualBoxSize - lessLbl.DesiredSize.Height) / 2);
            ChartCanvas.Children.Add(lessLbl);

            double curX = offsetX + lessLbl.DesiredSize.Width + 5;
            for(int i = 0; i < 5; i++)
            {
                var rect = new Rectangle { Width = actualBoxSize, Height = actualBoxSize, Fill = new SolidColorBrush(GreenScale[i]), RadiusX = 2, RadiusY = 2 };
                Canvas.SetLeft(rect, curX);
                Canvas.SetTop(rect, legendY);
                ChartCanvas.Children.Add(rect);
                curX += boxSize;
            }

            var moreLbl = new TextBlock { Text = "More", Foreground = new SolidColorBrush(Color.FromArgb(0xA0, 0xFF, 0xFF, 0xFF)), FontSize = Math.Max(9.5, boxSize * 0.45) };
            moreLbl.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(moreLbl, curX + 5);
            Canvas.SetTop(moreLbl, legendY + (actualBoxSize - moreLbl.DesiredSize.Height) / 2);
            ChartCanvas.Children.Add(moreLbl);
        }

        private void DrawPieChart(ContributionData data)
        {
            ChartCanvas.Children.Clear();
            if (data == null || data.TotalContributions == 0) return;

            double W = ChartCanvas.ActualWidth;
            double H = ChartCanvas.ActualHeight;
            if (W < 10 || H < 10) return;

            int sum = data.Commits + data.PullRequests + data.Issues + data.Reviews;
            int other = Math.Max(0, data.TotalContributions - sum);
            int total = sum + other;
            if (total == 0) return;

            var slices = new [] {
                new { Label = "Commits", Count = data.Commits, Color = Color.FromRgb(57, 211, 83) },
                new { Label = "Pull Requests", Count = data.PullRequests, Color = Color.FromRgb(163, 113, 247) },
                new { Label = "Issues", Count = data.Issues, Color = Color.FromRgb(86, 211, 100) },
                new { Label = "Reviews", Count = data.Reviews, Color = Color.FromRgb(121, 192, 255) },
                new { Label = "Other", Count = other, Color = Color.FromRgb(48, 54, 61) }
            }.Where(s => s.Count > 0).ToList();

            double radius = Math.Min(W, H) / 2.5;
            double cx = W / 3.0;
            double cy = H / 2.0;
            double currentAngle = -90; // Start at top

            int delay = 0;
            foreach (var slice in slices)
            {
                double angle = (slice.Count / (double)total) * 360.0;
                
                var path = new Path
                {
                    Fill = new SolidColorBrush(slice.Color),
                    Opacity = 0
                };

                if (angle >= 359.9)
                {
                    path.Data = new EllipseGeometry(new Point(cx, cy), radius, radius);
                }
                else
                {
                    double startAngleRad = currentAngle * Math.PI / 180.0;
                    double endAngleRad = (currentAngle + angle) * Math.PI / 180.0;

                    Point startPoint = new Point(cx + radius * Math.Cos(startAngleRad), cy + radius * Math.Sin(startAngleRad));
                    Point endPoint = new Point(cx + radius * Math.Cos(endAngleRad), cy + radius * Math.Sin(endAngleRad));

                    var fig = new PathFigure { StartPoint = new Point(cx, cy), IsClosed = true };
                    fig.Segments.Add(new LineSegment(startPoint, false));
                    fig.Segments.Add(new ArcSegment(endPoint, new Size(radius, radius), 0, angle > 180, SweepDirection.Clockwise, false));

                    var geo = new PathGeometry();
                    geo.Figures.Add(fig);
                    path.Data = geo;
                }

                ToolTipService.SetToolTip(path, $"{slice.Label}: {slice.Count} ({(slice.Count/(double)total):P1})");
                ChartCanvas.Children.Add(path);

                var fade = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(400)) { BeginTime = TimeSpan.FromMilliseconds(delay) };
                path.BeginAnimation(UIElement.OpacityProperty, fade);

                currentAngle += angle;
                delay += 50;
            }

            // Inner circle for Donut effect
            var innerRadius = radius * 0.6;
            var innerCircle = new Ellipse
            {
                Width = innerRadius * 2, Height = innerRadius * 2,
                Fill = new SolidColorBrush(Color.FromRgb(13, 17, 23)) // Background color to "cut out" the center
            };
            Canvas.SetLeft(innerCircle, cx - innerRadius);
            Canvas.SetTop(innerCircle, cy - innerRadius);
            ChartCanvas.Children.Add(innerCircle);

            // Legend
            double legendX = W * 0.65;
            double legendY = cy - (slices.Count * 12);
            for (int i = 0; i < slices.Count; i++)
            {
                var slice = slices[i];
                var dot = new Ellipse { Width = 10, Height = 10, Fill = new SolidColorBrush(slice.Color), Opacity = 0 };
                Canvas.SetLeft(dot, legendX);
                Canvas.SetTop(dot, legendY + i * 24);
                ChartCanvas.Children.Add(dot);

                var lbl = new TextBlock
                {
                    Text = $"{slice.Label} ({slice.Count})",
                    Foreground = new SolidColorBrush(Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF)),
                    FontSize = 11,
                    Opacity = 0
                };
                Canvas.SetLeft(lbl, legendX + 16);
                Canvas.SetTop(lbl, legendY + i * 24 - 2);
                ChartCanvas.Children.Add(lbl);

                var itemFade = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(300)) { BeginTime = TimeSpan.FromMilliseconds(300 + i * 50) };
                dot.BeginAnimation(UIElement.OpacityProperty, itemFade);
                lbl.BeginAnimation(UIElement.OpacityProperty, itemFade);
            }
        }

        private Color GetBarColor(int activeDays)
        {
            if (activeDays == 0)        return GreenScale[0];
            if (activeDays <= 6)        return GreenScale[1];
            if (activeDays <= 13)       return GreenScale[2];
            if (activeDays <= 20)       return GreenScale[3];
            return GreenScale[4];
        }
    public MainWindow()
        {
            InitializeComponent();

            AutoUpdater.Start("https://yourdomain.com/update.xml");
        }
    }
    // ---------------------------------------------------------------
    // Data models
    // ---------------------------------------------------------------
    public class MonthBarData
    {
        public string MonthName          { get; set; }
        public int    ActiveDays         { get; set; }
        public int    TotalContributions { get; set; }
    }
}