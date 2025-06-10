using Microsoft.Extensions.Configuration;
using Serilog;
using System;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace dCompare.Configuration
{
    public class ConfigurationService
    {
        private readonly IConfiguration _configuration;
        private readonly string _configFilePath;

        public ConfigurationService()
        {
            _configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dcompare.json");
            _configuration = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("dcompare.json", optional: true, reloadOnChange: true)
                .Build();
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
                Log.Information("Configuration saved to {ConfigFilePath}", _configFilePath);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to save configuration to {ConfigFilePath}", _configFilePath);
                MessageBox.Show($"Error saving configuration: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
            //viewModel.UpdateOptions(); // Sync Hotkeys back to Options.HotKeys
            var config = GetConfiguration();
            config.Options = options;
            SaveConfiguration(config);
        }
    }
}