namespace Nesto.Modulos.PedidoCompra.Models
{
    public class CrearFacturaCmpResponse
    {      
        public int AsientoFactura { get; set; }
        public int AsientoPago { get; set; }
        public bool Exito { get; set; }
        public int Factura { get; set; }
        public int Pedido { get; set; }
    }
}
