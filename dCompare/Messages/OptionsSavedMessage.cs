namespace dCompare.Messages
{
    /// <summary>
    /// Sent after Settings | Save has written dCompare.json. The open compare tabs reload their algorithm list and
    /// scan settings from disk on it. They used to reload on every tab switch instead, which silently reset the
    /// algorithm the user had just picked and re-read the config file for nothing.
    /// </summary>
    public class OptionsSavedMessage
    {
    }
}
