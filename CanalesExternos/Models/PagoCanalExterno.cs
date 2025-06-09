using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace Nesto.Modulos.CanalesExternos.Models
{
    public class PagoCanalExterno
    {
        public string PagoExternalId { get; set; }
        public string Estado { get; set; }
        public decimal Importe { get; set; }
        public decimal SaldoInicial { get; set; }
        public DateTime FechaPago { get; set; }
        public DateTime? FechaInicio { get; set; }
        public DateTime? FechaFinal { get; set; }
        public decimal AjusteRetencion { get; set; }
        public decimal RestoAjustes { get; set; }
        public decimal Comision { get; set; }
        public decimal Publicidad { get; set; }
        public string FacturaPublicidad { get; set; }
        public decimal ImporteOriginal { get; set; }
        public string MonedaOriginal { get; set; }
        public decimal CambioDivisas { get; set; }
        public ObservableCollection<DetallePagoCanalExterno> DetallesPago { get; set; }
        public decimal TotalDetallePagos => DetallesPago != null ? DetallesPago.Sum(p => p.Importe) : 0;
        public decimal TotalDetalleComisiones => DetallesPago != null ? DetallesPago.Sum(p => p.Comisiones) : 0;
        public decimal TotalDetallePromociones => DetallesPago != null ? DetallesPago.Sum(p => p.Promociones) : 0;
        public decimal TotalDetalle => TotalDetallePagos + TotalDetalleComisiones + TotalDetallePromociones + AjusteRetencion + RestoAjustes + Comision + Publicidad;
        public decimal Descuadre => Importe - TotalDetalle;
        public int Asiento { get; set; }
    }
}
