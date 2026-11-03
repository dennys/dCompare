using dCompare.Common;
using dCompare.Configuration;
using dCompare.Tools;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace dCompare.Tests.Configuration
{
    /// <summary>
    /// A fresh install used to get empty Tools and FileTypeMapping dictionaries, so every View and Diff command
    /// dead-ended at "no default tool" — and the only config that worked was an untracked file beside one
    /// developer's exe, which itself named two handler types that do not exist. These tests are what would have
    /// caught both.
    /// </summary>
    public class DefaultConfigurationTests : IDisposable
    {
        private readonly string _testRoot;

        public DefaultConfigurationTests()
        {
            _testRoot = Path.Combine(Path.GetTempPath(), "DefaultConfigTests", Path.GetRandomFileName());
            Directory.CreateDirectory(_testRoot);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_testRoot)) Directory.Delete(_testRoot, true);
            }
            catch (IOException)
            {
            }
        }

        [Fact]
        public void FreshConfiguration_HasToolsAndMappings()
        {
            var config = new AppConfiguration();

            Assert.NotEmpty(config.Options.Tools);
            Assert.NotEmpty(config.Options.FileTypeMapping);
        }

        // The handler string is exactly what ToolFactory refuses to construct unless it is one of dCompare's own
        // ITool types, so a default naming a type that does not exist ships a tool that can never start.
        [Fact]
        public void EveryDefaultToolHandler_IsARealBuiltInTool()
        {
            var config = new AppConfiguration();

            foreach (var (name, tool) in config.Options.Tools)
            {
                Type? type = Type.GetType(tool.Handler);
                Assert.NotNull(type);
                Assert.True(typeof(ITool).IsAssignableFrom(type), $"{name} handler {tool.Handler} is not an ITool");
                Assert.Equal(typeof(ITool).Assembly, type!.Assembly);
                Assert.NotEmpty(tool.SupportedOperations);
                Assert.NotEmpty(tool.SupportedExtensions);
                Assert.True(tool.Enabled);
            }
        }

        // Mirrors ConfigurationService.ValidateConfiguration, which rejects a mapping naming an undefined tool.
        [Fact]
        public void EveryMappingReferencesDefinedToolsOnly()
        {
            var config = new AppConfiguration();
            var names = config.Options.Tools.Keys.ToHashSet(StringComparer.Ordinal);

            foreach (var (type, mapping) in config.Options.FileTypeMapping)
            {
                Assert.NotEmpty(mapping.Extensions);
                foreach (string referenced in new[] { mapping.DefaultDiffTool, mapping.DefaultViewTool }
                             .Concat(mapping.PreferredTools.Diff).Concat(mapping.PreferredTools.View)
                             .Where(n => !string.IsNullOrEmpty(n)))
                {
                    Assert.Contains(referenced, names);
                }
            }
        }

        [Theory]
        [InlineData(".txt", DefaultConfiguration.TextDiffTool, DefaultConfiguration.TextViewerTool)]
        [InlineData(".cs", DefaultConfiguration.TextDiffTool, DefaultConfiguration.TextViewerTool)]
        [InlineData(".png", DefaultConfiguration.ImageDiffTool, DefaultConfiguration.ImageViewerTool)]
        public void KnownExtension_ResolvesToItsBuiltInTools(string extension, string expectedDiff, string expectedView)
        {
            var config = new AppConfiguration();
            var toolConfig = new ToolConfigService();

            Assert.Equal(expectedDiff, toolConfig.GetDefaultTool(config, ToolOperationTypes.Diff, extension)?.Name);
            Assert.Equal(expectedView, toolConfig.GetDefaultTool(config, ToolOperationTypes.View, extension)?.Name);
        }

        // The defaults are also what gets written to a user's first dCompare.json, so they have to survive the
        // round trip and still pass the load-time validation.
        [Fact]
        public async Task Defaults_RoundTripThroughTheConfigFile_AndPassValidation()
        {
            string path = Path.Combine(_testRoot, "dCompare.json");
            var service = new ConfigurationService(path);

            AppConfiguration written = await service.LoadConfigurationAsync();       // creates the file
            AppConfiguration reread = await service.LoadConfigurationAsync();        // reads and validates it

            Assert.Equal(written.Options.Tools.Count, reread.Options.Tools.Count);
            Assert.Equal(written.Options.FileTypeMapping.Count, reread.Options.FileTypeMapping.Count);
            Assert.Contains(DefaultConfiguration.TextViewerTool, reread.Options.Tools.Keys);
        }
    }
}
