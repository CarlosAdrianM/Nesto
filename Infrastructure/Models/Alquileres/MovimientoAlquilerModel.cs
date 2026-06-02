using System;

namespace Nesto.Infrastructure.Models.Alquileres
{
    /// <summary>
    /// Una línea de movimiento (pedido de venta) de un alquiler, para la pestaña "Movimientos".
    /// Solo lectura en la UI (Nesto#340, Fase 1C.2). Sustituye al uso directo de la entidad EF
    /// LinPedidoVta; el grid auto-genera las columnas a partir de estas propiedades.
    /// </summary>
    public class MovimientoAlquilerModel
    {
        public int NumeroOrden { get; set; }
        public DateTime FechaEntrega { get; set; }
        public string Producto { get; set; }
        public string Texto { get; set; }
        public short? Cantidad { get; set; }
        public decimal? Precio { get; set; }
        public decimal Total { get; set; }
        public short Estado { get; set; }
    }
}
