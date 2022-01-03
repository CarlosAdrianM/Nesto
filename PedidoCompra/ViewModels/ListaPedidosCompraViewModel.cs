using ControlesUsuario.Dialogs;
using Nesto.Infrastructure.Shared;
using Nesto.Modulos.PedidoCompra.Models;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Regions;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Nesto.Modulos.PedidoCompra.ViewModels
{
    public class ListaPedidosCompraViewModel : BindableBase
    {
        public IPedidoCompraService Servicio { get; }
        public IDialogService DialogService { get; }

        public ListaPedidosCompraViewModel(IPedidoCompraService servicio, IDialogService dialogService)
        {
            Servicio = servicio;
            DialogService = dialogService;
            CargarPedidosCommand = new DelegateCommand(OnCargarPedidos);
        }

        private bool _estaCargandoListaPedidos;
        public bool EstaCargandoListaPedidos
        {
            get => _estaCargandoListaPedidos;
            set => SetProperty(ref _estaCargandoListaPedidos, value);
        }

        private string _filtro;
        public string Filtro
        {
            get => _filtro;
            set
            {
                SetProperty(ref _filtro, value);
                AplicarFiltro();
            }
        }

        private ObservableCollection<PedidoCompraLookup> _listaPedidos;
        public ObservableCollection<PedidoCompraLookup> ListaPedidos
        {
            get => _listaPedidos;
            set
            {
                SetProperty(ref _listaPedidos, value);
                RaisePropertyChanged(nameof(ListaPedidosFiltrada));
            }
        }

        public ObservableCollection<PedidoCompraLookup> ListaPedidosFiltrada
        {
            get
            {
                if (ListaPedidosOriginal == null)
                {
                    return new ObservableCollection<PedidoCompraLookup>();
                }
                if (MostrarPedidosCreados && MostrarPedidosSinCrear)
                {
                    return ListaPedidosOriginal;
                } else if (MostrarPedidosSinCrear)
                {
                    return new ObservableCollection<PedidoCompraLookup>(ListaPedidosOriginal.Where(p => p.Pedido == 0));
                } else if (MostrarPedidosCreados)
                {
                    return new ObservableCollection<PedidoCompraLookup>(ListaPedidosOriginal.Where(p => p.Pedido != 0));
                }
                else
                {
                    return new ObservableCollection<PedidoCompraLookup>();
                }
            }
        }

        private ObservableCollection<PedidoCompraLookup> _listaPedidosOriginal;
        public ObservableCollection<PedidoCompraLookup> ListaPedidosOriginal
        {
            get => _listaPedidosOriginal;
            set => SetProperty(ref _listaPedidosOriginal, value);
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
                AplicarFiltro();
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
            }
        }

        private void AplicarFiltro()
        {
            if (Filtro != null)
            {
                ListaPedidos = new ObservableCollection<PedidoCompraLookup>(ListaPedidosFiltrada.Where(l => l.Nombre.ToLower().Contains(Filtro.ToLower()) || l.Proveedor == Filtro));
            }
            else
            {
                ListaPedidos = ListaPedidosFiltrada;
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
                    ListaPedidosOriginal.Add(new PedidoCompraLookup(pedido));
                }
                RaisePropertyChanged(nameof(ListaPedidosFiltrada));
                AplicarFiltro();
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

        private PedidoCompraLookup _pedidoLookupSeleccionado;
        public PedidoCompraLookup PedidoLookupSeleccionado
        {
            get => _pedidoLookupSeleccionado;
            set
            {
                SetProperty(ref _pedidoLookupSeleccionado, value);
                CargarPedidoSeleccionado();
            }
        }

        
        public ICommand CargarPedidosCommand { get; private set; }
        private async void OnCargarPedidos()
        {
            if (ListaPedidos == null || !ListaPedidos.Any())
            {
                try
                {
                    EstaCargandoListaPedidos = true;
                    ListaPedidos = await Servicio.CargarPedidos();
                    ListaPedidosOriginal = ListaPedidos;
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

        private void CargarPedidoSeleccionado()
        {
            NavigationParameters parameters;
            if (PedidoLookupSeleccionado != null && PedidoLookupSeleccionado.Pedido == 0)
            {
                parameters = new NavigationParameters
                {
                    { "PedidoParameter", ListaPedidosSinCrear.Single(p => p.Proveedor == PedidoLookupSeleccionado.Proveedor) }
                };
            }
            else
            {
                parameters = new NavigationParameters
                {
                    { "PedidoLookupParameter", PedidoLookupSeleccionado }
                };
            }
            ScopedRegionManager.RequestNavigate("DetallePedidoCompraRegion", "DetallePedidoCompraView", parameters);

        }
    }
}
