using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nesto.Modulos.PedidoCompra.Models
{
    public class DescuentoCantidadCompra
    {
        public int CantidadMinima { get; set; }
        public decimal Precio { get; set; }
        public decimal Descuento { get; set; }
    }
}
