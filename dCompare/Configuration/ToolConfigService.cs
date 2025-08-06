namespace dCompare.Configuration
{
    public class ToolConfigService : IToolConfigService
    {
        public IEnumerable<ToolOptions> GetSupportedTools(AppConfiguration config, Operations operation, string extension)
        {
            return config.Options.Tools.Values
                .Where(tool => tool.Enabled &&
                               tool.SupportsOperation(operation) &&
                               tool.SupportsExtension(extension));
        }

        public FileTypeMappingOptions? GetFileTypeMapping(AppConfiguration config, string extension)
        {
            return config.Options.FileTypeMapping.Values
                .FirstOrDefault(mapping => mapping.Extensions.Contains(extension, StringComparer.OrdinalIgnoreCase));
        }

        public ToolOptions? GetDefaultTool(AppConfiguration config, Operations operation, string extension)
        {
            var fileMapping = GetFileTypeMapping(config, extension);
            if (fileMapping == null) return null;

            var toolName = operation switch
            {
                Operations.Diff => fileMapping.DefaultDiffTool,
                Operations.View => fileMapping.DefaultViewTool,
                _ => null
            };

            return toolName != null && config.Options.Tools.TryGetValue(toolName, out var tool) ? tool : null;
        }

        public IEnumerable<ToolOptions> GetPreferredTools(AppConfiguration config, Operations operation, string extension)
        {
            var fileMapping = GetFileTypeMapping(config, extension);
            if (fileMapping == null) yield break;

            List<string> preferredToolNames;
            if (operation == Operations.Diff)
            {
                preferredToolNames = fileMapping.PreferredTools.Diff;
            }
            else if (operation == Operations.View)
            {
                preferredToolNames = fileMapping.PreferredTools.View;
            }
            else
            {
                preferredToolNames = Array.Empty<string>().ToList();
            }

            foreach (var toolName in preferredToolNames)
            {
                if (config.Options.Tools.TryGetValue(toolName, out var tool) &&
                    tool.Enabled &&
                    tool.SupportsOperation(operation) &&
                    tool.SupportsExtension(extension))
                {
                    yield return tool;
                }
            }
        }

        public IEnumerable<ToolOptions> GetAllToolsForOperation(AppConfiguration config, Operations operation)
        {
            return config.Options.Tools.Values
                .Where(tool => tool.Enabled && tool.SupportsOperation(operation));
        }

        public IEnumerable<string> GetSupportedExtensions(AppConfiguration config, string? toolName = null, Operations? operation = null)
        {
            foreach (var tool in config.Options.Tools.Values)
            {
                if (toolName != null && tool.Name != toolName) continue;
                if (operation != null && !tool.SupportsOperation((Operations)operation)) continue;
                foreach (var ext in tool.SupportedExtensions)
                {
                    yield return ext;
                }
            }
        }
    }
}
