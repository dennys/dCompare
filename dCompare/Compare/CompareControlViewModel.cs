using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;

namespace dCompare.Compare
{
    public class CompareControlViewModel : INotifyPropertyChanged
    {
        private string _path;
        private bool _isSuggestionsOpen;
        private List<string> _suggestions;
        private string _selectedSuggestion;
        private bool _suppressSuggestions;
        private string _searchText;

        public string Path
        {
            get => _path;
            set
            {
                if (_path != value)
                {
                    _path = value;
                    OnPropertyChanged();
                    if (!_suppressSuggestions)
                    {
                        UpdateSuggestions();
                    }
                    else
                    {
                        _suppressSuggestions = false;
                    }
                }
            }
        }

        public bool IsSuggestionsOpen
        {
            get => _isSuggestionsOpen;
            set
            {
                if (_isSuggestionsOpen != value)
                {
                    _isSuggestionsOpen = value;
                    OnPropertyChanged();
                }
            }
        }

        public List<string> Suggestions
        {
            get => _suggestions;
            set
            {
                _suggestions = value;
                OnPropertyChanged();
            }
        }

        public string SelectedSuggestion
        {
            get => _selectedSuggestion;
            set
            {
                if (_selectedSuggestion != value)
                {
                    _selectedSuggestion = value;
                    OnPropertyChanged();
                    if (_selectedSuggestion != null)
                    {
                        Path = _selectedSuggestion;
                        IsSuggestionsOpen = false;
                    }
                }
            }
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText != value)
                {
                    _searchText = value;
                    OnPropertyChanged();
                }
            }
        }

        public void SuppressSuggestionsOnce()
        {
            _suppressSuggestions = true;
        }

        private void UpdateSuggestions()
        {
            if (string.IsNullOrEmpty(Path))
            {
                IsSuggestionsOpen = false;
                return;
            }

            try
            {
                string directory = System.IO.Path.GetDirectoryName(Path);
                string prefix = System.IO.Path.GetFileName(Path);

                if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                {
                    IsSuggestionsOpen = false;
                    return;
                }

                var allDirectories = Directory.GetDirectories(directory).ToList();

                Suggestions = allDirectories
                    .Where(dir => System.IO.Path.GetFileName(dir).StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                IsSuggestionsOpen = Suggestions.Any();
            }
            catch (Exception)
            {
                IsSuggestionsOpen = false;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
