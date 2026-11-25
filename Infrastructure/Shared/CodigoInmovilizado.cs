using System.Linq;

namespace Nesto.Infrastructure.Shared
{
    /// <summary>
    /// Inmovilizados (Carlos 25/11/26): la nomenclatura de la casa para un inmovilizado es «I/1823» (I, barra y el número). Es lo
    /// que lleva la etiqueta pegada en la máquina, también en su código de barras. LA REGLA (pura): «I/», sin distinguir
    /// mayúsculas, seguido SOLO de cifras; los espacios de alrededor no cuentan. En una línea de pedido de inmovilizado
    /// (TipoLinea 3) el «Producto» es solo el número («1823»).
    /// </summary>
    public static class CodigoInmovilizado
    {
        public const string PREFIJO = "I/";

        /// <summary>True si el texto es un código de inmovilizado («I/1823», «i/1823 »); en <paramref name="numero"/>, «1823».</summary>
        public static bool TryLeer(string texto, out string numero)
        {
            numero = null;
            string limpio = texto?.Trim();
            if (string.IsNullOrEmpty(limpio) || limpio.Length <= PREFIJO.Length)
            {
                return false;
            }
            if (char.ToUpperInvariant(limpio[0]) != 'I' || limpio[1] != '/')
            {
                return false;
            }
            string cifras = limpio.Substring(PREFIJO.Length);
            if (!cifras.All(c => c >= '0' && c <= '9'))
            {
                return false;
            }
            numero = cifras;
            return true;
        }

        public static bool EsCodigo(string texto) => TryLeer(texto, out _);

        /// <summary>«1823» → «I/1823» (sin espacios). Vacío si no hay número.</summary>
        public static string Formatear(string numero)
        {
            string limpio = numero?.Trim();
            return string.IsNullOrEmpty(limpio) ? string.Empty : PREFIJO + limpio;
        }
    }
}
