using ProtankiTool.Hotkeys;
using ProtankiTool.Services;
using ProtankiTool.Types;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Input;
using Point = System.Windows.Point;

namespace ProtankiTool.Settings;
public class AppSettings
{
    public Dictionary<PowerupType, double> PowerupDelays { get; set; } = [];
    public Dictionary<PowerupType, Key> PowerupKeys { get; set; } = new()
    {
        { PowerupType.RepairKit, Key.D1 },
        { PowerupType.DoubleArmor, Key.D2 },
        { PowerupType.DoubleDamage, Key.D3 },
        { PowerupType.SpeedBoost, Key.D4 },
        { PowerupType.Mine, Key.D5 }
    };
    public List<PowerupType> GlobalToggleEnabledPowerups { get; set; } =
        Enum.GetValues<PowerupType>().ToList();

    public HotKey GlobalHotKey { get; set; } = new(Key.F6, ModifierKeys.None);
    public HotKey RedTeamHotKey { get; set; } = new(Key.F7, ModifierKeys.None);
    public HotKey BlueTeamHotKey { get; set; } = new(Key.F8, ModifierKeys.None);
    public HotKey GoldBoxTimerHotKey { get; set; } = new(Key.F9, ModifierKeys.None);
    public HotKey RailgunModeHotKey { get; set; } = new();
    public ThemeType Theme { get; set; } = ThemeType.Dark;
    public string? GameExecutablePath { get; set; } = null;
    public double ClickSpeed { get; set; } = 10;
    public bool HotkeysPaused { get; set; } = false;
    public bool IsTimerWindowTransparent { get; set; } = false;
    public string GameProcessName { get; set; } = "ProTanki";

    public Point RedTeamCoordinates { get; set; } = new(1186, 1017);
    public Point BlueTeamCoordinates { get; set; } = new(1544, 1009);

    public string? GetActionForHotKey(HotKey hotKey)
    {
        if (hotKey == GlobalHotKey)
        {
            return "ToggleAll";
        }

        return hotKey == RedTeamHotKey
            ? "RedTeam"
            : hotKey == BlueTeamHotKey ? "BlueTeam"
            : hotKey == GoldBoxTimerHotKey ? "StartTimer"
            : hotKey == RailgunModeHotKey ? "RailgunMode"
            : null;
    }

    public static AppSettings Load()
    {
        try
        {
            string settingsPath = GetSettingsPath();
            if (File.Exists(settingsPath))
            {
                string json = File.ReadAllText(settingsPath);
                JsonSerializerOptions options = new() { PropertyNameCaseInsensitive = true };

                return JsonSerializer.Deserialize<AppSettings>(json, options) ?? new AppSettings();
            }
        }
        catch (Exception ex)
        {
            LogService.LogError("Failed to load settings.", ex);
        }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            string settingsPath = GetSettingsPath();
            JsonSerializerOptions options = new() { WriteIndented = true };
            string json = JsonSerializer.Serialize(this, options);
            File.WriteAllText(settingsPath, json);
        }
        catch (Exception ex)
        {
            LogService.LogError("Failed to save settings.", ex);
        }
    }

    private static string GetSettingsPath()
    {
        string settingsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "TankAutomation"
        );

        if (!Directory.Exists(settingsDir))
        {
            _ = Directory.CreateDirectory(settingsDir);
        }

        return Path.Combine(settingsDir, "appsettings.json");
    }
}