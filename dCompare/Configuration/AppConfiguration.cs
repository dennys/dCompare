using System.Collections.Generic;

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
        public UIConfig UI { get; set; } = new();
        public CompareConfig Compare { get; set; } = new();
        public DiffConfig Diff { get; set; } = new();
        public SessionConfigOptions Session { get; set; } = new();
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

    public class UIConfig
    {
        public string Theme { get; set; } = "Light";
        public string Language { get; set; } = "en-US";
    }

    public class CompareConfig
    {
        public string Algorithm { get; set; } = "Quick";
        public string DoubleClick { get; set; } = "compare";
    }

    public class DiffConfig
    {
        public TextDiffConfig Text { get; set; } = new();
        public ImageDiffConfig Image { get; set; } = new();
    }

    public class TextDiffConfig
    {
        public List<string> Ext { get; set; } = new() { ".cs", ".json", ".log", ".md", ".txt", ".xaml", ".xml" };
        public string Viewer { get; set; } = "DiffPlex";
    }

    public class ImageDiffConfig
    {
        public List<string> Ext { get; set; } = new() { ".png", ".jpg", ".jpeg", ".bmp" };
        public string Viewer { get; set; } = "Not implement";
    }

    public class SessionConfigOptions
    {
        public string DefaultSessionFile { get; set; } = string.Empty;
        public bool AutoLoadDefaultSession { get; set; } = false;
        public bool AutoCompareAfterSessionLoad { get; set; }
    }

    public class LogConfig
    {
        public bool Enabled { get; set; } = true;
        public string Level { get; set; } = "Information";
        public int RetentionDays { get; set; } = 10;
    }
}