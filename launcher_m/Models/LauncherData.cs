using System;
using System.Collections.Generic;

namespace launcher_m.Models
{
    public class AccountProfile
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Username { get; set; } = string.Empty;
        public string UUID { get; set; } = string.Empty;
        public string AccessToken { get; set; } = string.Empty; 
        public bool IsOffline { get; set; } = true;
        public string LastLoginType { get; set; } = "Offline"; 
    }

    public class GameInstance
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "Нова збірка";
        public string GameVersion { get; set; } = "1.20.1";
        public string LoaderType { get; set; } = "Vanilla";
        public string IconSymbol { get; set; } = "Box24";
    }

    public class LauncherSettings
    {
        public string Language { get; set; } = "uk-UA";
        public string ActiveAccountId { get; set; } = string.Empty;
        public string ActiveInstanceId { get; set; } = string.Empty;

        public int MaxRamMb { get; set; } = 4096;
        public bool FullScreen { get; set; } = false;
        public int WindowWidth { get; set; } = 854;
        public int WindowHeight { get; set; } = 480;

        public bool ShowSnapshots { get; set; } = false;
        public bool ShowAlphaBeta { get; set; } = false;
        public string JvmArguments { get; set; } = "-XX:+UnlockExperimentalVMOptions -XX:+UseG1GC";

        public bool EnableGradient { get; set; } = true; 
    }

    public class LauncherData
    {
        public LauncherSettings Settings { get; set; } = new LauncherSettings();
        public List<AccountProfile> Accounts { get; set; } = new List<AccountProfile>();
        public List<GameInstance> Instances { get; set; } = new List<GameInstance>();
    }
}