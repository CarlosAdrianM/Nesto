using System;
using System.Globalization;
using System.Windows.Data;

namespace ControlesUsuario.Converters
{
    /// <summary>
    /// Formatea importes decimales como moneda salvo cuando el valor es el centinela
    /// -1, en cuyo caso devuelve el texto pasado por <c>ConverterParameter</c>.
    /// Uso típico (NestoAPI#185 / Nesto#353): campos como FaltaParaSalto o FinalTramo
    /// que el backend marca con -1 cuando no hay tramo superior al que saltar.
    ///
    /// Ejemplo:
    ///   Text="{Binding FaltaParaSalto,
    ///          Converter={StaticResource importeOTexto},
    ///          ConverterParameter='No hay más saltos'}"
    /// </summary>
    public class ImporteOTextoConverter : IValueConverter
    {
        public const decimal CentinelaSinLimite = -1m;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is decimal importe)
            {
                if (importe == CentinelaSinLimite)
                {
                    return parameter?.ToString() ?? string.Empty;
                }
                return importe.ToString("C", culture);
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value;
        }
    }
}
