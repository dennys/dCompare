using dCompare.Configuration;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
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

            AddMappingCommand = new RelayCommand(AddMapping);
            RemoveMappingCommand = new RelayCommand(RemoveMapping, CanRemoveMapping);
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

    public class FileTypeMappingViewModel : ViewModelBase
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

        public string PreferredDiffTools
        {
            get => string.Join(",", _mapping.PreferredTools.Diff);
            set { _mapping.PreferredTools.Diff = value.Split(',').ToList(); OnPropertyChanged(); }
        }

        public string PreferredViewTools
        {
            get => string.Join(",", _mapping.PreferredTools.View);
            set { _mapping.PreferredTools.View = value.Split(',').ToList(); OnPropertyChanged(); }
        }
    }
}
