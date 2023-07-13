using Prism.Mvvm;
using System;
using Prism.Ioc;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nesto.Infrastructure.Shared;

namespace Nesto.Modulos.PedidoCompra.Models
{
    public class LineaPedidoCompraWrapper : BindableBase
    {
        IPedidoCompraService Servicio;
        
        public LineaPedidoCompraWrapper()
        {
            // Este constructor solo vale para que permita ingresar desde el Datagrid
            var container = ContainerLocator.Container;
            Servicio = container.Resolve<IPedidoCompraService>();
            Model = new LineaPedidoCompraDTO
            {
                Cantidad = 1
            };
            _cantidadOriginal = Model.StockMaximo - Model.Stock + Model.PendienteEntregar - Model.PendienteRecibir;
        }
        
        public LineaPedidoCompraWrapper(LineaPedidoCompraDTO linea, IPedidoCompraService servicio)
        {
            Servicio = servicio;
            Model = linea;
            _cantidadOriginal = Model.StockMaximo - Model.Stock + Model.PendienteEntregar - Model.PendienteRecibir;
        }

        public LineaPedidoCompraDTO Model { get; set; }

        private PedidoCompraWrapper _pedido;
        public PedidoCompraWrapper Pedido
        {
            get => _pedido;
            set
            {
                SetProperty(ref _pedido, value);
                if (Estado == 0)
                {
                    Estado = Pedido.EstadoDefecto;
                }
                if (TipoLinea == null)
                {
                    TipoLinea = Pedido.UltimoTipoLinea;
                }
                if (FechaRecepcion == DateTime.MinValue)
                {
                    FechaRecepcion = Pedido.UltimaFechaRecepcion;
                }
                if (TipoLinea == Constantes.LineasPedido.TiposLinea.PRODUCTO && Pedido.Model.ParametrosIva != null && Pedido.Model.ParametrosIva.Any(p => p.CodigoIvaProducto == CodigoIvaProducto))
                {
                    PorcentajeIva = Pedido.Model.ParametrosIva.Single(p => p.CodigoIvaProducto == CodigoIvaProducto).PorcentajeIvaProducto;
                }
            }
        }

        public int Id
        {
            get => Model.Id;
            set
            {
                Model.Id = value;
                RaisePropertyChanged(nameof(Id));
            }
        }

        public int Estado
        {
            get => Model.Estado;
            set
            {
                Model.Estado = value;
                RaisePropertyChanged(nameof(Estado));
            }
        }
        public int EstadoProducto
        {
            get => Model.EstadoProducto;
            set
            {
                Model.EstadoProducto = value;
                RaisePropertyChanged(nameof(EstadoProducto));
            }
        }
        public string TipoLinea
        {
            get => Model.TipoLinea;
            set
            {
                var tipoLineaAnterior = Model.TipoLinea;
                Model.TipoLinea = value;
                RaisePropertyChanged(nameof(TipoLinea));
                if (Pedido != null)
                {
                    Pedido.UltimoTipoLinea = value;
                }
                if (tipoLineaAnterior != null && value == Constantes.LineasPedido.TiposLinea.PRODUCTO && value != tipoLineaAnterior)
                {
                    CargarProducto(Producto);
                }
                if (tipoLineaAnterior != null && value == Constantes.LineasPedido.TiposLinea.LINEA_TEXTO && value != tipoLineaAnterior)
                {
                    Cantidad = 0;
                }

            }
        }
        public string Producto
        {
            get => Model.Producto;
            set
            {
                Model.Producto = value;
                RaisePropertyChanged(nameof(Producto));
                CargarProducto(value);
            }
        }

        private async Task CargarProducto(string value)
        {
            if (Model.TipoLinea == Constantes.LineasPedido.TiposLinea.PRODUCTO)
            {
                var lineaProducto = await Servicio.LeerProducto(Pedido.Model.Empresa, Producto, Pedido.Model.Proveedor, Pedido.Model.CodigoIvaProveedor);
                if (lineaProducto == null)
                {
                    return;
                }
                lineaProducto.Id = -2; // para diferenciar de las que crean en la API que son -1
                var posicion = (Pedido.Model.Lineas as List<LineaPedidoCompraDTO>).IndexOf(Model);
                Model = lineaProducto;
                (Pedido.Model.Lineas as List<LineaPedidoCompraDTO>)[posicion] = Model;
                RaisePropertyChanged(string.Empty);
            }
        }

