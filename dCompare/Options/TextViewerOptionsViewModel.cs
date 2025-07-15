using dCompare.Configuration;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Input;
using System.Windows.Forms;
using System.Drawing;

namespace dCompare.Options
{
    public class TextViewerOptionsViewModel : ViewModelBase, IConfigurationUpdater
    {
        private readonly TextViewerOptions _model;

        public ICommand SelectFontCommand { get; }

        public TextViewerOptionsViewModel(TextViewerOptions model)
        {
            _model = model;
            SelectFontCommand = new RelayCommand(SelectFont);
        }

        public bool ReadOnly
        {
            get => _model.ReadOnly;
            set => _model.ReadOnly = value;
        }

        public bool LineNumber
        {
            get => _model.LineNumber;
            set => _model.LineNumber = value;
        }

        public bool SyntaxHighlight
        {
            get => _model.SyntaxHighlight;
            set => _model.SyntaxHighlight = value;
        }

        public string FontFamily
        {
            get => _model.Font.FontFamily;
            set
            {
                _model.Font.FontFamily = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FontDisplay));
            }
        }

        public double FontSize
        {
            get => _model.Font.FontSize;
            set
            {
                _model.Font.FontSize = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FontDisplay));
            }
        }

        public string FontDisplay => $"{FontFamily}, {FontSize}pt";

        public bool WordWrap
        {
            get => _model.WordWrap;
            set => _model.WordWrap = value;
        }

        public List<string> ScrollBarVisibilities { get; } = new List<string> { "Auto", "Visible", "Hidden" };

        public string HorizontalScrollBarVisibility
        {
            get => _model.HorizontalScrollBarVisibility;
            set => _model.HorizontalScrollBarVisibility = value;
        }

        public string VerticalScrollBarVisibility
        {
            get => _model.VerticalScrollBarVisibility;
            set => _model.VerticalScrollBarVisibility = value;
        }

        private void SelectFont(object obj)
        {
            var fontDialog = new FontDialog();
            fontDialog.Font = new Font(FontFamily, (float)FontSize);

            if (fontDialog.ShowDialog() == DialogResult.OK)
            {
                FontFamily = fontDialog.Font.FontFamily.Name;
                FontSize = fontDialog.Font.Size;
            }
        }

        public void UpdateModel()
        {
            // Model is updated directly via property setters
        }
    }
}
