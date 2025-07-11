using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System; // Required for UnauthorizedAccessException

namespace dCompare.Compare
{
    public partial class PathInputViewModel : ObservableObject
    {
        [ObservableProperty]
        private string? _path;

        [ObservableProperty]
        private ObservableCollection<string> _suggestions = new();

        [ObservableProperty]
        private bool _isSuggestionsOpen;

        private string? _selectedSuggestion;
        public string? SelectedSuggestion
        {
            get => _selectedSuggestion;
            set
            {
                if (SetProperty(ref _selectedSuggestion, value) && value != null)
                {
                    Path = value;
                    IsSuggestionsOpen = false;
                }
            }
        }

        async partial void OnPathChanged(string? oldValue, string? newValue)
        {
            if (string.IsNullOrWhiteSpace(newValue))
            {
                Suggestions.Clear();
                IsSuggestionsOpen = false;
                return;
            }

            // Avoid re-searching if the path was set by selecting a suggestion
            if (newValue == _selectedSuggestion && oldValue !=newValue) // if newvalue is from selectedSuggestion, then selectedSuggestion is already set
            {
                 //if we set selectedSuggestion, it will trigger path change, then isSuggestionOpen will be false.
                 //if path is changed by selection, we don't want to re-open it.
                if (!IsSuggestionsOpen) return;
            }


            await UpdateSuggestionsAsync(newValue);
        }


        private async Task UpdateSuggestionsAsync(string currentPath)
        {
            if (string.IsNullOrEmpty(currentPath))
            {
                Suggestions.Clear();
                IsSuggestionsOpen = false;
                return;
            }

            try
            {
                await Task.Run(() => // To avoid blocking UI thread for longer searches
                {
                    var directoryName = System.IO.Path.GetDirectoryName(currentPath);
                    string searchPattern = System.IO.Path.GetFileName(currentPath);

                    if (string.IsNullOrEmpty(directoryName) || !Directory.Exists(directoryName))
                    {
                        // If the base directory is invalid or doesn't exist, try to see if currentPath itself is a drive letter or start of one
                        if (currentPath.EndsWith(":\\") && Directory.Exists(currentPath))
                        {
                            directoryName = currentPath;
                            searchPattern = "*"; // Show all subdirectories in the drive root
                        }
                        else if (IsDriveOrPartialDrive(currentPath))
                        {
                            // It's something like "D:" or "d:\a" where "d:\a" doesn't exist yet,
                            // so we search from the drive root "D:\" for "a*"
                            directoryName = System.IO.Path.GetPathRoot(currentPath);
                            if (string.IsNullOrEmpty(directoryName) || !Directory.Exists(directoryName))
                            {
                                App.Current.Dispatcher.Invoke(() =>
                                {
                                    Suggestions.Clear();
                                    IsSuggestionsOpen = false;
                                });
                                return;
                            }
                            searchPattern = currentPath.Substring(directoryName.Length) + "*";
                        }
                        else
                        {
                             App.Current.Dispatcher.Invoke(() =>
                             {
                                Suggestions.Clear();
                                IsSuggestionsOpen = false;
                            });
                            return;
                        }
                    }
                    else
                    {
                        searchPattern += "*"; // Append wildcard for starts-with search
                    }

                    if (!Directory.Exists(directoryName))
                    {
                        App.Current.Dispatcher.Invoke(() =>
                        {
                            Suggestions.Clear();
                            IsSuggestionsOpen = false;
                        });
                        return;
                    }

                    var newSuggestions = Directory.GetDirectories(directoryName, searchPattern, SearchOption.TopDirectoryOnly)
                                                  .ToList();

                    // Recursive search for deeper matches if initial top-level matches are few or an exact match exists
                    if (Directory.Exists(currentPath))
                    {
                        var deeperSuggestions = Directory.GetDirectories(currentPath, "*", SearchOption.AllDirectories)
                                                         .Take(20); // Limit recursive results for performance
                        newSuggestions.AddRange(deeperSuggestions);
                        newSuggestions = newSuggestions.Distinct().OrderBy(s => s.Length).ThenBy(s => s).ToList();
                    }


                    // Ensure the current path itself is in suggestions if it's a valid directory
                    if (Directory.Exists(currentPath) && !newSuggestions.Contains(currentPath))
                    {
                        newSuggestions.Insert(0, currentPath);
                    }

                    // If the input path is exactly "D:\", list its immediate children.
                    // The previous logic might make searchPattern "*" and directoryName "D:\", which is good.
                    // If input is "D:\a", searchPattern becomes "a*", directoryName "D:\"
                    // If input is "D:\a1", searchPattern becomes "a1*", directoryName "D:\"
                    // If input is "D:\a12\b", searchPattern becomes "b*", directoryName "D:\a12\"

                    App.Current.Dispatcher.Invoke(() =>
                    {
                        Suggestions.Clear();
                        foreach (var suggestion in newSuggestions.Take(25)) // Limit total suggestions
                        {
                            Suggestions.Add(suggestion);
                        }
                        IsSuggestionsOpen = Suggestions.Any();
                    });
                });
            }
            catch (UnauthorizedAccessException)
            {
                // Handle cases where access to a directory is denied
                App.Current.Dispatcher.Invoke(() =>
                {
                    Suggestions.Clear();
                    Suggestions.Add("Access Denied"); // Or handle more gracefully
                    IsSuggestionsOpen = true;
                });
            }
            catch (Exception ex) // Catch other potential IO errors
            {
                System.Diagnostics.Debug.WriteLine($"Error updating suggestions: {ex.Message}");
                App.Current.Dispatcher.Invoke(() =>
                {
                    Suggestions.Clear();
                    // Optionally add error message to suggestions
                    // Suggestions.Add("Error loading suggestions");
                    IsSuggestionsOpen = false;
                });
            }
        }

        private bool IsDriveOrPartialDrive(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            // Matches "C:", "C:\", "C:\folder"
            return System.Text.RegularExpressions.Regex.IsMatch(path, @"^[a-zA-Z]:(\\.*)?$");
        }
    }
}
