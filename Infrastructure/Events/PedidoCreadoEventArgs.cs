using Nesto.Models;

namespace Nesto.Infrastructure.Events
{
    public class PedidoCreadoEventArgs
    {
        public PedidoVentaDTO Pedido { get; set; }
        public string NombreCliente { get; set; }
        public string DireccionCliente { get; set; }
        public string CodigoPostal { get; set; }
        public string Poblacion { get; set; }
        public string Provincia { get; set; }
        public bool TieneProductos { get; set; }
    }
}
