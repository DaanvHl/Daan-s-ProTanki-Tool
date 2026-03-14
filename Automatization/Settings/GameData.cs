using Automatization.Models;
using Automatization.Services;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Automatization.Settings
{
    public class GameData
    {
        public int DataVersion { get; set; } = 0;
        public List<Paint> Paints { get; set; } = [];
        public List<Turret> Turrets { get; set; } = [];

        private const int CurrentDataVersion = 1;

        private static readonly JsonSerializerOptions _options = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        public static GameData Load()
        {
            try
            {
                string path = GetDataPath();
                if (File.Exists(path))
                {
                    GameData data = JsonSerializer.Deserialize<GameData>(File.ReadAllText(path), _options) ?? new GameData();

                    // Seed defaults if both lists are empty (fresh or wiped file)
                    if (data.Paints.Count == 0 && data.Turrets.Count == 0)
                    {
                        data = GameDataDefaults.Create();
                        data.DataVersion = CurrentDataVersion;
                        data.Save();
                    }
                    // Migrate: rebuild turrets if Range/CanDefend data is missing
                    else if (data.DataVersion < CurrentDataVersion)
                    {
                        data.Turrets = GameDataDefaults.CreateTurrets();
                        data.DataVersion = CurrentDataVersion;
                        data.Save();
                    }

                    return data;
                }
            }
            catch (Exception ex)
            {
                LogService.LogError("Failed to load game data.", ex);
            }

            // First run — seed with defaults and persist
            GameData defaults = GameDataDefaults.Create();
            defaults.Save();
            return defaults;
        }

        public void Save()
        {
            try
            {
                File.WriteAllText(GetDataPath(), JsonSerializer.Serialize(this, _options));
            }
            catch (Exception ex)
            {
                LogService.LogError("Failed to save game data.", ex);
            }
        }

        private static string GetDataPath()
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TankAutomation");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return Path.Combine(dir, "gamedata.json");
        }
    }
}
