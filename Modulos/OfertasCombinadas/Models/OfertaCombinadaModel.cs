using System;
using System.Collections.Generic;

namespace Nesto.Modulos.OfertasCombinadas.Models
{
    public class OfertaCombinadaModel
    {
        public int Id { get; set; }
        public string Empresa { get; set; }
        public string Nombre { get; set; }
        public decimal ImporteMinimo { get; set; }
        public DateTime? FechaDesde { get; set; }
        public DateTime? FechaHasta { get; set; }
        public string Usuario { get; set; }
        public DateTime FechaModificacion { get; set; }
        public List<OfertaCombinadaDetalleModel> Detalles { get; set; }
    }

    public class OfertaCombinadaDetalleModel
    {
        public int Id { get; set; }
        public string Producto { get; set; }
        public string ProductoNombre { get; set; }
        public short Cantidad { get; set; }
        public decimal Precio { get; set; }
    }

    public class OfertaCombinadaCreateModel
    {
        public string Empresa { get; set; }
        public string Nombre { get; set; }
        public decimal ImporteMinimo { get; set; }
        public DateTime? FechaDesde { get; set; }
        public DateTime? FechaHasta { get; set; }
        public List<OfertaCombinadaDetalleCreateModel> Detalles { get; set; }
    }

    public class OfertaCombinadaDetalleCreateModel
    {
        public int Id { get; set; }
        public string Producto { get; set; }
        public short Cantidad { get; set; }
        public decimal Precio { get; set; }
    }

    public class OfertaPermitidaFamiliaModel
    {
        public int NOrden { get; set; }
        public string Empresa { get; set; }
        public string Familia { get; set; }
        public string FamiliaDescripcion { get; set; }
        public short CantidadConPrecio { get; set; }
        public short CantidadRegalo { get; set; }
        public string FiltroProducto { get; set; }
        public string Usuario { get; set; }
        public DateTime FechaModificacion { get; set; }
    }

    public class OfertaPermitidaFamiliaCreateModel
    {
        public string Empresa { get; set; }
        public string Familia { get; set; }
        public short CantidadConPrecio { get; set; }
        public short CantidadRegalo { get; set; }
        public string FiltroProducto { get; set; }
    }
}
