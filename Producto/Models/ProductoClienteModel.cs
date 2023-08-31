using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nesto.Modules.Producto.Models
{
    public class ProductoClienteModel
    {
        public string Vendedor { get; set; }
        public string Cliente { get; set; }
        public string Contacto { get; set; }
        public string Nombre { get; set; }
        public string Direccion { get; set; }
        public string CodigoPostal { get; set; }
        public string Poblacion { get; set; }
        public int Cantidad { get; set; }
        public DateTime UltimaCompra { get; set; }
        public int EstadoMinimo { get; set; }
        public int EstadoMaximo { get; set; }
    }
}
