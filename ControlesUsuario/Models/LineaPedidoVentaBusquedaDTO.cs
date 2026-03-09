using System;

namespace ControlesUsuario.Models
{
    public class LineaPedidoVentaBusquedaDTO
    {
        public string Empresa { get; set; }
        public int Pedido { get; set; }
        public string Factura { get; set; }
        public DateTime? FechaFactura { get; set; }
        public string Producto { get; set; }
        public string Texto { get; set; }
        public short? Cantidad { get; set; }
        public decimal BaseImponible { get; set; }
    }
}
