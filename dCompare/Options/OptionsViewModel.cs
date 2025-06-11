using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using dCompare.Configuration;
using dCompare.Options;

namespace dCompare
{
    public class OptionsViewModel : INotifyPropertyChanged
    {
        private readonly ConfigurationService _configService;
        private string _searchText;
        private object _currentConfigView;
        private readonly List<TreeItemViewModel> _treeItems;

        public OptionsConfig Options { get; }
        public List<HotkeyViewModel> Hotkeys { get; }
        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged(nameof(SearchText));
            }
        }

        public object CurrentConfigView
        {
            get => _currentConfigView;
            set
            {
                _currentConfigView = value;
                OnPropertyChanged(nameof(CurrentConfigView));
            }
        }

        public OptionsViewModel(ConfigurationService configService)
        {
            _configService = configService;
            Options = _configService.GetConfiguration().Options;
            Hotkeys = Options.HotKeys.Select(kvp => new HotkeyViewModel(kvp.Key, kvp.Value)).ToList();
            _treeItems = new List<TreeItemViewModel>
            {
                new TreeItemViewModel("General", true),
                new TreeItemViewModel("UI", true) { Children = new List<TreeItemViewModel>
                {
                    new TreeItemViewModel("General", true),
                    new TreeItemViewModel("Hotkeys", true)
                }},
                new TreeItemViewModel("Log", true)
            };
        }

        public void FilterTreeItems()
        {
            var search = SearchText?.ToLower() ?? "";
            foreach (var item in _treeItems)
            {
                item.Filter(search);
            }
        }

        public void SetCurrentConfigView(string tag)
        {
            UserControl view = tag switch
            {
                "General" => new GeneralConfigView { DataContext = Options },
                "UIGeneral" => new UIGeneralConfigView { DataContext = Options },
                "UIHotkeys" => new HotkeysConfigView { DataContext = this },
                "Log" => new LogConfigView { DataContext = Options },
                _ => null
            };
            CurrentConfigView = view;
        }

        public void UpdateOptions()
        {
            // Sync Hotkeys back to Options.HotKeys
            Options.HotKeys.Clear();
            foreach (var hotkey in Hotkeys)
            {
                Options.HotKeys[hotkey.Key] = hotkey.Value;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class TreeItemViewModel : INotifyPropertyChanged
    {
        private bool _isVisible = true;

        public string Name { get; }
        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                _isVisible = value;
                OnPropertyChanged(nameof(IsVisible));
            }
        }
        public List<TreeItemViewModel> Children { get; set; } = new List<TreeItemViewModel>();

        public TreeItemViewModel(string name, bool isVisible)
        {
            Name = name;
            IsVisible = isVisible;
        }

        public void Filter(string search)
        {
            bool isMatch = string.IsNullOrEmpty(search) || Name.ToLower().Contains(search);
            foreach (var child in Children)
            {
                child.Filter(search);
                isMatch |= child.IsVisible;
            }
            IsVisible = isMatch;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class HotkeyViewModel : INotifyPropertyChanged
    {
        private string _value;

        public string Key { get; }
        public string Value
        {
            get => _value;
            set
            {
                _value = value;
                OnPropertyChanged(nameof(Value));
            }
        }

        public HotkeyViewModel(string key, string value)
        {
            Key = key;
            Value = value;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}