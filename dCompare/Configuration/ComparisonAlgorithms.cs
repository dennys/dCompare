namespace dCompare.Configuration
{
    public enum ComparisonAlgorithm
    {
        ModifiedDateAndSize,
        xxHash64_Full,
        xxHash64_Partial,
        MD5,
        FullContents
    }

    public class ComparisonAlgorithmInfo
    {
        public string Description { get; set; } = "";
        public bool IsEnabled { get; set; }
        public bool IsDefault { get; set; }
        public int? ComparisonPercentage { get; set; } // Users can change this at runtime
    }

    public static class ComparisonAlgorithms
    {
        public static readonly Dictionary<ComparisonAlgorithm, ComparisonAlgorithmInfo> AlgorithmSettings =
            new()
            {
                [ComparisonAlgorithm.ModifiedDateAndSize] = new ComparisonAlgorithmInfo
                {
                    Description = "Quick (Modified Date and Size)",
                    IsEnabled = true,
                    IsDefault = true,
                    ComparisonPercentage = null
                },
                [ComparisonAlgorithm.xxHash64_Full] = new ComparisonAlgorithmInfo
                {
                    Description = "Hash (xxHash64 - Full)",
                    IsEnabled = true,
                    IsDefault = false,
                    ComparisonPercentage = 100
                },
                [ComparisonAlgorithm.xxHash64_Partial] = new ComparisonAlgorithmInfo
                {
                    Description = "Hash (xxHash64 - Partial)",
                    IsEnabled = true,
                    IsDefault = false,
                    ComparisonPercentage = 30 // Can be user-modified later
                },
                [ComparisonAlgorithm.MD5] = new ComparisonAlgorithmInfo
                {
                    Description = "Hash (MD5 - 100%)",
                    IsEnabled = false,
                    IsDefault = false,
                    ComparisonPercentage = 100
                },
                [ComparisonAlgorithm.FullContents] = new ComparisonAlgorithmInfo
                {
                    Description = "Full Contents (100%)",
                    IsEnabled = true,
                    IsDefault = false,
                    ComparisonPercentage = 100
                }
            };
    }
}