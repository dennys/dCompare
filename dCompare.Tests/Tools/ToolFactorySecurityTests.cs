using dCompare.Configuration;
using dCompare.Tools;
using Moq;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Controls;
using Xunit;

namespace dCompare.Tests.Tools
{
    // The "Handler" string in dCompare.json is attacker-influenced (a writable cwd, a shared config, a
    // tampered file). ToolFactory must refuse to construct anything that is not one of dCompare's own
    // ITool implementations *before* running the constructor -- constructing first was an arbitrary-type
    // execution hole (e.g. System.IO.StreamWriter truncating the user's file, then failing the cast).
    public class ToolFactorySecurityTests : IDisposable
    {
        private readonly string _testRoot;

        public ToolFactorySecurityTests()
        {
            _testRoot = Path.Combine(Path.GetTempPath(), "ToolFactorySecurityTests", Path.GetRandomFileName());
            Directory.CreateDirectory(_testRoot);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_testRoot))
                {
                    Directory.Delete(_testRoot, true);
                }
            }
            catch (IOException)
            {
            }
        }

        private static ToolFactory FactoryWith(string handler, string toolKey = "evilTool", bool forDiff = false)
        {
            var config = new AppConfiguration();
            // AppConfiguration ships a working tool set (DefaultConfiguration), and "Text" would claim .txt
            // before this fixture's mapping is reached. The gate under test is about the handler type only, so
            // the fixture arranges its own tool set and stays independent of what the defaults happen to be.
            config.Options.Tools.Clear();
            config.Options.FileTypeMapping.Clear();
            // Options is dCompare.Configuration.Options; fully qualified because dCompare.Options also exists.
            config.Options.FileTypeMapping["text"] = new FileTypeMappingOptions
            {
                Extensions = new System.Collections.Generic.List<string> { ".txt" },
                DefaultViewTool = forDiff ? string.Empty : toolKey,
                DefaultDiffTool = forDiff ? toolKey : string.Empty
            };
            config.Options.Tools[toolKey] = new ToolOptions { Name = toolKey, Handler = handler };

            var configService = new Mock<IConfigurationService>();
            configService.Setup(c => c.LoadConfigurationAsync()).ReturnsAsync(config);
            return new ToolFactory(configService.Object, new Mock<Microsoft.Extensions.Logging.ILogger<ToolFactory>>().Object);
        }

        private string MakeTargetFile(string name = "victim.txt")
        {
            string path = Path.Combine(_testRoot, name);
            File.WriteAllText(path, "important content that must survive");
            return path;
        }

        // The original vulnerability: a config that names an arbitrary BCL type whose constructor has a
        // side effect on the argument. The gate must reject it and the side effect must not happen.
        [Fact]
        public async Task CreateToolAsync_HandlerIsArbitraryBclType_ThrowsWithoutRunningConstructor()
        {
            string victim = MakeTargetFile();
            ToolFactory factory = FactoryWith("System.IO.StreamWriter, System.Private.CoreLib");

            ToolCreationException ex = await Assert.ThrowsAsync<ToolCreationException>(
                () => factory.CreateToolAsync(victim));

            Assert.Equal(string.Format(dCompare.Properties.Resources.ErrorToolHandlerNotBuiltInTool, "System.IO.StreamWriter, System.Private.CoreLib"), ex.Message);
            // Proof the constructor never ran: StreamWriter(path) would have truncated this file.
            Assert.Equal("important content that must survive", File.ReadAllText(victim));
        }

        // A type from the test assembly that does implement ITool must still be refused: the same-assembly
        // rule is what stops a plugin DLL dropped next to the exe from being instantiated.
        [Fact]
        public async Task CreateToolAsync_HandlerIsExternalToolImplementation_ThrowsWithoutRunningConstructor()
        {
            ForeignTool.CtorRan = false;
            string target = MakeTargetFile("foreign.txt");
            string handler = typeof(ForeignTool).AssemblyQualifiedName!;
            ToolFactory factory = FactoryWith(handler);

            ToolCreationException ex = await Assert.ThrowsAsync<ToolCreationException>(
                () => factory.CreateToolAsync(target));

            Assert.Equal(string.Format(dCompare.Properties.Resources.ErrorToolHandlerNotBuiltInTool, handler), ex.Message);
            Assert.False(ForeignTool.CtorRan);
        }

        // A dCompare type that is not an ITool must be refused too (it would otherwise be constructed and
        // only fail the (ITool) cast afterwards).
        [Fact]
        public async Task CreateToolAsync_HandlerIsNonToolTypeInDCompareAssembly_Throws()
        {
            string handler = typeof(ToolOptions).AssemblyQualifiedName!;
            ToolFactory factory = FactoryWith(handler);

            ToolCreationException ex = await Assert.ThrowsAsync<ToolCreationException>(
                () => factory.CreateToolAsync(MakeTargetFile("nontool.txt")));

            Assert.Equal(string.Format(dCompare.Properties.Resources.ErrorToolHandlerNotBuiltInTool, handler), ex.Message);
        }

        // Guard against the opposite failure: the gate must not lock out dCompare's real tools. A genuine
        // handler gets past the type gate -- any later failure comes from constructing a WPF control, never
        // from the "not a built-in tool" branch.
        [Theory]
        [InlineData("dCompare.Tools.Viewer.TextViewer, dCompare")]
        [InlineData("dCompare.Tools.Viewer.ImageViewer, dCompare")]
        [InlineData("dCompare.Tools.Diff.TextDiff, dCompare")]
        [InlineData("dCompare.Tools.Diff.TextDiffPlex, dCompare")]
        public async Task CreateToolAsync_HandlerIsRealBuiltInTool_NotRejectedByTypeGate(string handler)
        {
            ToolFactory factory = FactoryWith(handler);

            try
            {
                ITool? tool = await factory.CreateToolAsync(MakeTargetFile("builtin.txt"));
                Assert.NotNull(tool); // accepted and constructed
            }
            catch (ToolCreationException ex)
            {
                Assert.NotEqual(string.Format(dCompare.Properties.Resources.ErrorToolHandlerNotBuiltInTool, handler), ex.Message);
                Assert.NotEqual(string.Format(dCompare.Properties.Resources.ErrorToolTypeNotFound, handler), ex.Message);
            }
        }

        // An unknown handler must be reported as "type not found", not silently null and not as "not built-in".
        [Fact]
        public async Task CreateToolAsync_HandlerTypeDoesNotExist_ReportsTypeNotFound()
        {
            ToolFactory factory = FactoryWith("dCompare.Tools.Viewer.NoSuchTool, dCompare");

            ToolCreationException ex = await Assert.ThrowsAsync<ToolCreationException>(
                () => factory.CreateToolAsync(MakeTargetFile("missing.txt")));

            Assert.Equal(string.Format(dCompare.Properties.Resources.ErrorToolTypeNotFound, "dCompare.Tools.Viewer.NoSuchTool, dCompare"), ex.Message);
        }

        // Empty handler is a configuration error surfaced as ToolCreationException, not a null tool.
        [Fact]
        public async Task CreateToolAsync_HandlerIsEmpty_Throws()
        {
            ToolFactory factory = FactoryWith(string.Empty);

            await Assert.ThrowsAsync<ToolCreationException>(() => factory.CreateToolAsync(MakeTargetFile("empty.txt")));
        }

        // Lives in the *test* assembly, so it exercises the same-assembly half of the gate.
        public sealed class ForeignTool : ITool
        {
            public static bool CtorRan;

            public ForeignTool() => CtorRan = true;
            public ForeignTool(string filePath) => CtorRan = true;

            public void LoadFile(string filePath)
            {
            }

            public UserControl GetControl() => new();
        }
    }
}
