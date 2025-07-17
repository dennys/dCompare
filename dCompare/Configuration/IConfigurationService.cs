using dCompare.Configuration;
using System.Threading.Tasks;

namespace dCompare.Configuration
{
    public interface IConfigurationService
    {
        Task<AppConfiguration> LoadConfigurationAsync();
        Task SaveConfigurationAsync(AppConfiguration config);
        IEnumerable<ToolOptions> GetSupportedTools(string operation, string extension);
        FileTypeMappingOptions? GetFileTypeMapping(string extension);
        ToolOptions? GetDefaultTool(string operation, string extension);
        IEnumerable<ToolOptions> GetPreferredTools(string operation, string extension);
        IEnumerable<ToolOptions> GetAllToolsForOperation(string operation);
        IEnumerable<string> GetSupportedExtensions(string? toolName = null, string? operation = null);
    }
}