using dCompare.Configuration;
using dCompare.Options;
using dCompare.Properties;
using Microsoft.Win32; // For OpenFileDialog mocking
using Moq; // For mocking OpenFileDialog
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Xunit;

namespace dCompare.Tests.Options
{
    public class ViewModelTests
    {
        [Fact]
        public void UIOptionsViewModel_Properties_ReflectModel()
        {
            var model = new UIOptions { Theme = "Dark", Language = "en" };
            var viewModel = new UIOptionsViewModel(model);
            Assert.Equal("Dark", viewModel.Theme);
            Assert.Equal("en", viewModel.Language);
        }

        [Fact]
        public void UIOptionsViewModel_WhenViewModelChanges_ModelIsUpdated()
        {
            var model = new UIOptions { Theme = "Light" };
            var viewModel = new UIOptionsViewModel(model);
            viewModel.Theme = "Dark";
            Assert.Equal("Dark", model.Theme);
        }

        /// <summary>
        /// CompareOptions seeds its dictionary with a shallow copy of the static list in ComparisonAlgorithms, so
        /// the entries start out as the very same objects. The Options page used to tick those directly, which
        /// changed the live settings even when the user pressed Cancel.
        /// </summary>
        [Fact]
        public void CompareOptionsViewModel_TogglingAnAlgorithmEditsNeitherTheSeedNorTheModel()
        {
            var model = new CompareOptions();
            Assert.True(
                ReferenceEquals(model.Algorithms[ComparisonAlgorithm.MD5], ComparisonAlgorithms.AlgorithmSettings[ComparisonAlgorithm.MD5]),
                "CompareOptions is no longer seeded with shared entries, so this test no longer guards anything.");
            bool seedWasEnabled = ComparisonAlgorithms.AlgorithmSettings[ComparisonAlgorithm.MD5].IsEnabled;

            var viewModel = new CompareOptionsViewModel(model);
            viewModel.AlgorithmItems.First(item => item.Algorithm == ComparisonAlgorithm.MD5).IsEnabled = !seedWasEnabled;

            Assert.Equal(seedWasEnabled, ComparisonAlgorithms.AlgorithmSettings[ComparisonAlgorithm.MD5].IsEnabled);
            Assert.Equal(seedWasEnabled, model.Algorithms[ComparisonAlgorithm.MD5].IsEnabled);

            // Saving still has to land.
            viewModel.UpdateModel();
            Assert.Equal(!seedWasEnabled, model.Algorithms[ComparisonAlgorithm.MD5].IsEnabled);
        }

        [Fact]
        public void HotKeysOptionsViewModel_UpdateModel_CorrectlyPersistsChanges()
        {
            var model = new HotKeysOptions { AddTab = "Ctrl+N", Compare = "Ctrl+R" };
            var viewModel = new HotKeysOptionsViewModel(model);
            var addTabVm = viewModel.HotKeyItems.FirstOrDefault(h => h.Name == Resources.AddTab);
            Assert.NotNull(addTabVm);
            addTabVm.Combination = "Ctrl+T";
            Assert.Equal("Ctrl+N", model.AddTab);
            viewModel.UpdateModel();
            Assert.Equal("Ctrl+T", model.AddTab);
            Assert.Equal("Ctrl+R", model.Compare);
        }

        //[Fact]
        //public void DiffOptionsViewModel_ExternalViewer_CanAddAndRemove()
        //{
        //    // Arrange
        //    var model = new DiffOptions
        //    {
        //        Viewer = new List<ExternalViewer>
        //        {
        //            new ExternalViewer { Name = "WinMerge", Arguments = "/e /u \"{left}\" \"{right}\"" }
        //        }
        //    };
        //    var viewModel = new DiffOptionsViewModel(model);

        //    // Assert initial state
        //    Assert.Single(viewModel.ExternalViewers);
        //    Assert.Equal("WinMerge", viewModel.ExternalViewers[0].Name);

