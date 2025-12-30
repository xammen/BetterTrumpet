using System;
using System.Globalization;
using System.Windows.Data;

namespace EarTrumpet
{
    /// <summary>
    /// Converts null to true, non-null to false.
    /// Used to check if a custom brush is set.
    /// </summary>
    public class NullToBoolConverter : IValueConverter
    {
        public static readonly NullToBoolConverter Instance = new NullToBoolConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value == null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
