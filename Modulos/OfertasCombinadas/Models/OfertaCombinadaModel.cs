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
        // NestoAPI#290: la unidad a base 0 debe ser la de menor tarifa del conjunto y las pagadas
        // cubren su tarifa (suelo dinámico). Por defecto true en las ofertas nuevas.
        public bool RegalarMenorImporte { get; set; }
        public string Usuario { get; set; }
        public DateTime FechaModificacion { get; set; }
        public List<OfertaCombinadaDetalleModel> Detalles { get; set; }
    }

    public class OfertaCombinadaDetalleModel
    {
        public int Id { get; set; }
        public string Producto { get; set; }
        public string ProductoNombre { get; set; }
        // NestoAPI#282: fila de FILTRO (Producto null): casa las líneas del pedido por familia
        // y/o prefijo del nombre del producto; la cantidad se cuenta agregada entre todas.
        public string Familia { get; set; }
        public string FiltroProducto { get; set; }
        // NestoAPI#289: el filtro también puede casar por Grupo y/o Subgrupo del producto
        // (todos los criterios informados en AND). En blanco = igual que antes.
        public string Grupo { get; set; }
        public string Subgrupo { get; set; }
        public short Cantidad { get; set; }
        public decimal Precio { get; set; }
        // Líneas con el mismo GrupoAlternativa son intercambiables ("elige 1"); null = obligatoria.
        public int? GrupoAlternativa { get; set; }
        // Si true, Cantidad es un MÁXIMO: el pedido puede llevar de 0 a Cantidad sin que la oferta
        // deje de validar (extra opcional, p. ej. folletos/expositor). NestoAPI#239.
        public bool PermitirCantidadMenor { get; set; }
    }

    public class OfertaCombinadaCreateModel
    {
        public string Empresa { get; set; }
        public string Nombre { get; set; }
        public decimal ImporteMinimo { get; set; }
        public DateTime? FechaDesde { get; set; }
        public DateTime? FechaHasta { get; set; }
        // NestoAPI#290: por defecto true (las ofertas nuevas nacen con la regla activada).
        public bool RegalarMenorImporte { get; set; } = true;
        public List<OfertaCombinadaDetalleCreateModel> Detalles { get; set; }
    }

    public class OfertaCombinadaDetalleCreateModel
    {
        public int Id { get; set; }
        public string Producto { get; set; }
        // NestoAPI#282: fila de FILTRO (Producto null): familia y/o prefijo del nombre.
        public string Familia { get; set; }
        public string FiltroProducto { get; set; }
        // NestoAPI#289: filtro por Grupo y/o Subgrupo del producto (AND con familia/prefijo).
        public string Grupo { get; set; }
        public string Subgrupo { get; set; }
        public short Cantidad { get; set; }
        public decimal Precio { get; set; }
        public int? GrupoAlternativa { get; set; }
        public bool PermitirCantidadMenor { get; set; }
    }

    // NestoAPI#289: item del combo de subgrupos del detalle (GET api/Productos/Subgrupos, ya
    // ordenado por grupo + descripción). La opción en blanco (Grupo/Subgrupo vacíos) deja la
    // fila sin filtro de subgrupo, exactamente igual que antes de la #289.
    public class SubgrupoComboModel
    {
        public string Grupo { get; set; }
        public string Subgrupo { get; set; }
        public string Nombre { get; set; }
        // Clave estable para SelectedValue: con separador porque el grupo puede tener menos de 3 letras.
        public string Clave => $"{Grupo}|{Subgrupo}";
        public string Etiqueta => string.IsNullOrEmpty(Subgrupo) ? Nombre : $"{Grupo}/{Subgrupo} - {Nombre}";
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
