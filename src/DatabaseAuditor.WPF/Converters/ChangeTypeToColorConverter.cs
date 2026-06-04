namespace DatabaseAuditor.WPF.Converters;

using DatabaseAuditor.Domain.Enums;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

public class ChangeTypeToColorConverter : IValueConverter
{
    public object Convert(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture)
    {
        if (value is not ChangeType changeType)
            return GetBrush("TextSecondaryBrush");

        return changeType switch
        {
            ChangeType.Added => GetBrush("AddedBrush"),
            ChangeType.Deleted => GetBrush("DeletedBrush"),
            ChangeType.Modified => GetBrush("ModifiedBrush"),
            ChangeType.Unchanged => GetBrush("UnchangedBrush"),
            _ => GetBrush("TextSecondaryBrush")
        };
    }

    public object ConvertBack(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture)
        => throw new NotImplementedException();

    private static Brush GetBrush(string key)
    {
        if (Application.Current.Resources[key] is Brush brush)
            return brush;

        return Brushes.Gray;
    }
}

public class ChangeTypeToBgColorConverter : IValueConverter
{
    public object Convert(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture)
    {
        if (value is not ChangeType changeType)
            return Brushes.Transparent;

        return changeType switch
        {
            ChangeType.Added => GetBrush("AddedBgBrush"),
            ChangeType.Deleted => GetBrush("DeletedBgBrush"),
            ChangeType.Modified => GetBrush("ModifiedBgBrush"),
            ChangeType.Unchanged => Brushes.Transparent,
            _ => Brushes.Transparent
        };
    }

    public object ConvertBack(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture)
        => throw new NotImplementedException();

    private static Brush GetBrush(string key)
    {
        if (Application.Current.Resources[key] is Brush brush)
            return brush;

        return Brushes.Transparent;
    }
}

public class ChangeTypeToTextConverter : IValueConverter
{
    public object Convert(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture)
    {
        if (value is not ChangeType changeType)
            return string.Empty;

        return changeType switch
        {
            ChangeType.Added => "● Added",
            ChangeType.Deleted => "● Deleted",
            ChangeType.Modified => "● Modified",
            ChangeType.Unchanged => "● Unchanged",
            _ => string.Empty
        };
    }

    public object ConvertBack(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture)
        => throw new NotImplementedException();
}