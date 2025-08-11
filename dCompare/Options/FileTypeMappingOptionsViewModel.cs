using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using dCompare.Configuration;
using dCompare.Tools.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace dCompare.Options
{
    public partial class FileTypeMappingOptionsViewModel : ObservableObject, IConfigurationUpdater
    {
        private readonly AppConfiguration _config;
        public ObservableCollection<FileTypeMappingViewModel> Mappings { get; }

        public virtual bool IsValid => true;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RemoveMappingCommand))]
        private FileTypeMappingViewModel? _selectedMapping;

        public FileTypeMappingOptionsViewModel(AppConfiguration config)
        {
            _config = config;
            Mappings = new ObservableCollection<FileTypeMappingViewModel>(
                _config.Options.FileTypeMapping.Select(kvp => new FileTypeMappingViewModel(kvp.Key, kvp.Value, _config.Options.Tools.Values.ToList())));

            if (Mappings.Any())
            {
                SelectedMapping = Mappings.First();
            }
        }

        //[RelayCommand]
        //private void AddMapping()
        //{
        //    // TODO: Adding a new FileType mapping from the UI is complex because FileType is an enum.
        //    // This requires a more sophisticated UI to allow the user to define a new file type name
        //    // and potentially select it from a list of non-used enum values.
        //    // For now, adding is disabled. Mappings should be edited in dCompare.json.
        //    var newMappingName = "NewMapping" + (Mappings.Count + 1);
        //    var newMapping = new FileTypeMappingOptions();
        //    //_config.Options.FileTypeMapping.Add(newMappingName, newMapping);
        //    //var newMappingViewModel = new FileTypeMappingViewModel(newMappingName, newMapping, _config.Options.Tools.Values.ToList());
        //    //Mappings.Add(newMappingViewModel);
        //    //SelectedMapping = newMappingViewModel;
        //}

        //[RelayCommand(CanExecute = nameof(CanRemoveMapping))]
        //private void RemoveMapping()
        //{
        //    // TODO: Removing is disabled as adding is disabled.
        //    if (SelectedMapping == null) return;

        //    //_config.Options.FileTypeMapping.Remove(SelectedMapping.Key);
        //    //Mappings.Remove(SelectedMapping);
        //    //if (Mappings.Any())
        //    //{
        //    //    SelectedMapping = Mappings.First();
        //    //}
        //}

        private bool CanRemoveMapping()
        {
            return SelectedMapping != null;
        }

        public void UpdateModel()
        {
            // Model is updated as changes are made.
        }
    }

    public partial class FileTypeMappingViewModel : ObservableObject, IDataErrorInfo
    {
        private readonly FileTypeMappingOptions _mapping;
        public FileType Key { get; }

        public virtual bool IsValid => true;

        public List<string> AvailableTools { get; }

        public FileTypeMappingViewModel(FileType key, FileTypeMappingOptions mapping, List<ToolOptions> availableTools)
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

        public string Error => string.Empty;

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
                    if (extensions.Any(ext => !ext.StartsWith('.')))
                    {
                        return "All extensions must start with a dot.";
                    }
                }

                return string.Empty;
            }
        }
    }
}
