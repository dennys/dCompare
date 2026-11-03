using dCompare.Configuration;
using dCompare.Tools.Viewer;
using Moq;
using System.IO;
using Xunit;

namespace dCompare.Tests.Tools.Viewer
{
    public class TextViewerViewModelTests : IDisposable
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), "dCompareTests", Guid.NewGuid().ToString("N"));

        public TextViewerViewModelTests() => Directory.CreateDirectory(_dir);

        public void Dispose()
        {
            try { Directory.Delete(_dir, recursive: true); } catch { }
        }

        private (ConfigurationService config, Mock<IUserInteractionService> interaction) CorruptSetup()
        {
            string configPath = Path.Combine(_dir, "dCompare.json");
            File.WriteAllText(configPath, "{"); // half-written file: corrupt JSON
            var interaction = new Mock<IUserInteractionService>();
            return (new ConfigurationService(configPath), interaction);
        }

        [Fact]
        public async Task ApplySettings_CorruptConfig_ShowsErrorAndKeepsDefaults()
        {
            var (config, interaction) = CorruptSetup();
            var viewModel = new TextViewerViewModel(config, interaction.Object);

            // Must not throw out to the caller (previously: unhandled async void → process exit).
            await viewModel.ApplySettings();

            interaction.Verify(i => i.ShowError(It.IsAny<string>(), It.IsAny<string>()), Times.AtLeastOnce);
            // Defaults are retained so the control stays usable.
            Assert.Equal("Consolas", viewModel.FontFamily);
        }

        [Fact]
        public async Task LoadFile_CorruptConfig_StillLoadsTextAndShowsError()
        {
            string textPath = Path.Combine(_dir, "note.txt");
            File.WriteAllText(textPath, "hello world");

            var (config, interaction) = CorruptSetup();
            var viewModel = new TextViewerViewModel(config, interaction.Object);

            await viewModel.LoadFile(textPath);

            Assert.Equal("hello world", viewModel.Text);
            interaction.Verify(i => i.ShowError(It.IsAny<string>(), It.IsAny<string>()), Times.AtLeastOnce);
        }
    }
}
