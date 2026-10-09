namespace Nesto.Modulos.CanalesExternos.ApisExternas
{
    /// <summary>Cómo se presenta la clave del webservice de Prestashop a la tienda.</summary>
    public enum AutenticacionPrestashop
    {
        /// <summary>La clave como usuario de la autenticación básica (Nueva Visión, lo de siempre).</summary>
        Basica,
        /// <summary>La clave en la URL (<c>?ws_key=…</c>), para tiendas que no aceptan la autenticación básica.</summary>
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


        public static readonly TiendaPrestashop NuevaVision = new()
        {
            Nombre = "Nueva Visión",
            UrlApi = "https://www.productosdeesteticaypeluqueriaprofesional.com/api",
            ClaveConfiguracion = "PrestashopWebserviceKeyNV",
            Autenticacion = AutenticacionPrestashop.Basica,
            Serie = "NV",
            ConceptoPrepago = "Tienda Online"
        };

    }
}
