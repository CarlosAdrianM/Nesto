namespace Nesto.Modulos.PedidoCompra.Models
{
    public class CrearFacturaCmpRequest
    {
        public PedidoCompraDTO Pedido { get; set; }        
        public bool CrearPago { get; set; }
        public string ContraPartidaPago { get; set; }
        public string Documento { get; set; } // para poner datos que nos interese guardar
    }
}
