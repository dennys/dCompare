using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.IO.Abstractions;
using System.Text.Json;

namespace dCompare.Configuration
{
    public class ConfigurationService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly string _configFilePath;

        public ConfigurationService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            var configuration = _serviceProvider.GetRequiredService<IConfiguration>();
            _configFilePath = configuration.GetValue<string>("ConfigFilePath")
                ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dcompare.json");
        }

        private IConfiguration GetConfigurationService() => _serviceProvider.GetRequiredService<IConfiguration>();
        private ILogger<ConfigurationService> GetLogger() => _serviceProvider.GetRequiredService<ILogger<ConfigurationService>>();
        private IFileSystem GetFileSystem() => _serviceProvider.GetRequiredService<IFileSystem>();

        public AppConfiguration GetConfiguration()
        {
            return GetConfigurationService().Get<AppConfiguration>() ?? new AppConfiguration();
        }

        public void SaveConfiguration(AppConfiguration config)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(config, options);
                GetFileSystem().File.WriteAllText(_configFilePath, json);
                GetLogger().LogInformation("Configuration saved to {ConfigFilePath}", _configFilePath);
            }
            catch (Exception ex)
            {
                GetLogger().LogError(ex, "Failed to save configuration to {ConfigFilePath}", _configFilePath);
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