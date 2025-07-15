using dCompare.Viewer;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Search;
using Microsoft.Win32;
using System;
using dCompare.Configuration;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace dCompare.Viewer
{
    public partial class TextViewer : UserControl, IViewer
    {
        private string? currentFilePath;
        private SearchPanel? searchPanel;
        private Window? replaceDialog;
        private readonly IConfigurationService _configurationService;

        public TextViewer(IConfigurationService configurationService)
        {
            InitializeComponent();
            _configurationService = configurationService;
            textEditor.TextArea.DefaultInputHandler.NestedInputHandlers.Add(new TextAreaInputHandler(textEditor.TextArea));
            textEditor.PreviewKeyDown += TextEditor_PreviewKeyDown;
            textEditor.TextChanged += TextEditor_TextChanged;
            searchPanel = SearchPanel.Install(textEditor);
            LoadSyntaxHighlightingComboBox(syntaxHighlightComboBox);
            ApplySettings();
            UpdateReadOnlyButton();
        }

        private async void ApplySettings()
        {
            var config = await _configurationService.LoadConfigurationAsync();
            var settings = config.Options.TextViewer;

            textEditor.IsReadOnly = settings.ReadOnly;
            textEditor.ShowLineNumbers = settings.LineNumber;
            textEditor.WordWrap = settings.WordWrap;
            textEditor.FontFamily = new System.Windows.Media.FontFamily(settings.Font.FontFamily);
            textEditor.FontSize = settings.Font.FontSize;

            if (Enum.TryParse<ScrollBarVisibility>(settings.HorizontalScrollBarVisibility, out var horizontalVisibility))
            {
                textEditor.HorizontalScrollBarVisibility = horizontalVisibility;
            }

            if (Enum.TryParse<ScrollBarVisibility>(settings.VerticalScrollBarVisibility, out var verticalVisibility))
            {
                textEditor.VerticalScrollBarVisibility = verticalVisibility;
            }
        }

        // ✅ Public API
        public string Text
        {
            get => textEditor.Text;
            set => textEditor.Text = value;
        }

        public bool IsReadOnly
        {
            get => textEditor.IsReadOnly;
            set
            {
                textEditor.IsReadOnly = value;
                UpdateReadOnlyButton();
            }
        }

        public void LoadFile(string filePath)
        {
            textEditor.Load(filePath);
            currentFilePath = filePath;
            textEditor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinitionByExtension(Path.GetExtension(filePath));
            UpdateWordCount();
        }

        public void SaveFile(string? path = null)
        {
            if (path != null)
                currentFilePath = path;

            if (currentFilePath == null)
                throw new InvalidOperationException("No file path specified.");

            File.WriteAllText(currentFilePath, textEditor.Text);
        }

        public void HighlightLine(int lineNumber)
        {
            if (lineNumber < 1 || lineNumber > textEditor.Document.LineCount)
                return;

            var line = textEditor.Document.GetLineByNumber(lineNumber);
            textEditor.Select(line.Offset, line.Length);
            textEditor.ScrollToLine(lineNumber);
            textEditor.Focus();
        }

        // ✅ Toolbar handlers
        private void Open_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog();
            if (dlg.ShowDialog() == true)
            {
                LoadFile(dlg.FileName);
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (currentFilePath == null)
            {
                var dlg = new SaveFileDialog();
                if (dlg.ShowDialog() == true)
                {
                    currentFilePath = dlg.FileName;
                }
                else
                {
                    return;
                }
            }

            SaveFile();
            MessageBox.Show($"Saved to {currentFilePath}", "Save", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ToggleReadOnly_Click(object sender, RoutedEventArgs e)
        {
            IsReadOnly = !IsReadOnly;
        }

        private void UpdateReadOnlyButton()
        {
            toggleReadOnlyButton.Content = textEditor.IsReadOnly ? "🔒 Read Only" : "✏️ Edit";
        }

        private void Search_Click(object sender, RoutedEventArgs e)
        {
            searchPanel?.Open();
        }

        private void Replace_Click(object sender, RoutedEventArgs e)
        {
            replaceDialog = new Window
            {
                Title = "Find and Replace",
                Width = 300,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = new StackPanel
                {
                    Margin = new Thickness(10),
                    Children =
                    {
                        new TextBox { Name = "findText", Margin = new Thickness(5) },
                        new TextBox { Name = "replaceText", Margin = new Thickness(5) },
                        new Button { Content = "Replace All", Margin = new Thickness(5) }
                    }
                }
            };
            var replaceButton = (replaceDialog.Content as StackPanel).Children[2] as Button;
            replaceButton.Click += (s, args) =>
            {
                string find = ((replaceDialog.Content as StackPanel).Children[0] as TextBox).Text;
                string replace = ((replaceDialog.Content as StackPanel).Children[1] as TextBox).Text;
                textEditor.Text = textEditor.Text.Replace(find, replace);
                replaceDialog.Close();
            };
            replaceDialog.KeyDown += (s, args) =>
            {
                if (args.Key == Key.Escape)
                {
                    replaceDialog.Close();
                }
            };
            replaceDialog.ShowDialog();
        }

        private void GoToLine_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(LineInputBox.Text, out int line))
                HighlightLine(line);
        }

        private void Undo_Click(object sender, RoutedEventArgs e)
        {
            if (textEditor.Document.UndoStack.CanUndo)
                textEditor.Document.UndoStack.Undo();
        }

        private void Redo_Click(object sender, RoutedEventArgs e)
        {
            if (textEditor.Document.UndoStack.CanRedo)
                textEditor.Document.UndoStack.Redo();
        }

        private void ZoomIn_Click(object sender, RoutedEventArgs e)
        {
            textEditor.FontSize = Math.Min(textEditor.FontSize + 2, 48);
        }

        private void ZoomOut_Click(object sender, RoutedEventArgs e)
        {
            textEditor.FontSize = Math.Max(textEditor.FontSize - 2, 8);
        }

        private void ToggleLineNumbers_Click(object sender, RoutedEventArgs e)
        {
            textEditor.ShowLineNumbers = !textEditor.ShowLineNumbers;
        }

        private void LoadSyntaxHighlightingComboBox(ComboBox comboBox)
        {
            comboBox.Items.Add("None");
            foreach (var definition in HighlightingManager.Instance.HighlightingDefinitions)
            {
                comboBox.Items.Add(definition.Name);
            }
            comboBox.SelectedIndex = 0;
            comboBox.SelectionChanged += (s, e) =>
            {
                if (comboBox.SelectedItem is string selected)
                {
                    textEditor.SyntaxHighlighting = selected == "None"
                        ? null
                        : HighlightingManager.Instance.GetDefinition(selected);
                }
            };
        }

        private void TextEditor_TextChanged(object sender, EventArgs e)
        {
            UpdateWordCount();
        }

        private void UpdateWordCount()
        {
            string text = textEditor.Text;
            int wordCount = string.IsNullOrWhiteSpace(text) ? 0 : text.Split(new[] { ' ', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;
            WordCountLabel.Content = $"Words: {wordCount}, Chars: {text.Length}";
        }

        private void TextEditor_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.F)
            {
                searchPanel?.Open();
                e.Handled = true;
            }
            else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.H)
            {
                Replace_Click(sender, e);
                e.Handled = true;
            }
        }

        private void TextEditor_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            var menu = new ContextMenu();

            menu.Items.Add(new MenuItem { Header = "Copy", Command = ApplicationCommands.Copy });
            menu.Items.Add(new MenuItem { Header = "Paste", Command = ApplicationCommands.Paste });
            menu.Items.Add(new MenuItem { Header = "Cut", Command = ApplicationCommands.Cut });
            menu.Items.Add(new MenuItem { Header = "Select All", Command = ApplicationCommands.SelectAll });
            menu.Items.Add(new MenuItem { Header = "Undo", Command = ApplicationCommands.Undo });
            menu.Items.Add(new MenuItem { Header = "Redo", Command = ApplicationCommands.Redo });

            menu.Items.Add(new Separator());

            var goToLine = new MenuItem { Header = "Go to Line..." };
            goToLine.Click += (_, __) =>
            {
                var input = Microsoft.VisualBasic.Interaction.InputBox("Enter line number:", "Go to Line", "1");
                if (int.TryParse(input, out int line))
                {
                    HighlightLine(line);
                }
            };
            menu.Items.Add(goToLine);

            var toggleLineNumbers = new MenuItem { Header = "Toggle Line Numbers" };
            toggleLineNumbers.Click += ToggleLineNumbers_Click;
            menu.Items.Add(toggleLineNumbers);

            textEditor.ContextMenu = menu;
        }

        public UserControl GetControl()
        {
            return this;
        }
    }
}