using dCompare.Common;
using System.Windows;

namespace dCompare.FileOperation
{
    public partial class FileOperationWindow : Window
    {
        public FileOperationWindow()
        {
            InitializeComponent();
        }

        public Action<string, bool, string>? OnTransferStatusChanged { get; internal set; }

        internal List<string> CopyMultiple(List<(string Source, string TargetPath, SourceType Type)> items, bool v)
        {
            var jobIds = new List<string>();
            jobIds.Add("QQQQ");
            return jobIds;
        }

        internal List<string> DeleteMultiple(List<(string Path, SourceType Type)> items)
        {
            var jobIds = new List<string>();
            jobIds.Add("QQQQ");
            return jobIds;
        }

        internal List<string> MoveMultiple(List<(string Source, string TargetPath, SourceType Type)> items, bool v)
        {
            var jobIds = new List<string>();
            jobIds.Add("QQQQ");
            return jobIds;
        }
    }
}