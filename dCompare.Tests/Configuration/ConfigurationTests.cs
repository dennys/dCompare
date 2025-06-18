using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using dCompare.Configuration; // Assuming the models are in this namespace
using System.Linq;
using System.Collections.Generic;

// This using is for Assert.ThrowsAsync which is a standard xUnit feature.
// No special package is needed beyond the main xunit package.

namespace dCompare.Tests.Configuration
{
    /// <summary>
    /// Tests for the ConfigurationService.
    /// These tests interact with the file system, so they are integration tests.
    /// Using IDisposable to ensure cleanup after each test run.
    /// </summary>
    public class ConfigurationServiceTests : IDisposable
    {                           
        private readonly string _testConfigPath; 

        public ConfigurationServiceTests()
        {
            // Setup: Create a unique temporary file path for each test run.
            // This prevents tests from interfering with each other.
            _testConfigPath = Path.Combine(Path.GetTempPath(), $"test-config-{Path.GetRandomFileName()}.json");
        }

        /// <summary>
        /// Cleanup logic that runs after each test in this class.
        /// </summary>
        public void Dispose()
        {
            // Teardown: Delete the created test file if it exists.
            if (File.Exists(_testConfigPath))
            {
                File.Delete(_testConfigPath);
            }
        }

        [Fact]
        public async Task LoadConfigurationAsync_WhenFileDoesNotExist_CreatesAndReturnsDefaultConfig()
        {
            // Arrange
            var service = new ConfigurationService(_testConfigPath);

            // Act
            var config = await service.LoadConfigurationAsync();

            // Assert
            Assert.NotNull(config);
            Assert.NotNull(config.Options);
            Assert.True(File.Exists(_testConfigPath)); // Verify the file was created.
            Assert.Equal("Light", config.Options.UI.Theme); // Check a default value.
        }

        [Fact]
        public async Task SaveAndLoadConfigurationAsync_PersistsChangesCorrectly()
        {
            // Arrange
            var service = new ConfigurationService(_testConfigPath);
            var initialConfig = await service.LoadConfigurationAsync(); // Creates a default config.
            
            // Modify a value
            initialConfig.Options.UI.Theme = "Dark";
            initialConfig.Options.Log.RetentionDays = 99;
            initialConfig.Options.Diff.Text.Ext.Add(".tmp");

            // Act
            await service.SaveConfigurationAsync(initialConfig);
            var loadedConfig = await service.LoadConfigurationAsync();

            // Assert
            Assert.NotNull(loadedConfig);
            Assert.Equal("Dark", loadedConfig.Options.UI.Theme);
            Assert.Equal(99, loadedConfig.Options.Log.RetentionDays);
            Assert.Contains(".tmp", loadedConfig.Options.Diff.Text.Ext);
        }

        [Fact]
        public async Task LoadConfigurationAsync_WhenFileIsMalformed_ReturnsDefaultConfig()
        {
            // Arrange
            // Create a corrupted/invalid JSON file.
            await File.WriteAllTextAsync(_testConfigPath, "{ \"Options\": { \"UI\": { \"Theme\": \"Dark\" }"); // Missing closing brace
            var service = new ConfigurationService(_testConfigPath);

            // Act
            var config = await service.LoadConfigurationAsync();

            // Assert
            Assert.NotNull(config);
            // Verify it returned the default config, not null or an exception.
            Assert.Equal("Light", config.Options.UI.Theme); 
        }
    }

}