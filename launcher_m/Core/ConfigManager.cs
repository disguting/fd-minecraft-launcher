using System;
using System.IO;
using System.Text.Json;
using System.Windows; 
using launcher_m.Models;

namespace launcher_m.Core
{
    public static class ConfigManager
    {
        private static readonly string BasePath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
    ".FDlauncher"
); private static readonly string ConfigPath = Path.Combine(BasePath, "launcher_profiles.json");

        public static LauncherData Data { get; private set; } = new LauncherData();

        public static void Load()
        {
            try
            {
                if (!Directory.Exists(BasePath))
                {
                    Directory.CreateDirectory(BasePath);
                }

                if (File.Exists(ConfigPath))
                {
                    string json = File.ReadAllText(ConfigPath);
                    Data = JsonSerializer.Deserialize<LauncherData>(json) ?? new LauncherData();
                }
                else
                {
                    
                    Data = new LauncherData();
                    Save();
                }
            }
            catch (Exception ex)
            {
                string errFmt = Application.Current?.TryFindResource("Loc_ConfigReadError") as string ?? "Помилка читання конфігу: {0}";
                Console.WriteLine(string.Format(errFmt, ex.Message));
                Data = new LauncherData();
            }
        }

        public static void Save()
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(Data, options);
                File.WriteAllText(ConfigPath, json);
            }
            catch (Exception ex)
            {
                string errFmt = Application.Current?.TryFindResource("Loc_ConfigSaveError") as string ?? "Помилка збереження конфігу: {0}";
                Console.WriteLine(string.Format(errFmt, ex.Message));
            }
        }
    }
}