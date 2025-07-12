namespace dCompare
{
    public interface IUserInteractionService
    {
        void ShowError(string message, string title);
        void ShowInformation(string message, string title);
        bool ShowConfirmation(string message, string title);
        string PromptForTabName(string currentName, string prompt, string title);
    }
}