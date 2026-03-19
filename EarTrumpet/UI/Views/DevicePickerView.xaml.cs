using EarTrumpet.UI.ViewModels;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace EarTrumpet.UI.Views
{
    public partial class DevicePickerView : UserControl
    {
        public DevicePickerView()
        {
            InitializeComponent();
        }
    }

    public class FormFactorToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is uint ff)
            {
                switch (ff)
                {
                    case 3: case 5: return "\xE7F6";
                    case 1: case 2: return "\xE7F5";
                    case 4: return "\xE720";
                    default: return "\xE7F5";
                }
            }
            return "\xE7F5";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class EnumeratorToBadgeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var e = (value as string)?.ToUpperInvariant() ?? "";
            if (e.Contains("BTHENUM") || e.Contains("BLUETOOTH")) return "Bluetooth";
            if (e.Contains("USB")) return "USB";
            if (e.Contains("HDAUDIO") || e.Contains("HDA")) return "Jack 3.5";
            return e.Length > 12 ? e.Substring(0, 10) + "..." : e;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// MultiValueConverter: position, duration, containerWidth → fill width
    /// </summary>
    public class ProgressBarWidthConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length == 3 &&
                values[0] is double position &&
                values[1] is double duration &&
                values[2] is double containerWidth &&
                duration > 0)
            {
                return Math.Max(0, Math.Min(containerWidth, (position / duration) * containerWidth));
            }
            return 0.0;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
