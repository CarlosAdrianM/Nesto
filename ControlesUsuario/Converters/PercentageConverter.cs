using System;
using System.Diagnostics;
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
            Debug.WriteLine($"[PercentageConverter.Convert] value={value}, type={value?.GetType().Name}");

            if (value == null) return "0,00 %";

            if (decimal.TryParse(value.ToString(), out decimal fraction))
            {
                var result = fraction.ToString("P2", culture);
                Debug.WriteLine($"[PercentageConverter.Convert] result={result}");
                return result;
            }
            return "0,00 %";
        }

        /// <summary>
        /// UI ("30" o "30 %" o "30,00 %") -> Modelo (0.30)
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            Debug.WriteLine($"[PercentageConverter.ConvertBack] value={value}, type={value?.GetType().Name}");

            if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
            {
                Debug.WriteLine($"[PercentageConverter.ConvertBack] value es null/vacío, retornando 0");
                return 0m;
            }

            // Issue #266: Si el valor ya es un Decimal/Double (no viene del TextBox), devolverlo tal cual
            // Esto puede pasar en DataGrid cuando el binding pasa el valor del source en vez del TextBox
            if (value is decimal decValue)
            {
                Debug.WriteLine($"[PercentageConverter.ConvertBack] value es Decimal, retornando tal cual: {decValue}");
                return decValue;
            }
            if (value is double dblValue)
            {
                Debug.WriteLine($"[PercentageConverter.ConvertBack] value es Double, retornando tal cual: {dblValue}");
                return (decimal)dblValue;
            }

            string valueString = value.ToString().Trim();
            Debug.WriteLine($"[PercentageConverter.ConvertBack] valueString después de Trim: '{valueString}'");

            // Quitar el símbolo de porcentaje si existe
            valueString = valueString.Replace("%", "").Trim();
            Debug.WriteLine($"[PercentageConverter.ConvertBack] valueString después de quitar %: '{valueString}'");

            if (!decimal.TryParse(valueString, NumberStyles.Any, culture, out decimal parsedValue))
            {
                Debug.WriteLine($"[PercentageConverter.ConvertBack] TryParse con cultura {culture.Name} falló, intentando InvariantCulture");
                // Si falla con la cultura actual, intentar con cultura invariante
                if (!decimal.TryParse(valueString, NumberStyles.Any, CultureInfo.InvariantCulture, out parsedValue))
                {
                    Debug.WriteLine($"[PercentageConverter.ConvertBack] TryParse con InvariantCulture también falló, retornando 0");
                    return 0m;
                }
            }

            var result = parsedValue / 100m;
            Debug.WriteLine($"[PercentageConverter.ConvertBack] parsedValue={parsedValue}, result={result}");
            return result;
        }
    }
}
