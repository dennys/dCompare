using dCompare.Common;

namespace dCompare.Configuration
{
    public interface IToolConfigService
    {
        FileTypeMappingOptions? GetFileTypeMapping(AppConfiguration config, string extension);
        ToolOptions? GetDefaultTool(AppConfiguration config, ToolOperationTypes operation, string extension);
        IEnumerable<ToolOptions> GetAllToolsForOperation(AppConfiguration config, ToolOperationTypes operation);
    }
}
