using Nesto.Infrastructure.Shared;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;

namespace Nesto.Modules.Producto.Models
{
    public class ControlStockProductoWrapper : BindableBase
    {
        public ControlStockProductoWrapper(ControlStockProductoModel model)
        {
            if (model == null)
            {
                return;
            }
            Model = model;
            ControlesStocksAlmacen = new ObservableCollection<ControlStockAlmacenWrapper>();            
            ControlesStocksAlmacen.CollectionChanged += ContentCollectionChanged;
            model.StockMinimoInicial = model.StockMinimoActual;
            foreach (var control in Model.ControlesStocksAlmacen)
            {
                control.StockMaximoInicial = control.StockMaximoActual;
                ControlesStocksAlmacen.Add(new ControlStockAlmacenWrapper(control));
            }         
        }

        private void ContentCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (ControlStockAlmacenWrapper item in e.NewItems)
                {
                    if (item != null)
                    {
                        item.PropertyChanged += DetailOnPropertyChanged;
                        item.ControlStockProducto = this;
                    }
                }
            }

            if (e.OldItems != null)
            {
                foreach (ControlStockAlmacenWrapper item in e.OldItems)
                {
                    if (item != null)
                    {
                        item.PropertyChanged -= DetailOnPropertyChanged;
                        Model.ControlesStocksAlmacen.Remove(item.Model);
                    }
                }
                RaisePropertyChanged(string.Empty);
            }
        }

        private void DetailOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "StockMaximoActual") // no se puede usar NameOf porque vienen de otra clase
            {
                OnStockChanged();
            }
            //RaisePropertyChanged(string.Empty);
        }

        public ControlStockProductoModel Model { get; }
        private ObservableCollection<ControlStockAlmacenWrapper> _controlesStocksAlmacen;
        public ObservableCollection<ControlStockAlmacenWrapper> ControlesStocksAlmacen
        {
            get
            {
                return _controlesStocksAlmacen;
            }
            set
            {
                SetProperty(ref _controlesStocksAlmacen, value);
            }
        }
        public bool DesbloquearControlesStock { get; set; }
        public int StockMaximoInicial => Model != null ? Model.StockMaximoInicial : 0;
        public int StockMinimoActual 
        { 
            get => Model != null ? Model.StockMinimoActual : 0;
            set {
                if (Model.StockMinimoActual == value || value < 0 || value > ControlesStocksAlmacen.Single(c => c.Model.Almacen == Constantes.Almacenes.ALMACEN_CENTRAL).StockMaximoActual)
                {
                    return;
                }
                Model.StockMinimoActual = value;
                RaisePropertyChanged(nameof(StockMinimoActual));
                OnStockChanged();
            }
        }
        public int StockMinimoCalculado => Model != null ? Model.StockMinimoCalculado : 0;
        public int SumaStocksMaximos => Model != null ? Model.SumaStocksMaximos : 0;

        public List<ControlStock> ToListModificados
        {
            get
            {
                List<ControlStock> controles = new();
                foreach (var control in ControlesStocksAlmacen.Where(c => 
                    c.Model.StockMaximoInicial != c.Model.StockMaximoActual || 
                    (c.Model.Almacen == Constantes.Almacenes.ALMACEN_CENTRAL && c.ControlStockProducto.Model.StockMinimoInicial != c.ControlStockProducto.Model.StockMinimoActual)))
                {
                    ControlStock controlStock = new ControlStock
                    {
                        Empresa = Constantes.Empresas.EMPRESA_DEFECTO,
                        Almacén = control.Model.Almacen,
                        Número = Model.ProductoId,
                        Categoria = control.Model.Categoria,
                        Estacionalidad = control.Model.Estacionalidad,
                        Múltiplos = control.Model.Multiplos,
                        StockMínimo = control.Model.Almacen == Constantes.Almacenes.ALMACEN_CENTRAL ? control.ControlStockProducto.StockMinimoActual : 0,
                        StockMáximo = control.StockMaximoActual,
                        YaExiste = control.Model.YaExiste,
                        Usuario = Environment.UserDomainName + "\\" + Environment.UserName,
                        Fecha_Modificación = DateTime.Now
                    };
                    controles.Add(controlStock);
                }
                return controles;
            }
        }

        public event EventHandler StockChanged;

        protected virtual void OnStockChanged()
        {
            StockChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
