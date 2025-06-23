using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace dCompare.Configuration
{
    public class SessionConfig
    {
        public string Left { get; set; } = string.Empty;
        public string Right { get; set; } = string.Empty;
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ComparisonAlgorithm Algorithm { get; set; } = ComparisonAlgorithm.ModifiedDateAndSize;
    }

    /// <summary>
    /// Represents the root of the application configuration.
    /// </summary>
    public class AppConfiguration
    {
        public Dictionary<string, SessionConfig> Sessions { get; set; } = new();
        [JsonPropertyName("Options")]
        public Options Options { get; set; } = new();
    }

    public class Options
    {
        [JsonPropertyName("UI")]
        public UIOptions UI { get; set; } = new();

        [JsonPropertyName("Compare")]
        public CompareOptions Compare { get; set; } = new();

        [JsonPropertyName("Diff")]
        public DiffOptions Diff { get; set; } = new();

        [JsonPropertyName("Session")]
        public SessionOptions Session { get; set; } = new();

        [JsonPropertyName("HotKeys")]
        public HotKeysOptions HotKeys { get; set; } = new();

        [JsonPropertyName("Log")]
        public LogOptions Log { get; set; } = new();
    }

    public class UIOptions
    {
        [JsonPropertyName("Theme")]
        public string Theme { get; set; } = "Light";

        [JsonPropertyName("Language")]
        public string Language { get; set; } = "zh-TW";
    }

    public class CompareOptions
    {
        [JsonPropertyName("Algorithm")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ComparisonAlgorithm Algorithm { get; set; } = ComparisonAlgorithm.ModifiedDateAndSize;

        [JsonPropertyName("DoubleClick")]
        public string DoubleClick { get; set; } = "view";
    }

    public class DiffOptions
    {
        [JsonPropertyName("Text")]
        public TextDiffOptions Text { get; set; } = new();

        [JsonPropertyName("Image")]
        public ImageDiffOptions Image { get; set; } = new();
    }

    public class TextDiffOptions
    {
        [JsonPropertyName("Ext")]
        public List<string> Ext { get; set; } = new();

        [JsonPropertyName("Viewer")]
        public string Viewer { get; set; } = "DiffPlexQ";
    }

    public class ImageDiffOptions
    {
        [JsonPropertyName("Ext")]
        public List<string> Ext { get; set; } = new();

        [JsonPropertyName("Viewer")]
        public string Viewer { get; set; } = "Not implement";
    }

    public class SessionOptions
    {
        [JsonPropertyName("DefaultSessionFile")]
        public string DefaultSessionFile { get; set; } = "";

        [JsonPropertyName("AutoLoadDefaultSession")]
        public bool AutoLoadDefaultSession { get; set; }

        [JsonPropertyName("AutoCompareAfterSessionLoad")]
        public bool AutoCompareAfterSessionLoad { get; set; }
    }

    public class HotKeysOptions
    {
        [JsonPropertyName("AddTab")]
        public string AddTab { get; set; } = "Ctrl+N";

        [JsonPropertyName("CloseTab")]
        public string CloseTab { get; set; } = "Ctrl+W";

        [JsonPropertyName("Compare")]
        public string Compare { get; set; } = "Ctrl+R";

        [JsonPropertyName("SaveSession")]
        public string SaveSession { get; set; } = "Ctrl+S";

        [JsonPropertyName("LoadSession")]
        public string LoadSession { get; set; } = "Ctrl+O";
    }

    public class LogOptions
    {
        [JsonPropertyName("Enabled")]
        public bool Enabled { get; set; } = true;

        [JsonPropertyName("Level")]
        public string Level { get; set; } = "Information";

        [JsonPropertyName("RetentionDays")]
        public int RetentionDays { get; set; } = 10;
    }
}