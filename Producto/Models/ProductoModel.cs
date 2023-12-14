using Nesto.Infrastructure.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Nesto.Modules.Producto.Models
{
    public class ProductoModel : IFiltrableItem
    {
        public ProductoModel()
        {
            ProductosKit = new List<ProductoKit>();
            Stocks = new List<StockProducto>();
        }
        public string Producto { get; set; }
        public string Nombre { get; set; }
        public short? Tamanno { get; set; }
        public string UnidadMedida { get; set; }
        public string Familia { get; set; }
        public decimal PrecioProfesional { get; set; }
        public decimal PrecioPublicoFinal { get; set; }
        public bool EsKit { get => ProductosKit != null && ProductosKit.Any(); }
        public short Estado { get; set; }
        public string Grupo { get; set; }
        public string Subgrupo { get; set; }
        public string UrlEnlace { get; set; }
        public string UrlFoto { get; set; }
        public bool RoturaStockProveedor { get; set; }
        public int Stock => Stocks?.Sum(s => s.Stock) ?? 0;
        
        public ICollection<ProductoKit> ProductosKit { get; set; }
        public ICollection<StockProducto> Stocks { get; set; }

        public bool Contains(string filtro)
        {
            // Está sin implementar
            return Producto == filtro || Familia.ToLower().Contains(filtro, StringComparison.OrdinalIgnoreCase) || Nombre.ToLower().Contains(filtro, StringComparison.InvariantCultureIgnoreCase) || Subgrupo.ToLower().Contains(filtro, StringComparison.OrdinalIgnoreCase);
        }

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
