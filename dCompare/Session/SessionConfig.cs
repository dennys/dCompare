using dCompare.Configuration;
using System.Text.Json.Serialization;

namespace dCompare.Session
{
    public class SessionConfig
    {
        public string Left { get; set; } = string.Empty;
        public string Right { get; set; } = string.Empty;
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ComparisonAlgorithm Algorithm { get; set; } = ComparisonAlgorithm.ModifiedDateAndSize;
    }
}