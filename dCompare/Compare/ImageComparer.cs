using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace dCompare.Compare
{
    // Result interface and implementation
    public interface IImageComparisonResult
    {
        IReadOnlyList<(int X, int Y)> DifferenceBlocks { get; }
        IReadOnlyList<Rectangle> DifferenceRegions { get; }
        Bitmap GetMaskImage();
        bool HasDifferences { get; }
    }

    public class ImageComparisonResult : IImageComparisonResult
    {
        private readonly Lazy<Bitmap> _maskImage;

        public IReadOnlyList<(int X, int Y)> DifferenceBlocks { get; }
        public IReadOnlyList<Rectangle> DifferenceRegions { get; }
        public bool HasDifferences => DifferenceBlocks.Count > 0;

        internal ImageComparisonResult(IReadOnlyList<(int X, int Y)> differenceBlocks,
            IReadOnlyList<Rectangle> differenceRegions, Lazy<Bitmap> maskImage)
        {
            DifferenceBlocks = differenceBlocks;
            DifferenceRegions = differenceRegions;
            _maskImage = maskImage;
        }

        public Bitmap GetMaskImage() => _maskImage.Value;
    }

    // Main comparison class
    public static class ImageComparison
    {
        /// <summary>
        /// Compare two images using ImageSource objects
        /// </summary>
        /// <param name="imgSrc1">First image source</param>
        /// <param name="imgSrc2">Second image source</param>
        /// <param name="blockSize">Size of comparison blocks in pixels</param>
        /// <param name="threshold">Threshold for considering blocks different (0-255)</param>
        /// <param name="mergeAdjacent">Whether to merge adjacent difference blocks into regions</param>
        /// <returns>Comparison result with difference blocks and lazy mask generation</returns>
        public static IImageComparisonResult CompareImages(ImageSource imgSrc1, ImageSource imgSrc2,
            int blockSize = 16, int threshold = 30, bool mergeAdjacent = true)
        {
            if (imgSrc1 == null || imgSrc2 == null)
                throw new ArgumentNullException("Image sources cannot be null");

            var bitmap1 = ConvertImageSourceToBitmap(imgSrc1);
            var bitmap2 = ConvertImageSourceToBitmap(imgSrc2);

            return CompareImagesInternal(bitmap1, bitmap2, blockSize, threshold, mergeAdjacent);
        }

        /// <summary>
        /// Compare two images using file paths
        /// </summary>
        /// <param name="path1">Path to first image</param>
        /// <param name="path2">Path to second image</param>
        /// <param name="blockSize">Size of comparison blocks in pixels</param>
        /// <param name="threshold">Threshold for considering blocks different (0-255)</param>
        /// <param name="mergeAdjacent">Whether to merge adjacent difference blocks into regions</param>
        /// <returns>Comparison result with difference blocks and lazy mask generation</returns>
        public static IImageComparisonResult CompareImages(string path1, string path2,
            int blockSize = 16, int threshold = 30, bool mergeAdjacent = true)
        {
            if (string.IsNullOrEmpty(path1) || string.IsNullOrEmpty(path2))
                throw new ArgumentException("Image paths cannot be null or empty");

            if (!File.Exists(path1) || !File.Exists(path2))
                throw new FileNotFoundException("One or both image files do not exist");

            using var bitmap1 = new Bitmap(path1);
            using var bitmap2 = new Bitmap(path2);

            // Create copies to avoid disposal issues
            var copy1 = new Bitmap(bitmap1);
            var copy2 = new Bitmap(bitmap2);

            return CompareImagesInternal(copy1, copy2, blockSize, threshold, mergeAdjacent);
        }

        private static IImageComparisonResult CompareImagesInternal(Bitmap bitmap1, Bitmap bitmap2,
            int blockSize, int threshold, bool mergeAdjacent)
        {
            ValidateParameters(bitmap1, bitmap2, blockSize, threshold);

            // Ensure both images have the same dimensions for comparison
            if (bitmap1.Width != bitmap2.Width || bitmap1.Height != bitmap2.Height)
            {
                throw new ArgumentException("Images must have the same dimensions for comparison");
            }

            var differenceBlocks = FindDifferenceBlocks(bitmap1, bitmap2, blockSize, threshold);
            var differenceRegions = mergeAdjacent ?
                MergeAdjacentBlocks(differenceBlocks, blockSize) :
                ConvertBlocksToRegions(differenceBlocks, blockSize);

            // Create lazy mask image generator
            var lazyMask = new Lazy<Bitmap>(() => GenerateMaskImage(bitmap1, differenceRegions));

            return new ImageComparisonResult(differenceBlocks, differenceRegions, lazyMask);
        }

        private static List<(int X, int Y)> FindDifferenceBlocks(Bitmap bitmap1, Bitmap bitmap2,
            int blockSize, int threshold)
        {
            var differences = new List<(int X, int Y)>();

            // Lock bits for faster pixel access
            var rect1 = new Rectangle(0, 0, bitmap1.Width, bitmap1.Height);
            var rect2 = new Rectangle(0, 0, bitmap2.Width, bitmap2.Height);

            var bmpData1 = bitmap1.LockBits(rect1, ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            var bmpData2 = bitmap2.LockBits(rect2, ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format24bppRgb);

            try
            {
                unsafe
                {
                    byte* ptr1 = (byte*)bmpData1.Scan0;
                    byte* ptr2 = (byte*)bmpData2.Scan0;
                    int stride1 = bmpData1.Stride;
                    int stride2 = bmpData2.Stride;

                    // Compare blocks
                    for (int blockY = 0; blockY < bitmap1.Height; blockY += blockSize)
                    {
                        for (int blockX = 0; blockX < bitmap1.Width; blockX += blockSize)
                        {
                            if (IsBlockDifferent(ptr1, ptr2, stride1, stride2,
                                blockX, blockY, blockSize, threshold, bitmap1.Width, bitmap1.Height))
                            {
                                // Store top-left coordinates for block processing
                                differences.Add((blockX, blockY));
                            }
                        }
                    }
                }
            }
            finally
            {
                bitmap1.UnlockBits(bmpData1);
                bitmap2.UnlockBits(bmpData2);
            }

            return differences;
        }

        private static unsafe bool IsBlockDifferent(byte* ptr1, byte* ptr2, int stride1, int stride2,
            int blockX, int blockY, int blockSize, int threshold, int imageWidth, int imageHeight)
        {
            long totalDifference = 0;
            int pixelCount = 0;

            int endX = Math.Min(blockX + blockSize, imageWidth);
            int endY = Math.Min(blockY + blockSize, imageHeight);

            for (int y = blockY; y < endY; y++)
            {
                byte* row1 = ptr1 + (y * stride1);
                byte* row2 = ptr2 + (y * stride2);

                for (int x = blockX; x < endX; x++)
                {
                    int pixelIndex = x * 3; // 3 bytes per pixel (RGB)

                    byte r1 = row1[pixelIndex + 2];     // Red
                    byte g1 = row1[pixelIndex + 1];     // Green
                    byte b1 = row1[pixelIndex];         // Blue

                    byte r2 = row2[pixelIndex + 2];     // Red
                    byte g2 = row2[pixelIndex + 1];     // Green
                    byte b2 = row2[pixelIndex];         // Blue

                    // Calculate color difference using Euclidean distance
                    int dr = r1 - r2;
                    int dg = g1 - g2;
                    int db = b1 - b2;

                    totalDifference += (int)Math.Sqrt(dr * dr + dg * dg + db * db);
                    pixelCount++;
                }
            }

            double averageDifference = pixelCount > 0 ? totalDifference / (double)pixelCount : 0;
            return averageDifference > threshold;
        }

        private static List<Rectangle> MergeAdjacentBlocks(List<(int X, int Y)> blocks, int blockSize)
        {
            if (blocks.Count == 0) return new List<Rectangle>();

            // Convert blocks to rectangles first
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

                        // Check if blocks are adjacent (touching or overlapping)
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

        private static bool AreRectanglesAdjacent(Rectangle rect1, Rectangle rect2)
        {
            // Inflate rect1 by 1 pixel on all sides to check for touching
            var inflatedRect1 = new Rectangle(rect1.X - 1, rect1.Y - 1, rect1.Width + 2, rect1.Height + 2);
            return inflatedRect1.IntersectsWith(rect2);
        }

        private static List<Rectangle> ConvertBlocksToRegions(List<(int X, int Y)> blocks, int blockSize)
        {
            return blocks.Select(block => new Rectangle(block.X, block.Y, blockSize, blockSize)).ToList();
        }

        private static Bitmap GenerateMaskImage(Bitmap bitmap1, IReadOnlyList<Rectangle> differenceRegions)
        {
            var maskImage = new Bitmap(bitmap1.Width, bitmap1.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            using (var g = Graphics.FromImage(maskImage))
            {
                g.Clear(System.Drawing.Color.Transparent);

                using (var redBrush = new SolidBrush(System.Drawing.Color.FromArgb(128, 255, 0, 0)))
                {
                    foreach (var region in differenceRegions)
                    {
                        g.FillRectangle(redBrush, region);
                    }
                }
            }
            return maskImage;
        }

        private static Bitmap ConvertImageSourceToBitmap(ImageSource imageSource)
        {
            if (imageSource is BitmapSource bitmapSource)
            {
                // Convert BitmapSource to Bitmap
                var bitmap = new Bitmap(bitmapSource.PixelWidth, bitmapSource.PixelHeight,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                var bitmapData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                    ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                try
                {
                    bitmapSource.CopyPixels(new System.Windows.Int32Rect(0, 0, bitmapSource.PixelWidth, bitmapSource.PixelHeight),
                        bitmapData.Scan0, bitmapData.Height * bitmapData.Stride, bitmapData.Stride);
                }
                finally
                {
                    bitmap.UnlockBits(bitmapData);
                }

                return bitmap;
            }

            throw new ArgumentException("ImageSource must be a BitmapSource", nameof(imageSource));
        }

        private static void ValidateParameters(Bitmap bitmap1, Bitmap bitmap2, int blockSize, int threshold)
        {
            if (bitmap1 == null || bitmap2 == null)
                throw new ArgumentNullException("Bitmaps cannot be null");

            if (blockSize <= 0)
                throw new ArgumentException("Block size must be positive", nameof(blockSize));

            if (threshold < 0 || threshold > 255)
                throw new ArgumentException("Threshold must be between 0 and 255", nameof(threshold));
        }
    }
}
