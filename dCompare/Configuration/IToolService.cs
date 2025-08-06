using dCompare.Configuration;
using System.Collections.Generic;

namespace dCompare.Configuration
{
    public interface IToolService
    {
        IEnumerable<ToolOptions> GetSupportedTools(AppConfiguration config, Operations operation, string extension);
        FileTypeMappingOptions? GetFileTypeMapping(AppConfiguration config, string extension);
        ToolOptions? GetDefaultTool(AppConfiguration config, Operations operation, string extension);
        IEnumerable<ToolOptions> GetPreferredTools(AppConfiguration config, Operations operation, string extension);
        IEnumerable<ToolOptions> GetAllToolsForOperation(AppConfiguration config, Operations operation);
        IEnumerable<string> GetSupportedExtensions(AppConfiguration config, string? toolName = null, Operations? operation = null);
    }
}
