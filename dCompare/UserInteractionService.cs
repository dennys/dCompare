using System.Windows;
using Microsoft.VisualBasic;

namespace dCompare
{
    public class UserInteractionService : IUserInteractionService
    {
        public void ShowError(string message, string title)
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        public void ShowInformation(string message, string title)
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public bool ShowConfirmation(string message, string title)
        {
            var result = MessageBox.Show(message, title, MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            return result == MessageBoxResult.Yes;
        }

        public string PromptForTabName(string currentName, string prompt, string title)
        {
            return Interaction.InputBox(prompt, title, currentName);
        }
    }
}