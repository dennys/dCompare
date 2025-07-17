using System.Windows.Controls;

namespace dCompare.Tools.Viewer
{
    public interface IViewer
    {
        void LoadFile(string filePath);
        UserControl GetControl();
    }
}
