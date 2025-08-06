using Microsoft.Extensions.Configuration;

namespace dCompare.Configuration
{
    public interface IConfigurationService
    {
        IConfiguration GetConfiguration();
        Task<AppConfiguration> LoadConfigurationAsync();
        Task SaveConfigurationAsync(AppConfiguration config);
        IEnumerable<ToolOptions> GetSupportedTools(Operations operation, string extension);
        FileTypeMappingOptions? GetFileTypeMapping(string extension);
        ToolOptions? GetDefaultTool(Operations operation, string extension);
        IEnumerable<ToolOptions> GetPreferredTools(Operations operation, string extension);
        IEnumerable<ToolOptions> GetAllToolsForOperation(Operations operation);
        IEnumerable<string> GetSupportedExtensions(string? toolName = null, Operations? operation = null);
    }
}