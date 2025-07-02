using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace dCompare.Options
{
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Handle null or non-boolean values
            if (value == null)
            {
                return Visibility.Collapsed; // Null values hide the control
            }

            // Handle int? specifically for ComparisonPercentage
            if (value is int?)
            {
                return value != null ? Visibility.Visible : Visibility.Collapsed;
            }

            // Handle boolean values
            if (value is bool boolValue)
            {
                return boolValue ? Visibility.Visible : Visibility.Collapsed;
            }

            // Default case for unexpected types
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}