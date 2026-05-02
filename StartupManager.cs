using Microsoft.Win32;
using System;
using System.Reflection;

namespace GitHubContributionWidget
{
    public static class StartupManager
    {
        private const string AppName = "GitHubContributionWidget";
        private const string RegistryKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

        /// <summary>
        /// Returns true if the app is currently registered to run at Windows startup.
        /// </summary>
        public static bool IsStartupEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, false))
                {
                    if (key == null) return false;
                    string value = key.GetValue(AppName) as string;
                    return !string.IsNullOrEmpty(value);
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Registers or unregisters the app from Windows startup.
        /// </summary>
        public static bool SetStartup(bool enable)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, true))
                {
                    if (key == null) return false;

                    if (enable)
                    {
                        // Get the full path to the running .exe
                        string exePath = Assembly.GetExecutingAssembly().Location;
                        key.SetValue(AppName, $"\"{exePath}\"");
                    }
                    else
                    {
                        // Only delete if it exists
                        if (key.GetValue(AppName) != null)
                            key.DeleteValue(AppName);
                    }

                    return true;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}