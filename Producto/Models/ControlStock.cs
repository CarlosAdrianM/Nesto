using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nesto.Modules.Producto.Models
{
    public class ControlStock
    {
        public string Empresa { get; set; }
        public string Almacén { get; set;}
        public string Número { get; set;}
        public string Categoria { get; set;}
        public string Estacionalidad { get; set;}
        public bool YaExiste { get; set;}
        public int Múltiplos { get; set ; }
        public int StockMínimo { get; set; }
        public int StockMáximo { get; set; }
        public string Usuario { get; set;}
        public DateTime Fecha_Modificación { get; set; }
    }
}
