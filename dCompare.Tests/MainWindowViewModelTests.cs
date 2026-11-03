using dCompare.Compare;
using dCompare.Configuration;
using dCompare.Session;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using System.Threading;
using System.Windows.Controls;
using Xunit;

namespace dCompare.Tests
{
    public class MainWindowViewModelTests
    {
        // WPF element creation (TabControl) requires an STA thread; xunit runs on MTA.
        private static void RunSta(Action action)
        {
            Exception? error = null;
            var thread = new Thread(() =>
            {
                try { action(); } catch (Exception ex) { error = ex; }
            })
            { IsBackground = true };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (error != null) throw error;
        }

        // CompareControl's XAML resolves app-level StaticResources; a headless test needs an
        // Application instance carrying them (the real ones live in App.xaml / MainWindow.xaml).
        private static void EnsureTestApplication()
        {
            if (System.Windows.Application.Current != null) return;
            var app = new System.Windows.Application();
            app.Resources["BooleanToVisibilityConverter"] = new System.Windows.Controls.BooleanToVisibilityConverter();
            app.Resources["DepthToMarginConverter"] = new dCompare.Infrastructure.DepthToMarginConverter();
            app.Resources["TextToForegroundConverter"] = new dCompare.Infrastructure.TextToForegroundConverter();
            app.Resources["CopyToLeftIcon"] = new System.Windows.Controls.Image();
            app.Resources["CopyToRightIcon"] = new System.Windows.Controls.Image();
            app.Resources["DeleteIcon"] = new System.Windows.Controls.Image();
            app.Resources["LeftContextMenu"] = new System.Windows.Controls.ContextMenu();
            app.Resources["RightContextMenu"] = new System.Windows.Controls.ContextMenu();
            app.Resources["SortableColumnHeaderStyle"] = new System.Windows.Style(typeof(System.Windows.Controls.GridViewColumnHeader));
        }

        private (MainWindowViewModel vm, Mock<ISessionManager> sessions, Mock<IUserInteractionService> interaction) CreateViewModel()
        {
            var sessions = new Mock<ISessionManager>();
            sessions.Setup(m => m.CanLoadSession()).Returns(true);
            var interaction = new Mock<IUserInteractionService>();
            var vm = new MainWindowViewModel(
                new Mock<IConfigurationService>().Object,
                sessions.Object,
                new ServiceCollection().BuildServiceProvider(),
                interaction.Object,
                new Mock<ILogger<MainWindowViewModel>>().Object);
            return (vm, sessions, interaction);
        }

        [Fact]
        public void LoadSessionFile_LoadSucceeds_ReportsLoadedStatus()
        {
            var (vm, sessions, _) = CreateViewModel();
            sessions.Setup(m => m.LoadSession(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<Action<string>>(), It.IsAny<Action<dCompare.Compare.CompareControl>>())).Returns(true);

            vm.LoadSessionFileCommand.Execute(("C:\\sessions\\a.json", true));

            Assert.Equal(string.Format(dCompare.Properties.Resources.StatusLoadedSession, "C:\\sessions\\a.json", true), vm.StatusMessage);
        }

        [Fact]
        public void LoadSessionFile_LoadFails_DoesNotReportLoadedStatus()
        {
            var (vm, sessions, interaction) = CreateViewModel();
            sessions.Setup(m => m.LoadSession(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<Action<string>>(), It.IsAny<Action<dCompare.Compare.CompareControl>>())).Returns(false);

            vm.LoadSessionFileCommand.Execute(("C:\\sessions\\a.json", true));

            Assert.NotEqual(string.Format(dCompare.Properties.Resources.StatusLoadedSession, "C:\\sessions\\a.json", true), vm.StatusMessage);
            interaction.Verify(i => i.ShowError(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void LoadSessionFile_FileNotFound_DoesNotReportLoadedStatusAndShowsError()
        {
            var (vm, sessions, interaction) = CreateViewModel();
            sessions.Setup(m => m.LoadSession(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<Action<string>>(), It.IsAny<Action<dCompare.Compare.CompareControl>>()))
                .Throws(new FileNotFoundException());

            vm.LoadSessionFileCommand.Execute(("C:\\sessions\\a.json", true));

            Assert.NotEqual(string.Format(dCompare.Properties.Resources.StatusLoadedSession, "C:\\sessions\\a.json", true), vm.StatusMessage);
            interaction.Verify(i => i.ShowError(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public void AddTab_TwoTabsWithSameHeader_BothKeepDistinctTabIds()
        {
            RunSta(() =>
            {
                EnsureTestApplication();
                var provider = TestIoc.EnsureConfigured();
                var sessionManager = new SessionManager(provider);
                var tabControl = new TabControl();
                sessionManager.Initialize(tabControl, new MenuItem());
                var interaction = new Mock<IUserInteractionService>();
                var viewModel = new MainWindowViewModel(
                    provider.GetRequiredService<IConfigurationService>(),
                    sessionManager,
                    provider,
                    interaction.Object,
                    new Mock<ILogger<MainWindowViewModel>>().Object);
                viewModel.TabControl = tabControl;

                viewModel.AddTabCommand.Execute(null);
                viewModel.AddTabCommand.Execute(null);

                Assert.Equal(2, tabControl.Items.Count);

                // The collision scenario: two tabs with the same display header.
                ((TabItem)tabControl.Items[0]).Header = "Same";
                ((TabItem)tabControl.Items[1]).Header = "Same";

                // The map is keyed by the TabItem instance, so identical headers must not overwrite each other.
                Assert.Equal(2, sessionManager.TabIdMap.Count);
                string id0 = sessionManager.TabIdMap[(TabItem)tabControl.Items[0]]!;
                string id1 = sessionManager.TabIdMap[(TabItem)tabControl.Items[1]]!;
                Assert.NotEqual(id0, id1);
            });
        }
    }
}
