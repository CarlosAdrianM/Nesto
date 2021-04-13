using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Nesto.Models.PedidoVenta;

namespace Nesto.Modulos.CanalesExternos
{
    public class PedidoCanalExterno
    {
        public string PedidoCanalId { get; set; }
        public int PedidoNestoId { get; set; }
        public string Nombre { get; set; }
        public string Direccion { get; set; }
        public string CodigoPostal { get; set; }
        public string Poblacion { get; set; }
        public string Provincia { get; set; }
        public string TelefonoFijo { get; set; }
        public string TelefonoMovil { get; set; }
        public string CorreoElectronico { get; set; }
        public string PaisISO { get; set; }
        public string Observaciones { get; set; }
        public PedidoVentaDTO Pedido { get; set; }

    }
}
