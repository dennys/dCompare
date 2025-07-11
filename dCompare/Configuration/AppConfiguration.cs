using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace dCompare.Configuration
{
    public class AppConfiguration
    {
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

        [JsonPropertyName("Confirmation")]
        public ConfirmationOptions Confirmation { get; set; } = new();

        [JsonPropertyName("Session")]
        public SessionOptions Session { get; set; } = new();

        [JsonPropertyName("HotKeys")]
        public HotKeysOptions HotKeys { get; set; } = new();
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
        [JsonPropertyName("ScanMethod")]
        public ScanMethodOptions ScanMethod { get; set; } = new();

        [JsonPropertyName("Algorithms")]
        public Dictionary<ComparisonAlgorithm, ComparisonAlgorithmInfo> Algorithms { get; set; } =
            new Dictionary<ComparisonAlgorithm, ComparisonAlgorithmInfo>(ComparisonAlgorithms.AlgorithmSettings);

        [JsonPropertyName("DoubleClick")]
        public string DoubleClick { get; set; } = "view";
    }

    public class ScanMethodOptions
    {
        [JsonPropertyName("Method")]
        public string MethodOptions { get; set; } = "General";

        [JsonPropertyName("Parallel")]
        public bool ParallelOptions { get; set; } = true;
    }

    // New class for external viewer configuration
    public class ExternalViewer
    {
        [JsonPropertyName("Name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("Path")]
        public string Path { get; set; } = string.Empty;

        [JsonPropertyName("Arguments")]
        public string Arguments { get; set; } = string.Empty;

        [JsonPropertyName("SupportedFormats")]
        public string SupportedFormats { get; set; } = string.Empty;
    }

    public class DiffOptions
    {
        [JsonPropertyName("Text")]
        public TextDiffOptions Text { get; set; } = new();

        [JsonPropertyName("Image")]
        public ImageDiffOptions Image { get; set; } = new();

        // Updated to support a list of viewers
        [JsonPropertyName("Viewer")]
        public List<ExternalViewer> Viewer { get; set; } = new()
        {
            new ExternalViewer { Name = "WinMerge", Path = "", Arguments = "/e /u \"{left}\" \"{right}\"" }
        };
    }

    public class TextDiffOptions
    {
        [JsonPropertyName("Ext")]
        public List<string> Ext { get; set; } = new List<string> { ".txt", ".log", ".cs", ".json", ".xml", ".html", ".css", ".js", ".py", ".md", ".sql" };

        [JsonPropertyName("Viewer")]
        public string Viewer { get; set; } = "DiffPlex";
    }

    public class ImageDiffOptions
    {
        [JsonPropertyName("Ext")]
        public List<string> Ext { get; set; } = new List<string> { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tiff", ".webp" };

        [JsonPropertyName("Viewer")]
        public string Viewer { get; set; } = "Build-in";
    }

    public class ConfirmationOptions
    {
        [JsonPropertyName("CloseApplication")]
        public bool CloseApplication { get; set; } = false;
        [JsonPropertyName("CloseTab")]
        public bool CloseTab { get; set; } = false;
        [JsonPropertyName("Copy")]
        public bool Copy { get; set; } = false;
        [JsonPropertyName("Move")]
        public bool Move { get; set; } = false;
        [JsonPropertyName("CopyOverwrite")]
        public bool CopyOverwrite { get; set; } = true;
        [JsonPropertyName("MoveOverwrite")]
        public bool MoveOverwrite { get; set; } = true;
        [JsonPropertyName("DeleteFile")]
        public bool DeleteFile { get; set; } = true;
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

        [JsonPropertyName("OpenOptions")]
        public string OpenOptions { get; set; } = "Ctrl+Shift+O";
    }

    public class LogOptions
    {
        [JsonPropertyName("Enabled")]
        public bool Enabled { get; set; } = true;

        [JsonPropertyName("Using")]
        public List<string> Using { get; set; } = ["Serilog.Sinks.File"];

        [JsonPropertyName("MinimumLevel")]
        public string MinimumLevel { get; set; } = "Information";

        [JsonPropertyName("Enrich")]
        public List<string> Enrich { get; set; } = ["WithThreadId"];

        [JsonPropertyName("WriteTo")]
        public List<SerilogWriteToOption> WriteTo { get; set; } = new();
    }

    public class SerilogWriteToOption
    {
        [JsonPropertyName("Name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("Args")]
        public SerilogSinkArgs Args { get; set; } = new();
    }

    public class SerilogSinkArgs
    {
        [JsonPropertyName("path")]
        public string Path { get; set; } = "logs/dCompare.log";

        [JsonPropertyName("rollingInterval")]
        public string RollingInterval { get; set; } = "Day";

        [JsonPropertyName("outputTemplate")]
        public string OutputTemplate { get; set; } =
            "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] [T{ThreadId:D2}] {Message:lj}{NewLine}{Exception}";
    }
}