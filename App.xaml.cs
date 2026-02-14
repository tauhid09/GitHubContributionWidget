using System;
using System.Windows;

namespace GitHubContributionWidget
{
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            // Set shutdown mode to manual
            this.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            try
            {
                // Show login window first
                var loginWindow = new LoginWindow();
                bool? result = loginWindow.ShowDialog();

                if (result == true)
                {
                    // Login successful, create and show main window with credentials
                    string username = loginWindow.Username;
                    string token = loginWindow.Token;

                    var mainWindow = new MainWindow(username, token);

                    // Change shutdown mode back to close on last window
                    this.ShutdownMode = ShutdownMode.OnMainWindowClose;
                    this.MainWindow = mainWindow;

                    mainWindow.Show();
                }
                else
                {
                    // Login cancelled or closed, exit application
                    this.Shutdown();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Startup Error: {ex.Message}\n\nStack Trace: {ex.StackTrace}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                this.Shutdown();
            }
        }
    }
}