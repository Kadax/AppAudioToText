using System.Globalization;
using System.Windows.Data;

namespace SpeechToText.Converters;

/// <summary>
/// Инвертирует логическое значение.
/// </summary>
public sealed class InverseBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is not true;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is not true;
}
