using dCompare.Common;
using Serilog;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace dCompare.Configuration
{
    /// <summary>
    /// Service to manage loading, saving, and querying application configuration.
    /// </summary>
    public class ConfigurationService : IConfigurationService
    {
        private const string jsonFileName = Constant.DefaultConfigurationFile;
        private string _configPath;
        private static readonly JsonSerializerOptions _serializerOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        /// <summary>
        /// Initializes a new instance of the ConfigurationService.
        /// </summary>
        /// <param name="configFileName">The name of the configuration file, e.g., "dCompare.json".
        /// The default name resolves to the portable home next to the exe; any explicit path
        /// (absolute or relative) is used as given.</param>
        public ConfigurationService(string configFileName = jsonFileName)
        {
            _configPath = string.Equals(configFileName, jsonFileName, StringComparison.OrdinalIgnoreCase)
                ? AppPaths.ResolveExisting(jsonFileName)
                : Path.GetFullPath(configFileName);
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
                AppConfiguration defaultConfig = new();
                await SaveConfigurationAsync(defaultConfig);
                return defaultConfig;
            }
            try
            {
                var jsonString = await File.ReadAllTextAsync(_configPath);
                var config = JsonSerializer.Deserialize<AppConfiguration>(jsonString, _serializerOptions);
                if (config != null)
                {
                    ValidateConfiguration(config);
                }
                return config ?? new AppConfiguration();
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException($"Error in configuration file at path: {ex.Path}. {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                throw new InvalidDataException($"Error reading or deserializing configuration file: {ex.Message}", ex);
            }
        }

        private static void ValidateConfiguration(AppConfiguration config)
        {
            if (config.Options == null) return;

            var toolNames = new HashSet<string>(config.Options.Tools.Keys);

            foreach (var mapping in config.Options.FileTypeMapping.Values)
            {
                if (!string.IsNullOrEmpty(mapping.DefaultDiffTool) && !toolNames.Contains(mapping.DefaultDiffTool))
                {
                    throw new InvalidDataException($"The diff tool '{mapping.DefaultDiffTool}' set as the default for extensions '{string.Join(", ", mapping.Extensions)}' is not a defined tool.");
                }

                if (!string.IsNullOrEmpty(mapping.DefaultViewTool) && !toolNames.Contains(mapping.DefaultViewTool))
                {
                    throw new InvalidDataException($"The view tool '{mapping.DefaultViewTool}' set as the default for extensions '{string.Join(", ", mapping.Extensions)}' is not a defined tool.");
                }

                foreach (var toolName in mapping.PreferredTools.Diff)
                {
                    if (!toolNames.Contains(toolName))
                    {
                        throw new InvalidDataException($"The preferred diff tool '{toolName}' for extensions '{string.Join(", ", mapping.Extensions)}' is not a defined tool.");
                    }
                }

                foreach (var toolName in mapping.PreferredTools.View)
                {
                    if (!toolNames.Contains(toolName))
                    {
                        throw new InvalidDataException($"The preferred view tool '{toolName}' for extensions '{string.Join(", ", mapping.Extensions)}' is not a defined tool.");
                    }
                }
            }
        }

        /// <summary>
        /// Saves the provided configuration object to the JSON file.
        /// </summary>
        /// <param name="config">The AppConfiguration object to save.</param>
        public async Task SaveConfigurationAsync(AppConfiguration config)
        {
            var jsonString = JsonSerializer.Serialize(config, _serializerOptions);
            try
            {
                await WriteAtomicAsync(_configPath, jsonString);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                // Installed into a read-only folder (Program Files): retry once in %LOCALAPPDATA%\dCompare.
                string fallbackPath = AppPaths.GetFallbackPath(_configPath);
                Log.Warning(ex, "Cannot write configuration to '{ConfigPath}'; saving to '{FallbackPath}' instead.", _configPath, fallbackPath);
                await WriteAtomicAsync(fallbackPath, jsonString);
                _configPath = fallbackPath;
            }
        }

        private static async Task WriteAtomicAsync(string path, string jsonString)
        {
            // Write-then-replace: a crash or power loss during the write would otherwise leave a truncated
            // dCompare.json that LoadConfigurationAsync rejects on every later start, and the Options page
            // (the thing that repairs it) could not open either.
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string tempPath = path + ".tmp";
            await File.WriteAllTextAsync(tempPath, jsonString);
            File.Move(tempPath, path, true);
        }
    }
}