using dCompare.Configuration;
using dCompare.Options;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using System.Linq;
using System.Collections.Generic;

// This using is for Assert.ThrowsAsync which is a standard xUnit feature.
// No special package is needed beyond the main xunit package.

namespace dCompare.Tests.Options
{
    /// <summary>
    /// Tests for the various ViewModels to ensure they correctly wrap the data models.
    /// These are unit tests and do not require file I/O.
    /// </summary>
    public class ViewModelTests
    {
        [Fact]
        public void UIOptionsViewModel_Properties_ReflectModel()
        {
            // Arrange
            var model = new UIOptions { Theme = "Dark", Language = "en-US" };
            var viewModel = new UIOptionsViewModel(model);

            // Assert
            Assert.Equal("Dark", viewModel.Theme);
            Assert.Equal("en-US", viewModel.Language);
        }
        
        [Fact]
        public void UIOptionsViewModel_WhenViewModelChanges_ModelIsUpdated()
        {
            // Arrange
            var model = new UIOptions { Theme = "Light" };
            var viewModel = new UIOptionsViewModel(model);

            // Act
            viewModel.Theme = "Dark";

            // Assert
            Assert.Equal("Dark", model.Theme);
        }
        
        [Fact]
        public void TextDiffOptionsViewModel_ExtensionList_IsEditable()
        {
            // Arrange
            var model = new TextDiffOptions { Ext = new List<string> { ".cs" } };
            var viewModel = new TextDiffOptionsViewModel(model);

            // Assert initial state
            Assert.Single(viewModel.Extensions);
            Assert.Contains(".cs", viewModel.Extensions);

            // Act: Add an extension
            viewModel.AddExtensionCommand.Execute(null);
            
            // Assert: List is updated in both ViewModel and underlying Model
            Assert.Equal(2, viewModel.Extensions.Count);
            Assert.Contains(".new", viewModel.Extensions);
            Assert.Contains(".new", model.Ext); // Verify model is updated

            // Act: Remove an extension
            viewModel.SelectedExtension = ".cs";
            Assert.True(viewModel.RemoveExtensionCommand.CanExecute(null));
            viewModel.RemoveExtensionCommand.Execute(null);

            // Assert: List is updated
            Assert.Single(viewModel.Extensions);
            Assert.DoesNotContain(".cs", viewModel.Extensions);
            Assert.DoesNotContain(".cs", model.Ext); // Verify model is updated
        }

        [Fact]
        public void HotKeysOptionsViewModel_UpdateModel_CorrectlyPersistsChanges()
        {
            // Arrange
            var model = new HotKeysOptions
            {
                AddTab = "Ctrl+N",
                Compare = "Ctrl+R"
            };
            var viewModel = new HotKeysOptionsViewModel(model);

            // Act: Change a hotkey in the ViewModel's collection
            var addTabVm = viewModel.HotKeyItems.FirstOrDefault(h => h.Name == "Add Tab");
            Assert.NotNull(addTabVm);
            addTabVm.Combination = "Ctrl+T";

            // At this point, the underlying model should NOT have changed yet.
            Assert.Equal("Ctrl+N", model.AddTab);
            
            // Now, explicitly call the update method, simulating the save process.
            viewModel.UpdateModel();

            // Assert: The model is now updated with the new value.
            Assert.Equal("Ctrl+T", model.AddTab);
            Assert.Equal("Ctrl+R", model.Compare); // Ensure other values are untouched.
        }

        [Fact]
        public void CompareOptionsViewModel_AlgorithmMapping_IsCorrect()
        {
            // Arrange
            var model = new CompareOptions { Algorithm = "Binary" };
            var viewModel = new CompareOptionsViewModel(model);

            // Act
            viewModel.Algorithm = "Rules";

            // Assert
            Assert.Equal("Rules", model.Algorithm);
            // Check that the available options are present for the ComboBox binding
            Assert.Contains(viewModel.AvailableAlgorithms, kvp => kvp.Value == "Modified Date and Size");
            Assert.Contains(viewModel.AvailableAlgorithms, kvp => kvp.Key == "Thorough (Binary Content)");
        }
    }
}