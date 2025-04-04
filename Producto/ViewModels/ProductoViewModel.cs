using ControlesUsuario.Dialogs;
using Microsoft.Reporting.NETCore;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Events;
using Nesto.Infrastructure.Shared;
using Nesto.Modules.Producto.Models;
using Nesto.Modulos.Producto;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Regions;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;

namespace Nesto.Modules.Producto.ViewModels
{
    public class ProductoViewModel : BindableBase, INavigationAware
    {
        public event EventHandler DatosCargados;
        private IRegionManager _regionManager { get; }
        private IConfiguracion _configuracion { get; }
        private IProductoService _servicio { get; }
        private IEventAggregator _eventAggregator { get; }
        private IDialogService _dialogService { get; }

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
            _regionManager = regionManager;
            _configuracion = configuracion;
            _servicio = servicio;
            _eventAggregator = eventAggregator;
            _dialogService = dialogService;

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
                ProductoActual = await _servicio.LeerProducto(productoId);
                Titulo = "Producto " + ProductoActual.Producto;
                if (PestannaSeleccionada == Pestannas.Kits && !ProductoActual.EsKit)
                {
                    PestannaSeleccionada = Pestannas.Filtros;
                }
                if (ProductoActual.Estado == Constantes.Productos.Estados.EN_STOCK && (EsDelGrupoCompras || EsDelGrupoTiendas))
                {
                    ControlStock = new ControlStockProductoWrapper(await _servicio.LeerControlStock(ProductoActual.Producto).ConfigureAwait(true));
                    foreach (ControlStockAlmacenWrapper controlAlmacen in ControlStock.ControlesStocksAlmacen)
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
                _dialogService.ShowError(ex.Message);
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
                _ = SetProperty(ref _cantidadKitMontar, value);
                MontarKitCommand.RaiseCanExecuteChanged();
            }
        }
        public ObservableCollection<ProductoClienteModel> ClientesResultadoBusqueda
        {
            get => _clientesResultadoBusqueda; set => SetProperty(ref _clientesResultadoBusqueda, value);
        }
        public ControlStockProductoWrapper ControlStock { get; set; }
        private bool _estaCargandoControlesStock;
        public bool EstaCargandoControlesStock
        {
            get => _estaCargandoControlesStock;
            set => SetProperty(ref _estaCargandoControlesStock, value);
        }

        private bool _estaCargandoProductos;
        public bool EstaCargandoProductos
        {
            get => _estaCargandoProductos;
            private set => SetProperty(ref _estaCargandoProductos, value);
        }

        private int _etiquetaPrimera = 1;
        public int EtiquetaPrimera
        {
            get => _etiquetaPrimera;
            set => SetProperty(ref _etiquetaPrimera, value);
        }
        public List<int> EtiquetasPosibles { get; set; } = Enumerable.Range(1, 18).ToList();


        public string FiltroFamilia
        {
            get => _filtroFamilia;
            set
            {
                _ = SetProperty(ref _filtroFamilia, value);
                BuscarProductoCommand.RaiseCanExecuteChanged();
            }
        }

        public string FiltroNombre
        {
            get => _filtroNombre;
            set
            {
                _ = SetProperty(ref _filtroNombre, value);
                BuscarProductoCommand.RaiseCanExecuteChanged();
            }
        }

        public string FiltroSubgrupo
        {
            get => _filtroSubgrupo;
            set
            {
                _ = SetProperty(ref _filtroSubgrupo, value);
                BuscarProductoCommand.RaiseCanExecuteChanged();
            }
        }

        public bool MostrarBarraBusqueda => ProductosResultadoBusqueda != null && ProductosResultadoBusqueda.Lista != null && ProductosResultadoBusqueda.Lista.Any();

