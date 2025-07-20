using dCompare.Configuration;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Search;
using Microsoft.Win32;
using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace dCompare.Tools.Viewer
{
    public partial class TextViewer : UserControl, ITool
    {
        private readonly IConfigurationService _configurationService;
        private readonly SearchPanel? searchPanel;
        private string? currentFilePath;

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
        }

        private async void ApplySettings()
        {
            var config = await _configurationService.LoadConfigurationAsync();
            var settings = config.Options.TextViewer;

            IsReadOnly = settings.ReadOnly;
            textEditor.ShowLineNumbers = settings.LineNumber;
            textEditor.WordWrap = settings.WordWrap;
            textEditor.FontFamily = new System.Windows.Media.FontFamily(settings.Font.FontFamily);
            textEditor.FontSize = settings.Font.FontSize;

            if (settings.SyntaxHighlight)
            {
                LoadSyntaxHighlightingComboBox(syntaxHighlightComboBox);
            }

            if (Enum.TryParse<ScrollBarVisibility>(settings.HorizontalScrollBarVisibility, out var horizontalVisibility))
            {
                textEditor.HorizontalScrollBarVisibility = horizontalVisibility;
            }

            if (Enum.TryParse<ScrollBarVisibility>(settings.VerticalScrollBarVisibility, out var verticalVisibility))
            {
                textEditor.VerticalScrollBarVisibility = verticalVisibility;
            }

            statusBar.Visibility = settings.ShowStatusBar ? Visibility.Visible : Visibility.Collapsed;

            UpdateReadOnlyButton();
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
            }
        }

        public async void LoadFile(string filePath)
        {
            textEditor.Load(filePath);
            currentFilePath = filePath;
            var config = await _configurationService.LoadConfigurationAsync();
            if (config.Options.TextViewer.SyntaxHighlight)
            {
                textEditor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinitionByExtension(Path.GetExtension(filePath));
            }
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
            UpdateReadOnlyButton();
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
            var findReplaceDialog = new FindReplaceDialog();
            //findReplaceDialog.Owner = this;

            if (findReplaceDialog.ShowDialog() == true)
            {
                PerformReplace(findReplaceDialog.FindText, findReplaceDialog.ReplaceText,
                              findReplaceDialog.MatchCase, findReplaceDialog.ReplaceFirst,
                              findReplaceDialog.ReplaceInSelection);
            }
        }
        private void PerformReplace(string find, string replace, bool caseSensitive, bool replaceFirst, bool replaceSelection)
        {
            if (string.IsNullOrEmpty(find)) return;

            StringComparison comparison = caseSensitive ?
                StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

            try
            {
                if (replaceSelection && textEditor.SelectionLength > 0)
                {
                    string selectedText = textEditor.SelectedText;
                    string replacedText = replaceFirst ?
                        selectedText.ReplaceFirst(find, replace, comparison) :
                        selectedText.Replace(find, replace, comparison);
                    textEditor.Document.Replace(textEditor.SelectionStart, textEditor.SelectionLength, replacedText);
                }
                else if (replaceFirst)
                {
                    int index = textEditor.Text.IndexOf(find, comparison);
                    if (index >= 0)
                    {
                        textEditor.Document.Replace(index, find.Length, replace);
                    }
                }
                else
                {
                    textEditor.Text = textEditor.Text.Replace(find, replace, comparison);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during replace operation: {ex.Message}", "Replace Error",
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
        private void TextEditor_TextChanged(object? sender, EventArgs e)
        {
            UpdateWordCount();
        }
        private static readonly char[] WordSplitCharacters = new[] { ' ', '\n', '\r' };

        private void UpdateWordCount()
        {
            string text = textEditor.Text;
            int wordCount = string.IsNullOrWhiteSpace(text) ? 0 : text.Split(WordSplitCharacters, StringSplitOptions.RemoveEmptyEntries).Length;
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
    public partial class FindReplaceDialog : Window
    {
        public string FindText => findTextBox.Text;
        public string ReplaceText => replaceTextBox.Text;
        public bool MatchCase => matchCaseCheckBox.IsChecked ?? false;
        public bool ReplaceFirst => replaceFirstCheckBox.IsChecked ?? false;
        public bool ReplaceInSelection => replaceSelectionCheckBox.IsChecked ?? false;

        private TextBox findTextBox = null!;
        private TextBox replaceTextBox = null!;
        private CheckBox matchCaseCheckBox = null!;
        private CheckBox replaceFirstCheckBox = null!;
        private CheckBox replaceSelectionCheckBox = null!;

        public FindReplaceDialog()
        {
            //InitializeComponent();
            CreateUI();
            SetupEventHandlers();
        }

        private void CreateUI()
        {
            Title = "Find and Replace";
            Width = 380;
            Height = 280;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;

            var mainGrid = new Grid
            {
                Margin = new Thickness(15)
            };

            // Define rows
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(5) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(5) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(5) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(5) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(15) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Define columns
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Find label and textbox
            var findLabel = new Label { Content = "Find what:", VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(findLabel, 0);
            Grid.SetColumn(findLabel, 0);
            mainGrid.Children.Add(findLabel);

            findTextBox = new TextBox
            {
                VerticalAlignment = VerticalAlignment.Center,
                Height = 23
            };
            Grid.SetRow(findTextBox, 0);
            Grid.SetColumn(findTextBox, 2);
            mainGrid.Children.Add(findTextBox);

            // Replace label and textbox
            var replaceLabel = new Label { Content = "Replace with:", VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(replaceLabel, 2);
            Grid.SetColumn(replaceLabel, 0);
            mainGrid.Children.Add(replaceLabel);

            replaceTextBox = new TextBox
            {
                VerticalAlignment = VerticalAlignment.Center,
                Height = 23
            };
            Grid.SetRow(replaceTextBox, 2);
            Grid.SetColumn(replaceTextBox, 2);
            mainGrid.Children.Add(replaceTextBox);

            // Options
            matchCaseCheckBox = new CheckBox
            {
                Content = "Match case",
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(matchCaseCheckBox, 4);
            Grid.SetColumn(matchCaseCheckBox, 0);
            Grid.SetColumnSpan(matchCaseCheckBox, 3);
            mainGrid.Children.Add(matchCaseCheckBox);

            replaceFirstCheckBox = new CheckBox
            {
                Content = "Replace first occurrence only",
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(replaceFirstCheckBox, 6);
            Grid.SetColumn(replaceFirstCheckBox, 0);
            Grid.SetColumnSpan(replaceFirstCheckBox, 3);
            mainGrid.Children.Add(replaceFirstCheckBox);

            replaceSelectionCheckBox = new CheckBox
            {
                Content = "Replace in selection only",
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(replaceSelectionCheckBox, 8);
            Grid.SetColumn(replaceSelectionCheckBox, 0);
            Grid.SetColumnSpan(replaceSelectionCheckBox, 3);
            mainGrid.Children.Add(replaceSelectionCheckBox);

            // Buttons
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var replaceButton = new Button
            {
                Content = "Replace All",
                Width = 80,
                Height = 23,
                Margin = new Thickness(0, 0, 5, 0),
                IsDefault = true
            };
            replaceButton.Click += ReplaceButton_Click;

            var cancelButton = new Button
            {
                Content = "Cancel",
                Width = 80,
                Height = 23,
                IsCancel = true
            };

            buttonPanel.Children.Add(replaceButton);
            buttonPanel.Children.Add(cancelButton);

            Grid.SetRow(buttonPanel, 10);
            Grid.SetColumn(buttonPanel, 0);
            Grid.SetColumnSpan(buttonPanel, 3);
            mainGrid.Children.Add(buttonPanel);

            Content = mainGrid;
        }

        private void SetupEventHandlers()
        {
            // Focus on find textbox when dialog opens
            Loaded += (s, e) => findTextBox.Focus();

            // Handle Enter key in textboxes
            findTextBox.KeyDown += TextBox_KeyDown;
            replaceTextBox.KeyDown += TextBox_KeyDown;

            // Handle Escape key for the entire dialog
            KeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    DialogResult = false;
                    Close();
                }
            };
        }

        private void TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ReplaceButton_Click(sender, e);
                e.Handled = true;
            }
        }

        private void ReplaceButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(FindText))
            {
                MessageBox.Show("Please enter text to find.", "Find and Replace",
                               MessageBoxButton.OK, MessageBoxImage.Information);
                findTextBox.Focus();
                return;
            }

            DialogResult = true;
            Close();
        }
    }

    // Enhanced String Extensions with better performance
    public static class StringExtensions
    {
        public static string ReplaceFirst(this string text, string search, string replace, StringComparison comparison)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(search))
                return text;

            int pos = text.IndexOf(search, comparison);
            if (pos < 0)
                return text;

            return string.Concat(text.AsSpan(0, pos), replace, text.AsSpan(pos + search.Length));
        }

        public static string Replace(this string text, string search, string replace, StringComparison comparison)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(search))
                return text;

            // Use StringBuilder for better performance with multiple replacements
            var result = new StringBuilder(text);
            int pos = 0;
            int searchLength = search.Length;

            while (pos < result.Length)
            {
                int index = result.ToString().IndexOf(search, pos, comparison);
                if (index < 0)
                    break;

                result.Remove(index, searchLength);
                result.Insert(index, replace);
                pos = index + replace.Length;
            }

            return result.ToString();
        }
    }
}