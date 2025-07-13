using System.Windows.Controls;

namespace dCompare.Viewer
{
    public interface IViewer
    {
        void LoadFile(string filePath);
        UserControl GetControl();
    }
}
