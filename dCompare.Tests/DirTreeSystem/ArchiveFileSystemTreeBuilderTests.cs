using dCompare.Compare;
using dCompare.Compare.Models;
using dCompare.Configuration;
using dCompare.DirTreeSystem;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using Xunit;

namespace dCompare.Tests.DirTreeSystem
{
    /// <summary>
    /// Archive comparison. An entry's FullPath is fabricated (archive file + entry key) and cannot be opened,
    /// so identity has to come from the entry metadata: a comparison that tried to hash the stream instead
    /// reported every entry of two byte-identical archives as Conflict.
    /// </summary>
    public class ArchiveFileSystemTreeBuilderTests : IDisposable
    {
        private static readonly DateTimeOffset FixedStamp = new(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        private readonly string _testRoot;

        public ArchiveFileSystemTreeBuilderTests()
        {
            _testRoot = Path.Combine(Path.GetTempPath(), "ArchiveTreeTests", Path.GetRandomFileName());
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

        private string MakeArchive(string fileName, params (string Key, string Content)[] entries)
        {
            string path = Path.Combine(_testRoot, fileName);
            using (FileStream fs = new(path, FileMode.Create))
            using (ZipArchive zip = new(fs, ZipArchiveMode.Create))
            {
                foreach ((string key, string content) in entries)
                {
                    ZipArchiveEntry entry = zip.CreateEntry(key);
                    entry.LastWriteTime = FixedStamp;   // equal stamps: the test is about content, not dates
                    using Stream entryStream = entry.Open();
                    using StreamWriter writer = new(entryStream);
                    writer.Write(content);
                }
            }
            return path;
        }

        private ComparisonStatus StatusOf(DirectoryComparer comparer, string left, string right, string entryName, ComparisonAlgorithm algorithm)
        {
            List<DirectoryComparer.ComparisonItem> items = comparer
                .CompareAsync(left, right, CancellationToken.None, null, algorithm)
                .GetAwaiter().GetResult();

            DirectoryComparer.ComparisonItem row = items.FirstOrDefault(i =>
                i.Left?.Name == entryName || i.Right?.Name == entryName);
            return row?.Status ?? throw new InvalidOperationException(
                $"No comparison row for '{entryName}'; rows were: {string.Join(", ", items.Select(i => i.TreeIndent))}");
        }

        [Theory]
        [InlineData(ComparisonAlgorithm.ModifiedDateAndSize)]
        [InlineData(ComparisonAlgorithm.xxHash64_Full)]
        [InlineData(ComparisonAlgorithm.xxHash64_Partial)]
        [InlineData(ComparisonAlgorithm.MD5)]
        [InlineData(ComparisonAlgorithm.FullContents)]
        public void IdenticalArchives_EveryAlgorithm_EntryIsIdentical(ComparisonAlgorithm algorithm)
        {
            string left = MakeArchive("a.zip", ("dir/same.txt", new string('a', 5000)));
            string right = MakeArchive("b.zip", ("dir/same.txt", new string('a', 5000)));

            Assert.Equal(ComparisonStatus.Identical, StatusOf(new DirectoryComparer(), left, right, "same.txt", algorithm));
        }

        // Same name, same length, same timestamp: only the stored CRC can tell these apart, so this is the
        // test that fails if Crc is written but never read.
        [Theory]
        [InlineData(ComparisonAlgorithm.ModifiedDateAndSize)]
        [InlineData(ComparisonAlgorithm.xxHash64_Full)]
        [InlineData(ComparisonAlgorithm.MD5)]
        [InlineData(ComparisonAlgorithm.FullContents)]
        public void SameSizeDifferentContent_EntryIsModified(ComparisonAlgorithm algorithm)
        {
            string left = MakeArchive("a.zip", ("dir/same.txt", new string('a', 5000)));
            string right = MakeArchive("b.zip", ("dir/same.txt", new string('b', 5000)));

            Assert.Equal(ComparisonStatus.Modified, StatusOf(new DirectoryComparer(), left, right, "same.txt", algorithm));
        }

        // Entry keys are not normalised, so an archive made on Linux can hold both "Docs/" and "docs/". Matching
        // them case-sensitively produced two sibling nodes, and the comparer (case-insensitive) then visited
        // only the first one, hiding every file under the other from the results and from any transfer.
        [Fact]
        public void MixedCaseDirectoryEntries_BothFilesAppearInTheComparison()
        {
            string archive = MakeArchive("mixed.zip", ("Docs/upper.txt", "one"), ("docs/lower.txt", "two"));

            var builder = new ArchiveFileSystemTreeBuilder();
            var root = builder.BuildTreeRecursive(archive, 0, CancellationToken.None);
            Assert.Single(root.Children.Where(c => c.IsDirectory));

            var items = new DirectoryComparer().CompareAsync(archive, archive, CancellationToken.None, null,
                ComparisonAlgorithm.ModifiedDateAndSize).GetAwaiter().GetResult();

            Assert.Contains(items, i => i.Left?.Name == "upper.txt");
            Assert.Contains(items, i => i.Left?.Name == "lower.txt");
        }
    }
}
