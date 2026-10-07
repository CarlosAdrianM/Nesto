using System.Linq;
using System.Text.RegularExpressions;

namespace Nesto.Infrastructure.Shared
{
    /// <summary>
    /// NestoAPI#596: réplica en Nesto del normalizador único de la API
    /// (NestoAPI/Infraestructure/Direcciones/CodigoPostal.cs). Mismas reglas; si cambian allí,
    /// cambian aquí (los tests son los mismos casos que CodigoPostalTests de la API).
    ///
    /// Formato canónico:
    ///   - España (país "ES" o vacío): 5 cifras; si vienen 4 se rellena el cero («8850» → «08850»).
    ///   - Portugal ("PT", o formato portugués de 7 cifras con cualquier país o sin él): «dddd-ddd»;
    ///     si solo hay 4 cifras y el país es PT, «dddd».
    ///   - Resto de países: recortado y en mayúsculas.
    /// Nunca rechaza: lo que no reconoce lo devuelve recortado (y en mayúsculas).
    /// </summary>
    public static class CodigoPostal
    {
        public const string ESPANA = "ES";
        public const string PORTUGAL = "PT";

        // 4 cifras + 3 cifras con separador opcional (guion o espacios, incluso «4480 - 670»).
        private static readonly Regex PortugalCompleto = new(@"^([1-9]\d{3})\s*-?\s*(\d{3})$");
        private static readonly Regex PortugalCorto = new(@"^[1-9]\d{3}$");
        private static readonly Regex CuatroCifras = new(@"^\d{4}$");
        private static readonly Regex CincoCifras = new(@"^\d{5}$");

        /// <summary>
        /// País ISO-2 a partir de lo que guarde cada tabla: "ES"/"PT", "ESP"/"PRT" o los numéricos
        /// (34/724 España, 351/620 Portugal). Otro texto, recortado y en mayúsculas; null, "".
        /// </summary>
        public static string PaisIso(string pais)
        {
            string p = (pais ?? string.Empty).Trim().ToUpperInvariant();
            return p switch
            {
                "ES" or "ESP" or "34" or "724" => ESPANA,
                "PT" or "PRT" or "351" or "620" => PORTUGAL,
                _ => p
            };
        }

        /// <summary>Solo las cifras del texto ("" si es null).</summary>
        public static string Digitos(string texto)
            => string.IsNullOrEmpty(texto) ? string.Empty : new string(texto.Where(char.IsDigit).ToArray());

        /// <summary>¿Tiene forma de CP portugués, sin saber el país? 4 cifras o 4+3 con o sin separador.</summary>
        public static bool TieneFormatoPortugues(string texto)
        {
            string t = Limpiar(texto);
            return PortugalCompleto.IsMatch(t) || PortugalCorto.IsMatch(t);
        }

        /// <summary>
        /// ¿Es un CP portugués? Con país PT, si tiene forma portuguesa (4 o 7 cifras). Con España o sin
        /// país, solo si tiene 7 cifras. Con otro país, no.
        /// </summary>
        public static bool EsPortugues(string texto, string paisIso = null)
        {
            string iso = PaisIso(paisIso);
            string t = Limpiar(texto);
            if (iso == PORTUGAL)
            {
                return PortugalCompleto.IsMatch(t) || PortugalCorto.IsMatch(t);
            }
            if (iso.Length == 0 || iso == ESPANA)
            {
                return PortugalCompleto.IsMatch(t);
            }
            return false;
        }

        /// <summary>¿Es un CP español (01000-52999) una vez normalizado? Con otro país explícito, no.</summary>
        public static bool EsEspanol(string texto, string paisIso = null)
        {
            string iso = PaisIso(paisIso);
            if (iso.Length != 0 && iso != ESPANA)
            {
                return false;
            }
            string cp = Normalizar(texto, ESPANA);
            return cp != null && CincoCifras.IsMatch(cp)
                && int.TryParse(cp, out int numero) && numero >= 1000 && numero <= 52999;
        }

        /// <summary>Formato canónico para guardar. null se queda null; lo que no se reconoce, recortado y en mayúsculas.</summary>
        public static string Normalizar(string texto, string paisIso = null)
        {
            if (texto == null)
            {
                return null;
            }
            string iso = PaisIso(paisIso);
            string t = Limpiar(texto);

            if (iso.Length == 0 || iso == ESPANA || iso == PORTUGAL)
            {
                Match completo = PortugalCompleto.Match(t);
                if (completo.Success)
                {
                    return completo.Groups[1].Value + "-" + completo.Groups[2].Value;
                }
            }
            if (iso.Length == 0 || iso == ESPANA)
            {
                if (CuatroCifras.IsMatch(t) && t[0] != '0')
                {
                    return "0" + t;
                }
            }
            return t.ToUpperInvariant();
        }

        /// <summary>¿Son el mismo código postal aunque estén escritos en distinto formato («4430 999» = «4430-999»)?</summary>
        public static bool MismoCodigo(string uno, string otro, string paisIso = null)
        {
            string a = Normalizar(uno, paisIso);
            string b = Normalizar(otro, paisIso);
            return !string.IsNullOrEmpty(a) && a == b;
        }

        // Trim de todo blanco (los char de la BD vienen rellenados, y llegan espacios duros de
        // autocompletados) y los blancos interiores repetidos se quedan en uno.
        private static string Limpiar(string texto)
        {
            if (string.IsNullOrEmpty(texto))
            {
                return string.Empty;
            }
            string t = texto.Replace(' ', ' ').Trim();
            return Regex.Replace(t, @"\s+", " ");
        }
    }
}