        public Pestannas PestannaSeleccionada
        {
            get => _pestannaSeleccionada;
            set
            {
                _ = SetProperty(ref _pestannaSeleccionada, value);
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
            get => _productoActual;
            set
            {
                _ = SetProperty(ref _productoActual, value);
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
            get => _productoResultadoSeleccionado;
            set
            {
                _ = SetProperty(ref _productoResultadoSeleccionado, value);
                if (ProductoResultadoSeleccionado != null)
                {
                    ReferenciaBuscar = ProductoResultadoSeleccionado.Producto;
                }
            }
        }

        public ColeccionFiltrable ProductosResultadoBusqueda
        {
            get => _productosResultadoBusqueda; set => SetProperty(ref _productosResultadoBusqueda, value);
        }

        public string ReferenciaBuscar
        {
            get => _referenciaBuscar;
            set
            {
                if (value != _referenciaBuscar)
                {
                    _ = CargarProducto(value);
                    _ = SetProperty(ref _referenciaBuscar, value);
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
            _regionManager.RequestNavigate("MainRegion", "ProductoView");
        }

        public DelegateCommand AbrirProductoWebCommand { get; private set; }
        private bool CanAbrirProductoWeb()
        {
            return ProductoActual != null && !string.IsNullOrEmpty(ProductoActual.UrlEnlace);
        }
        private async void OnAbrirProductoWeb()
        {
            _ = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(ProductoActual.UrlEnlace + "&utm_medium=ficha_producto") { UseShellExecute = true });
        }


        public DelegateCommand BuscarClientesCommand { get; private set; }
        private bool CanBuscarClientes()
        {
            return ProductoActual != null && !string.IsNullOrEmpty(ProductoActual.Producto);
        }
        private async void OnBuscarClientes()
        {
            ICollection<ProductoClienteModel> resultadoBusqueda = await _servicio.BuscarClientes(ProductoActual.Producto);
            ClientesResultadoBusqueda = [.. resultadoBusqueda];
        }

        public DelegateCommand BuscarProductoCommand { get; private set; }
        private bool CanBuscarProducto()
        {
            return (FiltroNombre != null && FiltroNombre.Trim() != "") || (FiltroFamilia != null && FiltroFamilia.Trim() != "") || (FiltroSubgrupo != null && FiltroSubgrupo.Trim() != "");
        }
        private async void OnBuscarProducto()
        {
            EstaCargandoProductos = true;
            try
            {

                ICollection<ProductoModel> resultadoBusqueda = await _servicio.BuscarProductos(FiltroNombre, FiltroFamilia, FiltroSubgrupo);
                ObservableCollection<ProductoModel> listaResultadoBusqueda = [.. resultadoBusqueda];
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
                _dialogService.ShowError($"Se ha producido un error al cargar los productos:\n{ex.Message}");
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
            List<ControlStock> modificados = ControlStock.ToListModificados;
            foreach (ControlStock controlStock in modificados)
            {
                if (controlStock.YaExiste)
                {
                    await _servicio.GuardarControlStock(controlStock);
                }
                else
                {
                    await _servicio.CrearControlStock(controlStock);
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
                LocalReport report = new();
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
                _ = Process.Start(new ProcessStartInfo(fileName) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"No se han podido imprimir las etiquetas.\n{ex.Message}");
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
                string almacen = await _configuracion.leerParametro(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.AlmacenInventario);
                int traspaso = await _servicio.MontarKit(almacen, ProductoActual.Producto, CantidadKitMontar);
                if (traspaso != 0)
                {
                    ProductoActual = await _servicio.LeerProducto(ProductoActual.Producto);
                    _dialogService.ShowNotification($"Creado el kit con nº de traspaso {traspaso}");
                    if (almacen == Constantes.Almacenes.ALMACEN_CENTRAL)
                    {
                        await AbrirInformeMontarKitProductos(traspaso);
                    }
                }
                else
                {
                    _dialogService.ShowError("No se ha podido crear el kit");
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
        }

        private static async Task AbrirInformeMontarKitProductos(int traspaso)
        {
            Stream reportDefinition = Assembly.LoadFrom("Informes").GetManifestResourceStream("Nesto.Informes.MontarKitProductos.rdlc");
            List<Informes.MontarKitProductosModel> dataSource = await Informes.MontarKitProductosModel.CargarDatos(traspaso);
            LocalReport report = new();
            report.LoadReportDefinition(reportDefinition);
            report.DataSources.Add(new ReportDataSource("MontarKitProductosDataSet", dataSource));
            List<ReportParameter> listaParametros =
                    [
                        new ReportParameter("Traspaso", traspaso.ToString()),
                    ];
            report.SetParameters(listaParametros);
            byte[] pdf = report.Render("PDF");
            string fileName = Path.GetTempPath() + "InformeMontarKitProductos.pdf";
            File.WriteAllBytes(fileName, pdf);
            _ = Process.Start(new ProcessStartInfo(fileName) { UseShellExecute = true });
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
                _eventAggregator.GetEvent<ProductoSeleccionadoEvent>().Publish(ProductoResultadoSeleccionado.Producto);
                try
                {
                    ProductoView view = (ProductoView)_regionManager.Regions["MainRegion"].ActiveViews.FirstOrDefault();
                    Grid grid = (Grid)view.Content;
                    ProductoViewModel vm = (ProductoViewModel)grid.DataContext;
                    if (vm.Titulo == Titulo)
                    {
                        _regionManager.Regions["MainRegion"].Remove(view);
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
            object parametro = navigationContext.Parameters["numeroProductoParameter"];
            ReferenciaBuscar = parametro != null
                ? parametro.ToString()
                : await _configuracion.leerParametro(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.UltNumProducto);
            AlmacenDefecto = await _configuracion.leerParametro(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.AlmacenPedidoVta);
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
