using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;

namespace GitHubContributionWidget
{
    public partial class LoginWindow : Window
    {
        private const string CREDENTIALS_FILE = "github_credentials.dat";

        public string Username { get; private set; }
        public string Token { get; private set; }

        public LoginWindow()
        {
            InitializeComponent();
            LoadSavedCredentials();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Enable blur effect
            WindowBlur.EnableBlur(this);
            UsernameTextBox.Focus();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            string username = UsernameTextBox.Text.Trim();
            string token = TokenPasswordBox.Password.Trim();

            // Validate inputs
            if (string.IsNullOrEmpty(username))
            {
                ShowError("Please enter your GitHub username");
                UsernameTextBox.Focus();
                return;
            }

            if (string.IsNullOrEmpty(token))
            {
                ShowError("Please enter your Personal Access Token");
                TokenPasswordBox.Focus();
                return;
            }

            // Disable button and show loading
            LoginButton.IsEnabled = false;
            LoginButton.Content = "Verifying...";
            HideError();

            try
            {
                // Test the credentials
                var githubService = new GitHubService(username, token);
                var data = await githubService.GetContributionsAsync();

                // If we get here, credentials are valid
                Username = username;
                Token = token;

                // Save credentials if remember me is checked
                if (RememberMeCheckBox.IsChecked == true)
                {
                    SaveCredentials(username, token);
                }
                else
                {
                    DeleteSavedCredentials();
                }

                // Set DialogResult to true to signal success
                DialogResult = true;

                // Close the login window
                this.Close();
            }
            catch (Exception ex)
            {
                ShowError($"Login failed: {ex.Message}");
                LoginButton.IsEnabled = true;
                LoginButton.Content = "Login";
                TokenPasswordBox.Focus();
            }
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = e.Uri.AbsoluteUri,
                    UseShellExecute = true
                });
            }
            catch { }
            e.Handled = true;
        }

        private void ShowError(string message)
        {
            ErrorMessage.Text = message;
            ErrorMessageBorder.Visibility = Visibility.Visible;
        }

        private void HideError()
        {
            ErrorMessageBorder.Visibility = Visibility.Collapsed;
        }

        private void LoadSavedCredentials()
        {
            try
            {
                if (File.Exists(CREDENTIALS_FILE))
                {
                    string encryptedData = File.ReadAllText(CREDENTIALS_FILE);
                    string decryptedData = DecryptString(encryptedData);
                    string[] parts = decryptedData.Split('|');

                    if (parts.Length == 2)
                    {
                        UsernameTextBox.Text = parts[0];
                        TokenPasswordBox.Password = parts[1];
                        RememberMeCheckBox.IsChecked = true;
                    }
                }
            }
            catch
            {
                // If decryption fails, just ignore and start fresh
                DeleteSavedCredentials();
            }
        }

        private void SaveCredentials(string username, string token)
        {
            try
            {
                string data = $"{username}|{token}";
                string encryptedData = EncryptString(data);
                File.WriteAllText(CREDENTIALS_FILE, encryptedData);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save credentials: {ex.Message}",
                    "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void DeleteSavedCredentials()
        {
            try
            {
                if (File.Exists(CREDENTIALS_FILE))
                {
                    File.Delete(CREDENTIALS_FILE);
                }
            }
            catch { }
        }

        // Simple encryption using machine-specific key
        private string EncryptString(string plainText)
        {
            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
            byte[] encryptedBytes = ProtectedData.Protect(
                plainBytes,
                null,
                DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encryptedBytes);
        }

        private string DecryptString(string encryptedText)
        {
            byte[] encryptedBytes = Convert.FromBase64String(encryptedText);
            byte[] decryptedBytes = ProtectedData.Unprotect(
                encryptedBytes,
                null,
                DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(decryptedBytes);
        }

        // Focus event handlers for border highlighting
        private void TextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            UsernameBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(31, 111, 235)); // #1f6feb
            UsernameBorder.BorderThickness = new Thickness(2);
        }

        private void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            UsernameBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(192, 192, 192)); // #C0C0C0
            UsernameBorder.BorderThickness = new Thickness(1);
        }

        private void PasswordBox_GotFocus(object sender, RoutedEventArgs e)
        {
            TokenBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(31, 111, 235)); // #1f6feb
            TokenBorder.BorderThickness = new Thickness(2);
        }

        private void PasswordBox_LostFocus(object sender, RoutedEventArgs e)
        {
            TokenBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(192, 192, 192)); // #C0C0C0
            TokenBorder.BorderThickness = new Thickness(1);
        }
    }
}