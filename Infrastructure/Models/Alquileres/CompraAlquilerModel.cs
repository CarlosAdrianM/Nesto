using System;

namespace Nesto.Infrastructure.Models.Alquileres
{
    /// <summary>
    /// Una línea de compra (pedido de compra) asociada a un producto/número de serie de un alquiler,
    /// para la pestaña "Compra". Solo lectura en la UI (Nesto#340, Fase 1C.2). Sustituye al uso
    /// directo de la entidad EF LinPedidoCmp; el grid auto-genera las columnas a partir de estas propiedades.
    /// </summary>
    public class CompraAlquilerModel
    {
        public int NumeroOrden { get; set; }
        public int NumeroPedido { get; set; }
        public string Proveedor { get; set; }
        public DateTime? FechaRecepcion { get; set; }
        public string Producto { get; set; }
        public string Texto { get; set; }
        public short? Cantidad { get; set; }
        public decimal Precio { get; set; }
        public decimal Total { get; set; }
        public short Estado { get; set; }
        public string NumSerie { get; set; }
    }
}
