using dCompare.Common;

namespace dCompare.Configuration
{
    public class ToolConfigService : IToolConfigService
    {
        public FileTypeMappingOptions? GetFileTypeMapping(AppConfiguration config, string extension)
        {
            return config.Options.FileTypeMapping.Values
                .FirstOrDefault(mapping => mapping.Extensions.Contains(extension, StringComparer.OrdinalIgnoreCase));
        }

        public ToolOptions? GetDefaultTool(AppConfiguration config, ToolOperationTypes operation, string extension)
        {
            var fileMapping = GetFileTypeMapping(config, extension);
            if (fileMapping == null) return null;

            var toolName = operation switch
            {
                ToolOperationTypes.Diff => fileMapping.DefaultDiffTool,
                ToolOperationTypes.View => fileMapping.DefaultViewTool,
                _ => null
            };

            return toolName != null && config.Options.Tools.TryGetValue(toolName, out var tool) ? tool : null;
        }

        public IEnumerable<ToolOptions> GetAllToolsForOperation(AppConfiguration config, ToolOperationTypes operation)
        {
            return config.Options.Tools.Values
                .Where(tool => tool.Enabled && tool.SupportsOperation(operation));
        }
    }
}
