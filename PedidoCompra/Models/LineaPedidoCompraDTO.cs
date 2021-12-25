using System;
using System.Collections.Generic;
using System.Linq;

namespace Nesto.Modulos.PedidoCompra.Models
{
    public class LineaPedidoCompraDTO
    {
        public int Id { get; set; }
        public int Estado { get; set; }
        public string TipoLinea { get; set; }
        public string Producto { get; set; }
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

                    //if (PrecioUnitario != descuentoNuevo.Precio)
                    if (descuentoActual != null && PrecioUnitario == descuentoActual.Precio)
                    {
                        PrecioUnitario = descuentoNuevo.Precio;
                    }
                    if (DescuentoProducto != descuentoNuevo.Descuento)
                    {
                        DescuentoProducto = descuentoNuevo.Descuento;
                    }
                }
                _cantidad = value;
                if (Ofertas != null && Ofertas.Any())
                {
                    ActualizarOfertaActual();
                }
            }
        }
        public int CantidadBruta { get; set; }
        public decimal PrecioUnitario { get; set; }
        public decimal DescuentoLinea { get; set; }
        public decimal DescuentoProveedor { get; set; }
        public decimal DescuentoProducto { get; set; }
        public string CodigoIvaProducto { get; set; }
        public decimal PorcentajeIva { get; set; }
        public int StockMaximo { get; set; }
        public int PendienteEntregar { get; set; }
        public int PendienteRecibir { get; set; }
        public int Stock { get; set; }
        public int Multiplos { get; set; }
        public string Grupo { get; set; }
        public string Subgrupo { get; set; }
        public decimal PrecioTarifa { get; set; }
        public int EstadoProducto { get; set; }


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


        public decimal Bruto { get => (decimal)(CantidadCobrada == null ? Cantidad * PrecioUnitario : CantidadCobrada * PrecioUnitario); }
        public decimal SumaDescuentos { get => AplicarDescuentos ? 1 - (1 - DescuentoProveedor) * (1 - DescuentoProducto) * (1 - DescuentoLinea) : 0; }
        public bool AplicarDescuentos { get; set; } = true;
        public decimal ImporteDescuento { get => Bruto * SumaDescuentos; }
        public decimal BaseImponible { get => Bruto - ImporteDescuento; }
        public decimal ImporteIva { get => BaseImponible * PorcentajeIva; }
        public decimal Total { get => BaseImponible + ImporteIva; }
        public int? CantidadCobrada { get; set; }
        public int? CantidadRegalo { get; set; }


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
