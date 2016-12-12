using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ControlesUsuario.Models
{
    public class FormaPago
    {
        public string formaPago { get; set; }
        public string descripcion { get; set; }
        public bool bloquearPagos { get; set; }
        public bool cccObligatorio { get; set; }
    }
}
