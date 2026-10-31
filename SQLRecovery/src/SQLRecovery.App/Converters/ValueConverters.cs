using System.Globalization;
using System.Windows;
using System.Windows.Data;
using SQLRecovery.Export;

namespace SQLRecovery.App.Converters;

/// <summary>Renders a SQL value as display text, truncating very large payloads.</summary>
public sealed class CellTextConverter : IValueConverter
{
    private const int MaxDisplayLength = 512;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        string text;

        try
        {
            text = ValueFormatter.ToInvariantString(value);
        }
        catch
        {
            text = value?.ToString() ?? string.Empty;
        }

        if (text.Length > MaxDisplayLength)
            return text[..MaxDisplayLength] + "…";

        return text;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class FileSizeConverter : IValueConverter
{
    private static readonly string[] Units = { "B", "KB", "MB", "GB", "TB" };

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        double size = value switch
        {
            long l => l,
            int i => i,
            _ => 0
        };

        int unit = 0;

        while (size >= 1024 && unit < Units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        return unit == 0
            ? $"{size:0} {Units[unit]}"
            : $"{size:0.##} {Units[unit]}";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is Visibility.Visible;
}

public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is Visibility.Collapsed;
}

public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not true;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not true;
}

public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null)
            return Visibility.Collapsed;

        if (value is string text)
            return string.IsNullOrWhiteSpace(text) ? Visibility.Collapsed : Visibility.Visible;

        return Visibility.Visible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
