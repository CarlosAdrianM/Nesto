using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ControlesUsuario
{
    /// <summary>
    /// La lógica del <see cref="SelectorCorreo"/> sin WPF: qué correos se ofrecen (sin repetir, sin vacíos, separando los que
    /// vienen juntos con «;» o «,») y si lo escrito tiene forma de correo.
    /// </summary>
    public static class CorreosElegibles
    {
        private static readonly Regex FORMATO = new(@"^[^@\s;,]+@[^@\s;,]+\.[^@\s;,.]{2,}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly char[] SEPARADORES = { ';', ',' };

        /// <summary>Vacío vale (el correo es opcional); si hay algo, tiene que tener forma de correo (uno solo).</summary>
        public static bool EsValido(string correo) => string.IsNullOrWhiteSpace(correo) || FORMATO.IsMatch(correo.Trim());

        /// <summary>Los correos que se ofrecen, en el orden en que llegan, sin repetir (sin distinguir mayúsculas) y sin los que no son correos.</summary>
        public static List<string> Unicos(IEnumerable<string> correos)
        {
            var lista = new List<string>();
            if (correos == null)
            {
                return lista;
            }
            foreach (string correo in correos
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .SelectMany(c => c.Split(SEPARADORES, StringSplitOptions.RemoveEmptyEntries))
                .Select(c => c.Trim())
                .Where(c => c.Length > 0 && FORMATO.IsMatch(c)))
            {
                if (!lista.Any(c => string.Equals(c, correo, StringComparison.OrdinalIgnoreCase)))
                {
                    lista.Add(correo);
                }
            }
            return lista;
        }

        /// <summary>
        /// Qué correo queda puesto cuando cambian los que se ofrecen: si el usuario no ha escrito nada (o lo que había lo puso el
        /// propio selector), el primero de la lista nueva; si lo escribió él, se respeta.
        /// </summary>
        /// <param name="actual">Lo que hay escrito.</param>
        /// <param name="puestoPorElSelector">Lo último que puso el selector solo (null si nada).</param>
        /// <param name="nuevos">Los correos que se ofrecen ahora (ya sin repetir).</param>
        public static string CorreoTrasCambiarLista(string actual, string puestoPorElSelector, IReadOnlyList<string> nuevos)
        {
            bool loEscribioElUsuario = !string.IsNullOrWhiteSpace(actual)
                && !string.Equals(actual?.Trim(), puestoPorElSelector?.Trim(), StringComparison.OrdinalIgnoreCase);
            if (loEscribioElUsuario)
            {
                return actual;
            }
            return nuevos != null && nuevos.Count > 0 ? nuevos[0] : string.Empty;
        }
    }
}
