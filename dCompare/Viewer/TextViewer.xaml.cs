using System.IO;
using System.Windows.Controls;

namespace dCompare.Viewer
{
    public partial class TextViewer : UserControl, IViewer
    {
        public TextViewer()
        {
            InitializeComponent();
        }

        public void LoadFile(string filePath)
        {
            textEditor.Load(filePath);
        }

        public UserControl GetControl()
        {
            return this;
        }
    }
}
