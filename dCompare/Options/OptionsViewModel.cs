using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using dCompare.Configuration;
using Microsoft.VisualBasic;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Windows;

namespace dCompare.Options
{
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly ConfigurationService _configService;
        private AppConfiguration _appConfig;

        [ObservableProperty]
        private string _filterText = "Filter settings...";

        [ObservableProperty]
        private ObservableObject _selectedViewModel;

        public ObservableCollection<SettingsMenuItem> MenuItems { get; } = new();

        public SettingsViewModel(ConfigurationService configService)
        {
            _configService = configService;
            LoadConfigurationAsync();
        }


        //public SettingsViewModel()
        //{
        //    _filterText = "Filter...";
        //    _appConfig = new AppConfiguration();
        //    BuildMenu();
        //}

        private async void LoadConfigurationAsync()
        {
            _appConfig = await _configService.LoadConfigurationAsync();
            BuildMenu();
        }

        [RelayCommand]
        private async Task SaveConfiguration()
        {
            if (MenuItems.SelectMany(m => m.Children)
                                     .FirstOrDefault(c => c.ViewModel is HotKeysOptionsViewModel)?
                                     .ViewModel is HotKeysOptionsViewModel hotkeysVm)
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

            var uiNode = new SettingsMenuItem(Properties.Resources.UI, new UIOptionsViewModel(_appConfig.Options.UI), this);
            var compareNode = new SettingsMenuItem(Properties.Resources.Compare, new CompareOptionsViewModel(_appConfig.Options.Compare), this);

            var sessionNode = new SettingsMenuItem(Properties.Resources.Session, new SessionOptionsViewModel(_appConfig.Options.Session), this);
            var confirmationNode = new SettingsMenuItem(Properties.Resources.Confirmation, new ConfirmationOptionsViewModel(_appConfig.Options.Confirmation), this);
            var hotkeysNode = new SettingsMenuItem(Properties.Resources.HotKeys, new HotKeysOptionsViewModel(_appConfig.Options.HotKeys), this);
            var textViewerNode = new SettingsMenuItem(Properties.Resources.TextViewer, new TextViewerOptionsViewModel(_appConfig.Options.TextViewer), this);
            var toolsNode = new SettingsMenuItem(Properties.Resources.Tools, new ToolsOptionsViewModel(_appConfig), this);
            var fileTypeMappingNode = new SettingsMenuItem(Properties.Resources.FileTypeMapping, new FileTypeMappingOptionsViewModel(_appConfig), this);

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

        partial void OnFilterTextChanged(string value)
        {
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            string effectiveFilter = (string.IsNullOrWhiteSpace(FilterText) || FilterText == "Filter settings...") ? "" : FilterText;

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

        //public override bool IsValid
        //{
        //    get
        //    {
        //        return MenuItems.SelectMany(m => m.Children)
        //                        .All(c => c.ViewModel == null || c.ViewModel.IsValid);
        //    }
        //}
    }

    public partial class SettingsMenuItem : ObservableObject
    {
        [ObservableProperty]
        private bool _isSelected;
        [ObservableProperty]
        private bool _isExpanded;
        [ObservableProperty]
        private bool _isVisible = true;
        private readonly SettingsViewModel _mainViewModel;

        public string Name { get; }
        public ObservableObject ViewModel { get; }
        public ObservableCollection<SettingsMenuItem> Children { get; } = new();

        public SettingsMenuItem(string name, ObservableObject viewModel, SettingsViewModel mainViewModel)
        {
            Name = name;
            ViewModel = viewModel;
            _mainViewModel = mainViewModel;
        }

        partial void OnIsSelectedChanged(bool value)
        {
            if (value && ViewModel != null)
            {
                _mainViewModel.SelectedViewModel = ViewModel;
            }
        }
    }

    public interface IConfigurationUpdater
    {
        void UpdateModel();
    }

    public partial class UIOptionsViewModel : ObservableObject
    {
        private readonly UIOptions _model;
        public virtual bool IsValid => true;
        public UIOptionsViewModel(UIOptions model) { _model = model; }
        public UIOptionsViewModel() : this(new UIOptions()) { }
        public static List<string> AvailableThemes => ["Light", "Dark"];
        public static List<KeyValuePair<string, string>> AvailableLanguages =>
        [
            new KeyValuePair<string, string>("English", "en"),
            new KeyValuePair<string, string>("繁體中文", "zh-TW"),
            new KeyValuePair<string, string>("简体中文", "zh-CN")
        ];

        public string Theme { get => _model.Theme; set { _model.Theme = value; OnPropertyChanged(); } }
        public string Language { get => _model.Language; set { _model.Language = value; OnPropertyChanged(); } }
    }

    public partial class AlgorithmItemViewModel : ObservableObject
    {
        private readonly ComparisonAlgorithm _algorithm;
        private readonly ComparisonAlgorithmInfo _info;
        public virtual bool IsValid => true;
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
        public string? ComparisonPercentage
        {
            get
            {
                return _info.ComparisonPercentage?.ToString();
            }

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

        //public string this[string columnName]
        //{
        //    get
        //    {
        //        string result = null;
        //        if (columnName == nameof(ComparisonPercentage))
        //        {
        //            if (!string.IsNullOrWhiteSpace(ComparisonPercentage))
        //            {
        //                if (!int.TryParse(ComparisonPercentage, out int percentage))
        //                {
        //                    result = "Value must be a number.";
        //                }
        //                else if (percentage < 1 || percentage > 100)
        //                {
        //                    result = "Value must be between 1 and 100.";
        //                }
        //            }
        //        }
        //        return result;
        //    }
        //}
    }

    public partial class CompareOptionsViewModel : ObservableObject, IConfigurationUpdater
    {
        public static List<string> AvailableScanMethods => new() { "General", "MFT" };

        private readonly CompareOptions _model;
        public virtual bool IsValid => true;
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

        public static List<DoubleClickActions> AvailableDoubleClickActions => Enum.GetValues(typeof(DoubleClickActions)).Cast<DoubleClickActions>().ToList();

        public DoubleClickActions DoubleClickAction
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

    public abstract partial class ListEditorViewModelBase<TModel, TItem> : ObservableObject where TModel : class
    {
        protected readonly TModel _model;
        private readonly Action<TModel, List<string>> _updateModelAction;
        public virtual bool IsValid => true;
        [ObservableProperty]
        private string? _selectedExtension;

        public ObservableCollection<string> Extensions { get; set; }

        protected ListEditorViewModelBase(TModel model, List<string> extensions, Action<TModel, List<string>> updateModelAction)
        {
            _model = model;
            _updateModelAction = updateModelAction;
            Extensions = new ObservableCollection<string>(extensions);
            Extensions.CollectionChanged += (s, e) => _updateModelAction(_model, Extensions.ToList());
        }

        [RelayCommand]
        private void AddExtension()
        {
            string newExt = Interaction.InputBox("Enter new file extension (e.g., .txt):", "Add Extension", "");
            if (string.IsNullOrWhiteSpace(newExt))
            {
                return;
            }

            if (!newExt.StartsWith('.'))
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

        [RelayCommand(CanExecute = nameof(CanRemoveExtension))]
        private void RemoveExtension()
        {
            if (!string.IsNullOrEmpty(SelectedExtension))
            {
                Extensions.Remove(SelectedExtension);
            }
        }

        private bool CanRemoveExtension() => !string.IsNullOrEmpty(SelectedExtension);
    }

    public partial class SessionOptionsViewModel : ObservableObject
    {
        private readonly SessionOptions _model;
        public virtual bool IsValid => true;
        public SessionOptionsViewModel(SessionOptions model)
        {
            _model = model;
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

        [RelayCommand]
        private void BrowseSessionFile()
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

    public partial class HotKeyItemViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _combination;
        public virtual bool IsValid => true;
        [ObservableProperty]
        private bool _isDuplicate;
        private readonly HotKeysOptionsViewModel _parent;

        public string Name { get; }

        public HotKeyItemViewModel(string name, string combination, HotKeysOptionsViewModel parent)
        {
            Name = name;
            _combination = combination;
            _parent = parent;
        }

        partial void OnCombinationChanged(string value)
        {
            _parent.ValidateHotKeys();
        }

        [RelayCommand]
        private void Clear() => Combination = string.Empty;
    }

    public partial class HotKeysOptionsViewModel : ObservableObject, IConfigurationUpdater
    {
        private readonly HotKeysOptions _model;
        public ObservableCollection<HotKeyItemViewModel> HotKeyItems { get; } = new();
        public virtual bool IsValid => true;

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

    public partial class ConfirmationOptionsViewModel : ObservableObject, IConfigurationUpdater
    {
        private readonly ConfirmationOptions _model;
        public virtual bool IsValid => true;

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

    public class LogOptionsViewModel : ObservableObject
    {
        public virtual bool IsValid => true;
    }
}