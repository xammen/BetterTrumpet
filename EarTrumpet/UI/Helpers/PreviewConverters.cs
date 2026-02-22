using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace EarTrumpet
{
    /// <summary>
    /// Multiplies a width by a percentage parameter (e.g. "0.70") for the theme preview slider fill.
    /// </summary>
    public class PercentWidthConverter : IValueConverter
    {
        public static readonly PercentWidthConverter Instance = new PercentWidthConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double width && parameter is string pctStr &&
                double.TryParse(pctStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double pct))
            {
                return Math.Max(0, width * pct);
            }
            return 0.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts a width + percentage into a Thickness for positioning the thumb circle.
    /// The thumb is offset by (width * pct - thumbRadius).
    /// </summary>
    public class ThumbMarginConverter : IValueConverter
    {
        public static readonly ThumbMarginConverter Instance = new ThumbMarginConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double width && parameter is string pctStr &&
                double.TryParse(pctStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double pct))
            {
                double left = Math.Max(0, width * pct - 7); // 7 = half of 14px thumb
                return new Thickness(left, 0, 0, 0);
            }
            return new Thickness(0);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