        //    // Act: Add a new viewer
        //    viewModel.AddViewerCommand.Execute(null);
        //    Assert.Equal(2, viewModel.ExternalViewers.Count);
        //    Assert.Contains(viewModel.ExternalViewers, v => v.Name == "New Tool");

        //    // Act: Remove a viewer
        //    viewModel.SelectedExternalViewer = viewModel.ExternalViewers.FirstOrDefault(v => v.Name == "New Tool");
        //    Assert.True(viewModel.RemoveViewerCommand.CanExecute(null));
        //    viewModel.RemoveViewerCommand.Execute(null);

        //    // Assert: Viewer removed
        //    Assert.Single(viewModel.ExternalViewers);
        //    Assert.Equal("WinMerge", viewModel.ExternalViewers[0].Name);
        //    Assert.Single(model.Viewer);
        //    Assert.Equal("WinMerge", model.Viewer[0].Name);
        //}

        //[Fact]
        //public void DiffOptionsViewModel_ExternalViewer_WinMergeCannotBeRemoved()
        //{
        //    // Arrange
        //    var model = new DiffOptions
        //    {
        //        Viewer = new List<ExternalViewer>
        //        {
        //            new ExternalViewer { Name = "WinMerge", Arguments = "/e /u \"{left}\" \"{right}\"" }
        //        }
        //    };
        //    var viewModel = new DiffOptionsViewModel(model);

        //    // Act: Try to remove WinMerge
        //    viewModel.SelectedExternalViewer = viewModel.ExternalViewers.FirstOrDefault(v => v.Name == "WinMerge");
        //    Assert.False(viewModel.RemoveViewerCommand.CanExecute(null));

        //    // Assert: WinMerge not removed
        //    Assert.Single(viewModel.ExternalViewers);
        //    Assert.Equal("WinMerge", viewModel.ExternalViewers[0].Name);
        //}

        //[Fact]
        //public void DiffOptionsViewModel_BrowsePath_UpdatesViewerPath()
        //{
        //    // Arrange
        //    var model = new DiffOptions
        //    {
        //        Viewer = new List<ExternalViewer>
        //        {
        //            new ExternalViewer { Name = "NewTool", Arguments = "" }
        //        }
        //    };
        //    var viewModel = new DiffOptionsViewModel(model);

        //    // Mock OpenFileDialog
        //    var mockDialog = new Mock<OpenFileDialog>();
        //    mockDialog.Setup(d => d.ShowDialog()).Returns(true);
        //    mockDialog.Setup(d => d.FileName).Returns("C:\\Tools\\NewTool.exe");

        //    // Replace OpenFileDialog creation in BrowsePath with mock (requires dependency injection or similar, so we simulate)
        //    viewModel.SelectedExternalViewer = viewModel.ExternalViewers.FirstOrDefault(v => v.Name == "NewTool");
        //    viewModel.SelectedExternalViewer.Path = "C:\\Tools\\NewTool.exe"; // Simulate dialog result

        //    // Assert
        //    Assert.Equal("C:\\Tools\\NewTool.exe", viewModel.SelectedExternalViewer.Path);
        //    Assert.Equal("C:\\Tools\\NewTool.exe", model.Viewer[0].Path);
        //}


        //[Fact]
        //public void ExtensionListEditorViewModel_InvalidExtension_NotAdded()
        //{
        //    // Arrange
        //    var model = new TextDiffOptions { Ext = new List<string> { ".cs" } };
        //    var viewModel = new DiffOptionsViewModel.ExtensionListEditorViewModel<TextDiffOptions>(
        //        model, model.Ext, (m, l) => m.Ext = l);

        //    // Act: Try to add invalid extension
        //    viewModel.Extensions.Add("txt"); // Missing dot

        //    // Assert: Extension not added
        //    Assert.Single(viewModel.Extensions);
        //    Assert.Contains(".cs", viewModel.Extensions);
        //    Assert.DoesNotContain("txt", viewModel.Extensions);
        //    Assert.Single(model.Ext);
        //    Assert.Contains(".cs", model.Ext);
        //}
    }
}