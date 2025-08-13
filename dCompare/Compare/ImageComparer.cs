using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace dCompare.Compare
{
    // Core interfaces
    public interface IImageComparisonResult
    {
        IReadOnlyList<(int X, int Y)> DifferenceBlocks { get; }
        IReadOnlyList<Rectangle> DifferenceRegions { get; }
        Image<Rgba32> GetMaskImage();
        bool HasDifferences { get; }
        double SimilarityPercentage { get; }
        int TotalBlocksCompared { get; }
    }

    public interface IImageSource
    {
        Image<Rgba32> GetImage();
    }

    public class FileImageSource : IImageSource
    {
        private readonly string _path;

        public FileImageSource(string path)
        {
            _path = path ?? throw new ArgumentNullException(nameof(path));
        }

        public Image<Rgba32> GetImage()
        {
            if (!File.Exists(_path))
                throw new FileNotFoundException("Image file does not exist", _path);
            return Image.Load<Rgba32>(_path);
        }
    }

    public class StreamImageSource : IImageSource
    {
        private readonly Stream _stream;

        public StreamImageSource(Stream stream)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        }

        public Image<Rgba32> GetImage()
        {
            return Image.Load<Rgba32>(_stream);
        }
    }

    public class ImageSourceImageSource : IImageSource
    {
        private readonly Image<Rgba32> _image;

        public ImageSourceImageSource(Image<Rgba32> image)
        {
            _image = image ?? throw new ArgumentNullException(nameof(image));
        }

        public Image<Rgba32> GetImage()
        {
            return _image.Clone();
        }
    }

    public class BitmapImageSource : IImageSource
    {
        private readonly BitmapSource _bitmapSource;

        public BitmapImageSource(BitmapSource bitmapSource)
        {
            _bitmapSource = bitmapSource ?? throw new ArgumentNullException(nameof(bitmapSource));
        }

        public Image<Rgba32> GetImage()
        {
            using var stream = new MemoryStream();
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(_bitmapSource));
            encoder.Save(stream);
            stream.Position = 0;
            return Image.Load<Rgba32>(stream);
        }
    }

    public interface IImageComparer
    {
        IImageComparisonResult CompareImages(IImageSource source1, IImageSource source2, IImageComparisonOptions? options = null);
        IImageComparisonResult CompareImages(string path1, string path2, IImageComparisonOptions? options = null);
        IImageComparisonResult CompareImages(Image<Rgba32> image1, Image<Rgba32> image2, IImageComparisonOptions? options = null);
        IImageComparisonResult CompareImages(Stream stream1, Stream stream2, IImageComparisonOptions? options = null);
    }

    public interface IImageComparisonOptions
    {
        int BlockSize { get; }
        int Threshold { get; }
        bool MergeAdjacent { get; }
        ImageComparisonAlgorithm Algorithm { get; }
    }

    public enum ImageComparisonAlgorithm
    {
        EuclideanDistance,
        ManhattanDistance,
        PerceptualDifference
    }

    // Implementations
    public class ImageComparisonOptions : IImageComparisonOptions
    {
        public int BlockSize { get; set; } = 16;
        public int Threshold { get; set; } = 30;
        public bool MergeAdjacent { get; set; } = true;
        public ImageComparisonAlgorithm Algorithm { get; set; } = ImageComparisonAlgorithm.EuclideanDistance;
    }

    public class ImageComparisonResult : IImageComparisonResult
    {
        private readonly Lazy<Image<Rgba32>> _maskImage;

        public IReadOnlyList<(int X, int Y)> DifferenceBlocks { get; }
        public IReadOnlyList<Rectangle> DifferenceRegions { get; }
        public bool HasDifferences => DifferenceBlocks.Count > 0;
        public double SimilarityPercentage { get; }
        public int TotalBlocksCompared { get; }

        internal ImageComparisonResult(
            IReadOnlyList<(int X, int Y)> differenceBlocks,
            IReadOnlyList<Rectangle> differenceRegions,
            Lazy<Image<Rgba32>> maskImage,
            double similarityPercentage,
            int totalBlocksCompared)
        {
            DifferenceBlocks = differenceBlocks;
            DifferenceRegions = differenceRegions;
            _maskImage = maskImage;
            SimilarityPercentage = similarityPercentage;
            TotalBlocksCompared = totalBlocksCompared;
        }

        public Image<Rgba32> GetMaskImage() => _maskImage.Value.Clone();
    }

    public class ImageSharpComparer : IImageComparer
    {
        public IImageComparisonResult CompareImages(IImageSource source1, IImageSource source2, IImageComparisonOptions? options = null)
        {
            if (source1 == null || source2 == null)
                throw new ArgumentNullException("Image sources cannot be null");

            using var image1 = source1.GetImage();
            using var image2 = source2.GetImage();

            return CompareImagesInternal(image1, image2, options ?? new ImageComparisonOptions());
        }

        public IImageComparisonResult CompareImages(string path1, string path2, IImageComparisonOptions? options = null)
        {
            return CompareImages(new FileImageSource(path1), new FileImageSource(path2), options);
        }

        public IImageComparisonResult CompareImages(Image<Rgba32> image1, Image<Rgba32> image2, IImageComparisonOptions? options = null)
        {
            return CompareImages(new ImageSourceImageSource(image1), new ImageSourceImageSource(image2), options);
        }

        public IImageComparisonResult CompareImages(Stream stream1, Stream stream2, IImageComparisonOptions? options = null)
        {
            return CompareImages(new StreamImageSource(stream1), new StreamImageSource(stream2), options);
        }

        private IImageComparisonResult CompareImagesInternal(Image<Rgba32> image1, Image<Rgba32> image2, IImageComparisonOptions options)
        {
            ValidateParameters(image1, image2, options);

            if (image1.Width != image2.Width || image1.Height != image2.Height)
            {
                throw new ArgumentException("Images must have the same dimensions for comparison");
            }

            var (differenceBlocks, totalBlocks) = FindDifferenceBlocks(image1, image2, options);
            var differenceRegions = options.MergeAdjacent ?
                MergeAdjacentBlocks(differenceBlocks, options.BlockSize) :
                ConvertBlocksToRegions(differenceBlocks, options.BlockSize);

            var similarityPercentage = totalBlocks > 0 ?
                ((double)(totalBlocks - differenceBlocks.Count) / totalBlocks) * 100.0 : 100.0;

            var lazyMask = new Lazy<Image<Rgba32>>(() => GenerateMaskImage(image1.Width, image1.Height, differenceRegions));

            return new ImageComparisonResult(differenceBlocks, differenceRegions, lazyMask, similarityPercentage, totalBlocks);
        }

        private (List<(int X, int Y)> differences, int totalBlocks) FindDifferenceBlocks(
            Image<Rgba32> image1, Image<Rgba32> image2, IImageComparisonOptions options)
        {
            var differences = new List<(int X, int Y)>();
            int totalBlocks = 0;

            if (image1.DangerousTryGetSinglePixelMemory(out var memory1) &&
                image2.DangerousTryGetSinglePixelMemory(out var memory2))
            {
                var span1 = memory1.Span;
                var span2 = memory2.Span;

                for (int blockY = 0; blockY < image1.Height; blockY += options.BlockSize)
                {
                    for (int blockX = 0; blockX < image1.Width; blockX += options.BlockSize)
                    {
                        totalBlocks++;

                        if (IsBlockDifferent(span1, span2, image1.Width, image1.Height,
                            blockX, blockY, options.BlockSize, options.Threshold, options.Algorithm))
                        {
                            differences.Add((blockX, blockY));
                        }
                    }
                }
            }
            else
            {
                for (int blockY = 0; blockY < image1.Height; blockY += options.BlockSize)
                {
                    for (int blockX = 0; blockX < image1.Width; blockX += options.BlockSize)
                    {
                        totalBlocks++;

                        if (IsBlockDifferentSafe(image1, image2, blockX, blockY, options.BlockSize, options.Threshold, options.Algorithm))
                        {
                            differences.Add((blockX, blockY));
                        }
                    }
                }
            }

            return (differences, totalBlocks);
        }

        private bool IsBlockDifferent(ReadOnlySpan<Rgba32> span1, ReadOnlySpan<Rgba32> span2,
            int imageWidth, int imageHeight, int blockX, int blockY, int blockSize, int threshold, ImageComparisonAlgorithm algorithm)
        {
            double totalDifference = 0;
            int pixelCount = 0;

            int endX = Math.Min(blockX + blockSize, imageWidth);
            int endY = Math.Min(blockY + blockSize, imageHeight);

            for (int y = blockY; y < endY; y++)
            {
                for (int x = blockX; x < endX; x++)
                {
                    int index = y * imageWidth + x;
                    var pixel1 = span1[index];
                    var pixel2 = span2[index];

                    totalDifference += CalculatePixelDifference(pixel1, pixel2, algorithm);
                    pixelCount++;
                }
            }

            double averageDifference = pixelCount > 0 ? totalDifference / pixelCount : 0;
            return averageDifference > threshold;
        }

        private bool IsBlockDifferentSafe(Image<Rgba32> image1, Image<Rgba32> image2,
            int blockX, int blockY, int blockSize, int threshold, ImageComparisonAlgorithm algorithm)
        {
            double totalDifference = 0;
            int pixelCount = 0;

            int endX = Math.Min(blockX + blockSize, image1.Width);
            int endY = Math.Min(blockY + blockSize, image1.Height);

            for (int y = blockY; y < endY; y++)
            {
                for (int x = blockX; x < endX; x++)
                {
                    var pixel1 = image1[x, y];
                    var pixel2 = image2[x, y];

                    totalDifference += CalculatePixelDifference(pixel1, pixel2, algorithm);
                    pixelCount++;
                }
            }

            double averageDifference = pixelCount > 0 ? totalDifference / pixelCount : 0;
            return averageDifference > threshold;
        }

        private double CalculatePixelDifference(Rgba32 pixel1, Rgba32 pixel2, ImageComparisonAlgorithm algorithm)
        {
            return algorithm switch
            {
                ImageComparisonAlgorithm.EuclideanDistance => CalculateEuclideanDistance(pixel1, pixel2),
                ImageComparisonAlgorithm.ManhattanDistance => CalculateManhattanDistance(pixel1, pixel2),
                ImageComparisonAlgorithm.PerceptualDifference => CalculatePerceptualDifference(pixel1, pixel2),
                _ => CalculateEuclideanDistance(pixel1, pixel2)
            };
        }

        private double CalculateEuclideanDistance(Rgba32 pixel1, Rgba32 pixel2)
        {
            int dr = pixel1.R - pixel2.R;
            int dg = pixel1.G - pixel2.G;
            int db = pixel1.B - pixel2.B;
            return Math.Sqrt(dr * dr + dg * dg + db * db);
        }

        private double CalculateManhattanDistance(Rgba32 pixel1, Rgba32 pixel2)
        {
            return Math.Abs(pixel1.R - pixel2.R) + Math.Abs(pixel1.G - pixel2.G) + Math.Abs(pixel1.B - pixel2.B);
        }

        private double CalculatePerceptualDifference(Rgba32 pixel1, Rgba32 pixel2)
        {
            int dr = pixel1.R - pixel2.R;
            int dg = pixel1.G - pixel2.G;
            int db = pixel1.B - pixel2.B;
            return Math.Sqrt(0.299 * dr * dr + 0.587 * dg * dg + 0.114 * db * db);
        }

        private List<Rectangle> MergeAdjacentBlocks(List<(int X, int Y)> blocks, int blockSize)
        {
            if (blocks.Count == 0) return new List<Rectangle>();

            var blockRects = blocks.Select(block => new Rectangle(block.X, block.Y, blockSize, blockSize)).ToList();
            var merged = new List<Rectangle>();
            var processed = new HashSet<int>();

            for (int i = 0; i < blockRects.Count; i++)
            {
                if (processed.Contains(i)) continue;

                var currentRegion = blockRects[i];
                processed.Add(i);

                bool foundAdjacent;
                do
                {
                    foundAdjacent = false;
                    for (int j = 0; j < blockRects.Count; j++)
                    {
                        if (processed.Contains(j)) continue;

                        var candidate = blockRects[j];
                        if (AreRectanglesAdjacent(currentRegion, candidate))
                        {
                            currentRegion = Rectangle.Union(currentRegion, candidate);
                            processed.Add(j);
                            foundAdjacent = true;
                        }
                    }
                } while (foundAdjacent);

                merged.Add(currentRegion);
            }

            return merged;
        }

        private bool AreRectanglesAdjacent(Rectangle rect1, Rectangle rect2)
        {
            var inflatedRect1 = new Rectangle(rect1.X - 1, rect1.Y - 1, rect1.Width + 2, rect1.Height + 2);
            return inflatedRect1.IntersectsWith(rect2);
        }

        private List<Rectangle> ConvertBlocksToRegions(List<(int X, int Y)> blocks, int blockSize)
        {
            return blocks.Select(block => new Rectangle(block.X, block.Y, blockSize, blockSize)).ToList();
        }

        private Image<Rgba32> GenerateMaskImage(int width, int height, IReadOnlyList<Rectangle> differenceRegions)
        {
            var maskImage = new Image<Rgba32>(width, height);

            maskImage.Mutate(ctx =>
            {
                ctx.Clear(SixLabors.ImageSharp.Color.Transparent);
                var redColor = SixLabors.ImageSharp.Color.FromRgba(255, 0, 0, 128);

                foreach (var region in differenceRegions)
                {
                    var rect = new RectangleF(region.X, region.Y, region.Width, region.Height);
                    ctx.Fill(redColor, rect);
                }
            });

            return maskImage;
        }

        private void ValidateParameters(Image<Rgba32> image1, Image<Rgba32> image2, IImageComparisonOptions options)
        {
            if (image1 == null || image2 == null)
                throw new ArgumentNullException("Images cannot be null");

            if (options.BlockSize <= 0)
                throw new ArgumentException("Block size must be positive", nameof(options.BlockSize));

            if (options.Threshold < 0 || options.Threshold > 255)
                throw new ArgumentException("Threshold must be between 0 and 255", nameof(options.Threshold));
        }
    }

    public static class ImageComparerFactory
    {
        public static IImageComparer CreateImageSharpComparer() => new ImageSharpComparer();
    }

    public static class ImageComparisonExtensions
    {
        public static async Task<IImageComparisonResult> CompareImagesAsync(this IImageComparer comparer,
            IImageSource source1, IImageSource source2, IImageComparisonOptions? options = null)
        {
            return await Task.Run(() => comparer.CompareImages(source1, source2, options));
        }

        public static async Task<IImageComparisonResult> CompareImagesAsync(this IImageComparer comparer,
            string path1, string path2, IImageComparisonOptions? options = null)
        {
            return await Task.Run(() => comparer.CompareImages(path1, path2, options));
        }

        public static void SaveMaskImage(this IImageComparisonResult result, string outputPath)
        {
            using var maskImage = result.GetMaskImage();
            maskImage.Save(outputPath);
        }

        public static bool IsSimilarityAboveThreshold(this IImageComparisonResult result, double threshold)
        {
            return result.SimilarityPercentage >= threshold;
        }
    }
}