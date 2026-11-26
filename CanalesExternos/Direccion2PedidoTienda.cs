using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Nesto.Modulos.CanalesExternos
{
    /// <summary>La Dirección 2 de la entrega de un pedido de la tienda online y su tipo de lugar.</summary>
    public sealed class Direccion2PedidoTiendaResultado
    {
        /// <summary>En mayúsculas («3A», «LOCAL · RÓTULO: VENUS ESTILISTAS», «3A · RÓTULO: ESTÉTICA GOJI»); null si no hay nada.</summary>
        public string Direccion2 { get; set; }
        /// <summary>«Local» si la dirección trae empresa (company); si no, null. Nunca se inventa «Piso».</summary>
        public string TipoLugar { get; set; }
    }

    /// <summary>
    /// NestoAPI#878 (Carlos 26/11/26): en PrestaShop la «Dirección complementaria» (<c>address2</c>) y la empresa (<c>company</c>) son lo que el
    /// repartidor necesita además de la calle. GEMELA de la regla de la API (<c>NestoAPI.Infraestructure.Clientes.Direccion2PedidoTienda</c>):
    /// si se cambia allí, se cambia aquí (y al revés), con sus pruebas.
    /// <list type="bullet">
    /// <item><see cref="Componer"/>: <c>address2</c> en mayúsculas; si <c>company</c> viene y no está ya contenido en <c>address2</c> (sin mayúsculas
    /// ni tildes), se añade « · RÓTULO: COMPANY» («LOCAL · RÓTULO: …» si no hay <c>address2</c>); tipo «Local» con company. Como mucho 100.</item>
    /// <item><see cref="LineaComentario"/>: la línea «DIRECCIÓN 2: …» de los comentarios del pedido (la API la lee de forma tolerante).</item>
    /// </list>
    /// </summary>
    public static class Direccion2PedidoTienda
    {
        public const string ETIQUETA = "DIRECCIÓN 2:";
        public const string TIPO_LOCAL = "Local";
        /// <summary>Como Clientes.Direccion2 en la API (Direccion2Cliente.LONGITUD).</summary>
        public const int LONGITUD = 100;
        private const string SEPARADOR_ROTULO = " · ";
        private const string ROTULO = "RÓTULO: ";

        private static readonly CultureInfo ESPANOL = new CultureInfo("es-ES");
        private static readonly Regex ESPACIOS = new Regex(@"\s+", RegexOptions.Compiled);

        public static Direccion2PedidoTiendaResultado Componer(string address2, string company)
        {
            string direccion2 = Limpiar(address2);
            string empresa = Limpiar(company);
            string tipo = empresa == null ? null : TIPO_LOCAL;
            if (empresa != null && (direccion2 == null || !SinTildes(direccion2).Contains(SinTildes(empresa))))
            {
                string rotulo = ROTULO + empresa;
                direccion2 = direccion2 == null ? "LOCAL" + SEPARADOR_ROTULO + rotulo : direccion2 + SEPARADOR_ROTULO + rotulo;
            }
            if (direccion2 != null && direccion2.Length > LONGITUD)
            {
                direccion2 = direccion2.Substring(0, LONGITUD).TrimEnd();
            }
            return new Direccion2PedidoTiendaResultado { Direccion2 = direccion2, TipoLugar = direccion2 == null ? null : tipo };
        }

        /// <summary>«DIRECCIÓN 2: 3A · RÓTULO: ESTÉTICA GOJI»; null si no hay Dirección 2 (entonces no se escribe la línea).</summary>
        public static string LineaComentario(string direccion2)
        {
            string limpio = Limpiar(direccion2);
            return limpio == null ? null : ETIQUETA + " " + limpio;
        }

        private static string Limpiar(string texto)
            => string.IsNullOrWhiteSpace(texto) ? null : ESPACIOS.Replace(texto.Trim(), " ").ToUpper(ESPANOL);

        private static string SinTildes(string texto)
        {
            string descompuesto = (texto ?? string.Empty).ToUpperInvariant().Normalize(NormalizationForm.FormD);
            return new string(descompuesto.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());
        }
    }

    /// <summary>La dirección y las observaciones del envío ya con la Dirección 2.</summary>
    public sealed class DireccionEnvioCompuesta
    {
        public string Direccion { get; set; }
        public string Observaciones { get; set; }
        /// <summary>La Dirección 2 no cabe en ningún sitio: hay que ponerla a mano (de usted). Null si ha cabido o no había.</summary>
        public string Aviso { get; set; }
        public bool EnObservaciones { get; set; }
    }

    /// <summary>
    /// GEMELA de la regla de la API <c>NestoAPI.Infraestructure.Agencias.DireccionEnvioCliente.Componer</c> (NestoAPI#874): el rótulo, el piso o la
    /// nave tienen que llegar al repartidor. Dirección (80) = Dirección + «, » + Dirección 2 si cabe; si no, la Dirección 2 va delante de las
    /// Observaciones (80) con « · », sin quitar nada; si tampoco cabe, todo como estaba y <see cref="DireccionEnvioCompuesta.Aviso"/>.
    /// </summary>
    public static class DireccionEnvioPedidoExterno
    {
        public const int LONGITUD_DIRECCION = 80;
        public const int LONGITUD_OBSERVACIONES = 80;

        public static DireccionEnvioCompuesta Componer(string direccion, string direccion2, string observaciones)
        {
            var resultado = new DireccionEnvioCompuesta { Direccion = direccion, Observaciones = observaciones };
            string d2 = direccion2?.Trim();
            if (string.IsNullOrEmpty(d2))
            {
                return resultado;
            }
            string calle = direccion?.Trim() ?? string.Empty;
            string conDireccion2 = calle.Length == 0 ? d2 : calle + ", " + d2;
            if (conDireccion2.Length <= LONGITUD_DIRECCION)
            {
                resultado.Direccion = conDireccion2;
                return resultado;
            }
            string obs = observaciones?.Trim() ?? string.Empty;
            string conObservaciones = obs.Length == 0 ? d2 : d2 + " · " + obs;
            if (conObservaciones.Length <= LONGITUD_OBSERVACIONES)
            {
                resultado.Observaciones = conObservaciones;
                resultado.EnObservaciones = true;
                return resultado;
            }
            resultado.Aviso = $"La dirección 2 del cliente («{d2}») no cabe en la dirección ni en las observaciones del envío: " +
                "añádala a mano para que le llegue al repartidor.";
            return resultado;
        }
    }
}
