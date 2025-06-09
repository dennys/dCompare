

namespace dCompare.Configuration
{
    public class AppConfiguration
    {
        public Dictionary<string, SessionConfig> Sessions { get; set; } = new();
        public OptionsConfig Options { get; set; } = new();
    }

    public class SessionConfig
    {
        public string Left { get; set; } = string.Empty;
        public string Right { get; set; } = string.Empty;
    }

    public class OptionsConfig
    {
        public string Theme { get; set; } = "light";
        public string Language { get; set; } = "en-US";
        public Dictionary<string, string> HotKeys { get; set; } = new()
        {
            { "AddTab", "Ctrl+N" },
            { "CloseTab", "Ctrl+W" },
            { "Compare", "Ctrl+R" },
            { "SaveSession", "Ctrl+S" },
            { "LoadSession", "Ctrl+O" }
        };
        public LogConfig Log { get; set; } = new();
    }

    public class LogConfig
    {
        public bool Enabled { get; set; } = true;
        public string Level { get; set; } = "Information";
        public int RetentionDays { get; set; } = 10;
    }
}
