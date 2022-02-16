using ControlesUsuario.Dialogs;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Modulos.PedidoCompra.Events;
using Nesto.Modulos.PedidoCompra.Models;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Regions;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Nesto.Modulos.PedidoCompra.ViewModels
{
    public class ListaPedidosCompraViewModel : BindableBase
    {
        public IPedidoCompraService Servicio { get; }
        public IDialogService DialogService { get; }
        private IEventAggregator EventAggregator { get; }

        public ListaPedidosCompraViewModel(IPedidoCompraService servicio, IDialogService dialogService, IEventAggregator eventAggregator)
        {
            Servicio = servicio;
            DialogService = dialogService;
            EventAggregator = eventAggregator;
            CargarPedidosCommand = new DelegateCommand(OnCargarPedidos);

            ListaPedidos = new ColeccionFiltrable(new ObservableCollection<PedidoCompraLookup>());
            ListaPedidos.TieneDatosIniciales = true;
            ListaPedidos.ElementoSeleccionadoChanged += (sender, args) => { CargarPedidoSeleccionado(); };

            EventAggregator.GetEvent<PedidoCompraModificadoEvent>().Subscribe(ActualizarPedidoLookup);
        }

        private bool _estaCargandoListaPedidos;
        public bool EstaCargandoListaPedidos
        {
            get => _estaCargandoListaPedidos;
            set => SetProperty(ref _estaCargandoListaPedidos, value);
        }


        private ColeccionFiltrable _listaPedidos;
        public ColeccionFiltrable ListaPedidos
        {
            get => _listaPedidos;
            set
            {
                SetProperty(ref _listaPedidos, value);
            }
        }
        
        private void ActualizarMostrados()
        {
            /*
            if (ListaPedidos == null)
            {
                ListaPedidos = new();
            }
            if (ListaPedidos.ListaOriginal == null)
            {
                ListaPedidos.ListaOriginal = new ObservableCollection<IFiltrableItem>();
            }
            */
            if (MostrarPedidosCreados && MostrarPedidosSinCrear)
            {
                ListaPedidos.ListaFijada = ListaPedidos.ListaOriginal;
            } else if (MostrarPedidosSinCrear)
            {
                ListaPedidos.ListaFijada = new ObservableCollection<IFiltrableItem>(ListaPedidos.ListaOriginal.Where(p => (p as PedidoCompraLookup).Pedido == 0));
            }
            else if (MostrarPedidosCreados)
            {
                ListaPedidos.ListaFijada = new ObservableCollection<IFiltrableItem>(ListaPedidos.ListaOriginal.Where(p => (p as PedidoCompraLookup).Pedido != 0));
            }
            else
            {
                ListaPedidos.ListaFijada = new ObservableCollection<IFiltrableItem>();
            }
            ListaPedidos.RefrescarFiltro();
        }

        private List<PedidoCompraDTO> _listaPedidosSinCrear;
        public List<PedidoCompraDTO> ListaPedidosSinCrear
        {
            get => _listaPedidosSinCrear;
            set => SetProperty(ref _listaPedidosSinCrear, value);
        }

        private bool _mostrarPedidosCreados = true;
        public bool MostrarPedidosCreados
        {
            get => _mostrarPedidosCreados;
            set {
                SetProperty(ref _mostrarPedidosCreados, value);
                ActualizarMostrados();
            }
        }

        private bool _mostrarPedidosSinCrear;
        public bool MostrarPedidosSinCrear
        {
            get => _mostrarPedidosSinCrear;
            set
            {
                if (value && !_mostrarPedidosSinCrear && (ListaPedidosSinCrear == null || !ListaPedidosSinCrear.Any()))
                {
                    CargarPedidosAutomaticos();
                }
                SetProperty(ref _mostrarPedidosSinCrear, value);
                ActualizarMostrados();
            }
        }

        private async Task CargarPedidosAutomaticos()
        {
            try
            {
                EstaCargandoListaPedidos = true;
                ListaPedidosSinCrear = await Servicio.CargarPedidosAutomaticos(Constantes.Empresas.EMPRESA_DEFECTO);
                foreach (var pedido in ListaPedidosSinCrear)
                {
                    ListaPedidos.ListaOriginal.Add(new PedidoCompraLookup(pedido));
                }
                ActualizarMostrados();
            }
            catch (Exception ex)
            {
                DialogService.ShowError(ex.Message);
            }
            finally
            {
                EstaCargandoListaPedidos = false;
            }
        }

        private IRegionManager _scopedRegionManager;
        public IRegionManager ScopedRegionManager
        {
            get => _scopedRegionManager;
            set => SetProperty(ref _scopedRegionManager, value);
        }

        //private PedidoCompraLookup _pedidoLookupSeleccionado;
        //public PedidoCompraLookup PedidoLookupSeleccionado
        //{
        //    get => _pedidoLookupSeleccionado;
        //    set
        //    {
        //        SetProperty(ref _pedidoLookupSeleccionado, value);
        //        CargarPedidoSeleccionado();
        //    }
        //}

        
        public ICommand CargarPedidosCommand { get; private set; }
        private async void OnCargarPedidos()
        {
            if (ListaPedidos == null || ListaPedidos.Lista == null || !ListaPedidos.Lista.Any())
            {
                try
                {
                    EstaCargandoListaPedidos = true;
                    ListaPedidos.Lista = new ObservableCollection<IFiltrableItem>(await Servicio.CargarPedidos());
                    ListaPedidos.ListaOriginal = ListaPedidos.Lista;
                }
                catch (Exception ex)
                {
                    DialogService.ShowError(ex.Message);
                }
                finally
                {
                    EstaCargandoListaPedidos = false;
                }
                
            }
        }
        private void ActualizarPedidoLookup(PedidoCompraDTO pedido) {
            var lookupActual = ListaPedidos.ListaOriginal.Single(p => (p as PedidoCompraLookup).Pedido == 0 && (p as PedidoCompraLookup).Proveedor == pedido.Proveedor);
            var nuevoLookup = new PedidoCompraLookup(pedido);
            int indexActual = ListaPedidos.ListaOriginal.IndexOf(lookupActual);
            if (indexActual != -1)
            {          
                ListaPedidos.ListaOriginal.Remove(lookupActual);
                ListaPedidos.ListaOriginal.Add(nuevoLookup);
            }
            indexActual = ListaPedidos.ListaFijada.IndexOf(lookupActual);
            if (indexActual != -1)
            {
                ListaPedidos.ListaFijada.Remove(lookupActual);
                ListaPedidos.ListaFijada.Add(nuevoLookup);
            }
            indexActual = ListaPedidos.Lista.IndexOf(lookupActual);
            if (indexActual != -1)
            {
                ListaPedidos.Lista.Remove(lookupActual);
                ListaPedidos.Lista.Add(nuevoLookup);
            }
            ListaPedidos.ElementoSeleccionado = nuevoLookup;
        }

        private void CargarPedidoSeleccionado()
        {
            NavigationParameters parameters;
            if (ListaPedidos.ElementoSeleccionado != null && (ListaPedidos.ElementoSeleccionado as PedidoCompraLookup).Pedido == 0)
            {
                parameters = new NavigationParameters
                {
                    { "PedidoParameter", ListaPedidosSinCrear.Single(p => p.Proveedor == (ListaPedidos.ElementoSeleccionado as PedidoCompraLookup).Proveedor) }
                };
            }
            else
            {
                parameters = new NavigationParameters
                {
                    { "PedidoLookupParameter", ListaPedidos.ElementoSeleccionado }
                };
            }
            ScopedRegionManager.RequestNavigate("DetallePedidoCompraRegion", "DetallePedidoCompraView", parameters);
        }
    }
}
