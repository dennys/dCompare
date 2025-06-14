// LogicalTreeHelperExtensions.cs

using System.Windows;

namespace dCompare.Compare
{
    /// <summary>
    /// Provides extension methods for traversing the WPF logical tree.
    /// </summary>
    public static class LogicalTreeHelperExtensions
    {
        /// <summary>
        /// Finds the first parent of a given type in the logical tree.
        /// </summary>
        /// <typeparam name="T">The type of the parent to find.</typeparam>
        /// <param name="child">The starting dependency object.</param>
        /// <returns>The found parent or null if not found.</returns>
        public static T FindParent<T>(DependencyObject child) where T : DependencyObject
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
    }
}