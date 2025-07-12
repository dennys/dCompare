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
                    string localCurrentPath = currentPath; // Make a local copy for lambda
                    List<string> newSuggestions = new List<string>();

                    try
                    {
                        string? effectiveDirectoryName = null;
                        string searchPattern = "";

                        // Case 1: Input is a drive letter like "D:\" or "D:"
                        if (IsDriveRoot(localCurrentPath))
                        {
                            effectiveDirectoryName = System.IO.Path.GetPathRoot(localCurrentPath); // Ensures "D:\"
                            if (effectiveDirectoryName != null && Directory.Exists(effectiveDirectoryName))
                            {
                                searchPattern = "*"; // Get all items in root
                            }
                            else
                            {
                                // Invalid drive or drive not ready
                                App.Current.Dispatcher.Invoke(() => Suggestions.Clear());
                                return;
                            }
                        }
                        else // Case 2: Input is a partial path like "D:\folder" or "D:\fol"
                        {
                            effectiveDirectoryName = System.IO.Path.GetDirectoryName(localCurrentPath);
                            searchPattern = System.IO.Path.GetFileName(localCurrentPath);

                            if (string.IsNullOrEmpty(effectiveDirectoryName) && IsDriveOrPartialDrive(localCurrentPath))
                            {
                                // e.g. user typed "D", GetDirectoryName is null.
                                // We interpret this as wanting to list drives, or if "D:", list contents of D:\
                                // This case is tricky, for now, let's assume if it's like "D:", IsDriveRoot handles it.
                                // If it's just "D", we might not have enough to search.
                                // However, our IsDriveOrPartialDrive includes "D:", which is covered by IsDriveRoot.
                                // So this block might be for "D" which is not a valid directory.
                                App.Current.Dispatcher.Invoke(() => Suggestions.Clear());
                                return;
                            }

                            if (string.IsNullOrEmpty(effectiveDirectoryName) || !Directory.Exists(effectiveDirectoryName))
                            {
                                // Base directory doesn't exist. Try to search from root.
                                // e.g., currentPath = "D:\nonexistent\partial"
                                // We search on "D:\" for "nonexistent*"
                                string? root = System.IO.Path.GetPathRoot(localCurrentPath);
                                if (!string.IsNullOrEmpty(root) && Directory.Exists(root))
                                {
                                    searchPattern = localCurrentPath.Substring(root.Length) + "*";
                                    effectiveDirectoryName = root;
                                }
                                else
                                {
                                    App.Current.Dispatcher.Invoke(() => Suggestions.Clear());
                                    return; // Cannot determine a valid search base
                                }
                            }
                            else
                            {
                                // Base directory exists, search within it
                                searchPattern += "*";
                            }
                        }

                        if (effectiveDirectoryName == null || !Directory.Exists(effectiveDirectoryName))
                        {
                            App.Current.Dispatcher.Invoke(() => { Suggestions.Clear(); IsSuggestionsOpen = false; });
                            return;
                        }

                        // Perform the top-directory search
                        try
                        {
                            newSuggestions.AddRange(Directory.GetDirectories(effectiveDirectoryName, searchPattern, SearchOption.TopDirectoryOnly));
                        }
                        catch (UnauthorizedAccessException) { /* Log or ignore for this part */ }
                        catch (IOException) { /* Log or ignore */ }


                        // Recursive search for deeper matches if the current path is an existing directory
                        if (Directory.Exists(localCurrentPath))
                        {
                            try
                            {
                                var deeperSuggestions = Directory.EnumerateDirectories(localCurrentPath, "*", SearchOption.AllDirectories)
                                                                 .Take(20); // Limit recursive results
                                newSuggestions.AddRange(deeperSuggestions);
                            }
                            catch (UnauthorizedAccessException) { /* Fine-grained catch for recursion */ }
                            catch (IOException) { /* Fine-grained catch for recursion */ }
                        }

                        newSuggestions = newSuggestions.Distinct().OrderBy(s => s.Length).ThenBy(s => s).ToList();

                        // Ensure the current path itself is in suggestions if it's a valid directory
                        if (Directory.Exists(localCurrentPath) && !newSuggestions.Contains(localCurrentPath))
                        {
                            newSuggestions.Insert(0, localCurrentPath);
                        }
                    }
                    catch (UnauthorizedAccessException uae)
                    {
                        System.Diagnostics.Debug.WriteLine($"Outer UnauthorizedAccessException: {uae.Message}");
                        App.Current.Dispatcher.Invoke(() =>
                        {
                            Suggestions.Clear();
                            Suggestions.Add($"Access Denied: {uae.Message}");
                            IsSuggestionsOpen = true;
                        });
                        return; // Exit after handling top-level access denial
                    }
                    catch (Exception ex) // Catch other potential IO errors
                    {
                        System.Diagnostics.Debug.WriteLine($"Outer error updating suggestions: {ex.Message}");
                        App.Current.Dispatcher.Invoke(() => { Suggestions.Clear(); IsSuggestionsOpen = false; });
                        return; // Exit on other errors
                    }


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
            catch (Exception ex) // This outer catch for await Task.Run should ideally not be hit if inner catches are good.
            {
                // This might catch exceptions from Task.Run itself or if rethrown weirdly.
                System.Diagnostics.Debug.WriteLine($"Critical error in suggestion task: {ex.Message}");
                App.Current.Dispatcher.Invoke(() =>
                {
                    Suggestions.Clear();
                    IsSuggestionsOpen = false;
                });
            }
        }

        private bool IsDriveRoot(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            // Matches "C:\", "D:\", etc. or "C:", "D:"
            // Path.GetPathRoot will return "C:\" for "C:\anything" or "C:"
            // We want to ensure it's *just* the root.
            string? pathRoot = System.IO.Path.GetPathRoot(path);
            return !string.IsNullOrEmpty(pathRoot) && path.Equals(pathRoot, StringComparison.OrdinalIgnoreCase) ||
                   path.Equals(pathRoot.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);

        }

        private bool IsDriveOrPartialDrive(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            // Matches "C:", "C:\", "C:\folder"
            // Ensures it starts with a drive letter pattern.
            return System.Text.RegularExpressions.Regex.IsMatch(path, @"^[a-zA-Z]:(\\.*)?$");
        }
    }
}