        public string Texto
        {
            get => Model.Texto;
            set
            {
                Model.Texto = value;
                RaisePropertyChanged(nameof(Texto));
            }
        }
        public DateTime FechaRecepcion
        {
            get => Model.FechaRecepcion;
            set
            {
                Model.FechaRecepcion = value;
                RaisePropertyChanged(nameof(FechaRecepcion));
                if (Pedido != null)
                {
                    Pedido.UltimaFechaRecepcion= value;
                }
            }
        }
        public int Cantidad
        {
            get => Model.Cantidad;
            set
            {
                Model.Cantidad = value;
                RaisePropertyChanged(nameof(Cantidad));
                RaisePropertyChanged(nameof(CantidadCobrada));
                RaisePropertyChanged(nameof(CantidadRegalo));
                RaisePropertyChanged(nameof(PrecioUnitario));
                RaisePropertyChanged(nameof(DescuentoProducto));
                RaisePropertyChanged(nameof(SumaDescuentos));
                RaisePropertyChanged(nameof(Bruto));
                RaisePropertyChanged(nameof(ImporteDescuento));
                RaisePropertyChanged(nameof(BaseImponible));
                RaisePropertyChanged(nameof(ImporteIva));
                RaisePropertyChanged(nameof(Total));
            }
        }
        private int _cantidadOriginal;
        public int CantidadOriginal
        {
            get => _cantidadOriginal;
        }
        public string CodigoIvaProducto
        {
            get => Model.CodigoIvaProducto;
            set
            {
                var parametroIva = Pedido.Model.ParametrosIva.Single(p => p.CodigoIvaProducto.ToLower() == value.ToLower());
                Model.CodigoIvaProducto = parametroIva.CodigoIvaProducto;
                Model.PorcentajeIva = parametroIva.PorcentajeIvaProducto;
                RaisePropertyChanged(nameof(CodigoIvaProducto));
                RaisePropertyChanged(nameof(PorcentajeIva));
                RaisePropertyChanged(nameof(ImporteIva));
                RaisePropertyChanged(nameof(Total));
            }
        }

        public decimal PrecioUnitario
        {
            get => Model.PrecioUnitario;
            set
            {
                Model.PrecioUnitario = value;
                RaisePropertyChanged(nameof(PrecioUnitario));
                RaisePropertyChanged(nameof(Bruto));
                RaisePropertyChanged(nameof(ImporteDescuento));
                RaisePropertyChanged(nameof(BaseImponible));
                RaisePropertyChanged(nameof(ImporteIva));
                RaisePropertyChanged(nameof(Total));
            }
        }
        public decimal DescuentoLinea
        {
            get => Model.DescuentoLinea;
            set
            {
                Model.DescuentoLinea = value;
                RaisePropertyChanged(nameof(DescuentoLinea));
                RaisePropertyChanged(nameof(SumaDescuentos));
                RaisePropertyChanged(nameof(ImporteDescuento));
                RaisePropertyChanged(nameof(BaseImponible));
                RaisePropertyChanged(nameof(ImporteIva));
                RaisePropertyChanged(nameof(Total));
            }
        }
        public decimal DescuentoProveedor
        {
            get => Model.DescuentoProveedor;
            set
            {
                Model.DescuentoProveedor = value;
                RaisePropertyChanged(nameof(DescuentoProveedor));
                RaisePropertyChanged(nameof(SumaDescuentos));
                RaisePropertyChanged(nameof(ImporteDescuento));
                RaisePropertyChanged(nameof(BaseImponible));
                RaisePropertyChanged(nameof(ImporteIva));
                RaisePropertyChanged(nameof(Total));
            }
        }
        public decimal DescuentoProducto
        {
            get => Model.DescuentoProducto;
            set
            {
                Model.DescuentoProducto = value;
                RaisePropertyChanged(nameof(DescuentoProducto));
                RaisePropertyChanged(nameof(SumaDescuentos));
                RaisePropertyChanged(nameof(ImporteDescuento));
                RaisePropertyChanged(nameof(BaseImponible));
                RaisePropertyChanged(nameof(ImporteIva));
                RaisePropertyChanged(nameof(Total));
            }
        }
        /*
        public List<DescuentoCantidadCompra> Descuentos
        {
            get => Model.Descuentos;
            set
            {
                Model.Descuentos = value;
                RaisePropertyChanged(nameof(Descuentos));
            }
        }

        public List<OfertaCompra> Ofertas
        {
            get => Model.Ofertas;
            set
            {
                Model.Ofertas = value;
                RaisePropertyChanged(nameof(Ofertas));
            }
        }
        */

        public bool AplicarDescuento
        {
            get => Model.AplicarDescuento;
            set
            {
                Model.AplicarDescuento = value;
                RaisePropertyChanged(nameof(AplicarDescuento));
                RaisePropertyChanged(nameof(SumaDescuentos));
                RaisePropertyChanged(nameof(ImporteDescuento));
                RaisePropertyChanged(nameof(BaseImponible));
                RaisePropertyChanged(nameof(ImporteIva));
                RaisePropertyChanged(nameof(Total));
            }
        }
        public decimal PorcentajeIva
        {
            get => Model.PorcentajeIva;
            set
            {
                Model.PorcentajeIva = value;
                RaisePropertyChanged(nameof(PorcentajeIva));
                RaisePropertyChanged(nameof(ImporteIva));
                RaisePropertyChanged(nameof(Total));
            }
        }


        public decimal Bruto { get => Model.Bruto; }
        public decimal SumaDescuentos { get => Model.SumaDescuentos; }
        public decimal ImporteDescuento { get => Model.ImporteDescuento; }
        public decimal BaseImponible { get => Model.BaseImponible; }
        public decimal ImporteIva { get => Model.ImporteIva; }
        public decimal Total { get => Model.Total; }
        public int? CantidadCobrada { get => Model.CantidadCobrada; }
        public int? CantidadRegalo { get => Model.CantidadRegalo; }
    }
}
