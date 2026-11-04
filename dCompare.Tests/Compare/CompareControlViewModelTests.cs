using dCompare.Compare;
using dCompare.Configuration;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace dCompare.Tests.Compare
{
    /// <summary>
    /// The algorithm dropdown of a compare tab. It used to be bound to the static seed in
    /// <see cref="ComparisonAlgorithms"/>, so what the user enabled or disabled on the Options page was lost on
    /// the next start, and the tab could offer an algorithm the user had switched off.
    /// </summary>
    public class CompareControlViewModelTests
    {
        private static Dictionary<ComparisonAlgorithm, ComparisonAlgorithmInfo> Settings(
            params (ComparisonAlgorithm Algorithm, bool Enabled, bool IsDefault)[] entries)
        {
            var settings = new Dictionary<ComparisonAlgorithm, ComparisonAlgorithmInfo>();
            foreach ((ComparisonAlgorithm algorithm, bool enabled, bool isDefault) in entries)
            {
                settings[algorithm] = new ComparisonAlgorithmInfo
                {
                    Description = algorithm.ToString(),
                    IsEnabled = enabled,
                    IsDefault = isDefault,
                };
            }
            return settings;
        }

        [Fact]
        public void LoadAlgorithms_OffersOnlyTheEnabledAlgorithms()
        {
            var viewModel = new CompareControlViewModel();

            viewModel.LoadAlgorithms(Settings(
                (ComparisonAlgorithm.ModifiedDateAndSize, true, true),
                (ComparisonAlgorithm.MD5, false, false)));

            Assert.Equal(new[] { ComparisonAlgorithm.ModifiedDateAndSize }, viewModel.AvailableAlgorithms.Select(a => a.Value));
        }

        [Fact]
        public void LoadAlgorithms_ShowsTheConfiguredDescription()
        {
            var settings = Settings((ComparisonAlgorithm.FullContents, true, true));
            settings[ComparisonAlgorithm.FullContents].Description = "Full Contents (100%)";
            var viewModel = new CompareControlViewModel();

            viewModel.LoadAlgorithms(settings);

            Assert.Equal("Full Contents (100%)", viewModel.AvailableAlgorithms.Single().Key);
        }

        [Fact]
        public void LoadAlgorithms_SelectsTheConfiguredDefault()
        {
            var viewModel = new CompareControlViewModel();

            viewModel.LoadAlgorithms(Settings(
                (ComparisonAlgorithm.ModifiedDateAndSize, true, false),
                (ComparisonAlgorithm.xxHash64_Full, true, true)));

            Assert.Equal(ComparisonAlgorithm.xxHash64_Full, viewModel.SelectedAlgorithm);
        }

        [Fact]
        public void LoadAlgorithms_WhenTheDefaultIsDisabled_SelectsTheFirstEnabledInstead()
        {
            var viewModel = new CompareControlViewModel();

            // The Options page lets you switch off the algorithm that is still marked default; the box must not
            // be left showing a choice that is not in the list.
            viewModel.LoadAlgorithms(Settings(
                (ComparisonAlgorithm.ModifiedDateAndSize, true, false),
                (ComparisonAlgorithm.MD5, false, true),
                (ComparisonAlgorithm.xxHash64_Partial, true, false)));

            Assert.Equal(ComparisonAlgorithm.ModifiedDateAndSize, viewModel.SelectedAlgorithm);
            Assert.DoesNotContain(ComparisonAlgorithm.MD5, viewModel.AvailableAlgorithms.Select(a => a.Value));
        }

        [Fact]
        public void LoadAlgorithms_WhenNothingIsEnabled_StillLeavesAUsableSelection()
        {
            var viewModel = new CompareControlViewModel();

            viewModel.LoadAlgorithms(Settings((ComparisonAlgorithm.MD5, false, false)));

            Assert.Empty(viewModel.AvailableAlgorithms);
            Assert.Equal(ComparisonAlgorithm.ModifiedDateAndSize, viewModel.SelectedAlgorithm);
        }

        [Fact]
        public void LoadAlgorithms_RefreshFromSettings_KeepsWhatTheUserPicked()
        {
            var viewModel = new CompareControlViewModel();
            viewModel.LoadAlgorithms(Settings(
                (ComparisonAlgorithm.ModifiedDateAndSize, true, true),
                (ComparisonAlgorithm.MD5, true, false)));
            viewModel.SelectedAlgorithm = ComparisonAlgorithm.MD5;

            // Settings | Save re-sends the list to every open tab. Moving the dropdown back to the configured
            // default behind the user's back is the bug this locks out.
            viewModel.LoadAlgorithms(Settings(
                (ComparisonAlgorithm.ModifiedDateAndSize, true, true),
                (ComparisonAlgorithm.MD5, true, false),
                (ComparisonAlgorithm.xxHash64_Full, true, false)));

            Assert.Equal(ComparisonAlgorithm.MD5, viewModel.SelectedAlgorithm);
        }

        [Fact]
        public void LoadAlgorithms_RefreshFromSettings_WhenTheUsersChoiceWasSwitchedOff_FallsBackToTheDefault()
        {
            var viewModel = new CompareControlViewModel();
            viewModel.LoadAlgorithms(Settings(
                (ComparisonAlgorithm.ModifiedDateAndSize, true, true),
                (ComparisonAlgorithm.MD5, true, false)));
            viewModel.SelectedAlgorithm = ComparisonAlgorithm.MD5;

            viewModel.LoadAlgorithms(Settings(
                (ComparisonAlgorithm.ModifiedDateAndSize, true, true),
                (ComparisonAlgorithm.MD5, false, false)));

            Assert.DoesNotContain(ComparisonAlgorithm.MD5, viewModel.AvailableAlgorithms.Select(a => a.Value));
            Assert.Equal(ComparisonAlgorithm.ModifiedDateAndSize, viewModel.SelectedAlgorithm);
        }

        [Fact]
        public void ReportProgress_WhileTheTotalIsUnknown_KeepsTheBarIndeterminateAndCountsFolders()
        {
            var viewModel = new CompareControlViewModel();
            viewModel.BeginCompare();

            viewModel.ReportProgress(new DirectoryComparer.CompareProgress(null, 64));

            Assert.True(viewModel.IsCountingDirectories);
            Assert.Equal(64, viewModel.DirectoriesFound);
            Assert.Equal(0, viewModel.ComparisonProgress);
            Assert.Contains("64", viewModel.ComparisonProgressText);
        }

        [Fact]
        public void ReportProgress_OnceAFractionArrives_TheBarNeverGoesBackToBeingUnsure()
        {
            var viewModel = new CompareControlViewModel();
            viewModel.BeginCompare();
            viewModel.ReportProgress(new DirectoryComparer.CompareProgress(0.5, 40));

            Assert.False(viewModel.IsCountingDirectories);
            Assert.Equal(50, viewModel.ComparisonProgress);

            // The count walk can still be in flight behind the scan; a late count must not demote the bar.
            viewModel.ReportProgress(new DirectoryComparer.CompareProgress(null, 80));

            Assert.False(viewModel.IsCountingDirectories);
            Assert.Equal(50, viewModel.ComparisonProgress);
            Assert.Contains("50", viewModel.ComparisonProgressText);
        }
    }
}
