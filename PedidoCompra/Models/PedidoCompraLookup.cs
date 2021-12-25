using Nesto.Contratos;
using Prism.Mvvm;
using System;
using System.Linq;

namespace Nesto.Modulos.PedidoCompra.Models
{
    public class PedidoCompraLookup : BindableBase
    {
        public PedidoCompraLookup() { }
        public PedidoCompraLookup(PedidoCompraDTO pedidoOrigen)
        {
            Empresa = pedidoOrigen.Empresa;
            Pedido = pedidoOrigen.Id;
            Proveedor = pedidoOrigen.Proveedor;
            Contacto = pedidoOrigen.Contacto;
            Fecha = pedidoOrigen.Fecha;
            BaseImponible = pedidoOrigen.Lineas.Sum(l => l.BaseImponible);
            Total = pedidoOrigen.Lineas.Sum(l => l.Total);
            Nombre = pedidoOrigen.Nombre;
            Direccion = pedidoOrigen.Direccion;
        }
        public string Empresa { get; set; }
        private int _pedido;
        public int Pedido {
            get => _pedido;
            set { 
                SetProperty(ref _pedido, value);
                RaisePropertyChanged(nameof(EsPedidoSinCrear));
            }
        }
        public string Proveedor { get; set; }
        public string Contacto { get; set; }
        public string Nombre { get; set; }
        public string Direccion { get; set; }
        public string CodigoPostal { get; set; }
        public string Poblacion { get; set; }
        public string Provincia { get; set; }
        public DateTime Fecha { get; set; }
        public bool TieneAlbaran { get; set; }
        public bool TieneEnviado { get; set; }
        public bool TieneVistoBueno { get; set; }
        public bool EsPedidoSinCrear { get => Pedido == 0; }
        public decimal BaseImponible { get; set; }
        public decimal Total { get; set; }
    }
}
