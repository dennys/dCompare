using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;

namespace dCompare.Configuration
{
    /// <summary>
    /// Service to manage loading and saving application configuration.
    /// </summary>
    public class ConfigurationService : IConfigurationService
    {
        private const string jsonFileName = "dCompare.json";
        private readonly string _configPath;
        private readonly IConfigurationRoot _configuration;
        private static readonly JsonSerializerOptions _serializerOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        /// <summary>
        /// Initializes a new instance of the ConfigurationService.
        /// </summary>
        /// <param name="configFileName">The name of the configuration file, e.g., "dCompare.json".</param>
        public ConfigurationService(string configFileName = jsonFileName)
        {
            _configPath = Path.Combine(Directory.GetCurrentDirectory(), configFileName);
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile(configFileName, optional: true, reloadOnChange: true);
            _configuration = builder.Build();
        }

        /// <summary>
        /// Loads the application configuration from the JSON file.
        /// If the file does not exist, it creates and saves a default configuration.
        /// </summary>
        /// <returns>An AppConfiguration object.</returns>
        public async Task<AppConfiguration> LoadConfigurationAsync()
        {
            if (!File.Exists(_configPath))
            {
                // Rely on class initializers for defaults
                AppConfiguration defaultConfig = new();
                await SaveConfigurationAsync(defaultConfig);
                return defaultConfig;
            }
            try
            {
                // Force reload to ensure the latest configuration is read
                _configuration.Reload();
                var config = _configuration.Get<AppConfiguration>();
                return config ?? new AppConfiguration(); // Return new config with defaults if deserialization fails
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error reading configuration file: {ex.Message}");
                return new AppConfiguration(); // Return default config on error
            }
        }

        /// <summary>
        /// Saves the provided configuration object to the JSON file.
        /// </summary>
        /// <param name="config">The AppConfiguration object to save.</param>
        public async Task SaveConfigurationAsync(AppConfiguration config)
        {
            var jsonString = JsonSerializer.Serialize(config, _serializerOptions);
            await File.WriteAllTextAsync(_configPath, jsonString);
            // Trigger reload to ensure configuration is updated
            _configuration.Reload();
        }
    }
}