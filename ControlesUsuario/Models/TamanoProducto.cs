using System.Globalization;
using System.Text.RegularExpressions;

namespace ControlesUsuario.Models
{
    /// <summary>
    /// Sugerencia 1339 (Paloma, 18/11/26): el tamaño del producto en la lista del buscador («50 ml», «138 gr»).
    /// Hay productos con el mismo nombre y familia que solo se distinguen por el tamaño (44256 y 43493). Regla pura.
    /// </summary>
    public static class TamanoProducto
    {
        private static readonly CultureInfo CULTURA = CultureInfo.GetCultureInfo("es-ES");

        /// <summary>
        /// «50 ml»: sin decimales inútiles y con la unidad en minúsculas. Null si no hay tamaño (null o &lt;= 0) o no hay unidad.
        /// </summary>
        public static string Texto(decimal? tamanno, string unidadMedida)
        {
            if (!tamanno.HasValue || tamanno.Value <= 0 || string.IsNullOrWhiteSpace(unidadMedida))
            {
                return null;
            }
            return $"{tamanno.Value.ToString("0.###", CULTURA)} {unidadMedida.Trim().ToLower(CULTURA)}";
        }

        /// <summary>
        /// El nombre con el tamaño detrás («HIDRATANTE OPTIMA CREAM 50 ml»). Si no hay tamaño, o el nombre ya lo dice
        /// («… 50 ML»), el nombre tal cual.
        /// </summary>
        public static string NombreConTamano(string nombre, decimal? tamanno, string unidadMedida)
        {
            string tamano = Texto(tamanno, unidadMedida);
            if (tamano == null)
            {
                return nombre;
            }
            if (string.IsNullOrWhiteSpace(nombre))
            {
                return tamano;
            }
            if (YaLoDice(nombre, tamanno.Value, unidadMedida.Trim()))
            {
                return nombre;
            }
            return $"{nombre.TrimEnd()} {tamano}";
        }

        private static bool YaLoDice(string nombre, decimal tamanno, string unidad)
        {
            string numero = tamanno.ToString("0.###", CULTURA);
            // «150 ML» no dice «50 ml»: el número no puede ir pegado a otra cifra ni la unidad a otra letra
            string patron = $@"(?<![\d.,]){Regex.Escape(numero)}\s?{Regex.Escape(unidad)}(?![\p{{L}}\d])";
            return Regex.IsMatch(nombre, patron, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }
    }
}
