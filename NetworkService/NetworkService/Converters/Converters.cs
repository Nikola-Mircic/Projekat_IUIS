using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace NetworkService.Converters
{
    // bool → Visibility
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c) =>
            value is true ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object value, Type t, object p, CultureInfo c) =>
            throw new NotImplementedException();
    }

    // string → Visibility (vidljivo ako nije prazan string)
    public class StringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c) =>
            string.IsNullOrEmpty(value?.ToString())
                ? Visibility.Collapsed : Visibility.Visible;

        public object ConvertBack(object value, Type t, object p, CultureInfo c) =>
            throw new NotImplementedException();
    }

    // bool (IsOutOfRange) → boja teksta
    public class BoolToColorConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c) =>
            value is true
                ? new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26))  // crvena
                : new SolidColorBrush(Color.FromRgb(0x16, 0xA3, 0x4A)); // zelena

        public object ConvertBack(object value, Type t, object p, CultureInfo c) =>
            throw new NotImplementedException();
    }

    // bool (IsOutOfRange) → pozadinska boja canvas ćelije
    public class BoolToAlarmBrushConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c) =>
            value is true
                ? new SolidColorBrush(Color.FromRgb(0xFF, 0xF0, 0xF0))  // svetlo crvena
                : new SolidColorBrush(Color.FromRgb(0xF0, 0xFF, 0xF4)); // svetlo zelena

        public object ConvertBack(object value, Type t, object p, CultureInfo c) =>
            throw new NotImplementedException();
    }
}