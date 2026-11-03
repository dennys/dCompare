using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.IO;
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

    public class FileImageSource(string path) : IImageSource
    {
        private readonly string _path = path ?? throw new ArgumentNullException(nameof(path));

        public Image<Rgba32> GetImage()
        {
            if (!File.Exists(_path))
                throw new FileNotFoundException("Image file does not exist", _path);
            return Image.Load<Rgba32>(_path);
        }
    }

    public class StreamImageSource(Stream stream) : IImageSource
    {
        private readonly Stream _stream = stream ?? throw new ArgumentNullException(nameof(stream));

        public Image<Rgba32> GetImage()
        {
            return Image.Load<Rgba32>(_stream);
        }
    }

    public class ImageSourceImageSource(Image<Rgba32> image) : IImageSource
    {
        private readonly Image<Rgba32> _image = image ?? throw new ArgumentNullException(nameof(image));

        public Image<Rgba32> GetImage()
        {
            return _image.Clone();
        }
    }

    public class BitmapImageSource(BitmapSource bitmapSource) : IImageSource
    {
        private readonly BitmapSource _bitmapSource = bitmapSource ?? throw new ArgumentNullException(nameof(bitmapSource));

        public Image<Rgba32> GetImage()
        {
            using MemoryStream stream = new();
            PngBitmapEncoder encoder = new();
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

            using Image<Rgba32> image1 = source1.GetImage();
            using Image<Rgba32> image2 = source2.GetImage();

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

        private static ImageComparisonResult CompareImagesInternal(Image<Rgba32> image1, Image<Rgba32> image2, IImageComparisonOptions options)
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

        private static (List<(int X, int Y)> differences, int totalBlocks) FindDifferenceBlocks(
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

        private static bool IsBlockDifferent(ReadOnlySpan<Rgba32> span1, ReadOnlySpan<Rgba32> span2,
            int imageWidth, int imageHeight, int blockX, int blockY, int blockSize, int threshold, ImageComparisonAlgorithm algorithm)
        {
            double totalDifference = 0;
            double maxPixelDifference = 0;
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

                    double pixelDifference = CalculatePixelDifference(pixel1, pixel2, algorithm);
                    totalDifference += pixelDifference;
                    if (pixelDifference > maxPixelDifference) maxPixelDifference = pixelDifference;
                    pixelCount++;
                }
            }

            double averageDifference = pixelCount > 0 ? totalDifference / pixelCount : 0;
            // A small change (one inverted pixel in a 16x16 block averages ~255/256) must not be hidden
            // by the block average: a single pixel above the threshold marks the block as different too.
            return averageDifference > threshold || maxPixelDifference > threshold;
        }

        private static bool IsBlockDifferentSafe(Image<Rgba32> image1, Image<Rgba32> image2,
            int blockX, int blockY, int blockSize, int threshold, ImageComparisonAlgorithm algorithm)
        {
            double totalDifference = 0;
            double maxPixelDifference = 0;
            int pixelCount = 0;

            int endX = Math.Min(blockX + blockSize, image1.Width);
            int endY = Math.Min(blockY + blockSize, image1.Height);

            for (int y = blockY; y < endY; y++)
            {
                for (int x = blockX; x < endX; x++)
                {
                    var pixel1 = image1[x, y];
                    var pixel2 = image2[x, y];

                    double pixelDifference = CalculatePixelDifference(pixel1, pixel2, algorithm);
                    totalDifference += pixelDifference;
                    if (pixelDifference > maxPixelDifference) maxPixelDifference = pixelDifference;
                    pixelCount++;
                }
            }

            double averageDifference = pixelCount > 0 ? totalDifference / pixelCount : 0;
            return averageDifference > threshold || maxPixelDifference > threshold;
        }

        private static double CalculatePixelDifference(Rgba32 pixel1, Rgba32 pixel2, ImageComparisonAlgorithm algorithm)
        {
            return algorithm switch
            {
                ImageComparisonAlgorithm.EuclideanDistance => CalculateEuclideanDistance(pixel1, pixel2),
                ImageComparisonAlgorithm.ManhattanDistance => CalculateManhattanDistance(pixel1, pixel2),
                ImageComparisonAlgorithm.PerceptualDifference => CalculatePerceptualDifference(pixel1, pixel2),
                _ => CalculateEuclideanDistance(pixel1, pixel2)
            };
        }

        private static double CalculateEuclideanDistance(Rgba32 pixel1, Rgba32 pixel2)
        {
            int dr = pixel1.R - pixel2.R;
            int dg = pixel1.G - pixel2.G;
            int db = pixel1.B - pixel2.B;
            return Math.Sqrt(dr * dr + dg * dg + db * db);
        }

        private static double CalculateManhattanDistance(Rgba32 pixel1, Rgba32 pixel2)
        {
            return Math.Abs(pixel1.R - pixel2.R) + Math.Abs(pixel1.G - pixel2.G) + Math.Abs(pixel1.B - pixel2.B);
        }

        private static double CalculatePerceptualDifference(Rgba32 pixel1, Rgba32 pixel2)
        {
            int dr = pixel1.R - pixel2.R;
            int dg = pixel1.G - pixel2.G;
            int db = pixel1.B - pixel2.B;
            return Math.Sqrt(0.299 * dr * dr + 0.587 * dg * dg + 0.114 * db * db);
        }

        private static List<Rectangle> MergeAdjacentBlocks(List<(int X, int Y)> blocks, int blockSize)
        {
            if (blocks.Count == 0) return new List<Rectangle>();

            // Union-find over grid cells: for grid-aligned blocks, two blocks merge iff their
            // 1px-inflated rectangles intersect, which is exactly |dx| <= 1 && |dy| <= 1 in block
            // coordinates. Single pass + union = O(n) instead of repeatedly re-scanning all blocks.
            var parent = new int[blocks.Count];
            for (int i = 0; i < parent.Length; i++) parent[i] = i;

            var cellToIndex = new Dictionary<(int X, int Y), int>(blocks.Count);
            int[] dx = { -1, 0, 0, 1, -1, 1, -1, 1 };
            int[] dy = { 0, 0, 1, 0, -1, -1, 1, 1 };
            for (int i = 0; i < blocks.Count; i++)
            {
                int gx = blocks[i].X / blockSize;
                int gy = blocks[i].Y / blockSize;
                cellToIndex[(gx, gy)] = i;
                for (int d = 0; d < 8; d++)
                {
                    if (cellToIndex.TryGetValue((gx + dx[d], gy + dy[d]), out int neighbor)) Union(parent, i, neighbor);
                }
            }

            // Group by root, keeping first-seen order (same order as the old implementation).
            var regionOfRoot = new Dictionary<int, int>();
            var regions = new List<List<(int X, int Y)>>();
            for (int i = 0; i < blocks.Count; i++)
            {
                int root = Find(parent, i);
                if (!regionOfRoot.TryGetValue(root, out int regionIndex))
                {
                    regionOfRoot[root] = regions.Count;
                    regions.Add(new List<(int X, int Y)> { blocks[i] });
                }
                else
                {
                    regions[regionIndex].Add(blocks[i]);
                }
            }

            var merged = new List<Rectangle>(regions.Count);
            foreach (var region in regions)
            {
                int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
                foreach (var (x, y) in region)
                {
                    if (x < minX) minX = x;
                    if (y < minY) minY = y;
                    if (x + blockSize > maxX) maxX = x + blockSize;
                    if (y + blockSize > maxY) maxY = y + blockSize;
                }
                merged.Add(new Rectangle(minX, minY, maxX - minX, maxY - minY));
            }

            return merged;
        }

        private static int Find(int[] parent, int i)
        {
            while (parent[i] != i)
            {
                parent[i] = parent[parent[i]];
                i = parent[i];
            }
            return i;
        }

        private static void Union(int[] parent, int a, int b)
        {
            int rootA = Find(parent, a);
            int rootB = Find(parent, b);
            if (rootA != rootB) parent[rootB] = rootA;
        }

        private static List<Rectangle> ConvertBlocksToRegions(List<(int X, int Y)> blocks, int blockSize)
        {
            return blocks.Select(block => new Rectangle(block.X, block.Y, blockSize, blockSize)).ToList();
        }

        private static Image<Rgba32> GenerateMaskImage(int width, int height, IReadOnlyList<Rectangle> differenceRegions)
        {
            // ImageSharp v4 dropped Drawing's Clear/Fill (and the free rasterizer); solid-rect pixel writes are all the mask needs.
            // Rows are filled through the raw pixel memory (single pass, no per-pixel indexer dispatch).
            // DangerousTryGetSinglePixelMemory exists in both v3 and v4 but can fail for large images
            // (non-contiguous buffer); fall back to the per-pixel indexer in that case.
            var red = new Rgba32(255, 0, 0, 128);
            var maskImage = new Image<Rgba32>(width, height); // transparent by default, same as the old Clear(Transparent)

            if (maskImage.DangerousTryGetSinglePixelMemory(out Memory<Rgba32> memory))
            {
                var span = memory.Span;
                foreach (var region in differenceRegions)
                {
                    var left = Math.Max(0, region.X);
                    var right = Math.Min(width, region.X + region.Width);
                    var top = Math.Max(0, region.Y);
                    var bottom = Math.Min(height, region.Y + region.Height);
                    if (left >= right || top >= bottom) continue;
                    for (var y = top; y < bottom; y++)
                    {
                        span.Slice(y * width + left, right - left).Fill(red);
                    }
                }
            }
            else
            {
                foreach (var region in differenceRegions)
                {
                    var left = Math.Max(0, region.X);
                    var right = Math.Min(width, region.X + region.Width);
                    var top = Math.Max(0, region.Y);
                    var bottom = Math.Min(height, region.Y + region.Height);
                    for (var y = top; y < bottom; y++)
                    {
                        for (var x = left; x < right; x++)
                        {
                            maskImage[x, y] = red;
                        }
                    }
                }
            }

            return maskImage;
        }

        private static void ValidateParameters(Image<Rgba32> image1, Image<Rgba32> image2, IImageComparisonOptions options)
        {
            if (image1 == null || image2 == null)
                throw new ArgumentNullException("Images cannot be null");

            if (options.BlockSize <= 0)
                throw new ArgumentException("Block size must be positive", nameof(options.BlockSize));

            if (options.Threshold < 0 || options.Threshold > 255)
                throw new ArgumentException("Threshold must be between 0 and 255", nameof(options.Threshold));
        }
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