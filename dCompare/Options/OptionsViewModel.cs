using dCompare.Configuration;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using Microsoft.VisualBasic; // For InputBox
using System.Collections.Specialized;
using dCompare.Infrastructure;

namespace dCompare.Options
{
    public class ViewModelBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
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

        public virtual bool IsValid => true;
    }

    public class SettingsViewModel : ViewModelBase
    {
        private readonly ConfigurationService _configService;
        private AppConfiguration _appConfig;
        private string _filterText = "Filter settings...";
        private ViewModelBase _selectedViewModel;

        public ObservableCollection<SettingsMenuItem> MenuItems { get; } = new();
        public ICommand SaveCommand { get; }

        public SettingsViewModel(ConfigurationService configService)
        {
            _configService = configService;
            SaveCommand = new RelayCommand(async _ => await SaveConfiguration());
            LoadConfigurationAsync();
        }


        public SettingsViewModel()
        {
            _filterText = "Filter...";
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
            var hotkeysVm = MenuItems.SelectMany(m => m.Children)
                                     .FirstOrDefault(c => c.ViewModel is HotKeysOptionsViewModel)?
                                     .ViewModel as HotKeysOptionsViewModel;

            if (hotkeysVm != null)
            {
                hotkeysVm.ValidateHotKeys();
                if (hotkeysVm.HasDuplicateHotKeys())
                {
                    MessageBox.Show("There are duplicate hotkeys. Please resolve the conflicts before saving.", "Duplicate Hotkeys", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

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
            var optionsNode = new SettingsMenuItem(Properties.Resources.OptionsMenu, null, this);

            var uiNode = new SettingsMenuItem("UI", new UIOptionsViewModel(_appConfig.Options.UI), this);
            var compareNode = new SettingsMenuItem("Compare", new CompareOptionsViewModel(_appConfig.Options.Compare), this);

            var sessionNode = new SettingsMenuItem("Session", new SessionOptionsViewModel(_appConfig.Options.Session), this);
            var confirmationNode = new SettingsMenuItem("Confirmation", new ConfirmationOptionsViewModel(_appConfig.Options.Confirmation), this);
            var hotkeysNode = new SettingsMenuItem("HotKeys", new HotKeysOptionsViewModel(_appConfig.Options.HotKeys), this);
            var textViewerNode = new SettingsMenuItem("TextViewer", new TextViewerOptionsViewModel(_appConfig.Options.TextViewer), this);
            var toolsNode = new SettingsMenuItem("Tools", new ToolsOptionsViewModel(_appConfig), this);
            var fileTypeMappingNode = new SettingsMenuItem("FileTypeMapping", new FileTypeMappingOptionsViewModel(_appConfig), this);

            optionsNode.Children.Add(uiNode);
            optionsNode.Children.Add(compareNode);
            optionsNode.Children.Add(textViewerNode);
            optionsNode.Children.Add(toolsNode);
            optionsNode.Children.Add(fileTypeMappingNode);
            optionsNode.Children.Add(confirmationNode);
            optionsNode.Children.Add(sessionNode);
            optionsNode.Children.Add(hotkeysNode);

            MenuItems.Add(optionsNode);

            optionsNode.IsExpanded = true;
            if (uiNode != null) uiNode.IsSelected = true;
        }

        public string FilterText
        {
            get => _filterText;
            set
            {
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

        public override bool IsValid
        {
            get
            {
                return MenuItems.SelectMany(m => m.Children)
                                .All(c => c.ViewModel == null || c.ViewModel.IsValid);
            }
        }
    }

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

    public interface IConfigurationUpdater
    {
        void UpdateModel();
    }

    public class UIOptionsViewModel : ViewModelBase
    {
        private readonly UIOptions _model;
        public UIOptionsViewModel(UIOptions model) { _model = model; }
        public UIOptionsViewModel() : this(new UIOptions()) { }
        public static List<string> AvailableThemes => ["Light", "Dark"];
        public static List<KeyValuePair<string, string>> AvailableLanguages =>
        [
            new KeyValuePair<string, string>("English", "en"),
            new KeyValuePair<string, string>("繁體中文", "zh_TW"),
            new KeyValuePair<string, string>("简体中文", "zh_CN")
        ];

        public string Theme { get => _model.Theme; set { _model.Theme = value; OnPropertyChanged(); } }
        public string Language { get => _model.Language; set { _model.Language = value; OnPropertyChanged(); } }
    }

    public class AlgorithmItemViewModel : ViewModelBase
    {
        private readonly ComparisonAlgorithm _algorithm;
        private readonly ComparisonAlgorithmInfo _info;
        private readonly CompareOptionsViewModel _parent;
        private readonly bool _supportsPercentage;

        public AlgorithmItemViewModel(ComparisonAlgorithm algorithm, ComparisonAlgorithmInfo info, CompareOptionsViewModel parent)
        {
            _algorithm = algorithm;
            _info = info;
            _parent = parent;
            _supportsPercentage = _info.ComparisonPercentage.HasValue;
        }

        public ComparisonAlgorithm Algorithm => _algorithm;
        public string Description => _info.Description;
        public bool IsPercentageSettingEnabled
        {
            get
            {
                if (!_supportsPercentage || this.Description == "Quick (ModifiedDate and Size)")
                {
                    return false;
                }
                return true;
            }
        }
        public bool IsEnabled
        {
            get => _info.IsEnabled;
            set
            {
                _info.IsEnabled = value;
                OnPropertyChanged();
                if (!value && IsDefault)
                {
                    IsDefault = false;
                    _parent.UpdateDefaultAlgorithm(null);
                }
            }
        }

        public bool IsDefault
        {
            get => _info.IsDefault;
            set
            {
                if (_info.IsDefault == value) return;

                if (value && _info.IsEnabled)
                {
                    _info.IsDefault = true;
                    OnPropertyChanged();
                    _parent.UpdateDefaultAlgorithm(_algorithm);
                }
                else if (!value)
                {
                    _info.IsDefault = false;
                    OnPropertyChanged();
                }
            }
        }
        public string ComparisonPercentage
        {
            get => _info.ComparisonPercentage?.ToString();
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    _info.ComparisonPercentage = null;
                }
                else if (int.TryParse(value, out int percentage) && percentage >= 1 && percentage <= 100)
                {
                    _info.ComparisonPercentage = percentage;
                }
                OnPropertyChanged();
            }
        }

        public string this[string columnName]
        {
            get
            {
                string result = null;
                if (columnName == nameof(ComparisonPercentage))
                {
                    if (!string.IsNullOrWhiteSpace(ComparisonPercentage))
                    {
                        if (!int.TryParse(ComparisonPercentage, out int percentage))
                        {
                            result = "Value must be a number.";
                        }
                        else if (percentage < 1 || percentage > 100)
                        {
                            result = "Value must be between 1 and 100.";
                        }
                    }
                }
                return result;
            }
        }
    }

    public class CompareOptionsViewModel : ViewModelBase, IConfigurationUpdater
    {
        public static List<string> AvailableScanMethods => new() { "General", "MFT" };

        private readonly CompareOptions _model;
        public ObservableCollection<AlgorithmItemViewModel> AlgorithmItems { get; } = new();

        public CompareOptionsViewModel(CompareOptions model)
        {
            _model = model;
            foreach (var kvp in _model.Algorithms)
            {
                AlgorithmItems.Add(new AlgorithmItemViewModel(kvp.Key, kvp.Value, this));
            }
        }

        public string ScanMethod
        {
            get => _model.ScanMethod.MethodOptions;
            set
            {
                _model.ScanMethod.MethodOptions = value;
                OnPropertyChanged();
            }
        }

        public bool Parallel
        {
            get => _model.ScanMethod.ParallelOptions;
            set
            {
                _model.ScanMethod.ParallelOptions = value;
                OnPropertyChanged();
            }
        }

        public static List<string> AvailableDoubleClickActions => new() { "View", "Compare" };

        public string DoubleClickAction
        {
            get => _model.DoubleClick;
            set
            {
                _model.DoubleClick = value;
                OnPropertyChanged();
            }
        }

        public void UpdateDefaultAlgorithm(ComparisonAlgorithm? algorithm)
        {
            foreach (var item in AlgorithmItems)
            {
                if (item.Algorithm != algorithm)
                {
                    item.IsDefault = false;
                }
            }
        }

        public void UpdateModel()
        {
            _model.ScanMethod.MethodOptions = ScanMethod;
            _model.ScanMethod.ParallelOptions = Parallel;

            foreach (var item in AlgorithmItems)
            {
                _model.Algorithms[item.Algorithm] = new ComparisonAlgorithmInfo
                {
                    Description = item.Description,
                    IsEnabled = item.IsEnabled,
                    IsDefault = item.IsDefault,
                    ComparisonPercentage = item.ComparisonPercentage != null && int.TryParse(item.ComparisonPercentage, out int percentage) ? percentage : null
                };
            }
        }
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
            if (string.IsNullOrWhiteSpace(newExt))
            {
                return;
            }

            if (!newExt.StartsWith("."))
            {
                MessageBox.Show("Extension must start with a dot (e.g., .txt).", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (Extensions.Contains(newExt, StringComparer.OrdinalIgnoreCase))
            {
                MessageBox.Show("Extension already exists.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Extensions.Add(newExt);
        }

        private void RemoveExtension(object obj)
        {
            if (!string.IsNullOrEmpty(SelectedExtension))
            {
                Extensions.Remove(SelectedExtension);
            }
        }
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
                OnPropertyChanged(nameof(AutoCompareAfterSessionLoad));
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
        private bool _isDuplicate;
        private readonly HotKeysOptionsViewModel _parent;

        public string Name { get; }
        public string Combination
        {
            get => _combination;
            set
            {
                if (SetProperty(ref _combination, value))
                {
                    _parent.ValidateHotKeys();
                }
            }
        }

        public bool IsDuplicate
        {
            get => _isDuplicate;
            set => SetProperty(ref _isDuplicate, value);
        }

        public ICommand ClearCommand { get; }

        public HotKeyItemViewModel(string name, string combination, HotKeysOptionsViewModel parent)
        {
            Name = name;
            _combination = combination;
            _parent = parent;
            ClearCommand = new RelayCommand(_ => Combination = string.Empty);
        }
    }

    public class HotKeysOptionsViewModel : ViewModelBase, IConfigurationUpdater
    {
        private readonly HotKeysOptions _model;
        public ObservableCollection<HotKeyItemViewModel> HotKeyItems { get; } = new();

        public HotKeysOptionsViewModel(HotKeysOptions model)
        {
            _model = model;
            HotKeyItems.Add(new HotKeyItemViewModel("Add Tab", _model.AddTab, this));
            HotKeyItems.Add(new HotKeyItemViewModel("Close Tab", _model.CloseTab, this));
            HotKeyItems.Add(new HotKeyItemViewModel("Compare", _model.Compare, this));
            HotKeyItems.Add(new HotKeyItemViewModel("Save Session", _model.SaveSession, this));
            HotKeyItems.Add(new HotKeyItemViewModel("Load Session", _model.LoadSession, this));
            HotKeyItems.Add(new HotKeyItemViewModel("Open Options", _model.OpenOptions, this));
        }

        public void UpdateModel()
        {
            _model.AddTab = HotKeyItems.FirstOrDefault(i => i.Name == "Add Tab")?.Combination ?? _model.AddTab;
            _model.CloseTab = HotKeyItems.FirstOrDefault(i => i.Name == "Close Tab")?.Combination ?? _model.CloseTab;
            _model.Compare = HotKeyItems.FirstOrDefault(i => i.Name == "Compare")?.Combination ?? _model.Compare;
            _model.SaveSession = HotKeyItems.FirstOrDefault(i => i.Name == "Save Session")?.Combination ?? _model.SaveSession;
            _model.LoadSession = HotKeyItems.FirstOrDefault(i => i.Name == "Load Session")?.Combination ?? _model.LoadSession;
            _model.OpenOptions = HotKeyItems.FirstOrDefault(i => i.Name == "Open Options")?.Combination ?? _model.OpenOptions;
        }

        public void ValidateHotKeys()
        {
            var duplicateHotKeys = HotKeyItems
                .Where(h => !string.IsNullOrWhiteSpace(h.Combination))
                .GroupBy(h => h.Combination)
                .Where(g => g.Count() > 1)
                .SelectMany(g => g)
                .ToList();

            foreach (var item in HotKeyItems)
            {
                item.IsDuplicate = duplicateHotKeys.Contains(item);
            }
        }

        public bool HasDuplicateHotKeys()
        {
            return HotKeyItems.Any(h => h.IsDuplicate);
        }
    }

    public class ConfirmationOptionsViewModel : ViewModelBase, IConfigurationUpdater
    {
        private readonly ConfirmationOptions _model;

        public ConfirmationOptionsViewModel(ConfirmationOptions model)
        {
            _model = model;
        }

        public bool CloseApplication
        {
            get => _model.CloseApplication;
            set { _model.CloseApplication = value; OnPropertyChanged(); }
        }

        public bool CloseTab
        {
            get => _model.CloseTab;
            set { _model.CloseTab = value; OnPropertyChanged(); }
        }

        public bool Copy
        {
            get => _model.Copy;
            set { _model.Copy = value; OnPropertyChanged(); }
        }

        public bool Move
        {
            get => _model.Move;
            set { _model.Move = value; OnPropertyChanged(); }
        }

        public bool CopyOverwrite
        {
            get => _model.CopyOverwrite;
            set { _model.CopyOverwrite = value; OnPropertyChanged(); }
        }

        public bool MoveOverwrite
        {
            get => _model.MoveOverwrite;
            set { _model.MoveOverwrite = value; OnPropertyChanged(); }
        }

        public bool DeleteFile
        {
            get => _model.DeleteFile;
            set { _model.DeleteFile = value; OnPropertyChanged(); }
        }

        public void UpdateModel()
        {
            // Model is updated directly via property setters
        }
    }

    public class LogOptionsViewModel : ViewModelBase
    {
        // Implementation remains unchanged
    }
}