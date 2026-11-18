using System;
using System.Globalization;
using System.Windows.Data;

namespace ControlesUsuario.Converters
{
    /// <summary>
    /// Para un grupo de RadioButton enlazado a una sola propiedad: marcado si el valor es igual al ConverterParameter; al
    /// marcarlo devuelve el parámetro y al desmarcarlo no toca nada (lo pone el que se marca).
    /// </summary>
    public class IgualAConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => Equals(value, parameter);

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is true ? parameter : Binding.DoNothing;
    }
}
