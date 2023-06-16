using Nesto.Infrastructure.Shared;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace Nesto.Modulos.PedidoCompra.Models
{
    public class PedidoCompraWrapper : BindableBase
    {
        public PedidoCompraWrapper(PedidoCompraDTO pedido, IPedidoCompraService servicio)
        {
            if (pedido == null)
            {
                return;
            }
            Model = pedido;
            UltimaFechaRecepcion = pedido.Fecha;
            UltimoTipoLinea = Constantes.LineasPedido.TiposLinea.PRODUCTO;
            Lineas = new ObservableCollection<LineaPedidoCompraWrapper>();
            Lineas.CollectionChanged += ContentCollectionChanged;
            foreach (var linea in Model.Lineas)
            {
                var lineaNueva = new LineaPedidoCompraWrapper(linea as LineaPedidoCompraDTO, servicio);
                lineaNueva.Pedido = this;
                Lineas.Add(lineaNueva);
            }
        }

        private void ContentCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (LineaPedidoCompraWrapper item in e.NewItems)
                {
                    if (item != null)
                    {
                        item.PropertyChanged += LineaOnPropertyChanged;
                        if (item.Id == 0)
                        {
                            item.TipoLinea = UltimoTipoLinea;
                            item.FechaRecepcion = UltimaFechaRecepcion;
                            item.Pedido = this;
                            int posicion = Lineas.IndexOf(item);
                            ((List<LineaPedidoCompraDTO>)Model.Lineas).Insert(posicion, item.Model);
                        }
                        else
                        {
                            UltimoTipoLinea = item.TipoLinea;
                            UltimaFechaRecepcion = item.FechaRecepcion;
                        }
                    }
                }
            }

            if (e.OldItems != null)
            {
                foreach (LineaPedidoCompraWrapper item in e.OldItems)
                {
                    if (item != null)
                    {
                        item.PropertyChanged -= LineaOnPropertyChanged;
                        Model.Lineas.Remove(item.Model);
                    }
                }
                RaisePropertyChanged(string.Empty);
            }
        }

        private void LineaOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(BaseImponible))
            {
                RaisePropertyChanged(nameof(BaseImponible));
            }
            if (e.PropertyName == nameof(Total))
            {
                RaisePropertyChanged(nameof(Total));
            }
        }

        public PedidoCompraDTO Model { get; set; }

        public int Id
        {
            get => Model != null ? Model.Id : 0;
            set
            {
                Model.Id = value;
                RaisePropertyChanged(nameof(Id));
            }
        }

        public decimal BaseImponible => Model != null ? Model.BaseImponible : 0;
        public decimal Total => Model != null ? Model.Total : 0;
        internal int EstadoDefecto = 1;
        internal string UltimoTipoLinea;
        internal DateTime UltimaFechaRecepcion;

        private ObservableCollection<LineaPedidoCompraWrapper> _lineas;
        public ObservableCollection<LineaPedidoCompraWrapper> Lineas
        {
            get => _lineas;
            set
            {
                foreach (var detail in value)
                {
                    detail.Pedido = this;
                }
                SetProperty(ref _lineas, value); 
            }
        }
    }
}
