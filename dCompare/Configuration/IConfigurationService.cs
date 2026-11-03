namespace dCompare.Configuration
{
    /// <summary>
    /// Reads and writes dCompare.json. Tool and extension queries live on <see cref="IToolConfigService"/>,
    /// which takes the already-loaded AppConfiguration: reading the same file twice through two different
    /// readers let the context menus and the default tool disagree about what is configured.
    /// </summary>
    public interface IConfigurationService
    {
        Task<AppConfiguration> LoadConfigurationAsync();
        Task SaveConfigurationAsync(AppConfiguration config);
    }
}
