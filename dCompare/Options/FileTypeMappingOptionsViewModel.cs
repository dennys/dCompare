using dCompare.Configuration;
using dCompare.Infrastructure;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

namespace dCompare.Options
{
    public class FileTypeMappingOptionsViewModel : ViewModelBase, IConfigurationUpdater
    {
        private readonly AppConfiguration _config;
        public ObservableCollection<FileTypeMappingViewModel> Mappings { get; }
        public ICommand AddMappingCommand { get; }
        public ICommand RemoveMappingCommand { get; }

        private FileTypeMappingViewModel _selectedMapping;
        public FileTypeMappingViewModel SelectedMapping
        {
            get => _selectedMapping;
            set => SetProperty(ref _selectedMapping, value);
        }

        public FileTypeMappingOptionsViewModel(AppConfiguration config)
        {
            _config = config;
            Mappings = new ObservableCollection<FileTypeMappingViewModel>(
                _config.Options.FileTypeMapping.Select(kvp => new FileTypeMappingViewModel(kvp.Key, kvp.Value, _config.Options.Tools.Values.ToList())));

            AddMappingCommand = new RelayCommand(_ => AddMapping());
            RemoveMappingCommand = new RelayCommand(_ => RemoveMapping(), _ => CanRemoveMapping());

            _selectedMapping = Mappings.FirstOrDefault();
        }

        private void AddMapping()
        {
            var newMappingName = "NewMapping" + (Mappings.Count + 1);
            var newMapping = new FileTypeMappingOptions();
            _config.Options.FileTypeMapping.Add(newMappingName, newMapping);
            var newMappingViewModel = new FileTypeMappingViewModel(newMappingName, newMapping, _config.Options.Tools.Values.ToList());
            Mappings.Add(newMappingViewModel);
            SelectedMapping = newMappingViewModel;
        }

        private void RemoveMapping()
        {
            if (SelectedMapping != null)
            {
                _config.Options.FileTypeMapping.Remove(SelectedMapping.Key);
                Mappings.Remove(SelectedMapping);
                SelectedMapping = Mappings.FirstOrDefault();
            }
        }

        private bool CanRemoveMapping()
        {
            return SelectedMapping != null;
        }

        public void UpdateModel()
        {
            // Model is updated as changes are made.
        }
    }

    public class FileTypeMappingViewModel : ViewModelBase, IDataErrorInfo
    {
        private readonly FileTypeMappingOptions _mapping;
        public string Key { get; }

        public List<string> AvailableTools { get; }

        public FileTypeMappingViewModel(string key, FileTypeMappingOptions mapping, List<ToolOptions> availableTools)
        {
            Key = key;
            _mapping = mapping;
            AvailableTools = availableTools.Select(t => t.Name).ToList();
        }

        public string Extensions
        {
            get => string.Join(",", _mapping.Extensions);
            set { _mapping.Extensions = value.Split(',').ToList(); OnPropertyChanged(); }
        }

        public string DefaultDiffTool
        {
            get => _mapping.DefaultDiffTool;
            set { _mapping.DefaultDiffTool = value; OnPropertyChanged(); }
        }

        public string DefaultViewTool
        {
            get => _mapping.DefaultViewTool;
            set { _mapping.DefaultViewTool = value; OnPropertyChanged(); }
        }

        public List<string> PreferredDiffTools
        {
            get => _mapping.PreferredTools.Diff;
            set { _mapping.PreferredTools.Diff = value; OnPropertyChanged(); }
        }

        public List<string> PreferredViewTools
        {
            get => _mapping.PreferredTools.View;
            set { _mapping.PreferredTools.View = value; OnPropertyChanged(); }
        }

        public string Error
        {
            get
            {
                // The Error property is not used in this ViewModel.
                // Instead, individual properties are validated.
                return string.Empty;
            }
        }

        public string this[string columnName]
        {
            get
            {
                if (columnName == nameof(Extensions))
                {
                    if (string.IsNullOrWhiteSpace(Extensions))
                    {
                        return "Extensions cannot be empty.";
                    }

                    var extensions = Extensions.Split(',');
                    if (extensions.Any(ext => !ext.StartsWith(".")))
                    {
                        return "All extensions must start with a dot.";
                    }
                }

                return string.Empty;
            }
        }
    }
}
