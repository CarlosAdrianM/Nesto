using Nesto.Infrastructure.Shared;
using Nesto.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Nesto.Modulos.PedidoCompra.Models
{
    public class LineaPedidoCompraDTO : LineaPedidoBase
    {
        public LineaPedidoCompraDTO() : base() {
            
        }
        public int Id { get; set; }
        public int Estado { get; set; } = Constantes.LineasPedido.ESTADO_SIN_FACTURAR;
        public string TipoLinea { get; set; }
        //public string Producto { get; set; }
        public string Texto { get; set; }
        public DateTime FechaRecepcion { get; set; }
        private int _cantidad;
        public int Cantidad
        {
            get => _cantidad;
            set
            {
                if (Descuentos != null && Descuentos.Any())
                {
                    var descuentoActual = Descuentos.Where(d => d.CantidadMinima <= _cantidad).OrderByDescending(d => d.CantidadMinima).FirstOrDefault();
                    var descuentoNuevo = Descuentos.Where(d => d.CantidadMinima <= value).OrderByDescending(d => d.CantidadMinima).FirstOrDefault();
                    if (descuentoNuevo == null)
                    {
                        descuentoNuevo = new DescuentoCantidadCompra
                        {
                            CantidadMinima = 0,
                            Descuento = 0,
                            Precio = 0
                        };
                    }

                    if (PrecioUnitario != descuentoNuevo.Precio && PrecioUnitario != 0)
                    //if (descuentoActual != null && PrecioUnitario == descuentoActual.Precio)
                    {
                        PrecioUnitario = descuentoNuevo.Precio;
                    }
                    if (DescuentoProducto != descuentoNuevo.Descuento)
                    {
                        DescuentoProducto = descuentoNuevo.Descuento;
                    }
                }
                _cantidad = value;
                if (OfertaManual)
                {
                    // Nesto#403: la oferta manual mantiene el regalo pactado con el proveedor y
                    // ajusta las cobradas al nuevo total (Cobradas + Regalo = Cantidad siempre).
                    CantidadRegalo = Math.Min(CantidadRegalo ?? 0, value);
                    CantidadCobrada = value - CantidadRegalo;
                }
                else if (Ofertas != null && Ofertas.Any())
                {
                    ActualizarOfertaActual();
                }
            }
        }
        public int CantidadBruta { get; set; }
        public decimal PrecioUnitario { get; set; }
        //public decimal DescuentoLinea { get; set; }
        public decimal DescuentoProveedor { 
            get => DescuentoEntidad; 
            set => DescuentoEntidad = value; 
        }
        //public decimal DescuentoProducto { get; set; }
        public string CodigoIvaProducto { get; set; }
        //public decimal PorcentajeIva { get; set; }
        public int StockMaximo { get; set; }
        public int PendienteEntregar { get; set; }
        public int PendienteRecibir { get; set; }
        public int Stock { get; set; }
        public int Multiplos { get; set; }
        public string Grupo { get; set; }
        public string Subgrupo { get; set; }
        public decimal PrecioTarifa { get; set; }
        public int EstadoProducto { get; set; }
        public string Delegacion { get; set; }
        public string FormaVenta { get; set; }
        public string CentroCoste { get; set; }
        public string Departamento { get; set; }
        public bool Enviado { get; set; }


        public List<DescuentoCantidadCompra> Descuentos { get; set; }
        private List<OfertaCompra> _ofertas;
        public List<OfertaCompra> Ofertas {
            get => _ofertas;
            set
            {
                if (_ofertas != value)
                {
                    _ofertas = value;
                    if (_ofertas != null && _ofertas.Any())
                    {
                        ActualizarOfertaActual();
                    }
                }
            }
        }


        public override decimal Bruto { get => (decimal)(CantidadCobrada == null ? Cantidad * PrecioUnitario : CantidadCobrada * PrecioUnitario); }
        //public decimal SumaDescuentos { get => AplicarDescuento ? 1 - (1 - DescuentoProveedor) * (1 - DescuentoProducto) * (1 - DescuentoLinea) : 0; }
        //public bool AplicarDescuento { get; set; } = true;
        //public decimal ImporteDescuento { get => Bruto * SumaDescuentos; }
        //public override decimal BaseImponible { get => Bruto - ImporteDescuento; } // Esta hay que cambiarla por la de LineaPedidoBase cuando esté implementada
        //public decimal ImporteIva { get => BaseImponible * PorcentajeIva; }
        //public decimal Total { get => BaseImponible + ImporteIva; }
        public int? CantidadCobrada { get; set; }
        public int? CantidadRegalo { get; set; }

        /// <summary>
        /// Nesto#403: oferta puntual pactada con el proveedor SOLO para este pedido (no está en
        /// OfertasProveedores). Mientras está activa, el recálculo automático por cambio de
        /// cantidad no pisa las cantidades manuales. Al crear el pedido, el servidor genera las
        /// dos líneas (cobrada + regalo a precio 0) exactamente igual que con una oferta de tabla.
        /// </summary>
        public bool OfertaManual { get; private set; }

        /// <summary>Pone el regalo a mano y ajusta las cobradas (Cobradas + Regalo = Cantidad).
        /// Vaciarlo (null o 0) deshace la oferta manual y vuelve al automático de la tabla.</summary>
        public void PonerRegaloManual(int? cantidadRegalo)
        {
            if (cantidadRegalo == null || cantidadRegalo <= 0)
            {
                QuitarOfertaManual();
                return;
            }
            OfertaManual = true;
            CantidadRegalo = Math.Min(cantidadRegalo.Value, Cantidad);
            CantidadCobrada = Cantidad - CantidadRegalo;
        }

        /// <summary>Pone las cobradas a mano y ajusta el regalo (Cobradas + Regalo = Cantidad).
        /// Cobradas &gt;= Cantidad significa que no hay regalo: deshace la oferta manual.</summary>
        public void PonerCobradasManual(int? cantidadCobrada)
        {
            if (cantidadCobrada == null || cantidadCobrada < 0 || cantidadCobrada >= Cantidad)
            {
                QuitarOfertaManual();
                return;
            }
            OfertaManual = true;
            CantidadCobrada = cantidadCobrada;
            CantidadRegalo = Cantidad - cantidadCobrada;
        }

        /// <summary>Deshace la oferta manual y recalcula con las ofertas de OfertasProveedores.</summary>
        public void QuitarOfertaManual()
        {
            OfertaManual = false;
            CantidadCobrada = null;
            CantidadRegalo = null;
            if (Ofertas != null && Ofertas.Any())
            {
                ActualizarOfertaActual();
            }
        }

        private void ActualizarOfertaActual()
        {
            if (Ofertas == null)
            {
                return;
            }
            var oferta = Ofertas.OrderByDescending(o => o.CantidadCobrada + o.CantidadRegalo)
                    .FirstOrDefault(o => o.CantidadCobrada + o.CantidadRegalo <= Cantidad);
            if (oferta == null)
            {
                CantidadCobrada = null;
                CantidadRegalo = null;
                return;
            }
            int multiplo = Cantidad / (oferta.CantidadCobrada + oferta.CantidadRegalo);
            int resto = Cantidad % (oferta.CantidadCobrada + oferta.CantidadRegalo);
            CantidadCobrada = oferta.CantidadCobrada * multiplo;
            CantidadRegalo = oferta.CantidadRegalo * multiplo;
            CantidadCobrada += resto;
        }
    }
}
