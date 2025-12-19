using System;
using System.Globalization;
using System.Windows.Data;

namespace ControlesUsuario.Converters
{
    /// <summary>
    /// Convierte entre valores decimales (0.30) y porcentajes para UI ("30,00 %")
    /// El usuario escribe valores entre 0 y 100, internamente se guarda entre 0 y 1
    /// </summary>
    public class PercentageConverter : IValueConverter
    {
        /// <summary>
        /// Modelo (0.25) -> UI ("25,00 %")
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return "0,00 %";

            if (decimal.TryParse(value.ToString(), out decimal fraction))
            {
                return fraction.ToString("P2", culture);
            }
            return "0,00 %";
        }

        /// <summary>
        /// UI ("30" o "30 %" o "30,00 %") -> Modelo (0.30)
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
            {
                return 0m;
            }

            // Issue #266: Si el valor ya es un Decimal/Double (no viene del TextBox), devolverlo tal cual
            // Esto puede pasar en DataGrid cuando el binding pasa el valor del source en vez del TextBox
            if (value is decimal decValue)
            {
                return decValue;
            }
            if (value is double dblValue)
            {
                return (decimal)dblValue;
            }

            string valueString = value.ToString().Trim();

            // Quitar el símbolo de porcentaje si existe
            valueString = valueString.Replace("%", "").Trim();

            if (!decimal.TryParse(valueString, NumberStyles.Any, culture, out decimal parsedValue))
            {
                // Si falla con la cultura actual, intentar con cultura invariante
                if (!decimal.TryParse(valueString, NumberStyles.Any, CultureInfo.InvariantCulture, out parsedValue))
                {
                    return 0m;
                }
            }

            // El usuario escribe 30 para representar 30%, internamente guardamos 0.30
            return parsedValue / 100m;
        }
    }
}
