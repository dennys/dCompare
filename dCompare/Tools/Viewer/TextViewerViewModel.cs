using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using dCompare.Common;
using dCompare.Configuration;
using ICSharpCode.AvalonEdit.Highlighting;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace dCompare.Tools.Viewer
{
    public partial class TextViewerViewModel : ObservableObject
    {
        public event EventHandler<int>? GoToLineRequested;
        private readonly IConfigurationService _configurationService;
        private string? _currentFilePath;
        private bool _isReadOnly;
        private bool _showLineNumbers;
        private bool _wordWrap;
        private string _fontFamily = "Consolas";
        private double _fontSize;
        private bool _syntaxHighlight;
        private ScrollBarVisibility _horizontalScrollBarVisibility;
        private ScrollBarVisibility _verticalScrollBarVisibility;
        private bool _showStatusBar;
        private string _text = string.Empty;

        [ObservableProperty]
        private int _wordCount;

        [ObservableProperty]
        private int _charCount;
        private IHighlightingDefinition? _selectedSyntaxHighlighting;

        public TextViewerViewModel(IConfigurationService configurationService)
        {
            _configurationService = configurationService;
            ApplySettings();
        }

        public string Text { get => _text; set { SetProperty(ref _text, value); UpdateWordCount(); } }
        public bool IsReadOnly { get => _isReadOnly; set => SetProperty(ref _isReadOnly, value); }
        public bool ShowLineNumbers { get => _showLineNumbers; set => SetProperty(ref _showLineNumbers, value); }
        public bool WordWrap { get => _wordWrap; set => SetProperty(ref _wordWrap, value); }
        public string FontFamily { get => _fontFamily; set => SetProperty(ref _fontFamily, value); }
        public double FontSize { get => _fontSize; set => SetProperty(ref _fontSize, value); }
        public bool SyntaxHighlight { get => _syntaxHighlight; set => SetProperty(ref _syntaxHighlight, value); }
        public ScrollBarVisibility HorizontalScrollBarVisibility { get => _horizontalScrollBarVisibility; set => SetProperty(ref _horizontalScrollBarVisibility, value); }
        public ScrollBarVisibility VerticalScrollBarVisibility { get => _verticalScrollBarVisibility; set => SetProperty(ref _verticalScrollBarVisibility, value); }
        public bool ShowStatusBar { get => _showStatusBar; set => SetProperty(ref _showStatusBar, value); }
        public IHighlightingDefinition? SelectedSyntaxHighlighting { get => _selectedSyntaxHighlighting; set => SetProperty(ref _selectedSyntaxHighlighting, value); }

        public ObservableCollection<IHighlightingDefinition> HighlightingDefinitions { get; } = new ObservableCollection<IHighlightingDefinition>(HighlightingManager.Instance.HighlightingDefinitions);

        private async void ApplySettings()
        {
            TextViewerOptions settings = (await _configurationService.LoadConfigurationAsync()).Options.TextViewer;

            IsReadOnly = settings.ReadOnly;
            ShowLineNumbers = settings.LineNumber;
            WordWrap = settings.WordWrap;
            FontFamily = settings.Font.FontFamily;
            FontSize = settings.Font.FontSize;
            SyntaxHighlight = settings.SyntaxHighlight;
            HorizontalScrollBarVisibility = settings.HorizontalScrollBarVisibility;
            VerticalScrollBarVisibility = settings.VerticalScrollBarVisibility;
            ShowStatusBar = settings.ShowStatusBar;
        }

        public async void LoadFile(string filePath)
        {
            if (FileExtensions.IsTooLargeToView(filePath, out long length))
            {
                MessageBox.Show(string.Format(Properties.Resources.ErrorFileTooLarge, filePath, length / (1024 * 1024), FileExtensions.MaxViewableFileBytes / (1024 * 1024)), Properties.Resources.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Text = File.ReadAllText(filePath);
            _currentFilePath = filePath;
            var config = await _configurationService.LoadConfigurationAsync();
            if (config.Options.TextViewer.SyntaxHighlight)
            {
                SelectedSyntaxHighlighting = HighlightingManager.Instance.GetDefinitionByExtension(Path.GetExtension(filePath));
            }
            UpdateWordCount();
        }

        public void SaveFile(string? path = null)
        {
            if (path != null)
                _currentFilePath = path;

            if (_currentFilePath == null)
                throw new InvalidOperationException(Properties.Resources.ErrorNoFilePath);

            File.WriteAllText(_currentFilePath, Text);
        }

        [RelayCommand]
        private void OpenFile()
        {
            var dlg = new OpenFileDialog();
            if (dlg.ShowDialog() == true)
            {
                LoadFile(dlg.FileName);
            }
        }

        [RelayCommand]
        private void SaveFile()
        {
            if (_currentFilePath == null)
            {
                var dlg = new SaveFileDialog();
                if (dlg.ShowDialog() == true)
                {
                    _currentFilePath = dlg.FileName;
                }
                else
                {
                    return;
                }
            }

            SaveFile(_currentFilePath);
            MessageBox.Show(string.Format(Properties.Resources.SavedTo, _currentFilePath), Properties.Resources.Save, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        [RelayCommand]
        private void ToggleReadOnly()
        {
            IsReadOnly = !IsReadOnly;
        }

        [RelayCommand]
        private void GoToLine(string? lineText)
        {
            if (int.TryParse(lineText, out int line))
            {
                GoToLineRequested?.Invoke(this, line);
            }
        }

        [RelayCommand]
        private void ZoomIn()
        {
            FontSize = Math.Min(FontSize + 2, 48);
        }

        [RelayCommand]
        private void ZoomOut()
        {
            FontSize = Math.Max(FontSize - 2, 8);
        }

        [RelayCommand]
        private void ToggleLineNumbers()
        {
            ShowLineNumbers = !ShowLineNumbers;
        }

        private void UpdateWordCount()
        {
            WordCount = string.IsNullOrWhiteSpace(Text) ? 0 : Text.Split(new[] { ' ', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;
            CharCount = Text.Length;
        }
    }
}
