using Serilog;
using System.IO;

namespace Automatization.Services
{
    public static class LogService
    {
        private static string GetLogDirectory()
        {
            string settingsDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "TankAutomation",
                "Logs"
            );

            if (!Directory.Exists(settingsDir))
            {
                _ = Directory.CreateDirectory(settingsDir);
            }

            return settingsDir;
        }

        public static void Initialize()
        {
            string logDirectory = GetLogDirectory();
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File(Path.Combine(logDirectory, $"log-{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt"), shared: true)
                .CreateLogger();
        }

        public static void LogInfo(string message)
        {
            Log.Information(message);
        }

        public static void LogWarning(string message)
        {
            Log.Warning(message);
        }

        public static void LogError(string message, Exception? ex = null)
        {
            Log.Error(ex, message);
        }

        public static void Shutdown()
        {
            Log.CloseAndFlush();
        }

        public static void CleanOldLogs()
        {
            try
            {
                string logDirectory = GetLogDirectory();
                DateTime cutoffDate = DateTime.Now.AddDays(-14);

                foreach (string file in Directory.EnumerateFiles(logDirectory, "log-*.txt"))
                {
                    FileInfo fi = new(file);
                    if (fi.CreationTime < cutoffDate)
                    {
                        try
                        {
                            fi.Delete();
                            LogInfo($"Deleted old log file: {fi.Name}");
                        }
                        catch (Exception ex)
                        {
                            LogError($"Failed to delete log file {fi.Name}.", ex);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogError("Error during log cleanup.", ex);
            }
        }
    }
}
