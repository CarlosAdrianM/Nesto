using Nesto.Models.Nesto.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ControlesUsuario.Models
{
    public class DireccionesEntregaCliente
    {
        public string contacto { get; set; }
        public bool clientePrincipal { get; set; }
        public string nombre { get; set; }
        public string direccion { get; set; }
        public bool esDireccionPorDefecto { get; set; }
        public string poblacion { get; set; }
        public string comentarios { get; set; }
        public string codigoPostal { get; set; }
        public string provincia { get; set; }
        public Nullable<short> estado { get; set; }
        public string iva { get; set; }
        public string comentarioRuta { get; set; }
        public string comentarioPicking { get; set; }
        public decimal noComisiona { get; set; }
        public bool servirJunto { get; set; }
        public bool mantenerJunto { get; set; }
        public string vendedor { get; set; }
        public string periodoFacturacion { get; set; }
        public string ccc { get; set; }
        public string ruta { get; set; }
        public string formaPago { get; set; }
        public string plazosPago { get; set; }
        public string textoPoblacion
        {
            get
            {
                return String.Format("{0} {1} ({2})", codigoPostal, poblacion, provincia);
            }
        }
        public bool tieneCorreoElectronico { get; set; }
        public bool tieneFacturacionElectronica { get; set; }
        public string nif { get; set; }
    }
}
