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
            _comparer = ImageComparerFactory.CreateImageSharpComparer();
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