using dCompare.Configuration;
using System.Threading.Tasks;

namespace dCompare.Configuration
{
    public interface IConfigurationService
    {
        Task<AppConfiguration> LoadConfigurationAsync();
        Task SaveConfigurationAsync(AppConfiguration config);
        Task UpdateSessionAsync(string tabId, string leftPath, string rightPath);
        Task RemoveSessionAsync(string tabId);
    }
}