using System.Collections.Generic;

namespace dCompare.Configuration
{
    public static class ComparisonAlgorithms
    {
        public static readonly List<KeyValuePair<string, string>> AvailableAlgorithms = new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string, string>("Quick (Modified Date and Size)", "ModifiedDateAndSize"),
            new KeyValuePair<string, string>("Hash (xxHash64)", "xxHash64"),
            new KeyValuePair<string, string>("Hash (MD5)", "MD5"),
            new KeyValuePair<string, string>("Full Contents)", "FullContents")
        };
    }
}