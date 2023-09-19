using System.Collections.Generic;

namespace Nesto.Modules.Producto.Models
{
    public class DiarioProductoModel
    {
        public string Id { get; set; }
        public string Descripcion { get; set; }
        public bool EstaVacio { get;set; }
        public List<string> Almacenes { get; set; }
    }
}
