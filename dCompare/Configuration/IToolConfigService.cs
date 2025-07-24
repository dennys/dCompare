namespace dCompare.Configuration
{
    public interface IToolConfigService
    {
        IEnumerable<ToolOptions> GetSupportedTools(AppConfiguration config, string operation, string extension);
        FileTypeMappingOptions? GetFileTypeMapping(AppConfiguration config, string extension);
        ToolOptions? GetDefaultTool(AppConfiguration config, string operation, string extension);
        IEnumerable<ToolOptions> GetPreferredTools(AppConfiguration config, string operation, string extension);
        IEnumerable<ToolOptions> GetAllToolsForOperation(AppConfiguration config, string operation);
        IEnumerable<string> GetSupportedExtensions(AppConfiguration config, string? toolName = null, string? operation = null);
    }
}
