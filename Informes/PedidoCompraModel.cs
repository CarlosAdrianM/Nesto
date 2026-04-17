using System;
using System.Collections.Generic;

namespace Nesto.Informes
{
    public class PedidoCompraModel
    {
        public int Id { get; set; }
        public string Proveedor { get; set; }
        public string Nombre { get; set; }
        public string Direccion { get; set; }
        public string CodigoPostal { get; set; }
        public string Poblacion { get; set; }
        public string Provincia { get; set; }
        public string Telefono { get; set; }
        public string Cif { get; set; }
        public DateTime Fecha { get; set; }
        public bool PedidoValorado { get; set; }
        public List<LineaPedidoCompraModel> Lineas { get; set; }
    }

    public class LineaPedidoCompraModel
    {
        public string SuReferencia { get; set; }
        public string NuestraReferencia { get; set; }
        public string Descripcion { get; set; }
        public short? Tamanno { get; set; }
        public string UnidadMedida { get; set; }
        public short? Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }
        public decimal SumaDescuentos { get; set; }
        public decimal BaseImponible { get; set; }
    }
}
