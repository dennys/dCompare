using dCompare.Compare;
using dCompare.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace dCompare.Tests.Compare
{
    /// <summary>
    /// The compare progress bar. The total is a second walk of both trees that runs beside the scan, so the
    /// denominator is still unknown for the first part of every parallel compare. Clamping it to the numerator
    /// there made the very first folder report 100% and the bar sit there for the rest of the scan.
    /// </summary>
    public class DirectoryComparerProgressTests : IDisposable
    {
        private readonly string _tempDir;

        public DirectoryComparerProgressTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "ProgressTests_" + Guid.NewGuid().ToString()[..8]);
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true);
            }
            catch (IOException)
            {
            }
        }

        [Theory]
        [InlineData(1, 0, -1)]        // the count has not been published yet: nothing honest to report
        [InlineData(40, 0, -1)]
        [InlineData(0, 0, -1)]
        [InlineData(1, 100, 0)]
        [InlineData(50, 100, 5)]
        [InlineData(100, 100, 10)]
        [InlineData(120, 100, 10)]    // the count fell short of the walk: still never past 100%
        public void ProgressTenth_OnlyReportsAgainstAPublishedTotal(int processedDirs, int totalDirs, int expected)
        {
            Assert.Equal(expected, DirectoryComparer.ProgressTenth(processedDirs, totalDirs));
        }

        private string BuildTree(string side, int perLevel, int depth)
        {
            string root = Path.Combine(_tempDir, side);
            Directory.CreateDirectory(root);

            void Build(string parent, int remaining)
            {
                if (remaining == 0) return;
                for (int i = 1; i <= perLevel; i++)
                {
                    string dir = Path.Combine(parent, "dir" + i);
                    Directory.CreateDirectory(dir);
                    File.WriteAllText(Path.Combine(dir, "file.txt"), "x");
                    Build(dir, remaining - 1);
                }
            }

            Build(root, depth);
            return root;
        }

        /// <summary>
        /// Sequential mode runs the count first, so the denominator is known from the first folder and the
        /// reported sequence is deterministic: that is where the climb itself is locked down.
        /// </summary>
        [Fact]
        public async Task CompareAsync_SequentialScan_ClimbsInsteadOfClaimingHundredPercentEarly()
        {
            string left = BuildTree("left", perLevel: 3, depth: 3);
            string right = BuildTree("right", perLevel: 3, depth: 3);

            var config = new AppConfiguration();
            config.Options.Compare.ScanMethod.ParallelOptions = false;
            var comparer = new DirectoryComparer(config, new FileSystem(), new HashProvider());

            var reported = new List<double>();
            await comparer.CompareAsync(left, right, CancellationToken.None, p => reported.Add(p), ComparisonAlgorithm.ModifiedDateAndSize);

            Assert.NotEmpty(reported);
            Assert.Equal(1.0, reported[^1]);
            Assert.Contains(reported, p => p > 0 && p < 1.0);
            Assert.True(reported[^2] < 1.0, "The bar claimed 100% while the scan was still running.");
        }

        /// <summary>
        /// Parallel mode is the default and the shape the bug produced: a lone 1.0 arriving on the first
        /// folder. Whatever the timing, the bar may not reach 100% before every directory is done, so at most
        /// one tenth below the top can be missing from the report.
        /// </summary>
        [Fact]
        public async Task CompareAsync_ParallelScan_NeverReportsHundredPercentBeforeTheEnd()
        {
            string left = BuildTree("left", perLevel: 3, depth: 3);
            string right = BuildTree("right", perLevel: 3, depth: 3);

            var comparer = new DirectoryComparer(new AppConfiguration(), new FileSystem(), new HashProvider());

            var reported = new List<double>();
            await comparer.CompareAsync(left, right, CancellationToken.None, p => reported.Add(p), ComparisonAlgorithm.ModifiedDateAndSize);

            Assert.Equal(1.0, reported[^1]);
            Assert.True(reported.Count(p => p >= 1.0) == 1,
                $"Only the completion may report 100%, but got [{string.Join(", ", reported)}].");
        }
    }
}
