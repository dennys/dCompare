using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using System.Windows.Controls;

namespace dCompare.FileOperation
{
    public partial class FileOperationControl : UserControl
    {
        public FileOperationControl()
        {
            InitializeComponent();
            // The same IFileManager singleton the compare view transfers through, so Pause/Cancel reach
            // those transfers instead of a manager instance only this panel knows about.
            DataContext = new FileOperationControlViewModel(Ioc.Default.GetRequiredService<IFileManager>());
        }
    }
}
