using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.IO.Enumeration;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace dCompare
{
    public partial class MainWindow : Window
    {
        ObservableCollection<CompareItem> compareResults = new ObservableCollection<CompareItem>();

        public MainWindow()
        {
            InitializeComponent();
            ResultListView.ItemsSource = compareResults;
        }

        // 按鈕：左邊資料夾瀏覽
        private void BrowseLeft_Click(object sender, RoutedEventArgs e)
        {
            //using var dlg = new FolderBrowserDialog();
            //if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            //{
            //    LeftPathBox.Text = dlg.SelectedPath;
            //}
        }

        //// 按鈕：右邊資料夾瀏覽
        private void BrowseRight_Click(object sender, RoutedEventArgs e)
        {
            //using var dlg = new FolderBrowserDialog();
            //if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            //{
            //    RightPathBox.Text = dlg.SelectedPath;
            //}
        }

        // 按鈕：開始比對
        private void Compare_Click(object sender, RoutedEventArgs e)
        {
            compareResults.Clear();

            string leftDir = LeftPathBox.Text.Trim();
            string rightDir = RightPathBox.Text.Trim();

            if (!Directory.Exists(leftDir) || !Directory.Exists(rightDir))
            {
                System.Windows.MessageBox.Show("請輸入有效的左右資料夾路徑");
                return;
            }

            var leftFiles = ReadFilesRecursively(leftDir);
            var rightFiles = ReadFilesRecursively(rightDir);

            var allKeys = new SortedSet<string>(leftFiles.Keys);
            allKeys.UnionWith(rightFiles.Keys);

            foreach (var relativePath in allKeys)
            {
                leftFiles.TryGetValue(relativePath, out var leftInfo);
                rightFiles.TryGetValue(relativePath, out var rightInfo);

                var item = new CompareItem
                {
                    RelativePath = relativePath,
                    LeftSize = leftInfo?.Length.ToString("N0") ?? "",
                    LeftDate = leftInfo?.LastWriteTime.ToString("yyyy/MM/dd HH:mm:ss") ?? "",
                    RightSize = rightInfo?.Length.ToString("N0") ?? "",
                    RightDate = rightInfo?.LastWriteTime.ToString("yyyy/MM/dd HH:mm:ss") ?? ""
                };

                item.Status = GetStatus(leftInfo, rightInfo);
                item.StatusSymbol = StatusToSymbol(item.Status);
                item.TreeIndent = GetIndentedName(relativePath);

                compareResults.Add(item);
            }
        }

        // 讀取資料夾內所有檔案(包含子目錄)，回傳 relative path => FileInfo
        private Dictionary<string, FileInfo> ReadFilesRecursively(string root)
        {
            var dict = new Dictionary<string, FileInfo>(StringComparer.OrdinalIgnoreCase);
            var rootLen = root.EndsWith(System.IO.Path.DirectorySeparatorChar.ToString()) ? root.Length : root.Length + 1;

            foreach (var filePath in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
            {
                string relativePath = filePath.Substring(rootLen);
                var fi = new FileInfo(filePath);
                dict[relativePath] = fi;
            }
            return dict;
        }

        // 判斷檔案狀態
        private FileStatus GetStatus(FileInfo left, FileInfo right)
        {
            if (left == null && right != null) return FileStatus.OnlyRight;
            if (left != null && right == null) return FileStatus.OnlyLeft;
            if (left != null && right != null)
            {
                if (left.Length == right.Length &&
                    left.LastWriteTime == right.LastWriteTime)
                    return FileStatus.Identical;
                else
                    return FileStatus.Modified;
            }
            return FileStatus.Unknown;
        }

        // 狀態轉符號
        private string StatusToSymbol(FileStatus status)
        {
            return status switch
            {
                FileStatus.Identical => "=",       // 相同
                FileStatus.Modified => "≠",        // 修改
                FileStatus.OnlyLeft => "<",        // 只在左邊
                FileStatus.OnlyRight => ">",       // 只在右邊
                _ => "?"
            };
        }

        // 檔案名稱加縮排(用空白和 |-- 模擬樹狀結構)
        private string GetIndentedName(string relativePath)
        {
            var parts = relativePath.Split(System.IO.Path.DirectorySeparatorChar);
            if (parts.Length == 0) return relativePath;

            string indent = "";
            for (int i = 0; i < parts.Length - 1; i++)
            {
                indent += "   ";
            }

            // 用 ├─ 代替 -- 顯示樹狀結構感覺
            return indent + "├─ " + parts[^1];
        }

        // 按鈕：Copy to Left
        private void CopyToLeft_Click(object sender, RoutedEventArgs e)
        {
            CopySelectedFiles(toLeft: true);
        }

        // 按鈕：Copy to Right
        private void CopyToRight_Click(object sender, RoutedEventArgs e)
        {
            CopySelectedFiles(toLeft: false);
        }

        private void CopySelectedFiles(bool toLeft)
        {
            if (compareResults.Count == 0) return;

            var targetRoot = toLeft ? LeftPathBox.Text.Trim() : RightPathBox.Text.Trim();
            var sourceRoot = toLeft ? RightPathBox.Text.Trim() : LeftPathBox.Text.Trim();

            var selected = ResultListView.SelectedItems;
            if (selected.Count == 0)
            {
                System.Windows.MessageBox.Show("請選擇至少一個項目進行同步");
                return;
            }

            foreach (CompareItem item in selected)
            {
                string sourcePath = System.IO.Path.Combine(sourceRoot, item.RelativePath);
                string targetPath = System.IO.Path.Combine(targetRoot, item.RelativePath);

                try
                {
                    var targetDir = System.IO.Path.GetDirectoryName(targetPath);
                    if (!Directory.Exists(targetDir))
                        Directory.CreateDirectory(targetDir);

                    File.Copy(sourcePath, targetPath, true);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"複製檔案失敗: {item.RelativePath}\n{ex.Message}");
                }
            }
            System.Windows.MessageBox.Show("同步完成");
            Compare_Click(null, null); // 重新比對更新
        }

        // ContextMenu 開啟事件可選擇依需求擴充
        private void ResultListView_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            // 可加入動態判斷是否顯示某些項目
        }
    }

    // 比對結果物件
    public class CompareItem : INotifyPropertyChanged
    {
        public string RelativePath { get; set; }
        public FileStatus Status { get; set; }
        public string StatusSymbol { get; set; }
        public string TreeIndent { get; set; }
        public string LeftSize { get; set; }
        public string LeftDate { get; set; }
        public string RightSize { get; set; }
        public string RightDate { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
    }

    public enum FileStatus
    {
        Unknown,
        Identical,
        Modified,
        OnlyLeft,
        OnlyRight
    }
}