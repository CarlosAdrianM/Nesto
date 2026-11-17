using Nesto.Infrastructure.Contracts;

namespace Nesto.Infrastructure.Shared
{
    /// <summary>
    /// Nesto#645 (Novedades 1317 y 1296): al facturar en la tienda, antes de imprimir la factura, se pregunta si se mete el cobro
    /// en caja y se abre el cobro (módulo Cajas) con el efecto de esa factura ya marcado. Lo comparten el detalle del pedido (que
    /// lo abre) y Cajas (que registra el diálogo).
    /// </summary>
    public static class CobroEnCajaFactura
    {
        /// <summary>Nombre con el que se registra el diálogo de cobro (módulo Cajas).</summary>
        public const string DIALOGO = "CobroCajaDialog";

        public const string PARAMETRO_EMPRESA = "empresa";
        public const string PARAMETRO_CLIENTE = "cliente";
        public const string PARAMETRO_CONTACTO = "contacto";
        public const string PARAMETRO_DOCUMENTO = "documento";

        /// <summary>
        /// Quién lo ve: los usuarios del grupo Tiendas (los que meten el cobro en la caja de su tienda). Los demás facturan como
        /// siempre.
        /// </summary>
        public static bool EsUsuarioDeTienda(IConfiguracion configuracion)
            => configuracion != null && configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.TIENDAS);

        /// <summary>
        /// Se pregunta si es de tienda y la factura deja algo por cobrar. <paramref name="pendiente"/> null = no se ha podido leer:
        /// se pregunta igual (mejor recordarlo de más que olvidar el cobro).
        /// </summary>
        public static bool DebePreguntar(bool esUsuarioDeTienda, decimal? pendiente)
            => esUsuarioDeTienda && (pendiente is null || pendiente.Value > 0);
    }
}
