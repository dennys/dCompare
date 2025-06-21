using dCompare.Configuration;
using dCompare.Options;
using System.Collections.Generic;
using Xunit;

namespace dCompare.Tests.Options
{
    public class ViewModelTests
    {
        [Fact]
        public void TextDiffOptionsViewModel_ExtensionList_IsEditable()
        {
            // Arrange
            var model = new TextDiffOptions { Ext = new List<string> { ".cs" } };
            var viewModel = new TextDiffOptionsViewModel(model);
            string testExtension = ".test"; // Define a test-specific extension

            // Assert initial state
            Assert.Single(viewModel.Extensions);
            Assert.Contains(".cs", viewModel.Extensions);

            // Act: Add an extension programmatically
            // Assume AddExtensionCommand can accept a parameter for testing, or modify ViewModel to support this
            viewModel.Extensions.Add(testExtension); // Direct addition for testing (bypasses UI dialog)
            //viewModel.UpdateModel(); // Ensure model is updated (simulate save)

            // Assert: List is updated in both ViewModel and underlying Model
            Assert.Equal(2, viewModel.Extensions.Count);
            Assert.Contains(testExtension, viewModel.Extensions);
            Assert.Contains(testExtension, model.Ext); // Verify model is updated

            // Act: Remove an extension
            viewModel.SelectedExtension = ".cs";
            Assert.True(viewModel.RemoveExtensionCommand.CanExecute(null));
            viewModel.RemoveExtensionCommand.Execute(null);

            // Assert: List is updated
            Assert.Single(viewModel.Extensions);
            Assert.DoesNotContain(".cs", viewModel.Extensions);
            Assert.DoesNotContain(".cs", model.Ext); // Verify model is updated
            Assert.Contains(testExtension, viewModel.Extensions); // Ensure test extension remains
        }

        // Other tests remain unchanged
        [Fact]
        public void UIOptionsViewModel_Properties_ReflectModel()
        {
            var model = new UIOptions { Theme = "Dark", Language = "en-US" };
            var viewModel = new UIOptionsViewModel(model);
            Assert.Equal("Dark", viewModel.Theme);
            Assert.Equal("en-US", viewModel.Language);
        }

        [Fact]
        public void UIOptionsViewModel_WhenViewModelChanges_ModelIsUpdated()
        {
            var model = new UIOptions { Theme = "Light" };
            var viewModel = new UIOptionsViewModel(model);
            viewModel.Theme = "Dark";
            Assert.Equal("Dark", model.Theme);
        }

        [Fact]
        public void HotKeysOptionsViewModel_UpdateModel_CorrectlyPersistsChanges()
        {
            var model = new HotKeysOptions { AddTab = "Ctrl+N", Compare = "Ctrl+R" };
            var viewModel = new HotKeysOptionsViewModel(model);
            var addTabVm = viewModel.HotKeyItems.FirstOrDefault(h => h.Name == "Add Tab");
            Assert.NotNull(addTabVm);
            addTabVm.Combination = "Ctrl+T";
            Assert.Equal("Ctrl+N", model.AddTab);
            viewModel.UpdateModel();
            Assert.Equal("Ctrl+T", model.AddTab);
            Assert.Equal("Ctrl+R", model.Compare);
        }

        //[Fact]
        //public void CompareOptionsViewModel_AlgorithmMapping_IsCorrect()
        //{
        //    var model = new CompareOptions { Algorithm = "Binary" };
        //    var viewModel = new CompareOptionsViewModel(model);
        //    viewModel.Algorithm = "Rules";
        //    Assert.Equal("Rules", model.Algorithm);
        //    Assert.Contains(viewModel.AvailableAlgorithms, kvp => kvp.Value == "Modified Date and Size");
        //    Assert.Contains(viewModel.AvailableAlgorithms, kvp => kvp.Key == "Thorough (Binary Content)");
        //}
    }
}