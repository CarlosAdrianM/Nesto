using Prism.Commands;
using Prism.Interactivity.InteractionRequest;
using Prism.Events;
using Prism.Regions;
using Nesto.Contratos;
using Nesto.Modules.Producto;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Input;

namespace Nesto.Modulos.Producto
{
    public class ProductoViewModel : ViewModelBase, INavigationAware
    {
        private IRegionManager RegionManager { get; }
        private IConfiguracion Configuracion { get; }
        private IProductoService Servicio { get; }
        private IEventAggregator EventAggregator { get; }

        private string _filtroNombre;
        private string _filtroFamilia;
        private string _filtroSubgrupo;
        private TabItem _pestannaSeleccionada;
        private ProductoModel _productoActual;
        private ProductoModel _productoResultadoSeleccionado;
        private ObservableCollection<ProductoClienteModel> _clientesResultadoBusqueda;
        private ObservableCollection<ProductoModel> _productosResultadoBusqueda;
        private string _referenciaBuscar;


        public ProductoViewModel(IRegionManager regionManager, IConfiguracion configuracion, IProductoService servicio, IEventAggregator eventAggregator)
        {
            RegionManager = regionManager;
            Configuracion = configuracion;
            Servicio = servicio;
            EventAggregator = eventAggregator;

            AbrirModuloCommand = new DelegateCommand(OnAbrirModulo, CanAbrirModulo);
            BuscarProductoCommand = new DelegateCommand(OnBuscarProducto, CanBuscarProducto);
            BuscarClientesCommand = new DelegateCommand(OnBuscarClientes, CanBuscarClientes);
            SeleccionarProductoCommand = new DelegateCommand(OnSeleccionarProducto, CanSeleccionarProducto);

            Titulo = "Producto";
            NotificationRequest = new InteractionRequest<INotification>();
        }

        public async void CargarProducto()
        {
            try
            {
                ProductoActual = await Servicio.LeerProducto(ReferenciaBuscar);
                if ((ReferenciaBuscar == "" || ReferenciaBuscar ==  null) && ProductoActual != null)
                {
                    ReferenciaBuscar = ProductoActual.Producto;
                }
                Titulo = "Producto " + ProductoActual.Producto;
            } catch (Exception ex)
            {
                NotificationRequest.Raise(new Notification { Content = ex.Message, Title = "Error" });
            }            
        }

        #region "Propiedades Prism"
        public InteractionRequest<INotification> NotificationRequest { get; private set; }
        #endregion

        #region "Propiedades Nesto"
        public ObservableCollection<ProductoClienteModel> ClientesResultadoBusqueda
        {
            get { return _clientesResultadoBusqueda; }
            set { SetProperty(ref _clientesResultadoBusqueda, value); }
        }
        public string FiltroFamilia
        {
            get { return _filtroFamilia; }
            set {
                SetProperty(ref _filtroFamilia, value);
                BuscarProductoCommand.RaiseCanExecuteChanged();
            }
        }

        public string FiltroNombre
        {
            get { return _filtroNombre; }
            set {
                SetProperty(ref _filtroNombre, value);
                BuscarProductoCommand.RaiseCanExecuteChanged();
            }
        }

        public string FiltroSubgrupo
        {
            get { return _filtroSubgrupo; }
            set {
                SetProperty(ref _filtroSubgrupo, value);
                BuscarProductoCommand.RaiseCanExecuteChanged();
            }
        }

        public TabItem PestannaSeleccionada
        {
            get { return _pestannaSeleccionada; }
            set { 
                SetProperty(ref _pestannaSeleccionada, value); 
                if (PestannaSeleccionada?.Header?.ToString() == "Clientes") {
                    BuscarClientesCommand.Execute();
                }
            }
        }

        public ProductoModel ProductoActual {
            get { return _productoActual; }
            set { 
                SetProperty(ref _productoActual, value);
                if (PestannaSeleccionada?.Header?.ToString() == "Clientes")
                {
                    BuscarClientesCommand.Execute();
                }
            }
        }

        public ProductoModel ProductoResultadoSeleccionado
        {
            get { return _productoResultadoSeleccionado; }
            set {
                SetProperty(ref _productoResultadoSeleccionado, value);
                if (ProductoResultadoSeleccionado!= null)
                {
                    ReferenciaBuscar = ProductoResultadoSeleccionado.Producto;
                }
            }
        }

        public ObservableCollection<ProductoModel> ProductosResultadoBusqueda
        {
            get { return _productosResultadoBusqueda; }
            set { SetProperty(ref _productosResultadoBusqueda, value); }
        }
        
        public string ReferenciaBuscar
        {
            get { return _referenciaBuscar; }
            set {
                SetProperty(ref _referenciaBuscar, value);
                CargarProducto();
            }
        }
        #endregion

        #region "Comandos"
        public ICommand AbrirModuloCommand { get; private set; }
        private bool CanAbrirModulo()
        {
            return true;
        }
        private void OnAbrirModulo()
        {
            RegionManager.RequestNavigate("MainRegion", "ProductoView");
        }

        public DelegateCommand BuscarClientesCommand { get; private set; }
        private bool CanBuscarClientes()
        {
            return ProductoActual != null && !string.IsNullOrEmpty(ProductoActual.Producto);
        }
        private async void OnBuscarClientes()
        {
            ICollection<ProductoClienteModel> resultadoBusqueda = await Servicio.BuscarClientes(ProductoActual.Producto);
            ClientesResultadoBusqueda = new ObservableCollection<ProductoClienteModel>();
            foreach (var cliente in resultadoBusqueda)
            {
                ClientesResultadoBusqueda.Add(cliente);
            }
        }

        public DelegateCommand BuscarProductoCommand { get; private set; }
        private bool CanBuscarProducto()
        {
            return (FiltroNombre != null && FiltroNombre.Trim() != "") || (FiltroFamilia != null && FiltroFamilia.Trim() != "") || (FiltroSubgrupo != null && FiltroSubgrupo.Trim()!="");
        }
        private async void OnBuscarProducto()
        {
            ICollection<ProductoModel> resultadoBusqueda = await Servicio.BuscarProductos(FiltroNombre, FiltroFamilia, FiltroSubgrupo);
            ProductosResultadoBusqueda = new ObservableCollection<ProductoModel>();
            foreach (var producto in resultadoBusqueda)
            {
                ProductosResultadoBusqueda.Add(producto);
            }
        }


        public ICommand SeleccionarProductoCommand { get; private set; }
        private bool CanSeleccionarProducto()
        {
            return true;
        }
        private void OnSeleccionarProducto()
        {
            if (ProductoResultadoSeleccionado != null)
            {
                EventAggregator.GetEvent<ProductoSeleccionadoEvent>().Publish(ProductoResultadoSeleccionado.Producto);
                try
                {
                    ProductoView view = (ProductoView)RegionManager.Regions["MainRegion"].ActiveViews.FirstOrDefault();
                    Grid grid = (Grid)view.Content;
                    ProductoViewModel vm = (ProductoViewModel)grid.DataContext;
                    if (vm.Titulo == this.Titulo)
                    {
                        RegionManager.Regions["MainRegion"].Remove(view);
                    }
                } finally
                {

                }
            }
        }

        #endregion

        public new void OnNavigatedTo(NavigationContext navigationContext)
        {
            var parametro = navigationContext.Parameters["numeroProductoParameter"];
            if (parametro != null)
            {
                ReferenciaBuscar = parametro.ToString();
            } else
            {
                ReferenciaBuscar = "";
            }
        }

    }
}
