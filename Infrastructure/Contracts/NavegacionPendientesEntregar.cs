namespace Nesto.Infrastructure.Contracts
{
    /// <summary>
    /// Nesto#628 (sugerencia 1106, Alfredo): la pantalla «Pendientes de entregar» (módulo PedidoVenta, Nesto#552) registrada
    /// por nombre, para que otros módulos (la ficha de producto) la abran ya filtrada sin referenciar el módulo.
    /// Si ya está abierta se reutiliza la pestaña con los filtros nuevos (se pueden cambiar después).
    /// </summary>
    public static class NavegacionPendientesEntregar
    {
        public const string NOMBRE_VISTA = "PendientesEntregarView";
        public const string PARAMETRO_PRODUCTO = "producto";
        public const string PARAMETRO_ALMACEN = "almacen";
    }
}
