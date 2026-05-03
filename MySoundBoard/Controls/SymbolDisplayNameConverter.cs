using System.Globalization;
using System.Windows.Data;
using MySoundBoard.Utilities;
using Wpf.Ui.Controls;

namespace MySoundBoard.Controls
{
    public class SymbolDisplayNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is SymbolRegular symbol)
                return IconNameFormatter.FormatDisplayName(symbol.ToString());
            return value?.ToString() ?? string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
