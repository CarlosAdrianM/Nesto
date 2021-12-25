using System;
using System.Collections.Generic;
using System.Linq;

namespace Nesto.Modulos.PedidoCompra.Models
{
    public class PedidoCompraDTO
    {
        public PedidoCompraDTO()
        {
            Lineas = new List<LineaPedidoCompraDTO>();
        }
        public int Id { get; set; }
        public string Empresa { get; set; }
        public string Proveedor { get; set; }
        public string Contacto { get; set; }
        public DateTime Fecha { get; set; }
        public string FormaPago { get; set; }
        public string PlazosPago { get; set; }
        public DateTime PrimerVencimiento { get; set; }
        public int DiasEnServir { get; set; }
        public string CodigoIvaProveedor { get; set; }
        public string FacturaProveedor { get; set; }
        public string Comentarios { get; set; }
        public string Nombre { get; set; }
        public string Direccion { get; set; }
        public string PeriodoFacturacion { get; set; }
        public decimal BaseImponible => Math.Round(Lineas.Sum(l => l.BaseImponible), 2, MidpointRounding.AwayFromZero);
        public decimal Total => Math.Round(Lineas.Sum(l => l.Total), 2, MidpointRounding.AwayFromZero);
        
        public string Usuario { get; set; }

        public ICollection<LineaPedidoCompraDTO> Lineas { get; set; }
        public IEnumerable<ParametrosIvaCompra> ParametrosIva { get; set; }
    }
}
