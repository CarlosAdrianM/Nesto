using System.Collections.Generic;
using System.Linq;

namespace Nesto.Modules.Producto.Models
{
    public class ControlStockProductoModel
    {
        public ControlStockProductoModel()
        {
            ControlesStocksAlmacen = new List<ControlStockAlmacenModel>();
        }
        public string ProductoId { get;set; }
        public int SumaStocksMaximos => ControlesStocksAlmacen.Sum(c => c.StockMaximoActual);
        public int StockMaximoInicial => ControlesStocksAlmacen.Sum(c => c.StockMaximoInicial);
        private int _stockMinimoActual;
        public int StockMinimoActual {
            get => _stockMinimoActual;
            set => _stockMinimoActual = value;
        }
        public int StockMinimoCalculado { get; set; }
        public int StockMinimoInicial { get; set; }
        // Nesto#392: valor de los múltiplos del almacén central al cargar, para detectar cambios
        // (espejo de StockMinimoInicial). Lo rellena el wrapper, no viene de la API.
        public int MultiplosInicial { get; set; }
        public ICollection<ControlStockAlmacenModel> ControlesStocksAlmacen { get; set; }
    }
}
