using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using dCompare.Configuration;
using dCompare.Infrastructure;
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Input;

namespace dCompare.Options
{
    public partial class TextViewerOptionsViewModel : ViewModelBase, IConfigurationUpdater
    {
        private readonly TextViewerOptions _model;

        public TextViewerOptionsViewModel(TextViewerOptions model)
        {
            _model = model;

            _readOnly = _model.ReadOnly;
            _lineNumber = _model.LineNumber;
            _syntaxHighlight = _model.SyntaxHighlight;
            _fontFamily = _model.Font.FontFamily;
            _fontSize = _model.Font.FontSize;
            _wordWrap = _model.WordWrap;
            _showStatusBar = _model.ShowStatusBar;
            _horizontalScrollBarVisibility = _model.HorizontalScrollBarVisibility;
            _verticalScrollBarVisibility = _model.VerticalScrollBarVisibility;
        }

        [ObservableProperty]
        private bool _readOnly;

        [ObservableProperty]
        private bool _lineNumber;

        [ObservableProperty]
        private bool _syntaxHighlight;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FontDisplay))]
        private string _fontFamily;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FontDisplay))]
        private int _fontSize;

        [ObservableProperty]
        private bool _wordWrap;

        [ObservableProperty]
        private bool _showStatusBar;

        [ObservableProperty]
        private string _horizontalScrollBarVisibility;

        [ObservableProperty]
        private string _verticalScrollBarVisibility;

        public string FontDisplay => $"{FontFamily}, {FontSize}pt";

        public List<string> ScrollBarVisibilities { get; } = new List<string> { "Auto", "Visible", "Hidden" };

        [RelayCommand]
        private void SelectFont()
        {
            var fontDialog = new FontDialog();
            fontDialog.Font = new Font(FontFamily, (float)FontSize);

            if (fontDialog.ShowDialog() == DialogResult.OK)
            {
                FontFamily = fontDialog.Font.FontFamily.Name;
                FontSize = (int)fontDialog.Font.Size;
            }
        }

        public void UpdateModel()
        {
            _model.ReadOnly = ReadOnly;
            _model.LineNumber = LineNumber;
            _model.SyntaxHighlight = SyntaxHighlight;
            _model.Font.FontFamily = FontFamily;
            _model.Font.FontSize = FontSize;
            _model.WordWrap = WordWrap;
            _model.ShowStatusBar = ShowStatusBar;
            _model.HorizontalScrollBarVisibility = HorizontalScrollBarVisibility;
            _model.VerticalScrollBarVisibility = VerticalScrollBarVisibility;
        }

        [RelayCommand]
        private void ResetToDefault()
        {
            var defaultOptions = new TextViewerOptions();
            ReadOnly = defaultOptions.ReadOnly;
            LineNumber = defaultOptions.LineNumber;
            SyntaxHighlight = defaultOptions.SyntaxHighlight;
            FontFamily = defaultOptions.Font.FontFamily;
            FontSize = defaultOptions.Font.FontSize;
            WordWrap = defaultOptions.WordWrap;
            ShowStatusBar = defaultOptions.ShowStatusBar;
            HorizontalScrollBarVisibility = defaultOptions.HorizontalScrollBarVisibility;
            VerticalScrollBarVisibility = defaultOptions.VerticalScrollBarVisibility;
        }
    }
}
