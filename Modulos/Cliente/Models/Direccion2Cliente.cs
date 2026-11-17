using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Nesto.Modulos.Cliente.Models
{
    /// <summary>Nesto#643 (NestoAPI#874): una opción de «¿Qué es?» (Codigo = el TipoLugar que viaja a la API).</summary>
    public class TipoLugarModel
    {
        public string Codigo { get; set; }
        public string Nombre { get; set; }
    }

    /// <summary>
    /// Nesto#643 (NestoAPI#874, Carlos 17/11/26): en «Número» solo va el número del portal (cifras con letra o BIS opcionales,
    /// o «S/N»). Espejo de NestoAPI.Infraestructure.Clientes.NumeroPropioDireccion.FormatoValido: «Nº 99», «99 local 6» o
    /// «Edificio Rojo» no valen nunca (lo demás va en «¿Qué es?»).
    /// </summary>
    public static class NumeroPortal
    {
        public const string MOTIVO_FORMATO = "En «Número» solo va el número del portal (por ejemplo, 99) o S/N. " +
            "Lo demás (piso, local, edificio, rótulo…) va en «¿Qué es?».";

        private static readonly Regex FORMATO = new Regex(@"^\d{1,5}(BIS|[A-Z])?$");
        private static readonly Regex SIN_NUMERO = new Regex(@"^(S\s*[/.\-]?\s*N(º|O|\.)?|SIN\s+N(Ú|U)MERO)\.?$", RegexOptions.IgnoreCase);

        public static bool FormatoValido(string numero)
        {
            if (string.IsNullOrWhiteSpace(numero))
            {
                return false;
            }
            string limpio = numero.Trim();
            return SIN_NUMERO.IsMatch(limpio) || FORMATO.IsMatch(Regex.Replace(limpio, @"\s+", string.Empty).ToUpperInvariant());
        }
    }

    /// <summary>
    /// Nesto#643 (gemela de NestoAPI#874): espejo en el cliente de la regla de la API (NestoAPI.Infraestructure.Clientes.Direccion2Cliente)
    /// para que el usuario vea en el asistente lo que le falta y cómo quedará, sin esperar al error del servidor. Quien manda es la API:
    /// mismos tipos, mismos textos y misma composición («1º D», «LOCAL 6 · RÓTULO: EL EDÉN», «CASA», «NAVE 4»).
    /// </summary>
    public static class Direccion2Cliente
    {
        public const string PISO = "Piso";
        public const string LOCAL = "Local";
        public const string CASA = "Casa";
        public const string NAVE = "Nave";
        public const string OTRO = "Otro";
        public const int LONGITUD = 100;

        public static readonly IReadOnlyList<TipoLugarModel> TIPOS = new[]
        {
            new TipoLugarModel { Codigo = PISO, Nombre = "Piso" },
            new TipoLugarModel { Codigo = LOCAL, Nombre = "Local" },
            new TipoLugarModel { Codigo = CASA, Nombre = "Casa / chalet" },
            new TipoLugarModel { Codigo = NAVE, Nombre = "Nave / polígono" },
            new TipoLugarModel { Codigo = OTRO, Nombre = "Otro" }
        };

        public const string MOTIVO_SIN_TIPO = "Indique qué es la dirección (piso, local, casa o chalet, nave u otro): " +
            "sin eso el repartidor no la encuentra.";
        public const string MOTIVO_PISO = "En un piso indique el piso y la puerta (por ejemplo, 1º D).";
        public const string MOTIVO_LOCAL = "En un local indique qué pone en el rótulo de la entrada: es lo que busca el repartidor.";
        public const string MOTIVO_NAVE = "En una nave indique su número.";
        public const string MOTIVO_OTRO = "Describa el lugar en «Otro» (por ejemplo, «CENTRO COMERCIAL, PLANTA 2»).";

        private static readonly CultureInfo ESPANOL = new CultureInfo("es-ES");
        private static readonly Regex PISO_NUMERICO = new Regex(@"^(\d+)\s*[ºª°oO]?$");
        private static readonly Regex ESPACIOS = new Regex(@"\s+");

        /// <summary>Lo que se guardará en Dirección 2, o null si falta algo (y entonces <paramref name="motivo"/> dice qué, de usted).</summary>
        public static string Componer(string tipo, string piso, string puerta, string numeroLocal, string rotulo, string numeroNave, string otroTexto,
            out string motivo)
        {
            motivo = null;
            string texto;
            switch (TIPOS.FirstOrDefault(t => string.Equals(t.Codigo, tipo?.Trim(), StringComparison.OrdinalIgnoreCase))?.Codigo)
            {
                case PISO:
                    string elPiso = Limpiar(piso), laPuerta = Limpiar(puerta);
                    if (elPiso == null || laPuerta == null)
                    {
                        motivo = MOTIVO_PISO;
                        return null;
                    }
                    Match numerico = PISO_NUMERICO.Match(elPiso);
                    texto = (numerico.Success ? numerico.Groups[1].Value + "º" : elPiso) + " " + laPuerta;
                    break;
                case LOCAL:
                    string elRotulo = Limpiar(rotulo);
                    if (elRotulo == null)
                    {
                        motivo = MOTIVO_LOCAL;
                        return null;
                    }
                    string elLocal = Limpiar(numeroLocal);
                    texto = (elLocal == null ? "LOCAL" : "LOCAL " + elLocal) + " · RÓTULO: " + elRotulo;
                    break;
                case CASA:
                    texto = "CASA";
                    break;
                case NAVE:
                    string laNave = Limpiar(numeroNave);
                    if (laNave == null)
                    {
                        motivo = MOTIVO_NAVE;
                        return null;
                    }
                    texto = "NAVE " + laNave;
                    break;
                case OTRO:
                    texto = Limpiar(otroTexto);
                    if (texto == null)
                    {
                        motivo = MOTIVO_OTRO;
                        return null;
                    }
                    break;
                default:
                    motivo = MOTIVO_SIN_TIPO;
                    return null;
            }
            if (texto.Length > LONGITUD)
            {
                motivo = $"La dirección 2 («{texto}») no puede superar {LONGITUD} caracteres: acórtela.";
                return null;
            }
            return texto;
        }

        private static string Limpiar(string texto)
            => string.IsNullOrWhiteSpace(texto) ? null : ESPACIOS.Replace(texto.Trim(), " ").ToUpper(ESPANOL);
    }
}
