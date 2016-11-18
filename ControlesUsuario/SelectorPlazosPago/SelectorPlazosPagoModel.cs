using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ControlesUsuario.Models
{
    public class PlazosPago
    {
        public string plazoPago { get; set; }
        public string descripcion { get; set; }
        public short numeroPlazos { get; set; }
        public short diasPrimerPlazo { get; set; }
        public short diasEntrePlazos { get; set; }
        public short mesesPrimerPlazo { get; set; }
        public short mesesEntrePlazos { get; set; }
        public decimal descuentoPP { get; set; }
        public decimal? financiacion { get; set; }
    }
}
