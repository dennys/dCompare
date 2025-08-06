using dCompare.Common;
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

        [JsonPropertyName("Confirmation")]
        public ConfirmationOptions Confirmation { get; set; } = new();

        [JsonPropertyName("Session")]
        public SessionOptions Session { get; set; } = new();

        [JsonPropertyName("HotKeys")]
        public HotKeysOptions HotKeys { get; set; } = new();

        [JsonPropertyName("TextViewer")]
        public TextViewerOptions TextViewer { get; set; } = new();
        [JsonPropertyName("Tools")]
        public Dictionary<string, ToolOptions> Tools { get; set; } = new();

        [JsonPropertyName("FileTypeMapping")]
        public Dictionary<string, FileTypeMappingOptions> FileTypeMapping { get; set; } = new();
    }

    public class FontOptions
    {
        [JsonPropertyName("FontFamily")]
        public string FontFamily { get; set; } = "Consolas";

        [JsonPropertyName("FontSize")]
        public int FontSize { get; set; } = 12;
    }

    public class TextViewerOptions
    {
        [JsonPropertyName("ReadOnly")]
        public bool ReadOnly { get; set; } = false;

        [JsonPropertyName("LineNumber")]
        public bool LineNumber { get; set; } = true;

        [JsonPropertyName("SyntaxHighlight")]
        public bool SyntaxHighlight { get; set; } = true;

        [JsonPropertyName("Font")]
        public FontOptions Font { get; set; } = new();

        [JsonPropertyName("WordWrap")]
        public bool WordWrap { get; set; } = false;

        [JsonPropertyName("HorizontalScrollBarVisibility")]
        public string HorizontalScrollBarVisibility { get; set; } = "Auto";

        [JsonPropertyName("VerticalScrollBarVisibility")]
        public string VerticalScrollBarVisibility { get; set; } = "Auto";

        [JsonPropertyName("ShowStatusBar")]
        public bool ShowStatusBar { get; set; } = true;
    }

    public class UIOptions
    {
        [JsonPropertyName("Theme")]
        public string Theme { get; set; } = "Light";

        [JsonPropertyName("Language")]
        public string Language { get; set; } = "en";
    }

    public class CompareOptions
    {
        [JsonPropertyName("ScanMethod")]
        public ScanMethodOptions ScanMethod { get; set; } = new();

        [JsonPropertyName("Algorithms")]
        public Dictionary<ComparisonAlgorithm, ComparisonAlgorithmInfo> Algorithms { get; set; } =
            new Dictionary<ComparisonAlgorithm, ComparisonAlgorithmInfo>(ComparisonAlgorithms.AlgorithmSettings);

        [JsonPropertyName("DoubleClick")]
        public DoubleClickActions DoubleClick { get; set; } = DoubleClickActions.Compare;
    }

    public class ScanMethodOptions
    {
        [JsonPropertyName("Method")]
        public string MethodOptions { get; set; } = "General";

        [JsonPropertyName("Parallel")]
        public bool ParallelOptions { get; set; } = true;
    }

    public class ToolOptions
    {
        [JsonPropertyName("Name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("Type")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ToolTypes Type { get; set; } = ToolTypes.BuiltIn;

        [JsonPropertyName("Handler")]
        public string Handler { get; set; } = string.Empty;

        [JsonPropertyName("Path")]
        public string Path { get; set; } = string.Empty;

        [JsonPropertyName("DiffArguments")]
        public string DiffArguments { get; set; } = string.Empty;

        [JsonPropertyName("ViewArguments")]
        public string ViewArguments { get; set; } = string.Empty;

        [JsonPropertyName("SupportedOperations")]
        //[JsonConverter(typeof(JsonStringEnumConverter))]
        public List<Operations> SupportedOperations { get; set; } = new();

        [JsonPropertyName("SupportedExtensions")]
        public List<string> SupportedExtensions { get; set; } = new();

        [JsonPropertyName("Features")]
        public List<string> Features { get; set; } = new();

        [JsonPropertyName("Enabled")]
        public bool Enabled { get; set; } = true;

        public bool SupportsOperation(Operations operation) =>
            SupportedOperations.Contains(operation);

        public bool SupportsExtension(string extension) =>
            SupportedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    public class FileTypeMappingOptions
    {
        [JsonPropertyName("Extensions")]
        public List<string> Extensions { get; set; } = new();

        [JsonPropertyName("DefaultDiffTool")]
        public string DefaultDiffTool { get; set; } = string.Empty;

        [JsonPropertyName("DefaultViewTool")]
        public string DefaultViewTool { get; set; } = string.Empty;

        [JsonPropertyName("PreferredTools")]
        public PreferredToolsConfiguration PreferredTools { get; set; } = new();
    }

    public class PreferredToolsConfiguration
    {
        [JsonPropertyName("diff")]
        public List<string> Diff { get; set; } = new();

        [JsonPropertyName("view")]
        public List<string> View { get; set; } = new();
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

    public enum ToolTypes
    {
        BuiltIn,
        External
    }

    public enum Operations
    {
        View,
        Diff
    }

}