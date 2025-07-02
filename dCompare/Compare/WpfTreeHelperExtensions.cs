// WpfTreeHelperExtensions.cs

using System.Windows;
using System.Windows.Media;

namespace dCompare.Compare
{
    /// <summary>
    /// Provides extension methods for traversing the WPF logical and visual trees.
    /// </summary>
    public static class WpfTreeHelperExtensions
    {
        /// <summary>
        /// Finds the first parent of a given type in the logical tree.
        /// </summary>
        /// <typeparam name="T">The type of the parent to find.</typeparam>
        /// <param name="child">The starting dependency object.</param>
        /// <returns>The found parent or null if not found.</returns>
        public static T? FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            while (child != null)
            {
                child = LogicalTreeHelper.GetParent(child);
                if (child is T parent)
                {
                    return parent;
                }
            }
            return null;
        }

        // Helper method to traverse the visual tree
        public static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null)
                return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T found)
                {
                    return found;
                }

                var result = FindVisualChild<T>(child);
                if (result != null)
                {
                    return result;
                }
            }
            return null;
        }
    }
}