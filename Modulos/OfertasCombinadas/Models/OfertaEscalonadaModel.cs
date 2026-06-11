using System;
using System.Collections.Generic;

namespace Nesto.Modulos.OfertasCombinadas.Models
{
    // Ofertas escalonadas (NestoAPI#226): descuento por volumen sobre una lista de referencias
    // combinables entre sí. Los tramos son cantidad MÍNIMA ("6 o más"), no cantidad exacta.
    public class OfertaEscalonadaModel
    {
        public int Id { get; set; }
        public string Empresa { get; set; }
        public string Nombre { get; set; }
        public DateTime? FechaDesde { get; set; }
        public DateTime? FechaHasta { get; set; }
        public string Usuario { get; set; }
        public DateTime FechaModificacion { get; set; }
        public List<OfertaEscalonadaProductoModel> Productos { get; set; }
        public List<OfertaEscalonadaTramoModel> Tramos { get; set; }
    }

    public class OfertaEscalonadaProductoModel
    {
        public int Id { get; set; }
        public string Producto { get; set; }
        public string ProductoNombre { get; set; }
        public decimal PrecioBase { get; set; }
    }

    public class OfertaEscalonadaTramoModel
    {
        public int Id { get; set; }
        public short CantidadMinima { get; set; }
        // En tanto por uno (0.25 = 25 %).
        public decimal Descuento { get; set; }
    }

    public class OfertaEscalonadaCreateModel
    {
        public string Empresa { get; set; }
        public string Nombre { get; set; }
        public DateTime? FechaDesde { get; set; }
        public DateTime? FechaHasta { get; set; }
        public List<OfertaEscalonadaProductoCreateModel> Productos { get; set; }
        public List<OfertaEscalonadaTramoCreateModel> Tramos { get; set; }
    }

    public class OfertaEscalonadaProductoCreateModel
    {
        public int Id { get; set; }
        public string Producto { get; set; }
        // Null = el servidor precarga el PVP de la ficha del producto.
        public decimal? PrecioBase { get; set; }
    }

    public class OfertaEscalonadaTramoCreateModel
    {
        public int Id { get; set; }
        public short CantidadMinima { get; set; }
        public decimal Descuento { get; set; }
    }
}
