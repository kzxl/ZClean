using System.Globalization;
using System.Windows.Data;

namespace ZeroClean.UI.Converters;

public class BytesToHumanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not long bytes)
        {
            if (value is int intVal)
                bytes = intVal;
            else
                return "0 B";
        }

        if (bytes <= 0) return "0 B";

        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int counter = 0;
        decimal number = bytes;
        while (Math.Round(number / 1024) >= 1 && counter < suffixes.Length - 1)
        {
            number /= 1024;
            counter++;
        }
        return $"{number:n1} {suffixes[counter]}";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw exoticException();
    }

    private static NotImplementedException exoticException() => new();
}
