using dCompare.Common;

namespace dCompare.Configuration
{
    public interface IToolConfigService
    {
        IEnumerable<ToolOptions> GetSupportedTools(AppConfiguration config, ToolOperationTypes operation, string extension);
        FileTypeMappingOptions? GetFileTypeMapping(AppConfiguration config, string extension);
        ToolOptions? GetDefaultTool(AppConfiguration config, ToolOperationTypes operation, string extension);
        IEnumerable<ToolOptions> GetPreferredTools(AppConfiguration config, ToolOperationTypes operation, string extension);
        IEnumerable<ToolOptions> GetAllToolsForOperation(AppConfiguration config, ToolOperationTypes operation);
        IEnumerable<string> GetSupportedExtensions(AppConfiguration config, string? toolName = null, ToolOperationTypes? operation = null);
    }
}
