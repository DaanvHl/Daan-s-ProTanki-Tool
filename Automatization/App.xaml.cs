using Automatization.Services;
using Automatization.Settings;
using Automatization.Types;
using System.Windows;

namespace Automatization;
public partial class App : System.Windows.Application
{
    public static AppSettings Settings { get; private set; } = null!;

    private static ResourceDictionary? _currentThemeDictionary;

    private void Application_Startup(object sender, StartupEventArgs e)
    {
        LogService.Initialize();
        LogService.CleanOldLogs();
        LogService.LogInfo("Application starting.");

        Settings = AppSettings.Load();

        ApplyTheme(Settings.Theme);

        MainWindow mainWindow = new();
        mainWindow.Show();

        LogService.LogInfo("Main window shown.");
    }

    private void Application_Exit(object sender, ExitEventArgs e)
    {
        LogService.LogInfo("Application shutting down.");
        LogService.Shutdown();
    }

    public static void ApplyTheme(ThemeType mode)
    {
        string themePath = mode == ThemeType.Light
            ? "Themes/Light.xaml"
            : "Themes/Dark.xaml";

        try
        {
            ResourceDictionary dict = new() { Source = new Uri(themePath, UriKind.Relative) };

            if (_currentThemeDictionary != null)
            {
                _ = Current.Resources.MergedDictionaries.Remove(_currentThemeDictionary);
            }

            Current.Resources.MergedDictionaries.Add(dict);
            _currentThemeDictionary = dict;

            LogService.LogInfo($"Theme changed to {mode}.");
        }
        catch (Exception)
        {
            LogService.LogWarning($"Failed to apply theme: {themePath}");
        }
    }
}
