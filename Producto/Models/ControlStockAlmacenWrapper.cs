using Nesto.Infrastructure.Shared;
using Prism.Mvvm;
using System.Linq;

namespace Nesto.Modules.Producto.Models
{
    public class ControlStockAlmacenWrapper : BindableBase
    {
        public ControlStockAlmacenWrapper(ControlStockAlmacenModel model)
        {
            Model = model;
        }

        public ControlStockAlmacenModel Model { get; }


        public bool IsActive { get; set; }
        public ControlStockProductoWrapper ControlStockProducto { get; set; }
        public bool Modificado => Model.StockMaximoActual != Model.StockMaximoInicial;
        public int StockMaximoActual
        {
            get => Model.StockMaximoActual;
            set {
                if (Model.StockMaximoActual == value || value < 0) {
                    return;
                }
                var nuevoStockMaximo = ControlStockProducto.SumaStocksMaximos + value - Model.StockMaximoActual;
                ControlStockAlmacenWrapper stockCentral = new ControlStockAlmacenWrapper(new ControlStockAlmacenModel());
                int nuevoStockCentral = 0;
                if (Model.Almacen != Constantes.Almacenes.ALMACEN_CENTRAL)
                {
                    stockCentral = ControlStockProducto.ControlesStocksAlmacen.Single(c => c.Model.Almacen == Constantes.Almacenes.ALMACEN_CENTRAL);
                    nuevoStockCentral = stockCentral.StockMaximoActual + Model.StockMaximoActual - value;
                    if (nuevoStockCentral >= 0 && nuevoStockCentral >= ControlStockProducto.StockMinimoActual)
                    {
                        nuevoStockMaximo += nuevoStockCentral - stockCentral.StockMaximoActual;
                    }
                }

                if ((!ControlStockProducto.DesbloquearControlesStock && nuevoStockMaximo > ControlStockProducto.StockMaximoInicial) || 
                    (Model.Almacen == Constantes.Almacenes.ALMACEN_CENTRAL && nuevoStockMaximo < ControlStockProducto.StockMinimoActual) ||
                    (Model.Almacen != Constantes.Almacenes.ALMACEN_CENTRAL && nuevoStockCentral < ControlStockProducto.StockMinimoActual)) {
                    return;
                }
                if (Model.Almacen != Constantes.Almacenes.ALMACEN_CENTRAL && nuevoStockCentral < stockCentral.StockMaximoActual)
                {
                    stockCentral.StockMaximoActual = nuevoStockCentral;
                }
                Model.StockMaximoActual = value;
                if (Model.Almacen != Constantes.Almacenes.ALMACEN_CENTRAL && nuevoStockCentral > stockCentral.StockMaximoActual)
                {
                    stockCentral.StockMaximoActual = nuevoStockCentral;
                }
                RaisePropertyChanged(nameof(StockMaximoActual));
            } 
        }
        public int StockMaximoCalculado => Model != null ? Model.StockMaximoCalculado : 0;
    }
}
