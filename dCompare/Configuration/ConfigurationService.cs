using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace dCompare.Configuration
{
    /// <summary>
    /// Service to manage loading and saving application configuration.
    /// </summary>
    public class ConfigurationService: IConfigurationService
    {
        private const string jsonFileName = "dCompare.json";
        private readonly string _configPath;
        private readonly IConfiguration _configuration;
        private static readonly JsonSerializerOptions _serializerOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        /// <summary>
        /// Initializes a new instance of the ConfigurationService.
        /// </summary>
        /// <param name="configFileName">The name of the configuration file, e.g., "appsettings.json".</param>
        public ConfigurationService(string configFileName = jsonFileName)
        {
            _configPath = Path.Combine(Directory.GetCurrentDirectory(), configFileName);
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile(configFileName, optional: true, reloadOnChange: false);
            _configuration = builder.Build();
        }

        /// <summary>
        /// Loads the application configuration from the JSON file.
        /// If the file does not exist, it returns a new default configuration.
        /// </summary>
        /// <returns>An AppConfiguration object.</returns>
        public async Task<AppConfiguration> LoadConfigurationAsync()
        {
            if (!File.Exists(_configPath))
            {
                // If no config file, create one with default values and return it
                var defaultConfig = new AppConfiguration();
                await SaveConfigurationAsync(defaultConfig);
                return defaultConfig;
            }
            try
            {
                var config = _configuration.Get<AppConfiguration>();
                return config ?? new AppConfiguration(); // Return new config if file is empty or deserialization fails
            }
            catch (Exception ex)
            {
                // Handle configuration errors
                System.Diagnostics.Debug.WriteLine($"Error reading configuration file: {ex.Message}");
                return new AppConfiguration(); // Return default config on error
            }
        }

        //public async Task UpdateSessionAsync(string tabId, string leftPath, string rightPath)
        //{
        //    //var config = await LoadConfigurationAsync();
        //    //config.Sessions[tabId] = new SessionConfig { Left = leftPath, Right = rightPath };
        //    //await SaveConfigurationAsync(config);
        //}

        //public async Task RemoveSessionAsync(string tabId)
        //{
        //    //var config = await LoadConfigurationAsync();
        //    //config.Sessions.Remove(tabId);
        //    //await SaveConfigurationAsync(config);
        //}

        /// <summary>
        /// Saves the provided configuration object to the JSON file.
        /// </summary>
        /// <param name="config">The AppConfiguration object to save.</param>
        public async Task SaveConfigurationAsync(AppConfiguration config)
        {
            var jsonString = JsonSerializer.Serialize(config, _serializerOptions);
            await File.WriteAllTextAsync(_configPath, jsonString);
        }
    }
}