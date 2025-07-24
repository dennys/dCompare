using dCompare.Configuration;
using dCompare.Infrastructure;
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Input;

namespace dCompare.Options
{
    public class TextViewerOptionsViewModel : ViewModelBase, IConfigurationUpdater
    {
        private readonly TextViewerOptions _model;

        public ICommand SelectFontCommand { get; }
        public ICommand ResetToDefaultCommand { get; }

        public TextViewerOptionsViewModel(TextViewerOptions model)
        {
            _model = model;
            SelectFontCommand = new RelayCommand(SelectFont);
            ResetToDefaultCommand = new RelayCommand(ResetToDefault);

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

        private bool _readOnly;
        private bool _lineNumber;
        private bool _syntaxHighlight;
        private string _fontFamily;
        private int _fontSize;
        private bool _wordWrap;
        private bool _showStatusBar;
        private string _horizontalScrollBarVisibility;
        private string _verticalScrollBarVisibility;

        public bool ReadOnly
        {
            get => _readOnly;
            set => SetProperty(ref _readOnly, value);
        }

        public bool LineNumber
        {
            get => _lineNumber;
            set => SetProperty(ref _lineNumber, value);
        }

        public bool SyntaxHighlight
        {
            get => _syntaxHighlight;
            set => SetProperty(ref _syntaxHighlight, value);
        }

        public string FontFamily
        {
            get => _fontFamily;
            set
            {
                if (SetProperty(ref _fontFamily, value))
                {
                    OnPropertyChanged(nameof(FontDisplay));
                }
            }
        }

        public int FontSize
        {
            get => _fontSize;
            set
            {
                if (SetProperty(ref _fontSize, value))
                {
                    OnPropertyChanged(nameof(FontDisplay));
                }
            }
        }

        public string FontDisplay => $"{FontFamily}, {FontSize}pt";

        public bool WordWrap
        {
            get => _wordWrap;
            set => SetProperty(ref _wordWrap, value);
        }

        public bool ShowStatusBar
        {
            get => _showStatusBar;
            set => SetProperty(ref _showStatusBar, value);
        }

        public List<string> ScrollBarVisibilities { get; } = new List<string> { "Auto", "Visible", "Hidden" };

        public string HorizontalScrollBarVisibility
        {
            get => _horizontalScrollBarVisibility;
            set => SetProperty(ref _horizontalScrollBarVisibility, value);
        }

        public string VerticalScrollBarVisibility
        {
            get => _verticalScrollBarVisibility;
            set => SetProperty(ref _verticalScrollBarVisibility, value);
        }

        private void SelectFont(object obj)
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

        private void ResetToDefault(object obj)
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
