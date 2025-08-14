using dCompare.Common;
using Microsoft.Extensions.Configuration;

namespace dCompare.Configuration
{
    public interface IConfigurationService
    {
        IConfiguration GetConfiguration();
        Task<AppConfiguration> LoadConfigurationAsync();
        Task SaveConfigurationAsync(AppConfiguration config);
        IEnumerable<ToolOptions> GetSupportedTools(ToolOperationTypes operation, string extension);
        FileTypeMappingOptions? GetFileTypeMapping(string extension);
        ToolOptions? GetDefaultTool(ToolOperationTypes operation, string extension);
        IEnumerable<ToolOptions> GetPreferredTools(ToolOperationTypes operation, string extension);
        IEnumerable<ToolOptions> GetAllToolsForOperation(ToolOperationTypes operation);
        IEnumerable<string> GetSupportedExtensions(string? toolName = null, ToolOperationTypes? operation = null);
    }
}