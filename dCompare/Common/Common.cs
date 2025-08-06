namespace dCompare.Common
{
    public static class Constant
    {
        public const string DefaultConfigurationFile = "dCompare.json"; // default configuration json file
        public const string DefaultLanguageCode = "en"; // default language code
        public const string FileDateTimeFormat = "yyyy/MM/dd HH:mm";
    }
    public enum FileOperationTypes
    {
        Copy,
        Move,
        Delete
    }
}
