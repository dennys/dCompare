using System.Windows.Controls;

namespace dCompare.FileOperation
{
    public partial class FileOperationControl : UserControl
    {
        public FileOperationControl()
        {
            InitializeComponent();
            DataContext = new FileOperationControlViewModel();
        }
    }
}