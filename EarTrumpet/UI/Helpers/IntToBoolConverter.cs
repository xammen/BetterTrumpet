using System;
using System.Globalization;
using System.Windows.Data;

namespace EarTrumpet.UI.Helpers
{
    /// <summary>
    /// Converts an int to a bool for RadioButton binding.
    /// Returns true if the value equals the ConverterParameter.
    /// </summary>
    public class IntToBoolConverter : IValueConverter
    {
        public static readonly IntToBoolConverter Instance = new IntToBoolConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null) return false;
            
            int intValue = (int)value;
            int paramValue = int.Parse(parameter.ToString());
            
            return intValue == paramValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null) return 60;
            
            bool boolValue = (bool)value;
            if (!boolValue) return Binding.DoNothing;
            
            return int.Parse(parameter.ToString());
        }
    }
}
