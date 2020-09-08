using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nesto.Modulos.CanalesExternos.Models
{
    public class DetallePagoCanalExterno
    {
        public int Id { get; set; }
        public string ExternalId { get; set; }
        public string Empresa { get; set; }
        public int Pedido { get; set; }
        public string CuentaContablePago { get; set; }
        public decimal Importe { get; set; }
        public DateTime FechaPago { get; set; }
        public string PagoExternalId { get; set; }
        public decimal Comisiones { get; set; }
        public string ComisionesExternalId { get; set; }
        public decimal Promociones { get; set; }
        public int PedidoCmpComisiones { get; set; }
        public string CuentaContableComisiones { get; set; }
    }
}
