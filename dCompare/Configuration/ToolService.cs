using System;
using System.Collections.Generic;
using System.Linq;

namespace dCompare.Configuration
{
    public class ToolService : IToolService
    {
        public IEnumerable<ToolOptions> GetSupportedTools(AppConfiguration config, string operation, string extension)
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

        public ToolOptions? GetDefaultTool(AppConfiguration config, string operation, string extension)
        {
            var fileMapping = GetFileTypeMapping(config, extension);
            if (fileMapping == null) return null;

            var toolName = operation.ToLower() switch
            {
                Operations.Diff => fileMapping.DefaultDiffTool,
                Operations.View => fileMapping.DefaultViewTool,
                _ => null
            };

            return toolName != null && config.Options.Tools.TryGetValue(toolName, out var tool) ? tool : null;
        }

        public IEnumerable<ToolOptions> GetPreferredTools(AppConfiguration config, string operation, string extension)
        {
            var fileMapping = GetFileTypeMapping(config, extension);
            if (fileMapping == null) yield break;

            List<string> preferredToolNames;
            if (operation.ToLower() == Operations.Diff)
            {
                preferredToolNames = fileMapping.PreferredTools.Diff;
            }
            else if (operation.ToLower() == Operations.View)
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

        public IEnumerable<ToolOptions> GetAllToolsForOperation(AppConfiguration config, string operation)
        {
            return config.Options.Tools.Values
                .Where(tool => tool.Enabled && tool.SupportsOperation(operation));
        }

        public IEnumerable<string> GetSupportedExtensions(AppConfiguration config, string? toolName = null, string? operation = null)
        {
            foreach (var tool in config.Options.Tools.Values)
            {
                if (toolName != null && tool.Name != toolName) continue;
                if (operation != null && !tool.SupportsOperation(operation)) continue;
                foreach (var ext in tool.SupportedExtensions)
                {
                    yield return ext;
                }
            }
        }
    }
}
