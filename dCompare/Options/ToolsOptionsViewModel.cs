using dCompare.Configuration;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using System.ComponentModel;

namespace dCompare.Options
{
    public class ToolsOptionsViewModel : ViewModelBase, IConfigurationUpdater
    {
        private readonly AppConfiguration _config;
        public ObservableCollection<ToolOptionsViewModel> Tools { get; }
        public ICommand AddToolCommand { get; }
        public ICommand RemoveToolCommand { get; }

        private ToolOptionsViewModel _selectedTool;
        public ToolOptionsViewModel SelectedTool
        {
            get => _selectedTool;
            set => SetProperty(ref _selectedTool, value);
        }

        public ToolsOptionsViewModel(AppConfiguration config)
        {
            _config = config;
            Tools = new ObservableCollection<ToolOptionsViewModel>(
                _config.Options.Tools.Select(kvp => new ToolOptionsViewModel(kvp.Key, kvp.Value)));

            AddToolCommand = new RelayCommand(_ => AddTool());
            RemoveToolCommand = new RelayCommand(_ => RemoveTool(), _ => CanRemoveTool());
        }

        private void AddTool()
        {
            var newToolName = "NewTool" + (Tools.Count + 1);
            var newTool = new ToolOptions { Name = newToolName };
            _config.Options.Tools.Add(newToolName, newTool);
            var newToolViewModel = new ToolOptionsViewModel(newToolName, newTool);
            Tools.Add(newToolViewModel);
            SelectedTool = newToolViewModel;
        }

        private void RemoveTool()
        {
            if (SelectedTool != null)
            {
                _config.Options.Tools.Remove(SelectedTool.Key);
                Tools.Remove(SelectedTool);
                SelectedTool = Tools.FirstOrDefault();
            }
        }

        private bool CanRemoveTool()
        {
            return SelectedTool != null;
        }

        public void UpdateModel()
        {
            // The model is updated as changes are made in the UI.
        }
    }

    public class ToolOptionsViewModel : ViewModelBase, IDataErrorInfo
    {
        private readonly ToolOptions _tool;
        public string Key { get; }

        public List<string> AvailableTypes => new List<string> { ToolTypes.BuiltIn, ToolTypes.External };
        public List<string> AvailableOperations => new List<string> { Operations.View, Operations.Diff };

        public ICommand BrowseCommand { get; }

        public ToolOptionsViewModel(string key, ToolOptions tool)
        {
            Key = key;
            _tool = tool;
            BrowseCommand = new RelayCommand(_ => BrowseForPath());
        }

        private void BrowseForPath()
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog();
            if (openFileDialog.ShowDialog() == true)
            {
                Path = openFileDialog.FileName;
            }
        }

        public string Name
        {
            get => _tool.Name;
            set { _tool.Name = value; OnPropertyChanged(); }
        }

        public string Type
        {
            get => _tool.Type;
            set
            {
                _tool.Type = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsExternal));
            }
        }

        public bool IsExternal => Type == ToolTypes.External;

        public string Handler
        {
            get => _tool.Handler;
            set { _tool.Handler = value; OnPropertyChanged(); }
        }

        public string Path
        {
            get => _tool.Path;
            set { _tool.Path = value; OnPropertyChanged(); }
        }

        public string DiffArguments
        {
            get => _tool.DiffArguments;
            set { _tool.DiffArguments = value; OnPropertyChanged(); }
        }

        public string ViewArguments
        {
            get => _tool.ViewArguments;
            set { _tool.ViewArguments = value; OnPropertyChanged(); }
        }

        public List<string> SupportedOperations
        {
            get => _tool.SupportedOperations;
            set
            {
                _tool.SupportedOperations = value;
                OnPropertyChanged();
            }
        }

        public string SupportedExtensions
        {
            get => string.Join(",", _tool.SupportedExtensions);
            set { _tool.SupportedExtensions = value.Split(',').ToList(); OnPropertyChanged(); }
        }

        public string Features
        {
            get => string.Join(",", _tool.Features);
            set { _tool.Features = value.Split(',').ToList(); OnPropertyChanged(); }
        }

        public bool Enabled
        {
            get => _tool.Enabled;
            set { _tool.Enabled = value; OnPropertyChanged(); }
        }

        public string Error => null;

        public string this[string columnName]
        {
            get
            {
                string result = null;
                if (columnName == nameof(Name))
                {
                    if (string.IsNullOrWhiteSpace(Name))
                    {
                        result = "Name cannot be empty.";
                    }
                }
                if (columnName == nameof(SupportedExtensions))
                {
                    if (string.IsNullOrWhiteSpace(SupportedExtensions))
                    {
                        result = "Supported Extensions cannot be empty.";
                    }
                    else
                    {
                        var extensions = SupportedExtensions.Split(',');
                        foreach (var ext in extensions)
                        {
                            if (!ext.StartsWith("."))
                            {
                                result = "All extensions must start with a dot.";
                                break;
                            }
                        }
                    }
                }
                if (columnName == nameof(SupportedOperations))
                {
                    if (SupportedOperations == null || SupportedOperations.Count == 0)
                    {
                        result = "At least one supported operation must be selected.";
                    }
                }
                if (columnName == nameof(Path))
                {
                    if (Type == ToolTypes.External && string.IsNullOrWhiteSpace(Path))
                    {
                        result = "Path is required for external tools.";
                    }
                }
                return result;
            }
        }
    }
}
