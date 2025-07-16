using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;

namespace dCompare.Configuration
{
    /// <summary>
    /// Service to manage loading, saving, and querying application configuration.
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
                AppConfiguration defaultConfig = new();
                await SaveConfigurationAsync(defaultConfig);
                return defaultConfig;
            }
            try
            {
                var jsonString = await File.ReadAllTextAsync(_configPath);
                var config = JsonSerializer.Deserialize<AppConfiguration>(jsonString, _serializerOptions);
                return config ?? new AppConfiguration();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error reading or deserializing configuration file: {ex.Message}");
                return new AppConfiguration();
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

        /// <summary>
        /// Gets all enabled tools that support the given operation and extension.
        /// </summary>
        /// <param name="operation">The operation to check (e.g., "view" or "diff").</param>
        /// <param name="extension">The file extension to check (e.g., ".txt" or ".png").</param>
        /// <returns>An enumerable of supported ToolConfiguration objects.</returns>
        public IEnumerable<ToolConfiguration> GetSupportedTools(string operation, string extension)
        {
            return _configuration.GetSection("Tools").Get<Dictionary<string, ToolConfiguration>>()?.Values
                .Where(tool => tool.Enabled &&
                              tool.SupportsOperation(operation) &&
                              tool.SupportsExtension(extension)) ?? Enumerable.Empty<ToolConfiguration>();
        }

        /// <summary>
        /// Gets the file type mapping for the given extension.
        /// </summary>
        /// <param name="extension">The file extension to check (e.g., ".txt" or ".png").</param>
        /// <returns>The FileTypeMappingConfiguration for the extension, or null if not found.</returns>
        public FileTypeMappingConfiguration? GetFileTypeMapping(string extension)
        {
            return _configuration.GetSection("FileTypeMapping").Get<Dictionary<string, FileTypeMappingConfiguration>>()?.Values
                .FirstOrDefault(mapping => mapping.Extensions.Contains(extension, StringComparer.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Gets the default tool for the given operation and extension.
        /// </summary>
        /// <param name="operation">The operation to check (e.g., "view" or "diff").</param>
        /// <param name="extension">The file extension to check (e.g., ".txt" or ".png").</param>
        /// <returns>The default ToolConfiguration, or null if not found.</returns>
        public ToolConfiguration? GetDefaultTool(string operation, string extension)
        {
            var fileMapping = GetFileTypeMapping(extension);
            if (fileMapping == null) return null;

            var toolName = operation.ToLower() switch
            {
                Operations.Diff => fileMapping.DefaultDiffTool,
                Operations.View => fileMapping.DefaultViewTool,
                _ => null
            };

            var tools = _configuration.GetSection("Tools").Get<Dictionary<string, ToolConfiguration>>();
            return toolName != null && tools != null && tools.TryGetValue(toolName, out var tool) ? tool : null;
        }

        /// <summary>
        /// Gets preferred tools for the given operation and extension (in priority order).
        /// </summary>
        /// <param name="operation">The operation to check (e.g., "view" or "diff").</param>
        /// <param name="extension">The file extension to check (e.g., ".txt" or ".png").</param>
        /// <returns>An enumerable of preferred ToolConfiguration objects in priority order.</returns>
        public IEnumerable<ToolConfiguration> GetPreferredTools(string operation, string extension)
        {
            var fileMapping = GetFileTypeMapping(extension);
            if (fileMapping == null) yield break;

            List<string> preferredToolNames;
            if (operation.ToLower() == Operations.Diff)
            {
                preferredToolNames = fileMapping.PreferredTools.Diff;
            }
            else if (operation.ToLower() == Operations.View)
            {
                preferredToolNames = fileMapping.PreferredTools.View;
            }
            else
            {
                preferredToolNames = Array.Empty<string>().ToList();
            }

            var tools = _configuration.GetSection("Tools").Get<Dictionary<string, ToolConfiguration>>();
            if (tools == null) yield break;

            foreach (var toolName in preferredToolNames)
            {
                if (tools.TryGetValue(toolName, out var tool) &&
                    tool.Enabled &&
                    tool.SupportsOperation(operation) &&
                    tool.SupportsExtension(extension))
                {
                    yield return tool;
                }
            }
        }
        public IEnumerable<ToolConfiguration> GetAllToolsForOperation(string operation)
        {
            var tools = _configuration.GetSection("Tools").Get<Dictionary<string, ToolConfiguration>>();
            return tools?.Values
                .Where(tool => tool.Enabled && tool.SupportsOperation(operation))
                ?? Enumerable.Empty<ToolConfiguration>();
        }
        public IEnumerable<string> GetSupportedExtensions(string? toolName = null, string? operation = null)
        {
            var tools = _configuration.GetSection("Tools").Get<Dictionary<string, ToolConfiguration>>();
            if (tools == null) yield break;

            foreach (var tool in tools.Values)
            {
                if (toolName != null && tool.Name != toolName) continue;
                if (operation != null && !tool.SupportsOperation(operation)) continue;
                foreach (var ext in tool.SupportedExtensions)
                {
                    yield return ext;
                }
            }
        }
    }
}