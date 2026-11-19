using Nesto.Models;

namespace Nesto.Infrastructure.Events
{
    public class PedidoCreadoEventArgs
    {
        public PedidoVentaDTO Pedido { get; set; }
        public string NombreCliente { get; set; }
        public string DireccionCliente { get; set; }
        /// <summary>NestoAPI#874: la dirección con la Dirección 2 (la que se enseña), o null.</summary>
        public string DireccionCompletaCliente { get; set; }
        public string CodigoPostal { get; set; }
        public string Poblacion { get; set; }
        public string Provincia { get; set; }
        public bool TieneProductos { get; set; }
    }
}
