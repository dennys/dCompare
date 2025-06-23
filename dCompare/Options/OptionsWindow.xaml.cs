using dCompare.Configuration;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.VisualBasic; // For InputBox

namespace dCompare.Options
{
    public partial class OptionsWindow : Window
    {
        public OptionsWindow()
        {
            InitializeComponent();
            // Check if we are in design mode to avoid runtime errors in the designer
            if (!DesignerProperties.GetIsInDesignMode(this))
            {
                this.DataContext = new SettingsViewModel(new ConfigurationService());
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        // Placeholder text logic for the filter box
        private void TextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            TextBox tb = (TextBox)sender;
            if (tb.Text == (string)tb.Tag)
            {
                tb.Text = string.Empty;
                tb.Foreground = System.Windows.Media.Brushes.Black;
            }
        }

        private void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            TextBox tb = (TextBox)sender;
            if (string.IsNullOrWhiteSpace(tb.Text))
            {
                tb.Text = (string)tb.Tag;
                tb.Foreground = System.Windows.Media.Brushes.Gray;
            }
        }
    }

    // ================== Base Classes for MVVM ==================

    public class ViewModelBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }

    public class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;
        private readonly Predicate<object> _canExecute;
        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
        public RelayCommand(Action<object> execute, Predicate<object> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }
        public bool CanExecute(object parameter) => _canExecute == null || _canExecute(parameter);
        public void Execute(object parameter) => _execute(parameter);
    }

    // ================== Main ViewModel ==================

    public class SettingsViewModel : ViewModelBase
    {
        private readonly ConfigurationService _configService;
        private AppConfiguration _appConfig;
        private string _filterText = "Filter settings...";
        private ViewModelBase _selectedViewModel;
        private SettingsMenuItem _selectedMenuItem;

        public ObservableCollection<SettingsMenuItem> MenuItems { get; } = new();
        public ICommand SaveCommand { get; }

        public SettingsViewModel(ConfigurationService configService)
        {
            _configService = configService;
            SaveCommand = new RelayCommand(async _ => await SaveConfiguration());
            // Use async void carefully. It's acceptable for top-level event handlers.
            LoadConfigurationAsync();
        }

        // DESIGNER FIX: Parameterless constructor for the designer
        public SettingsViewModel()
        {
            _filterText = "Filter...";
            // Create dummy data for the designer
            _appConfig = new AppConfiguration();
            BuildMenu();
        }

        private async void LoadConfigurationAsync()
        {
            _appConfig = await _configService.LoadConfigurationAsync();
            BuildMenu();
        }

        private async Task SaveConfiguration()
        {
            // Before saving, ensure all child VMs have pushed their changes to the model
            foreach (var menuItem in MenuItems.SelectMany(m => m.Children).Where(c => c.ViewModel != null))
            {
                if (menuItem.ViewModel is IConfigurationUpdater updater)
                {
                    updater.UpdateModel();
                }
            }
            await _configService.SaveConfigurationAsync(_appConfig);
            MessageBox.Show("Settings saved successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        private void BuildMenu()
        {
            MenuItems.Clear();
            var optionsNode = new SettingsMenuItem("Options", null, this);

            var uiNode = new SettingsMenuItem("UI", new UIOptionsViewModel(_appConfig.Options.UI), this);
            var compareNode = new SettingsMenuItem("Compare", new CompareOptionsViewModel(_appConfig.Options.Compare), this);

            var diffNode = new SettingsMenuItem("Diff", null, this);
            var textDiffNode = new SettingsMenuItem("Text", new TextDiffOptionsViewModel(_appConfig.Options.Diff.Text), this);
            var imageDiffNode = new SettingsMenuItem("Image", new ImageDiffOptionsViewModel(_appConfig.Options.Diff.Image), this);
            diffNode.Children.Add(textDiffNode);
            diffNode.Children.Add(imageDiffNode);

            var sessionNode = new SettingsMenuItem("Session", new SessionOptionsViewModel(_appConfig.Options.Session), this);
            var hotkeysNode = new SettingsMenuItem("HotKeys", new HotKeysOptionsViewModel(_appConfig.Options.HotKeys), this);
            var logNode = new SettingsMenuItem("Log", new LogOptionsViewModel(_appConfig.Options.Log), this);

            optionsNode.Children.Add(uiNode);
            optionsNode.Children.Add(compareNode);
            optionsNode.Children.Add(diffNode);
            optionsNode.Children.Add(sessionNode);
            optionsNode.Children.Add(hotkeysNode);
            optionsNode.Children.Add(logNode);

            MenuItems.Add(optionsNode);

            // Select the first item by default
            optionsNode.IsExpanded = true;
            if (uiNode != null) uiNode.IsSelected = true;
        }
        public string FilterText
        {
            get => _filterText;
            set
            {
                // Check if the value is actually different to avoid cycles
                if (SetProperty(ref _filterText, value))
                {
                    ApplyFilter();
                }
            }
        }

        private void ApplyFilter()
        {
            string effectiveFilter = (string.IsNullOrWhiteSpace(_filterText) || _filterText == "Filter settings...") ? "" : _filterText;

            foreach (var node in MenuItems)
            {
                FilterNode(node, effectiveFilter);
            }
        }

        private bool FilterNode(SettingsMenuItem node, string filter)
        {
            bool anyChildVisible = node.Children.Any()
                ? node.Children.Select(child => FilterNode(child, filter)).Any(isVis => isVis)
                : false;

            if (string.IsNullOrEmpty(filter))
            {
                node.IsVisible = true;
                return true;
            }

            if (node.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) || anyChildVisible)
            {
                node.IsVisible = true;
                if (anyChildVisible) node.IsExpanded = true;
                return true;
            }

            node.IsVisible = false;
            return false;
        }

        public ViewModelBase SelectedViewModel
        {
            get => _selectedViewModel;
            set => SetProperty(ref _selectedViewModel, value);
        }
    }

    // ================== TreeView Menu Item ViewModel ==================

    public class SettingsMenuItem : ViewModelBase
    {
        private bool _isSelected;
        private bool _isExpanded;
        private bool _isVisible = true;
        private readonly SettingsViewModel _mainViewModel;

        public string Name { get; }
        public ViewModelBase ViewModel { get; }
        public ObservableCollection<SettingsMenuItem> Children { get; } = new();

        public SettingsMenuItem(string name, ViewModelBase viewModel, SettingsViewModel mainViewModel)
        {
            Name = name;
            ViewModel = viewModel;
            _mainViewModel = mainViewModel;
        }
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (SetProperty(ref _isSelected, value) && _isSelected && ViewModel != null)
                {
                    _mainViewModel.SelectedViewModel = ViewModel;
                }
            }
        }

        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }

        public bool IsVisible
        {
            get => _isVisible;
            set => SetProperty(ref _isVisible, value);
        }
    }

    // ================== Individual Settings ViewModels ==================

    // Interface to ensure ViewModels can update the master config object
    public interface IConfigurationUpdater
    {
        void UpdateModel();
    }
    public class UIOptionsViewModel : ViewModelBase
    {
        private readonly UIOptions _model;
        public UIOptionsViewModel(UIOptions model) { _model = model; }
        public UIOptionsViewModel() : this(new UIOptions()) { } // For designer
        public List<string> AvailableThemes => new List<string> { "Light", "Dark" };
        public List<KeyValuePair<string, string>> AvailableLanguages => new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string, string>("English (United States)", "en_US"),
            new KeyValuePair<string, string>("繁體中文", "zh_TW"),
            new KeyValuePair<string, string>("简体中文", "zh_CN")
        };

        public string Theme { get => _model.Theme; set { _model.Theme = value; OnPropertyChanged(); } }
        public string Language { get => _model.Language; set { _model.Language = value; OnPropertyChanged(); } }
    }

    public class CompareOptionsViewModel : ViewModelBase
    {
        private readonly CompareOptions _model;
        public CompareOptionsViewModel(CompareOptions model) { _model = model; }

        // Reference the static AvailableAlgorithms
        public List<KeyValuePair<string, ComparisonAlgorithm>> AvailableAlgorithms => ComparisonAlgorithms.AvailableAlgorithms;
        public List<string> AvailableDoubleClickActions => new List<string> { "view", "edit", "open" };

        public ComparisonAlgorithm Algorithm { get => _model.Algorithm; set { _model.Algorithm = value; OnPropertyChanged(); } }
        public string DoubleClickAction { get => _model.DoubleClick; set { _model.DoubleClick = value; OnPropertyChanged(); } }
    }

    public abstract class ListEditorViewModelBase<TModel, TItem> : ViewModelBase where TModel : class
    {
        protected readonly TModel _model;
        private readonly Action<TModel, List<string>> _updateModelAction;
        private string _selectedExtension;

        public ObservableCollection<string> Extensions { get; set; }
        public ICommand AddExtensionCommand { get; }
        public ICommand RemoveExtensionCommand { get; }

        protected ListEditorViewModelBase(TModel model, List<string> extensions, Action<TModel, List<string>> updateModelAction)
        {
            _model = model;
            _updateModelAction = updateModelAction;
            Extensions = new ObservableCollection<string>(extensions);
            Extensions.CollectionChanged += (s, e) => _updateModelAction(_model, Extensions.ToList());

            AddExtensionCommand = new RelayCommand(AddExtension);
            RemoveExtensionCommand = new RelayCommand(RemoveExtension, _ => !string.IsNullOrEmpty(SelectedExtension));
        }

        public string SelectedExtension { get => _selectedExtension; set => SetProperty(ref _selectedExtension, value); }
        private void AddExtension(object obj)
        {
            string newExt = Interaction.InputBox("Enter new file extension (e.g., .txt):", "Add Extension", "");
            if (!string.IsNullOrWhiteSpace(newExt) && newExt.StartsWith(".") && !Extensions.Contains(newExt, StringComparer.OrdinalIgnoreCase))
            {
                Extensions.Add(newExt);
            }
            else if (!string.IsNullOrWhiteSpace(newExt) && !newExt.StartsWith("."))
            {
                MessageBox.Show("Extension must start with a dot (e.g., .txt).", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        private void RemoveExtension(object obj) { if (!string.IsNullOrEmpty(SelectedExtension)) Extensions.Remove(SelectedExtension); }
    }

    public class TextDiffOptionsViewModel : ListEditorViewModelBase<TextDiffOptions, string>
    {
        private readonly TextDiffOptions _textModel;
        public TextDiffOptionsViewModel(TextDiffOptions model) : base(model, model.Ext, (m, l) => m.Ext = l) { _textModel = model; }
        public List<string> AvailableViewers => new List<string> { "DiffPlexQ", "VS Code", "Beyond Compare" };
        public string Viewer { get => _textModel.Viewer; set { _textModel.Viewer = value; OnPropertyChanged(); } }
    }

    public class ImageDiffOptionsViewModel : ListEditorViewModelBase<ImageDiffOptions, string>
    {
        private readonly ImageDiffOptions _imageModel;
        public ImageDiffOptionsViewModel(ImageDiffOptions model) : base(model, model.Ext, (m, l) => m.Ext = l) { _imageModel = model; }
        public List<string> AvailableViewers => new List<string> { "Not implement", "IrfanView", "Paint.NET" };
        public string Viewer { get => _imageModel.Viewer; set { _imageModel.Viewer = value; OnPropertyChanged(); } }
    }

    public class SessionOptionsViewModel : ViewModelBase
    {
        private readonly SessionOptions _model;
        public SessionOptionsViewModel(SessionOptions model)
        {
            _model = model;
            BrowseSessionFileCommand = new RelayCommand(BrowseSessionFile);
        }

        public string DefaultSessionFile
        {
            get => _model.DefaultSessionFile;
            set
            {
                _model.DefaultSessionFile = value;
                OnPropertyChanged();
            }
        }

        public bool AutoLoadDefaultSession
        {
            get => _model.AutoLoadDefaultSession;
            set
            {
                _model.AutoLoadDefaultSession = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(AutoCompareAfterSessionLoad)); // Notify dependent property
            }
        }

        public bool AutoCompareAfterSessionLoad
        {
            get => _model.AutoCompareAfterSessionLoad;
            set
            {
                _model.AutoCompareAfterSessionLoad = value;
                OnPropertyChanged();
            }
        }

        public ICommand BrowseSessionFileCommand { get; }

        private void BrowseSessionFile(object parameter)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Session Files (*.session)|*.session|All Files (*.*)|*.*",
                Title = "Select Default Session File"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                DefaultSessionFile = openFileDialog.FileName;
            }
        }
    }

    public class HotKeyItemViewModel : ViewModelBase
    {
        private string _combination;
        public string Name { get; } // The name (e.g., "Add Tab") doesn't change
        public string Combination { get => _combination; set => SetProperty(ref _combination, value); }
        public HotKeyItemViewModel(string name, string combination)
        {
            Name = name;
            _combination = combination;
        }
    }

    public class HotKeysOptionsViewModel : ViewModelBase, IConfigurationUpdater
    {
        private readonly HotKeysOptions _model;
        public ObservableCollection<HotKeyItemViewModel> HotKeyItems { get; } = new();

        public HotKeysOptionsViewModel(HotKeysOptions model)
        {
            _model = model;
            // Use the new mutable ViewModel
            HotKeyItems.Add(new HotKeyItemViewModel("Add Tab", _model.AddTab));
            HotKeyItems.Add(new HotKeyItemViewModel("Close Tab", _model.CloseTab));
            HotKeyItems.Add(new HotKeyItemViewModel("Compare", _model.Compare));
            HotKeyItems.Add(new HotKeyItemViewModel("Save Session", _model.SaveSession));
            HotKeyItems.Add(new HotKeyItemViewModel("Load Session", _model.LoadSession));
        }

        // This method is called before saving to push UI changes back to the data model
        public void UpdateModel()
        {
            // A more robust solution could use a dictionary or reflection
            _model.AddTab = HotKeyItems.FirstOrDefault(i => i.Name == "Add Tab")?.Combination ?? _model.AddTab;
            _model.CloseTab = HotKeyItems.FirstOrDefault(i => i.Name == "Close Tab")?.Combination ?? _model.CloseTab;
            _model.Compare = HotKeyItems.FirstOrDefault(i => i.Name == "Compare")?.Combination ?? _model.Compare;
            _model.SaveSession = HotKeyItems.FirstOrDefault(i => i.Name == "Save Session")?.Combination ?? _model.SaveSession;
            _model.LoadSession = HotKeyItems.FirstOrDefault(i => i.Name == "Load Session")?.Combination ?? _model.LoadSession;
        }
    }

    public class LogOptionsViewModel : ViewModelBase
    {
        private readonly LogOptions _model;
        public LogOptionsViewModel(LogOptions model) { _model = model; }

        public List<string> AvailableLogLevels => new List<string> { "Verbose", "Debug", "Information", "Warning", "Error" };
        public bool IsEnabled { get => _model.Enabled; set { _model.Enabled = value; OnPropertyChanged(); } }
        public string Level { get => _model.Level; set { _model.Level = value; OnPropertyChanged(); } }
        public int RetentionDays { get => _model.RetentionDays; set { _model.RetentionDays = value; OnPropertyChanged(); } }
    }
}