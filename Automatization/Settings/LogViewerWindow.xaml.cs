using System.IO;
using System.Windows;

namespace Automatization.Settings
{
    public partial class LogViewerWindow : Window
    {
        public LogViewerWindow()
        {
            InitializeComponent();
            LoadLatestLog();
        }

        private void LoadLatestLog()
        {
            try
            {
                string logDirectory = GetLogDirectory();
                DirectoryInfo directory = new(logDirectory);
                FileInfo? logFile = directory.GetFiles("*.txt")
                                       .OrderByDescending(f => f.LastWriteTime)
                                       .FirstOrDefault();

                if (logFile != null)
                {
                    string tempFilePath = Path.GetTempFileName();
                    File.Copy(logFile.FullName, tempFilePath, true);
                    LogTextBox.Text = File.ReadAllText(tempFilePath);
                    File.Delete(tempFilePath);
                    LogTextBox.ScrollToEnd();
                }
                else
                {
                    LogTextBox.Text = "No log files found.";
                }
            }
            catch (Exception ex)
            {
                LogTextBox.Text = $"Error loading log file: {ex.Message}";
            }
        }

        private static string GetLogDirectory()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "TankAutomation",
                "Logs"
            );
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            LoadLatestLog();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
