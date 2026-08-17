namespace Nesto.Modulos.PedidoCompra.Models
{
    public class CrearFacturaCmpResponse
    {      
        public int AsientoFactura { get; set; }
        public int AsientoPago { get; set; }
        public bool Exito { get; set; }
        public int Factura { get; set; }
        public int Pedido { get; set; }
        /// <summary>NestoAPI#384: la factura del proveedor ya estaba contabilizada y el
        /// servidor no ha creado nada (idempotencia al reintentar tras un error).</summary>
        public bool YaExistia { get; set; }
    }
}
