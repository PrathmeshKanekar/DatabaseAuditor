namespace DatabaseAuditor.WPF.Converters;

using System.Globalization;
using System.Windows;
using System.Windows.Data;

public class BoolToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; } = false;
    public bool UseHidden { get; set; } = false;

    public object Convert(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture)
    {
        var boolValue = value is bool b && b;

        if (Invert) boolValue = !boolValue;

        return boolValue
            ? Visibility.Visible
            : (UseHidden ? Visibility.Hidden : Visibility.Collapsed);
    }

    public object ConvertBack(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture)
    {
        if (value is not Visibility visibility)
            return false;

        var result = visibility == Visibility.Visible;
        return Invert ? !result : result;
    }
}

public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture)
    {
        var boolValue = value is bool b && b;
        return boolValue ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture)
    {
        if (value is not Visibility visibility)
            return false;

        return visibility != Visibility.Visible;
    }
}

public class NullToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; } = false;

    public object Convert(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture)
    {
        var isNull = value == null;
        if (Invert) isNull = !isNull;
        return isNull ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture)
        => throw new NotImplementedException();
}

public class ZeroToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; } = false;

    public object Convert(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture)
    {
        var isZero = value is int i && i == 0
                  || value is long l && l == 0;

        if (Invert) isZero = !isZero;
        return isZero ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture)
        => throw new NotImplementedException();
}

public class BoolToInverseBoolConverter : IValueConverter
{
    public object Convert(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture)
        => value is bool b && !b;

    public object ConvertBack(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture)
        => value is bool b && !b;
}