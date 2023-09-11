using System.Security.Policy;

namespace Nesto.Modules.Producto.Models
{
    public class ControlStockAlmacenModel
    {
        public string Almacen {  get; set; }
        public string Categoria { get; set; }
        public string Estacionalidad { get; set; }
        public int Multiplos { get; set; }
        public int StockMaximoActual { get; set; }
        public int StockMaximoCalculado { get; set; }
        public int StockMaximoInicial { get; set; }
        public bool YaExiste { get; set; }
    }
}
