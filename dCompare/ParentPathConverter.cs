using System;
using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;

namespace dCompare
{
    public class ParentPathConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string relativePath)
            {
                var parent = Path.GetDirectoryName(relativePath)?.Replace(Path.DirectorySeparatorChar, '/');
                return string.IsNullOrEmpty(parent) ? "(Root)" : parent;
            }
            return "(Root)";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
