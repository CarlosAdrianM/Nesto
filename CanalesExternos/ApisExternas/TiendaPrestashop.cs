namespace Nesto.Modulos.CanalesExternos.ApisExternas
{
    /// <summary>Cómo se presenta la clave del webservice de Prestashop a la tienda.</summary>
    public enum AutenticacionPrestashop
    {
        /// <summary>La clave como usuario de la autenticación básica (Nueva Visión, lo de siempre).</summary>
        Basica,
        /// <summary>La clave en la URL (<c>?ws_key=…</c>). Eva Visnú: con autenticación básica devuelve 401.</summary>
        ClaveEnUrl
    }

    /// <summary>
    /// Nesto#520: lo que distingue a una tienda Prestashop de otra. El núcleo
    /// (<see cref="PrestashopService"/> y <see cref="CanalExternoPedidosPrestashop"/>) es el mismo para todas;
    /// cada tienda solo aporta estos datos.
    /// </summary>
    public sealed class TiendaPrestashop
    {
        /// <summary>Nombre que ve el usuario (mensajes de error y concepto del prepago).</summary>
        public string Nombre { get; init; }

        /// <summary>Raíz del webservice, sin barra final (p. ej. https://www.evavisnu.com/api).</summary>
        public string UrlApi { get; init; }

        /// <summary>Clave de appSettings (clavesSecretas.config) con la clave del webservice.</summary>
        public string ClaveConfiguracion { get; init; }

        public AutenticacionPrestashop Autenticacion { get; init; }

        /// <summary>Serie de los pedidos que entran desde esta tienda.</summary>
        public string Serie { get; init; }

        /// <summary>Principio del concepto del prepago («Tienda Online PayPal»…).</summary>
        public string ConceptoPrepago { get; init; }

        /// <summary>
        /// Si al confirmar el envío se cambia el transportista del pedido en la tienda por el que declara
        /// NestoAPI para la agencia (RegistroSeguimientoAgencias.TransportistaPrestashop). Esos identificadores
        /// son los de la tienda de Nueva Visión; en las demás se deja el transportista que ya tiene el pedido
        /// y solo se añade el número de seguimiento.
        /// </summary>
        public bool UsaTransportistaDeNestoAPI { get; init; }

        public static readonly TiendaPrestashop NuevaVision = new()
        {
            Nombre = "Nueva Visión",
            UrlApi = "https://www.productosdeesteticaypeluqueriaprofesional.com/api",
            ClaveConfiguracion = "PrestashopWebserviceKeyNV",
            Autenticacion = AutenticacionPrestashop.Basica,
            Serie = "NV",
            ConceptoPrepago = "Tienda Online",
            UsaTransportistaDeNestoAPI = true
        };

        public static readonly TiendaPrestashop EvaVisnu = new()
        {
            Nombre = "Eva Visnú",
            UrlApi = "https://www.evavisnu.com/api",
            ClaveConfiguracion = "PrestashopWebserviceKeyEV",
            Autenticacion = AutenticacionPrestashop.ClaveEnUrl,
            Serie = "EV",
            ConceptoPrepago = "Tienda Online Eva Visnú",
            UsaTransportistaDeNestoAPI = false
        };
    }
}
