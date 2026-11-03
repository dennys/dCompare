using dCompare.Common;

namespace dCompare.Configuration
{
    /// <summary>
    /// The tools and extension mappings a first run needs. A fresh install had none: AppConfiguration starts
    /// with empty dictionaries, LoadConfigurationAsync then wrote that empty set to dCompare.json, and every
    /// View, Diff and double-click command dead-ended at "no default tool". The working set existed only as an
    /// untracked dCompare.json next to one developer's exe, so it could not be reproduced or reviewed.
    ///
    /// Only the four built-in entries below are listed (three real <see cref="ITool"/> types; ImageViewer serves
    /// as both the image viewer and the image diff). Handlers are assembly-qualified because ToolFactory resolves
    /// them with Type.GetType, which needs the assembly name to find types outside the calling assembly.
    /// </summary>
    public static class DefaultConfiguration
    {
        private const string TextViewerHandler = "dCompare.Tools.Viewer.TextViewer, dCompare";
        private const string ImageViewerHandler = "dCompare.Tools.Viewer.ImageViewer, dCompare";
        private const string TextDiffHandler = "dCompare.Tools.Diff.TextDiffPlex, dCompare";

        public const string TextViewerTool = "InternalTextViewer";
        public const string ImageViewerTool = "InternalImageViewer";
        public const string TextDiffTool = "InternalTextDiff";
        public const string ImageDiffTool = "InternalImageDiff";

        public static Dictionary<string, ToolOptions> Tools() => new()
        {
            [TextViewerTool] = BuiltIn(TextViewerTool, TextViewerHandler, ToolOperationTypes.View, FileExtensions.TextExtensions),
            [TextDiffTool] = BuiltIn(TextDiffTool, TextDiffHandler, ToolOperationTypes.Diff, FileExtensions.TextExtensions),
            [ImageViewerTool] = BuiltIn(ImageViewerTool, ImageViewerHandler, ToolOperationTypes.View, FileExtensions.ImageExtensions),
            // ImageViewer takes (left, right) as well as (path), so it is both the image viewer and the image diff.
            [ImageDiffTool] = BuiltIn(ImageDiffTool, ImageViewerHandler, ToolOperationTypes.Diff, FileExtensions.ImageExtensions),
        };

        public static Dictionary<string, FileTypeMappingOptions> FileTypeMapping() => new()
        {
            ["Text"] = new FileTypeMappingOptions
            {
                Extensions = FileExtensions.TextExtensions.ToList(),
                DefaultDiffTool = TextDiffTool,
                DefaultViewTool = TextViewerTool,
                PreferredTools = new PreferredToolsConfiguration
                {
                    Diff = new List<string> { TextDiffTool },
                    View = new List<string> { TextViewerTool },
                },
            },
            ["Image"] = new FileTypeMappingOptions
            {
                Extensions = FileExtensions.ImageExtensions.ToList(),
                DefaultDiffTool = ImageDiffTool,
                DefaultViewTool = ImageViewerTool,
                PreferredTools = new PreferredToolsConfiguration
                {
                    Diff = new List<string> { ImageDiffTool, ImageViewerTool },
                    View = new List<string> { ImageViewerTool },
                },
            },
            // No built-in tool opens an archive file itself: the archive feature is comparing two archives in
            // the compare tab, which reads entry metadata and never extracts. Add an external tool in Options
            // if an archive should open somewhere else.
            ["Archive"] = new FileTypeMappingOptions
            {
                Extensions = FileExtensions.ArchiveExtensions.ToList(),
            },
        };

        private static ToolOptions BuiltIn(string name, string handler, ToolOperationTypes operation, IEnumerable<string> extensions) => new()
        {
            Name = name,
            Type = ToolTypes.BuiltIn,
            Handler = handler,
            SupportedOperations = new List<ToolOperationTypes> { operation },
            SupportedExtensions = extensions.ToList(),
            Enabled = true,
        };
    }
}
