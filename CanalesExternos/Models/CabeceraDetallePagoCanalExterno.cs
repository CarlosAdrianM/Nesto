using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nesto.Modulos.CanalesExternos.Models
{
    public class CabeceraDetallePagoCanalExterno
    {
        public CabeceraDetallePagoCanalExterno()
        {
            DetallePagos = new ObservableCollection<DetallePagoCanalExterno>();
        }
        public decimal AjusteRetencion { get; set; }
        public decimal RestoAjustes { get; set; }
        public decimal Comision { get; set; }
        public decimal Publicidad { get; set; }
        public string FacturaPublicidad { get; set; }
        public string MonedaOriginal { get; set; }
        public decimal CambioDivisas { get; set; }
        public ObservableCollection<DetallePagoCanalExterno> DetallePagos { get; set; }
    }
}
