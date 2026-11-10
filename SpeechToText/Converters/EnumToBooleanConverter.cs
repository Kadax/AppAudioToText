using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SpeechToText.Converters;

/// <summary>
/// Сравнивает значение перечисления с параметром конвертера.
/// Используется для привязки RadioButton к свойству-перечислению.
/// </summary>
public sealed class EnumToBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is not null
            && parameter is string expected
            && string.Equals(value.ToString(), expected, StringComparison.OrdinalIgnoreCase);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is true && parameter is string expected
            && targetType.IsEnum
            && Enum.TryParse(targetType, expected, ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        return Binding.DoNothing;
    }
}
