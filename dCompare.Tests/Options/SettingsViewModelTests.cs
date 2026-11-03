using dCompare.Configuration;
using dCompare.Options;
using Moq;
using System.IO;
using Xunit;

namespace dCompare.Tests.Options
{
    public class SettingsViewModelTests : IDisposable
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), "dCompareTests", Guid.NewGuid().ToString("N"));

        public SettingsViewModelTests() => Directory.CreateDirectory(_dir);

        public void Dispose()
        {
            try { Directory.Delete(_dir, recursive: true); } catch { }
        }

        private string WriteCorruptConfig()
        {
            string path = Path.Combine(_dir, "dCompare.json");
            File.WriteAllText(path, "{"); // half-written file: corrupt JSON
            return path;
        }

        [Fact]
        public async Task LoadConfigurationAsync_CorruptConfig_ShowsErrorAndFallsBackToDefaults()
        {
            var configService = new ConfigurationService(WriteCorruptConfig());
            var interaction = new Mock<IUserInteractionService>();
            var viewModel = new SettingsViewModel(configService, interaction.Object);

            // Must not throw out to the caller (previously: unhandled async void → process exit).
            await viewModel.LoadConfigurationAsync();

            interaction.Verify(i => i.ShowError(It.IsAny<string>(), It.IsAny<string>()), Times.AtLeastOnce);
            // The menu is still built (with default config) so the page can save a repaired config.
            Assert.NotEmpty(viewModel.MenuItems);
        }

        [Fact]
        public async Task LoadConfigurationAsync_ValidConfig_BuildsMenuWithoutError()
        {
            var configService = new ConfigurationService(Path.Combine(_dir, "missing.json"));
            var interaction = new Mock<IUserInteractionService>();
            var viewModel = new SettingsViewModel(configService, interaction.Object);

            await viewModel.LoadConfigurationAsync();

            interaction.Verify(i => i.ShowError(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            Assert.NotEmpty(viewModel.MenuItems);
        }
    }
}
