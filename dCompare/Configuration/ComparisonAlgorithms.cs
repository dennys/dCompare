using System.Collections.Generic;

namespace dCompare.Configuration
{
    public enum ComparisonAlgorithm
    {
        ModifiedDateAndSize,
        xxHash64,
        MD5,
        FullContents
    }

    public static class ComparisonAlgorithms
    {
        public static readonly List<KeyValuePair<string, ComparisonAlgorithm>> AvailableAlgorithms =
        [
            new KeyValuePair<string, ComparisonAlgorithm>("Quick (Modified Date and Size)", ComparisonAlgorithm.ModifiedDateAndSize),
            new KeyValuePair<string, ComparisonAlgorithm>("Hash (xxHash64)", ComparisonAlgorithm.xxHash64),
            new KeyValuePair<string, ComparisonAlgorithm>("Hash (MD5)", ComparisonAlgorithm.MD5),
            new KeyValuePair<string, ComparisonAlgorithm>("Full Contents)", ComparisonAlgorithm.FullContents)
        ];
    }
}