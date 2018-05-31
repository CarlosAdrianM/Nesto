using System;
using System.Collections.Generic;

namespace Nesto.Modules.Producto
{
    public class ProductoModel
    {
        public ProductoModel()
        {
            Stocks = new List<StockProducto>();
        }
        public string Producto { get; set; }
        public string Nombre { get; set; }
        public short? Tamanno { get; set; }
        public string UnidadMedida { get; set; }
        public string Familia { get; set; }
        public decimal PrecioProfesional { get; set; }
        public short Estado { get; set; }
        public string Grupo { get; set; }
        public string Subgrupo { get; set; }
        public string UrlFoto { get; set; }
        public ICollection<StockProducto> Stocks { get; set; }

        public class StockProducto
        {
            public string Almacen { get; set; }
            public int Stock { get; set; }
            public int PendienteEntregar { get; set; }
            public int PendienteRecibir { get; set; }
            public int CantidadDisponible { get; set; }
            public DateTime FechaEstimadaRecepcion { get; set; }
            public int PendienteReposicion { get; set; }
            public bool HayPendienteDeRecibir { get { return PendienteRecibir > 0; } }
        }

    }
}
