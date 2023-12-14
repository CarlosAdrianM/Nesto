using Prism.Commands;
using Prism.Events;
using Prism.Regions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Input;
using Prism.Services.Dialogs;
using Prism.Mvvm;
using ControlesUsuario.Dialogs;
using Nesto.Infrastructure.Events;
using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.Producto;
using Nesto.Modules.Producto.Models;
using Nesto.Infrastructure.Shared;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Microsoft.Reporting.NETCore;
using System.Threading.Tasks;

namespace Nesto.Modules.Producto.ViewModels
{
    public class ProductoViewModel : BindableBase, INavigationAware
    {
        public event EventHandler DatosCargados;
        private IRegionManager RegionManager { get; }
        private IConfiguracion Configuracion { get; }
        private IProductoService Servicio { get; }
        private IEventAggregator EventAggregator { get; }
        private IDialogService DialogService { get; }

        private string _filtroNombre;
        private string _filtroFamilia;
        private string _filtroSubgrupo;
        private Pestannas _pestannaSeleccionada;
        private ProductoModel _productoActual;
        private ProductoModel _productoResultadoSeleccionado;
        private ObservableCollection<ProductoClienteModel> _clientesResultadoBusqueda;
        private ColeccionFiltrable _productosResultadoBusqueda;
        private string _referenciaBuscar;
        public bool EsDelGrupoCompras { get; }
        public bool EsDelGrupoTiendas { get; }
        public bool EsDeGrupoPermitido => EsDelGrupoCompras || EsDelGrupoTiendas;
        public string AlmacenDefecto { get; set; }


