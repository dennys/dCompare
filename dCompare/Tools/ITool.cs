using System.Windows.Controls;

namespace dCompare.Tools
{
    public interface ITool
    {
        void LoadFile(string filePath);
        UserControl GetControl();
    }
}
