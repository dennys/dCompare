using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Text.Json;

namespace dCompare.Configuration
{
    public class ConfigurationService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<ConfigurationService> _logger;
        private readonly string _configFilePath;

        public ConfigurationService(IConfiguration configuration, ILogger<ConfigurationService> logger, string configFilePath = null)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configFilePath = configFilePath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dcompare.json");
        }

        public AppConfiguration GetConfiguration()
        {
            return _configuration.Get<AppConfiguration>() ?? new AppConfiguration();
        }

        public void SaveConfiguration(AppConfiguration config)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(config, options);
                File.WriteAllText(_configFilePath, json);
                _logger.LogInformation("Configuration saved to {ConfigFilePath}", _configFilePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save configuration to {ConfigFilePath}", _configFilePath);
                throw;
            }
        }

        public void UpdateSession(string tabId, string leftPath, string rightPath)
        {
            var config = GetConfiguration();
            config.Sessions[tabId] = new SessionConfig { Left = leftPath, Right = rightPath };
            SaveConfiguration(config);
        }

        public void RemoveSession(string tabId)
        {
            var config = GetConfiguration();
            config.Sessions.Remove(tabId);
            SaveConfiguration(config);
        }

        public void UpdateOptions(OptionsConfig options)
        {
            var config = GetConfiguration();
            config.Options = options;
            SaveConfiguration(config);
        }
    }
}