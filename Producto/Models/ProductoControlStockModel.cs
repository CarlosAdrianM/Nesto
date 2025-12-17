using Prism.Mvvm;

namespace Nesto.Modules.Producto.Models
{
    public class ProductoControlStockModel : BindableBase
    {
        public string ProductoId { get; set; }
        public string Nombre { get; set; }
        public int StockMinimoActual { get; set; }
        public int StockMinimoCalculado { get; set; }
        public int StockMaximoActual { get; set; }
        public int StockMaximoCalculado { get; set; }
        public bool YaExiste { get; set; }
        public string Categoria { get; set; }
        public string Estacionalidad { get; set; }
        public int Multiplos { get; set; }

        private bool _tieneError;
        public bool TieneError
        {
            get => _tieneError;
            set => SetProperty(ref _tieneError, value);
        }

        private string _mensajeError;
        public string MensajeError
        {
            get => _mensajeError;
            set => SetProperty(ref _mensajeError, value);
        }

        private bool _actualizado;
        public bool Actualizado
        {
            get => _actualizado;
            set => SetProperty(ref _actualizado, value);
        }

        public bool RequiereActualizacion =>
            StockMinimoCalculado != 0 || StockMaximoCalculado != 0;

        public bool TieneCambios =>
            StockMinimoActual != StockMinimoCalculado || StockMaximoActual != StockMaximoCalculado;
    }
}
