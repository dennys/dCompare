using dCompare.Compare;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace dCompare.Compare.Tests
{
    public class ImageComparerTests
    {
        private readonly IImageComparer _comparer;

        public ImageComparerTests()
        {
            _comparer = new ImageSharpComparer();
        }

        [Fact]
        public void CompareImages_IdenticalImages_ReturnsNoDifferences()
        {
            // Arrange
            using var image = CreateSolidColorImage(100, 100, new Rgba32(255, 0, 0));
            var source1 = new ImageSourceImageSource(image);
            var source2 = new ImageSourceImageSource(image);
            var options = new ImageComparisonOptions { BlockSize = 10, Threshold = 30 };

            // Act
            var result = _comparer.CompareImages(source1, source2, options);

            // Assert
            Assert.False(result.HasDifferences);
            Assert.Empty(result.DifferenceBlocks);
            Assert.Empty(result.DifferenceRegions);
            Assert.Equal(100.0, result.SimilarityPercentage);
            Assert.Equal(100, result.TotalBlocksCompared); // 100x100 image, 10x10 blocks = 100 blocks
        }

        [Fact]
        public void CompareImages_DifferentImages_ReturnsDifferences()
        {
            // Arrange
            using var image1 = CreateSolidColorImage(100, 100, new Rgba32(255, 0, 0));
            using var image2 = CreateSolidColorImage(100, 100, new Rgba32(0, 255, 0));
            var source1 = new ImageSourceImageSource(image1);
            var source2 = new ImageSourceImageSource(image2);
            var options = new ImageComparisonOptions { BlockSize = 10, Threshold = 30 };

            // Act
            var result = _comparer.CompareImages(source1, source2, options);

            // Assert
            Assert.True(result.HasDifferences);
            Assert.NotEmpty(result.DifferenceBlocks);
            Assert.NotEmpty(result.DifferenceRegions);
            Assert.True(result.SimilarityPercentage < 100.0);
            Assert.Equal(100, result.TotalBlocksCompared);
        }

        [Fact]
        public void CompareImages_DifferentDimensions_ThrowsArgumentException()
        {
            // Arrange
            using var image1 = CreateSolidColorImage(100, 100, new Rgba32(255, 0, 0));
            using var image2 = CreateSolidColorImage(50, 50, new Rgba32(255, 0, 0));
            var source1 = new ImageSourceImageSource(image1);
            var source2 = new ImageSourceImageSource(image2);

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => _comparer.CompareImages(source1, source2));
            Assert.Contains("Images must have the same dimensions", exception.Message);
        }

        [Fact]
        public void CompareImages_NullSource_ThrowsArgumentNullException()
        {
            // Arrange
            using var image = CreateSolidColorImage(100, 100, new Rgba32(255, 0, 0));
            var source = new ImageSourceImageSource(image);

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => _comparer.CompareImages(source, null));
            Assert.Throws<ArgumentNullException>(() => _comparer.CompareImages(null, source));
        }

        [Fact]
        public void CompareImages_InvalidBlockSize_ThrowsArgumentException()
        {
            // Arrange
            using var image1 = CreateSolidColorImage(100, 100, new Rgba32(255, 0, 0));
            using var image2 = CreateSolidColorImage(100, 100, new Rgba32(255, 0, 0));
            var source1 = new ImageSourceImageSource(image1);
            var source2 = new ImageSourceImageSource(image2);
            var options = new ImageComparisonOptions { BlockSize = 0, Threshold = 30 };

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => _comparer.CompareImages(source1, source2, options));
            Assert.Contains("Block size must be positive", exception.Message);
        }

        [Fact]
        public void CompareImages_InvalidThreshold_ThrowsArgumentException()
        {
            // Arrange
            using var image1 = CreateSolidColorImage(100, 100, new Rgba32(255, 0, 0));
            using var image2 = CreateSolidColorImage(100, 100, new Rgba32(255, 0, 0));
            var source1 = new ImageSourceImageSource(image1);
            var source2 = new ImageSourceImageSource(image2);
            var options = new ImageComparisonOptions { BlockSize = 10, Threshold = 256 };

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => _comparer.CompareImages(source1, source2, options));
            Assert.Contains("Threshold must be between 0 and 255", exception.Message);
        }

        [Fact]
        public void CompareImages_MergeAdjacentEnabled_MergesAdjacentBlocks()
        {
            // Arrange
            using var image1 = CreateImageWithDifferenceBlock(100, 100);
            using var image2 = CreateSolidColorImage(100, 100, new Rgba32(255, 0, 0));
            var source1 = new ImageSourceImageSource(image1);
            var source2 = new ImageSourceImageSource(image2);
            var options = new ImageComparisonOptions { BlockSize = 10, Threshold = 30, MergeAdjacent = true };

            // Act
            var result = _comparer.CompareImages(source1, source2, options);

            // Assert
            Assert.True(result.HasDifferences);
            Assert.Equal(4, result.DifferenceBlocks.Count); // 2x2 blocks
            Assert.Single(result.DifferenceRegions); // Merged into one region
            Assert.Equal(new Rectangle(20, 20, 20, 20), result.DifferenceRegions[0]); // Merged 2x2 blocks
        }

        [Fact]
        public void CompareImages_MergeAdjacentDisabled_ReturnsIndividualBlocks()
        {
            // Arrange
            using var image1 = CreateImageWithDifferenceBlock(100, 100);
            using var image2 = CreateSolidColorImage(100, 100, new Rgba32(255, 0, 0));
            var source1 = new ImageSourceImageSource(image1);
            var source2 = new ImageSourceImageSource(image2);
            var options = new ImageComparisonOptions { BlockSize = 10, Threshold = 30, MergeAdjacent = false };

            // Act
            var result = _comparer.CompareImages(source1, source2, options);

            // Assert
            Assert.True(result.HasDifferences);
            Assert.Equal(4, result.DifferenceBlocks.Count); // 2x2 blocks
            Assert.Equal(4, result.DifferenceRegions.Count); // Individual blocks
        }

        [Fact]
        public void GetMaskImage_ReturnsCorrectMask()
        {
            // Arrange
            using var image1 = CreateImageWithDifferenceBlock(100, 100);
            using var image2 = CreateSolidColorImage(100, 100, new Rgba32(255, 0, 0));
            var source1 = new ImageSourceImageSource(image1);
            var source2 = new ImageSourceImageSource(image2);
            var options = new ImageComparisonOptions { BlockSize = 10, Threshold = 30, MergeAdjacent = true };

            // Act
            var result = _comparer.CompareImages(source1, source2, options);
            using var mask = result.GetMaskImage();

            // Assert
            Assert.Equal(100, mask.Width);
            Assert.Equal(100, mask.Height);
            var pixel = mask[20, 20]; // Inside difference region
            Assert.Equal(255, pixel.R);
            Assert.Equal(0, pixel.G);
            Assert.Equal(0, pixel.B);
            Assert.Equal(128, pixel.A);
        }

        [Fact]
        public async Task CompareImagesAsync_IdenticalImages_ReturnsNoDifferences()
        {
            // Arrange
            using var image = CreateSolidColorImage(100, 100, new Rgba32(255, 0, 0));
            var source1 = new ImageSourceImageSource(image);
            var source2 = new ImageSourceImageSource(image);
            var options = new ImageComparisonOptions { BlockSize = 10, Threshold = 30 };

            // Act
            var result = await _comparer.CompareImagesAsync(source1, source2, options);

            // Assert
            Assert.False(result.HasDifferences);
            Assert.Empty(result.DifferenceBlocks);
            Assert.Empty(result.DifferenceRegions);
            Assert.Equal(100.0, result.SimilarityPercentage);
        }

        [Theory]
        [InlineData(ImageComparisonAlgorithm.EuclideanDistance)]
        [InlineData(ImageComparisonAlgorithm.ManhattanDistance)]
        [InlineData(ImageComparisonAlgorithm.PerceptualDifference)]
        public void CompareImages_DifferentAlgorithms_ReturnsConsistentResults(ImageComparisonAlgorithm algorithm)
        {
            // Arrange
            using var image1 = CreateSolidColorImage(100, 100, new Rgba32(255, 0, 0));
            using var image2 = CreateSolidColorImage(100, 100, new Rgba32(0, 255, 0));
            var source1 = new ImageSourceImageSource(image1);
            var source2 = new ImageSourceImageSource(image2);
            var options = new ImageComparisonOptions { BlockSize = 10, Threshold = 30, Algorithm = algorithm };

            // Act
            var result = _comparer.CompareImages(source1, source2, options);

            // Assert
            Assert.True(result.HasDifferences);
            Assert.Equal(100, result.DifferenceBlocks.Count); // All blocks different
            Assert.True(result.SimilarityPercentage < 1.0); // Significant difference
        }

        [Fact]
        public void CompareImages_SinglePixelDifference_IsDetected()
        {
            // Arrange: two images that differ in exactly one pixel (white -> black).
            // The block average of one inverted pixel in a 16x16 block is ~255*sqrt(3)/256 < Threshold,
            // so the max per-pixel difference must carry the detection.
            using var image1 = CreateSolidColorImage(48, 48, new Rgba32(0, 0, 0));
            using var image2 = CreateSolidColorImage(48, 48, new Rgba32(0, 0, 0));
            image2[10, 10] = new Rgba32(255, 255, 255);
            var source1 = new ImageSourceImageSource(image1);
            var source2 = new ImageSourceImageSource(image2);
            var options = new ImageComparisonOptions { BlockSize = 16, Threshold = 30 };

            // Act
            var result = _comparer.CompareImages(source1, source2, options);

            // Assert
            Assert.True(result.HasDifferences);
            Assert.Single(result.DifferenceBlocks);
            Assert.Equal((0, 0), result.DifferenceBlocks[0]);
            Assert.True(result.SimilarityPercentage < 100.0);
        }

        [Fact]
        public void CompareImages_SmallRegionDifference_IsDetected()
        {
            // Arrange: a 4x4 red patch on black — the block average is small, the per-pixel diff is not.
            using var image1 = CreateSolidColorImage(32, 32, new Rgba32(0, 0, 0));
            using var image2 = CreateSolidColorImage(32, 32, new Rgba32(0, 0, 0));
            for (var y = 24; y < 28; y++)
                for (var x = 24; x < 28; x++)
                    image2[x, y] = new Rgba32(255, 0, 0);
            var source1 = new ImageSourceImageSource(image1);
            var source2 = new ImageSourceImageSource(image2);
            var options = new ImageComparisonOptions { BlockSize = 16, Threshold = 30 };

            // Act
            var result = _comparer.CompareImages(source1, source2, options);

            // Assert
            Assert.True(result.HasDifferences);
            Assert.NotEmpty(result.DifferenceBlocks);
            Assert.True(result.SimilarityPercentage < 100.0);
        }

        [Fact]
        public void CompareImages_TwoHorizontallyAdjacentBlocks_MergeIntoSingleRegion()
        {
            // Arrange: 32x16 image, block size 16; both blocks differ.
            using var image1 = CreateSolidColorImage(32, 16, new Rgba32(0, 0, 0));
            using var image2 = CreateSolidColorImage(32, 16, new Rgba32(0, 0, 0));
            for (var y = 0; y < 16; y++)
                for (var x = 4; x < 28; x++)
                    image2[x, y] = new Rgba32(255, 255, 255);
            var source1 = new ImageSourceImageSource(image1);
            var source2 = new ImageSourceImageSource(image2);
            var options = new ImageComparisonOptions { BlockSize = 16, Threshold = 30, MergeAdjacent = true };

            // Act
            var result = _comparer.CompareImages(source1, source2, options);

            // Assert: the two adjacent difference blocks form one region.
            Assert.Equal(2, result.DifferenceBlocks.Count);
            Assert.Single(result.DifferenceRegions);
            Assert.Equal(new Rectangle(0, 0, 32, 16), result.DifferenceRegions[0]);
        }

        [Fact]
        public void CompareImages_LargeImageWithManyDifferences_CompletesQuickly()
        {
            // Arrange: half of a 2000x2000 image differs — one huge connected component,
            // the worst case for the old O(n^2) re-scanning merge.
            using var image1 = CreateSolidColorImage(2000, 2000, new Rgba32(0, 0, 0));
            using var image2 = CreateSolidColorImage(2000, 2000, new Rgba32(0, 0, 0));
            for (int y = 0; y < 2000; y++)
                for (int x = 1000; x < 2000; x++)
                    image2[x, y] = new Rgba32(255, 255, 255);
            var source1 = new ImageSourceImageSource(image1);
            var source2 = new ImageSourceImageSource(image2);
            var options = new ImageComparisonOptions { BlockSize = 16, Threshold = 30, MergeAdjacent = true };

            // Act (generous bound: the linear merge finishes well under a second; the old
            // quadratic one took minutes, so this stays far from timing-sensitive).
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var result = _comparer.CompareImages(source1, source2, options);
            stopwatch.Stop();

            // Assert
            Assert.True(result.HasDifferences);
            Assert.Single(result.DifferenceRegions);
            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(30), $"Took {stopwatch.Elapsed}");
        }

        [Fact]
        public void GetMaskImage_MaskBytesExact_ForFixedInput()
        {
            // Arrange: pins the mask output byte-for-byte (every pixel red or transparent),
            // so the row-span rewrite cannot change the result.
            using var image1 = CreateImageWithDifferenceBlock(100, 100);
            using var image2 = CreateSolidColorImage(100, 100, new Rgba32(255, 0, 0));
            var source1 = new ImageSourceImageSource(image1);
            var source2 = new ImageSourceImageSource(image2);
            var options = new ImageComparisonOptions { BlockSize = 10, Threshold = 30, MergeAdjacent = true };

            // Act
            var result = _comparer.CompareImages(source1, source2, options);
            using var mask = result.GetMaskImage();

            // Assert: exactly the merged region (20,20,20,20) is semi-transparent red, everything else transparent.
            for (var y = 0; y < 100; y++)
            {
                for (var x = 0; x < 100; x++)
                {
                    var pixel = mask[x, y];
                    bool inside = x >= 20 && x < 40 && y >= 20 && y < 40;
                    if (inside)
                    {
                        Assert.Equal(new Rgba32(255, 0, 0, 128), pixel);
                    }
                    else
                    {
                        Assert.Equal(new Rgba32(0, 0, 0, 0), pixel);
                    }
                }
            }
        }

        private static Image<Rgba32> CreateSolidColorImage(int width, int height, Rgba32 color)
        {
            var image = new Image<Rgba32>(width, height, color);
            return image;
        }

        private static Image<Rgba32> CreateImageWithDifferenceBlock(int width, int height)
        {
            var image = new Image<Rgba32>(width, height, new Rgba32(255, 0, 0));
            // ImageSharp v4 dropped Drawing's Fill; write the diff block (2x2 blocks at 10x10 block size) directly.
            for (var y = 20; y < 40; y++)
                for (var x = 20; x < 40; x++)
                    image[x, y] = new Rgba32(0, 255, 0);
            return image;
        }
    }
}