using dCompare.Common;
using Microsoft.Extensions.Configuration;

namespace dCompare.Configuration
{
    public interface IConfigurationService
    {
        IConfiguration GetConfiguration();
        Task<AppConfiguration> LoadConfigurationAsync();
        Task SaveConfigurationAsync(AppConfiguration config);
        FileTypeMappingOptions? GetFileTypeMapping(string extension);
        ToolOptions? GetDefaultTool(ToolOperationTypes operation, string extension);
        IEnumerable<ToolOptions> GetAllToolsForOperation(ToolOperationTypes operation);
    }
}