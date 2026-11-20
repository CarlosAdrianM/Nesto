namespace Nesto.Infrastructure.Events
{
    /// <summary>
    /// Novedades 1283: se ha borrado un pedido de venta entero desde su detalle. Las listas de pedidos lo quitan y, si era el que
    /// tenían abierto, cierran el detalle.
    /// </summary>
    public sealed class PedidoBorradoMensaje
    {
        public PedidoBorradoMensaje(string empresa, int numero)
        {
            Empresa = empresa?.Trim();
            Numero = numero;
        }

        public string Empresa { get; }
        public int Numero { get; }
    }
}
