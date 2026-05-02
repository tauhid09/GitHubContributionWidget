using System;
using System.Windows;

namespace GitHubContributionWidget
{
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            // Set shutdown mode to manual so we control when the app exits
            this.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            try
            {
                // If credentials are saved, open the widget IMMEDIATELY — no blocking API call.
                // The widget itself fetches data in the background after it appears.
                if (LoginWindow.TryGetSavedCredentials(out string savedUser, out string savedToken))
                {
                    var mw = new MainWindow(savedUser, savedToken);
                    this.ShutdownMode = ShutdownMode.OnMainWindowClose;
                    this.MainWindow = mw;
                    mw.Show();
                    return;
                }

                // No saved credentials — show the login window
                var loginWindow = new LoginWindow();
                bool? result = loginWindow.ShowDialog();

                if (result == true)
                {
                    string username = loginWindow.Username;
                    string token   = loginWindow.Token;

                    var mainWindow = new MainWindow(username, token);
                    this.ShutdownMode = ShutdownMode.OnMainWindowClose;
                    this.MainWindow   = mainWindow;
                    mainWindow.Show();
                }
                else
                {
                    // Login cancelled or closed — exit
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