        public ProductoViewModel(IRegionManager regionManager, IConfiguracion configuracion, IProductoService servicio, IEventAggregator eventAggregator, IDialogService dialogService)
        {
            RegionManager = regionManager;
            Configuracion = configuracion;
            Servicio = servicio;
            EventAggregator = eventAggregator;
            DialogService = dialogService;

            AbrirModuloCommand = new DelegateCommand(OnAbrirModulo, CanAbrirModulo);
            AbrirProductoWebCommand = new DelegateCommand(OnAbrirProductoWeb, CanAbrirProductoWeb);
            BuscarProductoCommand = new DelegateCommand(OnBuscarProducto, CanBuscarProducto);
            BuscarClientesCommand = new DelegateCommand(OnBuscarClientes, CanBuscarClientes);
            GuardarProductoCommand = new DelegateCommand(OnGuardarProducto, CanGuardarProducto);
            ImprimirEtiquetasProductoCommand = new DelegateCommand(OnImprimirEtiquetasProducto, CanImprimirEtiquetasProducto);
            MontarKitCommand = new DelegateCommand(OnMontarKit, CanMontarKit);
            SeleccionarProductoCommand = new DelegateCommand(OnSeleccionarProducto, CanSeleccionarProducto);

            Titulo = "Producto";

            EsDelGrupoCompras = configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.COMPRAS);
            EsDelGrupoTiendas = configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.TIENDAS);
        }

        public async Task CargarProducto(string productoId)
        {
            try
            {
                EstaCargandoControlesStock = true;
                ControlStock = null;
                ProductoActual = await Servicio.LeerProducto(productoId);
                Titulo = "Producto " + ProductoActual.Producto;
                if (PestannaSeleccionada == Pestannas.Kits && !ProductoActual.EsKit)
                {
                    PestannaSeleccionada = Pestannas.Filtros;
                }
                if (ProductoActual.Estado == Constantes.Productos.Estados.EN_STOCK && (EsDelGrupoCompras || EsDelGrupoTiendas))
                {
                    ControlStock = new ControlStockProductoWrapper(await Servicio.LeerControlStock(ProductoActual.Producto).ConfigureAwait(true));
                    foreach (var controlAlmacen in ControlStock.ControlesStocksAlmacen)
                    {
                        controlAlmacen.IsActive = EsDelGrupoCompras || controlAlmacen.Model.Almacen == AlmacenDefecto;
                    }
                    ControlStock.DesbloquearControlesStock = EsDelGrupoCompras;
                }
                else
                {
                    ControlStock = new ControlStockProductoWrapper(new ControlStockProductoModel());
                }
                DatosCargados?.Invoke(this, EventArgs.Empty);
                ControlStock.StockChanged += ControlStockChanged;
            }
            catch (Exception ex)
            {
                DialogService.ShowError(ex.Message);
                EstaCargandoControlesStock = false;
            } 
            finally
            {
                EstaCargandoControlesStock = false;
            }
        }


        #region "Propiedades Nesto"
        private int _cantidadKitMontar = 1;
        public int CantidadKitMontar
        {
            get => _cantidadKitMontar;
            set 
            { 
                SetProperty(ref _cantidadKitMontar, value);
                MontarKitCommand.RaiseCanExecuteChanged();
            }
        }
        public ObservableCollection<ProductoClienteModel> ClientesResultadoBusqueda
        {
            get { return _clientesResultadoBusqueda; }
            set { SetProperty(ref _clientesResultadoBusqueda, value); }
        }
        public ControlStockProductoWrapper ControlStock { get; set; }
        private bool _estaCargandoControlesStock;
        public bool EstaCargandoControlesStock { 
            get => _estaCargandoControlesStock;
            set => SetProperty(ref _estaCargandoControlesStock, value); 
        }
        
        private bool _estaCargandoProductos;
        public bool EstaCargandoProductos { 
            get => _estaCargandoProductos; 
            private set => SetProperty(ref _estaCargandoProductos, value);
        }

        private int _etiquetaPrimera = 1;
        public int EtiquetaPrimera { 
            get => _etiquetaPrimera; 
            set => SetProperty(ref _etiquetaPrimera, value); 
        }
        public List<int> EtiquetasPosibles { get; set; } = Enumerable.Range(1, 18).ToList();


        public string FiltroFamilia
        {
            get { return _filtroFamilia; }
            set
            {
                SetProperty(ref _filtroFamilia, value);
                BuscarProductoCommand.RaiseCanExecuteChanged();
            }
        }

        public string FiltroNombre
        {
            get { return _filtroNombre; }
            set
            {
                SetProperty(ref _filtroNombre, value);
                BuscarProductoCommand.RaiseCanExecuteChanged();
            }
        }

        public string FiltroSubgrupo
        {
            get { return _filtroSubgrupo; }
            set
            {
                SetProperty(ref _filtroSubgrupo, value);
                BuscarProductoCommand.RaiseCanExecuteChanged();
            }
        }

        public bool MostrarBarraBusqueda => ProductosResultadoBusqueda != null && ProductosResultadoBusqueda.Lista != null && ProductosResultadoBusqueda.Lista.Any();

        public Pestannas PestannaSeleccionada
        {
            get { return _pestannaSeleccionada; }
            set
            {
                SetProperty(ref _pestannaSeleccionada, value);
                //if (PestannaSeleccionada?.Header?.ToString() == "Clientes")
                //{
                //    BuscarClientesCommand.Execute();
                //}
                if (PestannaSeleccionada == Pestannas.Clientes)
                {
                    BuscarClientesCommand.Execute();
                }
            }
        }

        public ProductoModel ProductoActual
        {
            get { return _productoActual; }
            set
            {
                SetProperty(ref _productoActual, value);
                //if (PestannaSeleccionada?.Header?.ToString() == "Clientes")
                //{
                //    BuscarClientesCommand.Execute();
                //}
                if (PestannaSeleccionada == Pestannas.Clientes)
                {
                    BuscarClientesCommand.Execute();
                }
                AbrirProductoWebCommand.RaiseCanExecuteChanged();
            }
        }

        public ProductoModel ProductoResultadoSeleccionado
        {
            get { return _productoResultadoSeleccionado; }
            set
            {
                SetProperty(ref _productoResultadoSeleccionado, value);
                if (ProductoResultadoSeleccionado != null)
                {
                    ReferenciaBuscar = ProductoResultadoSeleccionado.Producto;
                }
            }
        }

        public ColeccionFiltrable ProductosResultadoBusqueda
        {
            get { return _productosResultadoBusqueda; }
            set { SetProperty(ref _productosResultadoBusqueda, value); }
        }

        public string ReferenciaBuscar
        {
            get { return _referenciaBuscar; }
            set
            {
                if (value != _referenciaBuscar)
                {
                    CargarProducto(value);
                    SetProperty(ref _referenciaBuscar, value);
                }
            }
        }
        public string Titulo { get; private set; }
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

        public DelegateCommand AbrirProductoWebCommand { get; private set; }
        private bool CanAbrirProductoWeb()
        {
            return ProductoActual != null && !string.IsNullOrEmpty(ProductoActual.UrlEnlace);
        }
        private async void OnAbrirProductoWeb()
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(ProductoActual.UrlEnlace + "&utm_medium=ficha_producto") { UseShellExecute = true });
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
            return FiltroNombre != null && FiltroNombre.Trim() != "" || FiltroFamilia != null && FiltroFamilia.Trim() != "" || FiltroSubgrupo != null && FiltroSubgrupo.Trim() != "";
        }
        private async void OnBuscarProducto()
        {
            EstaCargandoProductos = true;
            try
            {
                
                ICollection<ProductoModel> resultadoBusqueda = await Servicio.BuscarProductos(FiltroNombre, FiltroFamilia, FiltroSubgrupo);
                var listaResultadoBusqueda = new ObservableCollection<ProductoModel>();
                foreach (var producto in resultadoBusqueda)
                {
                    listaResultadoBusqueda.Add(producto);
                }
                ProductosResultadoBusqueda = new ColeccionFiltrable(listaResultadoBusqueda)
                {
                    TieneDatosIniciales = true
                };
                ProductosResultadoBusqueda.FijarFiltroCommand.Execute("-stock:0");
                if (!ProductosResultadoBusqueda.Lista.Any())
                {
                    ProductosResultadoBusqueda.QuitarFiltroCommand.Execute("-stock:0");
                }
                RaisePropertyChanged(nameof(MostrarBarraBusqueda));
                ImprimirEtiquetasProductoCommand.RaiseCanExecuteChanged();
            }
            catch (Exception ex)
            {
                DialogService.ShowError($"Se ha producido un error al cargar los productos:\n{ex.Message}");
            }
            finally
            {
                EstaCargandoProductos = false;
            }

        }

        public DelegateCommand GuardarProductoCommand { get; private set; }
        private bool CanGuardarProducto()
        {
            return ControlStock != null && 
                (ControlStock.Model.StockMinimoActual != ControlStock.Model.StockMinimoInicial || ControlStock.Model.ControlesStocksAlmacen.Any(c => c.StockMaximoInicial != c.StockMaximoActual));
        }
        private async void OnGuardarProducto()
        {
            var modificados = ControlStock.ToListModificados;
            foreach (var controlStock in modificados)
            {
                if (controlStock.YaExiste)
                {
                    await Servicio.GuardarControlStock(controlStock);
                } 
                else
                {
                    await Servicio.CrearControlStock(controlStock);
                }
                ControlStock.Model.ControlesStocksAlmacen.Single(c => c.Almacen == controlStock.Almacén).StockMaximoInicial = controlStock.StockMáximo;
            }
            GuardarProductoCommand.RaiseCanExecuteChanged();
        }


        public DelegateCommand ImprimirEtiquetasProductoCommand { get; private set; }
        private bool CanImprimirEtiquetasProducto()
        {
            return ProductosResultadoBusqueda != null && ProductosResultadoBusqueda.Lista != null && ProductosResultadoBusqueda.Lista.Any();
        }
        private async void OnImprimirEtiquetasProducto()
        {
            EstaCargandoProductos = true;
            try
            {
                Stream reportDefinition = Assembly.LoadFrom("Informes").GetManifestResourceStream("Nesto.Informes.EtiquetasTienda.rdlc");
                List<string> listaDeProductos = ProductosResultadoBusqueda.Lista.Select(item => (item as ProductoModel).Producto).ToList();
                List<Informes.FilaEtiquetasModel> dataSource = await Informes.FilaEtiquetasModel.CargarDatos(listaDeProductos, EtiquetaPrimera);
                LocalReport report = new LocalReport();
                report.LoadReportDefinition(reportDefinition);
                report.DataSources.Add(new ReportDataSource("FilaEtiquetasDataSet", dataSource));
                /*
                List<ReportParameter> listaParametros = new List<ReportParameter>
                {
                    new ReportParameter("Fecha", "21/09/23"),
                    new ReportParameter("NombreAgencia", "Agencia Prueba")
                };
                report.SetParameters(listaParametros);
                */
                byte[] pdf = report.Render("PDF");
                string fileName = Path.GetTempPath() + "InformeEtiquetasTienda.pdf";
                File.WriteAllBytes(fileName, pdf);
                Process.Start(new ProcessStartInfo(fileName) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                DialogService.ShowError($"No se han podido imprimir las etiquetas.\n{ex.Message}");
            }
            finally
            {
                EstaCargandoProductos = false;
            }

        }

        public DelegateCommand MontarKitCommand { get; private set; }
        private bool CanMontarKit()
        {
            return CantidadKitMontar != 0;
        }
        private async void OnMontarKit()
        {
            try
            {
                var traspaso = await Servicio.MontarKit(ProductoActual.Producto, CantidadKitMontar);
                if (traspaso != 0)
                {
                    ProductoActual = await Servicio.LeerProducto(ProductoActual.Producto);
                    DialogService.ShowNotification($"Creado el kit con nº de traspaso {traspaso}");
                }
                else
                {
                    DialogService.ShowError("No se ha podido crear el kit");
                }
            }
            catch (Exception ex)
            {
                DialogService.ShowError(ex.Message);
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
                    if (vm.Titulo == Titulo)
                    {
                        RegionManager.Regions["MainRegion"].Remove(view);
                    }
                }
                finally
                {

                }
            }
        }

        #endregion

        public new async void OnNavigatedTo(NavigationContext navigationContext)
        {
            var parametro = navigationContext.Parameters["numeroProductoParameter"];
            if (parametro != null)
            {
                ReferenciaBuscar = parametro.ToString();
            }
            else
            {
                ReferenciaBuscar = await Configuracion.leerParametro(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.UltNumProducto);
            }
            AlmacenDefecto = await Configuracion.leerParametro(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.AlmacenPedidoVta);
        }

        public bool IsNavigationTarget(NavigationContext navigationContext)
        {
            return false;
        }

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {

        }

        private void ControlStockChanged(object sender, EventArgs e)
        {
            GuardarProductoCommand.RaiseCanExecuteChanged();
        }
    }

    public enum Pestannas
    {
        Filtros = 0,
        Clientes = 1,
        Kits = 2
    }
}
