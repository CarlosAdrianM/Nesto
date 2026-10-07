using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Events;
using Nesto.Infrastructure.Shared;
using Nesto.Modules.Producto.Models;
using Nesto.Modulos.Producto;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Nesto.Modules.Producto.ViewModels
{
    public class ProductoViewModel : ObservableObject, IReceptorNavegacionPestanaNueva
    {
        public event EventHandler DatosCargados;
        public event Action<VideoModel> VideoCompletoSeleccionadoCambiado;
        private IServicioNavegacion _navegacion { get; }
        private IConfiguracion _configuracion { get; }
        private IProductoService _servicio { get; }
        private IMessenger _messenger { get; }
        private IServicioDialogos _dialogService { get; }

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
        public bool EsDelGrupoTiendaOnline { get; }
        public bool EsDeGrupoPermitido => EsDelGrupoCompras || EsDelGrupoTiendas;
        // La pestaña Web la mantienen Compras y Tienda Online. Tiendas NO: son las tiendas
        // físicas, y esto es cosa de la tienda online.
        public bool PuedeEditarDatosWeb => EsDelGrupoCompras || EsDelGrupoTiendaOnline;
        public string AlmacenDefecto { get; set; }


        private readonly Nesto.Infrastructure.Services.InformesService _servicioInformes;

        public ProductoViewModel(IServicioNavegacion navegacion, IConfiguracion configuracion, IProductoService servicio, IMessenger messenger, IServicioDialogos dialogService, IServicioAutenticacion servicioAutenticacion)
        {
            _navegacion = navegacion;
            _configuracion = configuracion;
            _servicio = servicio;
            _messenger = messenger;
            _dialogService = dialogService;
            _servicioInformes = new Nesto.Infrastructure.Services.InformesService(configuracion, servicioAutenticacion);

            AbrirActualizarControlesStockCommand = new RelayCommand(OnAbrirActualizarControlesStock);
            AbrirModuloCommand = new RelayCommand(OnAbrirModulo, CanAbrirModulo);
            AbrirProductoCommand = new RelayCommand<string>(OnAbrirProducto);
            AbrirProductoWebCommand = new RelayCommand(OnAbrirProductoWeb, CanAbrirProductoWeb);
            BuscarProductoCommand = new RelayCommand(OnBuscarProducto, CanBuscarProducto);
            BuscarContextualCommand = new RelayCommand<string>(OnBuscarContextual, CanBuscarContextual);
            BuscarClientesCommand = new RelayCommand(OnBuscarClientes, CanBuscarClientes);
            CorrigeVideoProductoCommand = new RelayCommand(OnCorrigeVideoProducto, CanCorrigeVideoProducto);
            GuardarProductoCommand = new RelayCommand(OnGuardarProducto, CanGuardarProducto);
            GuardarGruposComisionablesCommand = new RelayCommand(OnGuardarGruposComisionables, () => ProductoActual != null);
            GuardarExclusivoProfesionalCommand = new RelayCommand(OnGuardarExclusivoProfesional, () => ProductoActual != null);
            AnnadirCategoriaSecundariaCommand = new RelayCommand(OnAnnadirCategoriaSecundaria, () => SubgrupoWebSeleccionado != null);
            QuitarCategoriaSecundariaCommand = new RelayCommand(OnQuitarCategoriaSecundaria, () => CategoriaSecundariaSeleccionada != null);
            SubirCategoriaSecundariaCommand = new RelayCommand(OnSubirCategoriaSecundaria, CanSubirCategoriaSecundaria);
            BajarCategoriaSecundariaCommand = new RelayCommand(OnBajarCategoriaSecundaria, CanBajarCategoriaSecundaria);
            GuardarCategoriasSecundariasCommand = new RelayCommand(OnGuardarCategoriasSecundarias, () => ProductoActual != null);
            AnnadirVarianteCommand = new RelayCommand(OnAnnadirVariante, CanAnnadirVariante);
            QuitarVarianteCommand = new RelayCommand(OnQuitarVariante, () => VarianteSeleccionada != null);
            SubirVarianteCommand = new RelayCommand(OnSubirVariante, CanSubirVariante);
            BajarVarianteCommand = new RelayCommand(OnBajarVariante, CanBajarVariante);
            GuardarVariantesCommand = new RelayCommand(OnGuardarVariantes, () => ProductoActual != null);
            ImprimirEtiquetasProductoCommand = new RelayCommand(OnImprimirEtiquetasProducto, CanImprimirEtiquetasProducto);
            MontarKitCommand = new RelayCommand(OnMontarKit, CanMontarKit);
            SeleccionarProductoCommand = new RelayCommand(OnSeleccionarProducto, CanSeleccionarProducto);
            AnnadirCodigoBarrasCommand = new RelayCommand(OnAnnadirCodigoBarras, CanAnnadirCodigoBarras);
            HacerPrincipalCodigoBarrasCommand = new RelayCommand(OnHacerPrincipalCodigoBarras, CanCambiarCodigoBarrasSeleccionado);
            DarDeBajaCodigoBarrasCommand = new RelayCommand(OnDarDeBajaCodigoBarras, CanCambiarCodigoBarrasSeleccionado);

            Titulo = "Producto";

            EsDelGrupoCompras = configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.COMPRAS);
            EsDelGrupoTiendas = configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.TIENDAS);
            EsDelGrupoTiendaOnline = configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.TIENDA_ON_LINE);
        }

        public async Task CargarProducto(string productoId)
        {
            try
            {
                EstaCargandoControlesStock = true;
                ControlStock = null;
                ProductoActual = await _servicio.LeerProducto(productoId);
                if (ProductoActual is not null)
                {
                    Titulo = "Producto " + ProductoActual.Producto;
                }
                else
                {
                    Titulo = "Producto";
                    return;
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
                VideosRelacionados = await _servicio.BuscarVideosRelacionados(ProductoActual.Producto);
                if (PestannaSeleccionada == Pestannas.Videos && !TieneVideosRelacionados)
                {
                    PestannaSeleccionada = Pestannas.Filtros;
                }
                ProductosKit = await _servicio.LeerKitsContienePertenece(productoId);
                // NestoAPI#421: la casilla refleja lo que hay en la ficha. No guarda al cargar:
                // se guarda con su botón, como los grupos comisionables.
                ExclusivoProfesional = ProductoActual.ExclusivoProfesional;
                GuardarExclusivoProfesionalCommand.NotifyCanExecuteChanged();
                await CargarCategoriasWebAsync(productoId);
                await CargarVariantesAsync(productoId);
                await CargarCodigosBarrasAsync();
                await CargarGruposComisionablesAsync(productoId);
                if (PestannaSeleccionada == Pestannas.Kits && !ProductosKit.Any())
                {
                    PestannaSeleccionada = Pestannas.Filtros;
                }
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
                MontarKitCommand.NotifyCanExecuteChanged();
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
                BuscarProductoCommand.NotifyCanExecuteChanged();
            }
        }

        public string FiltroNombre
        {
            get => _filtroNombre;
            set
            {
                _ = SetProperty(ref _filtroNombre, value);
                BuscarProductoCommand.NotifyCanExecuteChanged();
                BuscarContextualCommand.NotifyCanExecuteChanged();
            }
        }

        public string FiltroSubgrupo
        {
            get => _filtroSubgrupo;
            set
            {
                _ = SetProperty(ref _filtroSubgrupo, value);
                BuscarProductoCommand.NotifyCanExecuteChanged();
            }
        }

        public bool HayVideosProductosSinReferencia => OtrosProductosEnEsteVideo is not null &&
                                                       OtrosProductosEnEsteVideo.Any(p => !string.IsNullOrWhiteSpace(p.NombreProducto) &&
                                                            string.IsNullOrWhiteSpace(p.Referencia));

        public bool MostrarBarraBusqueda => ProductosResultadoBusqueda != null && ProductosResultadoBusqueda.Lista != null && ProductosResultadoBusqueda.Lista.Any();
        public bool MostrarPestannaKits => ProductosKit != null && ProductosKit.Any();

        public Pestannas PestannaSeleccionada
        {
            get => _pestannaSeleccionada;
            set
            {
                if (SetProperty(ref _pestannaSeleccionada, value))
                {
                    if (PestannaSeleccionada == Pestannas.Clientes)
                    {
                        BuscarClientesCommand.Execute(null);
                    }
                }
            }
        }

        private ObservableCollection<ProductoVideoModel> _otrosProductosEnEsteVideo;
        public ObservableCollection<ProductoVideoModel> OtrosProductosEnEsteVideo
        {
            get => _otrosProductosEnEsteVideo;
            set
            {
                if (SetProperty(ref _otrosProductosEnEsteVideo, value))
                {
                    OnPropertyChanged(nameof(HayVideosProductosSinReferencia));
                }
            }
        }

        public ProductoModel ProductoActual
        {
            get => _productoActual;
            set
            {
                if (SetProperty(ref _productoActual, value))
                {
                    if (PestannaSeleccionada == Pestannas.Clientes)
                    {
                        BuscarClientesCommand.Execute(null);
                    }
                    AbrirProductoWebCommand.NotifyCanExecuteChanged();
                }
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

        public List<KitContienePerteneceModel> _productosKit;
        public List<KitContienePerteneceModel> ProductosKit
        {
            get => _productosKit;
            set
            {
                if (SetProperty(ref _productosKit, value))
                {
                    OnPropertyChanged(nameof(MostrarPestannaKits));
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

        public bool TieneVideosRelacionados => VideosRelacionados is not null && VideosRelacionados.Any();

        public string Titulo { get; private set; }

        public string UrlVideoProductoSeleccionado => VideoCompletoSeleccionado != null && VideoCompletoSeleccionado.Productos != null ?
            VideoCompletoSeleccionado.Productos.FirstOrDefault(p => p.Referencia == ProductoActual.Producto).UrlVideo :
            string.Empty;

        private VideoModel _videoCompletoSeleccionado;
        public VideoModel VideoCompletoSeleccionado
        {
            get => _videoCompletoSeleccionado;
            set
            {
                if (SetProperty(ref _videoCompletoSeleccionado, value))
                {
                    VideoCompletoSeleccionadoCambiado?.Invoke(value);
                    OnPropertyChanged(nameof(UrlVideoProductoSeleccionado));

                    // Filtrar productos del video que mencionan al producto actual (por referencia o por nombre)
                    OtrosProductosEnEsteVideo = new ObservableCollection<ProductoVideoModel>(
                         (_videoCompletoSeleccionado?.Productos != null && ProductoActual != null
                             ? _videoCompletoSeleccionado.Productos
                                 .ToList()
                             : null
                         ) ?? []
                     );

                    OnPropertyChanged(nameof(HayVideosProductosSinReferencia));
                    CorrigeVideoProductoCommand.NotifyCanExecuteChanged();
                }
            }
        }

        private VideoLookupModel _videoRelacionadoSeleccionado;
        public VideoLookupModel VideoRelacionadoSeleccionado
        {
            get => _videoRelacionadoSeleccionado;
            set
            {
                if (SetProperty(ref _videoRelacionadoSeleccionado, value))
                {
                    if (value != null)
                    {
                        // Cargar el video completo
                        _ = Task.Run(async () =>
                        {
                            VideoModel videoCompleto = await _servicio.CargarVideoCompleto(value.Id);

                            // IMPORTANTE: Actualizar en el UI thread
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                VideoCompletoSeleccionado = videoCompleto;
                            });
                        });
                    }
                    else
                    {
                        // Si no hay selección, limpiar el video completo
                        VideoCompletoSeleccionado = null;
                    }
                }
            }
        }

        private List<VideoLookupModel> _videosRelacionados;
        public List<VideoLookupModel> VideosRelacionados
        {
            get => _videosRelacionados;
            set
            {
                if (SetProperty(ref _videosRelacionados, value))
                {
                    OnPropertyChanged(nameof(TieneVideosRelacionados));
                }
            }
        }

        #endregion

        #region "Comandos"
        public RelayCommand AbrirActualizarControlesStockCommand { get; }
        private async void OnAbrirActualizarControlesStock()
        {
            await _dialogService.ShowDialogAsync("ActualizarControlesStockPopupView", new ParametrosDialogo());
        }

        public ICommand AbrirModuloCommand { get; private set; }
        private bool CanAbrirModulo()
        {
            return true;
        }
        private void OnAbrirModulo()
        {
            _navegacion.RequestNavigate("MainRegion", "ProductoView");
        }


        public RelayCommand<string> AbrirProductoCommand { get; private set; }
        private async void OnAbrirProducto(string productoId)
        {
            if (!string.IsNullOrEmpty(productoId))
            {
                var parameters = new ParametrosNavegacion
                {
                    { "numeroProductoParameter", productoId }
                };
                _navegacion.RequestNavigate("MainRegion", "ProductoView", parameters);
            }
        }

        public RelayCommand AbrirProductoWebCommand { get; private set; }
        private bool CanAbrirProductoWeb()
        {
            return ProductoActual != null && !string.IsNullOrEmpty(ProductoActual.UrlEnlace);
        }
        private async void OnAbrirProductoWeb()
        {
            _ = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(ProductoActual.UrlEnlace + "&utm_medium=ficha_producto") { UseShellExecute = true });
        }

        public RelayCommand BuscarClientesCommand { get; private set; }
        private bool CanBuscarClientes()
        {
            return ProductoActual != null && !string.IsNullOrEmpty(ProductoActual.Producto);
        }
        private async void OnBuscarClientes()
        {
            ICollection<ProductoClienteModel> resultadoBusqueda = await _servicio.BuscarClientes(ProductoActual.Producto);
            ClientesResultadoBusqueda = [.. resultadoBusqueda];
        }

        public RelayCommand BuscarProductoCommand { get; private set; }
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
                OnPropertyChanged(nameof(MostrarBarraBusqueda));
                ImprimirEtiquetasProductoCommand.NotifyCanExecuteChanged();
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

        /// <summary>
        /// Issue #341: búsqueda contextual (Lucene) complementaria a la búsqueda por filtros.
        /// Usa el filtro como texto libre y llama al motor de búsqueda contextual ya existente
        /// (mismo que PlantillaVenta). El filtro que se pasa como CommandParameter suele ser
        /// FiltroNombre, pero lo recibimos como parámetro para poder atajar con Alt+C aunque
        /// el foco esté en otro textbox.
        /// </summary>
        public RelayCommand<string> BuscarContextualCommand { get; private set; }
        private bool CanBuscarContextual(string filtro)
        {
            return !string.IsNullOrWhiteSpace(filtro);
        }
        private async void OnBuscarContextual(string filtro)
        {
            EstaCargandoProductos = true;
            try
            {
                ICollection<ProductoModel> resultadoBusqueda = await _servicio.BuscarProductosContextual(filtro, usarBusquedaConAND: false);

                // Issue #341: si el usuario tiene filtros de familia/subgrupo activos en el panel
                // de búsqueda por filtros, los aplicamos también al resultado contextual para que
                // ambos modos de búsqueda sean coherentes.
                if (!string.IsNullOrWhiteSpace(FiltroFamilia))
                {
                    resultadoBusqueda = resultadoBusqueda
                        .Where(p => !string.IsNullOrEmpty(p.Familia) &&
                                    p.Familia.IndexOf(FiltroFamilia.Trim(), StringComparison.OrdinalIgnoreCase) >= 0)
                        .ToList();
                }
                if (!string.IsNullOrWhiteSpace(FiltroSubgrupo))
                {
                    resultadoBusqueda = resultadoBusqueda
                        .Where(p => !string.IsNullOrEmpty(p.Subgrupo) &&
                                    p.Subgrupo.IndexOf(FiltroSubgrupo.Trim(), StringComparison.OrdinalIgnoreCase) >= 0)
                        .ToList();
                }

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
                OnPropertyChanged(nameof(MostrarBarraBusqueda));
                ImprimirEtiquetasProductoCommand.NotifyCanExecuteChanged();
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"Se ha producido un error al buscar productos:\n{ex.Message}");
            }
            finally
            {
                EstaCargandoProductos = false;
            }
        }


        public RelayCommand CorrigeVideoProductoCommand { get; }

        private async void OnCorrigeVideoProducto()
        {
            if (VideoCompletoSeleccionado == null)
            {
                return;
            }

            var dialogParameters = new ParametrosDialogo
            {
                { "producto", VideoCompletoSeleccionado }
            };

            var result = await _dialogService.ShowDialogAsync("CorreccionVideoProductoView", dialogParameters);

            if (result.Result == ResultadoBoton.OK)
            {
                VideoCompletoSeleccionado = await _servicio.CargarVideoCompleto(VideoCompletoSeleccionado.Id);
            }
        }

        private bool CanCorrigeVideoProducto()
        {
            return VideoCompletoSeleccionado != null;
        }


        public RelayCommand GuardarProductoCommand { get; private set; }
        private bool CanGuardarProducto()
        {
            return ControlStock != null &&
                (ControlStock.Model.StockMinimoActual != ControlStock.Model.StockMinimoInicial ||
                 ControlStock.MultiplosActual != ControlStock.Model.MultiplosInicial || // Nesto#392
                 ControlStock.Model.ControlesStocksAlmacen.Any(c => c.StockMaximoInicial != c.StockMaximoActual));
        }
        private async void OnGuardarProducto()
        {
            try
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
                        // Nesto#512: si al final ya existía (409), el servicio lo modifica
                        await _servicio.CrearControlStock(controlStock);
                    }
                    ControlStockAlmacenModel controlAlmacen = ControlStock.Model.ControlesStocksAlmacen.Single(c => c.Almacen == controlStock.Almacén);
                    controlAlmacen.StockMaximoInicial = controlStock.StockMáximo;
                    // Nesto#512: ya está creado; el siguiente guardado con la ficha abierta va por el PUT
                    // (antes repetía el POST y la API contestaba 409)
                    controlAlmacen.YaExiste = true;
                }
                // Refrescar los valores iniciales para que el botón Guardar se desactive tras guardar.
                ControlStock.Model.StockMinimoInicial = ControlStock.Model.StockMinimoActual;
                ControlStock.Model.MultiplosInicial = ControlStock.MultiplosActual;
                GuardarProductoCommand.NotifyCanExecuteChanged();
            }
            catch (Exception ex)
            {
                // Nesto#512: el motivo que da la API, al usuario (antes la excepción escapaba del async void)
                _dialogService.ShowError(ex.Message);
            }
        }

        // NestoAPI#249: grupo alternativo por el que puede comisionar el producto (pestaña Comisiones).
        // Se listan todos los grupos menos el de la ficha como radio buttons: aunque la tabla y la API
        // admiten varios (many-to-many), desde la UI solo se marca UNO (la conversión es a un único
        // grupo y permitir varios solo añadiría ambigüedad de desempate).
        public ObservableCollection<GrupoComisionableModel> GruposComisionables { get; } = new();

        private bool _ningunGrupoComisionable = true;
        public bool NingunGrupoComisionable
        {
            get => _ningunGrupoComisionable;
            set
            {
                if (SetProperty(ref _ningunGrupoComisionable, value) && value)
                {
                    foreach (GrupoComisionableModel grupo in GruposComisionables)
                    {
                        grupo.Seleccionado = false;
                    }
                }
            }
        }

        private async Task CargarGruposComisionablesAsync(string productoId)
        {
            GruposComisionables.Clear();
            List<string> disponibles = await _servicio.LeerGruposProducto();
            List<string> marcados = await _servicio.LeerGruposComisionables(productoId) ?? new List<string>();
            string grupoFicha = ProductoActual?.Grupo?.Trim();
            foreach (string grupo in disponibles.Where(g => !g.Equals(grupoFicha, StringComparison.OrdinalIgnoreCase)))
            {
                GruposComisionables.Add(new GrupoComisionableModel
                {
                    Grupo = grupo,
                    Seleccionado = marcados.Any(m => m != null && m.Trim().Equals(grupo, StringComparison.OrdinalIgnoreCase))
                });
            }
            NingunGrupoComisionable = !GruposComisionables.Any(g => g.Seleccionado);
            GuardarGruposComisionablesCommand.NotifyCanExecuteChanged();
        }

        public RelayCommand GuardarGruposComisionablesCommand { get; private set; }
        private async void OnGuardarGruposComisionables()
        {
            try
            {
                List<string> grupos = GruposComisionables.Where(g => g.Seleccionado).Select(g => g.Grupo).ToList();
                await _servicio.GuardarGruposComisionables(ProductoActual.Producto, grupos);
                _dialogService.ShowNotification("Grupos comisionables guardados correctamente");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
        }


        // NestoAPI#421 / prestashop-nestosync#19: "exclusivo profesional" es un dato de la ficha,
        // NO se deduce de las categorías. Los subgrupos EP* (COS/EPC, APA/EXP, PEL/EXP...) son
        // categorías navegables normales y sus productos se venden al público con normalidad.
        private bool _exclusivoProfesional;
        public bool ExclusivoProfesional
        {
            get => _exclusivoProfesional;
            set => SetProperty(ref _exclusivoProfesional, value);
        }

        public RelayCommand GuardarExclusivoProfesionalCommand { get; private set; }
        private async void OnGuardarExclusivoProfesional()
        {
            try
            {
                await _servicio.GuardarExclusivoProfesional(ProductoActual.Producto, ExclusivoProfesional);
                ProductoActual.ExclusivoProfesional = ExclusivoProfesional;
                _dialogService.ShowNotification(ExclusivoProfesional
                    ? "El producto deja de venderse al público en la tienda online"
                    : "El producto vuelve a venderse al público en la tienda online");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
        }

        // Nesto#456 / NestoAPI#414: categorías comerciales SECUNDARIAS del producto en la tienda
        // online. La principal (Grupo/Subgrupo de la ficha) se ve arriba en solo lectura, para que
        // no haya duda de qué es principal y qué secundario.
        //
        // Se muestran siempre con el código delante ("COS/OFE — Ofertas Estética"): quien mantiene
        // esto son Laura y Enrique, no informática, y con una lista plana de descripciones no hay
        // manera de saber de qué grupo cuelga cada una.
        public ObservableCollection<CategoriaSecundariaModel> CategoriasSecundarias { get; } = new();

        private List<SubgrupoProductoModel> _todosLosSubgrupos = new();
        private string _grupoPrincipal;
        private string _subgrupoPrincipal;

        public ObservableCollection<string> GruposWeb { get; } = new();
        public ObservableCollection<SubgrupoProductoModel> SubgruposDelGrupoWeb { get; } = new();

        private string _categoriaPrincipalTexto;
        public string CategoriaPrincipalTexto
        {
            get => _categoriaPrincipalTexto;
            set => SetProperty(ref _categoriaPrincipalTexto, value);
        }

        private string _grupoWebSeleccionado;
        public string GrupoWebSeleccionado
        {
            get => _grupoWebSeleccionado;
            set
            {
                if (SetProperty(ref _grupoWebSeleccionado, value))
                {
                    RellenarSubgruposDelGrupo();
                }
            }
        }

        private SubgrupoProductoModel _subgrupoWebSeleccionado;
        public SubgrupoProductoModel SubgrupoWebSeleccionado
        {
            get => _subgrupoWebSeleccionado;
            set
            {
                if (SetProperty(ref _subgrupoWebSeleccionado, value))
                {
                    AnnadirCategoriaSecundariaCommand.NotifyCanExecuteChanged();
                }
            }
        }

        private CategoriaSecundariaModel _categoriaSecundariaSeleccionada;
        public CategoriaSecundariaModel CategoriaSecundariaSeleccionada
        {
            get => _categoriaSecundariaSeleccionada;
            set
            {
                if (SetProperty(ref _categoriaSecundariaSeleccionada, value))
                {
                    RefrescarComandosDeCategorias();
                }
            }
        }

        private async Task CargarCategoriasWebAsync(string productoId)
        {
            // En su propio try: que no se pueda cargar la pestaña Web no puede impedir abrir la
            // ficha del producto, que es para lo que la mayoría entra aquí.
            try
            {
                if (!_todosLosSubgrupos.Any())
                {
                    _todosLosSubgrupos = await _servicio.LeerSubgruposProducto();
                    GruposWeb.Clear();
                    foreach (string grupo in _todosLosSubgrupos
                        .Select(sg => sg.Grupo?.Trim())
                        .Where(g => !string.IsNullOrEmpty(g))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(g => g))
                    {
                        GruposWeb.Add(grupo);
                    }
                }

                // La ficha trae el código del subgrupo aparte de su descripción, así que la
                // principal se identifica por código y no emparejando textos.
                _grupoPrincipal = ProductoActual?.Grupo?.Trim();
                _subgrupoPrincipal = ProductoActual?.SubgrupoCodigo?.Trim();
                CategoriaPrincipalTexto = string.IsNullOrEmpty(_subgrupoPrincipal)
                    ? $"{_grupoPrincipal} — {ProductoActual?.Subgrupo?.Trim()}"
                    : $"{_grupoPrincipal}/{_subgrupoPrincipal} — {ProductoActual?.Subgrupo?.Trim()}";

                CategoriasSecundarias.Clear();
                foreach (CategoriaSecundariaModel categoria in await _servicio.LeerCategoriasSecundarias(productoId))
                {
                    CategoriasSecundarias.Add(categoria);
                }
                CategoriaSecundariaSeleccionada = null;
                GrupoWebSeleccionado = null;
                RellenarSubgruposDelGrupo();
                RefrescarComandosDeCategorias();
                GuardarCategoriasSecundariasCommand.NotifyCanExecuteChanged();
            }
            catch (Exception ex)
            {
                _dialogService.ShowError("No se han podido cargar las categorías web: " + ex.Message);
            }
        }

        private void RellenarSubgruposDelGrupo()
        {
            SubgruposDelGrupoWeb.Clear();
            SubgrupoWebSeleccionado = null;
            if (string.IsNullOrEmpty(GrupoWebSeleccionado))
            {
                return;
            }
            foreach (SubgrupoProductoModel subgrupo in _todosLosSubgrupos
                .Where(sg => string.Equals(sg.Grupo?.Trim(), GrupoWebSeleccionado.Trim(), StringComparison.OrdinalIgnoreCase))
                // La principal no se puede añadir como secundaria de sí misma
                .Where(sg => !(string.Equals(sg.Grupo?.Trim(), _grupoPrincipal, StringComparison.OrdinalIgnoreCase)
                            && string.Equals(sg.Subgrupo?.Trim(), _subgrupoPrincipal, StringComparison.OrdinalIgnoreCase)))
                .OrderBy(sg => sg.Nombre))
            {
                SubgruposDelGrupoWeb.Add(subgrupo);
            }
        }

        private void RefrescarComandosDeCategorias()
        {
            QuitarCategoriaSecundariaCommand.NotifyCanExecuteChanged();
            SubirCategoriaSecundariaCommand.NotifyCanExecuteChanged();
            BajarCategoriaSecundariaCommand.NotifyCanExecuteChanged();
        }

        public RelayCommand AnnadirCategoriaSecundariaCommand { get; private set; }
        private void OnAnnadirCategoriaSecundaria()
        {
            SubgrupoProductoModel elegido = SubgrupoWebSeleccionado;
            if (elegido == null)
            {
                return;
            }
            if (CategoriasSecundarias.Any(c => c.EsLaMisma(elegido.Grupo, elegido.Subgrupo)))
            {
                _dialogService.ShowNotification($"El producto ya está en {elegido.Descripcion}");
                return;
            }
            CategoriasSecundarias.Add(new CategoriaSecundariaModel
            {
                Grupo = elegido.Grupo?.Trim(),
                Subgrupo = elegido.Subgrupo?.Trim(),
                DescripcionSubgrupo = elegido.Nombre?.Trim()
            });
            RefrescarComandosDeCategorias();
        }

        public RelayCommand QuitarCategoriaSecundariaCommand { get; private set; }
        private void OnQuitarCategoriaSecundaria()
        {
            if (CategoriaSecundariaSeleccionada == null)
            {
                return;
            }
            _ = CategoriasSecundarias.Remove(CategoriaSecundariaSeleccionada);
            CategoriaSecundariaSeleccionada = null;
        }

        public RelayCommand SubirCategoriaSecundariaCommand { get; private set; }
        private bool CanSubirCategoriaSecundaria()
        {
            return CategoriaSecundariaSeleccionada != null
                && CategoriasSecundarias.IndexOf(CategoriaSecundariaSeleccionada) > 0;
        }
        private void OnSubirCategoriaSecundaria()
        {
            MoverCategoriaSecundaria(-1);
        }

        public RelayCommand BajarCategoriaSecundariaCommand { get; private set; }
        private bool CanBajarCategoriaSecundaria()
        {
            int indice = CategoriaSecundariaSeleccionada == null
                ? -1
                : CategoriasSecundarias.IndexOf(CategoriaSecundariaSeleccionada);
            return indice >= 0 && indice < CategoriasSecundarias.Count - 1;
        }
        private void OnBajarCategoriaSecundaria()
        {
            MoverCategoriaSecundaria(1);
        }

        private void MoverCategoriaSecundaria(int desplazamiento)
        {
            CategoriaSecundariaModel seleccionada = CategoriaSecundariaSeleccionada;
            int indice = CategoriasSecundarias.IndexOf(seleccionada);
            int destino = indice + desplazamiento;
            if (indice < 0 || destino < 0 || destino >= CategoriasSecundarias.Count)
            {
                return;
            }
            CategoriasSecundarias.Move(indice, destino);
            CategoriaSecundariaSeleccionada = seleccionada;   // el orden es lo que viaja: no se pierde el foco
            RefrescarComandosDeCategorias();
        }

        public RelayCommand GuardarCategoriasSecundariasCommand { get; private set; }
        private async void OnGuardarCategoriasSecundarias()
        {
            try
            {
                await _servicio.GuardarCategoriasSecundarias(ProductoActual.Producto, CategoriasSecundarias.ToList());
                _dialogService.ShowNotification(CategoriasSecundarias.Any()
                    ? "Categorías web guardadas. El producto se republica en unos minutos"
                    : "El producto se queda sin categorías secundarias en la web");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
        }


        #region Variantes para la tienda (NestoAPI#477)

        // NestoAPI#477: familia de variantes (color, tapizado...) de la que forma parte el producto.
        // En la tienda es UNA ficha —la de la principal— con una combinación por referencia; el dato
        // vive en Nesto y viaja por el bus. Misma mecánica que las categorías web: la lista completa
        // se guarda de una vez y el orden es la posición. Vale para cualquier casa, no solo Mirplay.
        public ObservableCollection<VarianteModel> Variantes { get; } = new();

        private string _principalVariantes;
        /// <summary>La referencia cuya ficha es la de la tienda. Con familia, la suya; sin familia, esta ficha.</summary>
        public string PrincipalVariantes
        {
            get => _principalVariantes;
            set => SetProperty(ref _principalVariantes, value);
        }

        private string _textoPrincipalVariantes;
        public string TextoPrincipalVariantes
        {
            get => _textoPrincipalVariantes;
            set => SetProperty(ref _textoPrincipalVariantes, value);
        }

        private VarianteModel _varianteSeleccionada;
        public VarianteModel VarianteSeleccionada
        {
            get => _varianteSeleccionada;
            set
            {
                if (SetProperty(ref _varianteSeleccionada, value))
                {
                    RefrescarComandosDeVariantes();
                }
            }
        }

        private string _nuevaVarianteReferencia;
        public string NuevaVarianteReferencia
        {
            get => _nuevaVarianteReferencia;
            set
            {
                if (SetProperty(ref _nuevaVarianteReferencia, value))
                {
                    AnnadirVarianteCommand.NotifyCanExecuteChanged();
                }
            }
        }

        private string _nuevaVarianteAtributo = "Color";
        public string NuevaVarianteAtributo
        {
            get => _nuevaVarianteAtributo;
            set
            {
                if (SetProperty(ref _nuevaVarianteAtributo, value))
                {
                    AnnadirVarianteCommand.NotifyCanExecuteChanged();
                }
            }
        }

        private string _nuevaVarianteValor;
        public string NuevaVarianteValor
        {
            get => _nuevaVarianteValor;
            set
            {
                if (SetProperty(ref _nuevaVarianteValor, value))
                {
                    AnnadirVarianteCommand.NotifyCanExecuteChanged();
                }
            }
        }

        private async Task CargarVariantesAsync(string productoId)
        {
            // En su propio try, como las categorías: que esto falle no puede impedir abrir la ficha.
            try
            {
                Variantes.Clear();
                foreach (VarianteModel variante in await _servicio.LeerVariantes(productoId))
                {
                    Variantes.Add(variante);
                }
                // Con familia manda su principal; sin familia, si se crea una, la principal es esta ficha.
                PrincipalVariantes = Variantes.FirstOrDefault()?.Principal?.Trim() ?? productoId?.Trim();
                bool esEstaFicha = string.Equals(PrincipalVariantes, productoId?.Trim(), StringComparison.OrdinalIgnoreCase);
                TextoPrincipalVariantes = !Variantes.Any()
                    ? $"{PrincipalVariantes} (esta ficha; todavía sin familia)"
                    : esEstaFicha
                        ? $"{PrincipalVariantes} (esta ficha)"
                        : $"{PrincipalVariantes} — esta ficha es una variante suya";
                VarianteSeleccionada = null;
                NuevaVarianteReferencia = null;
                NuevaVarianteValor = null;
                RefrescarComandosDeVariantes();
                GuardarVariantesCommand.NotifyCanExecuteChanged();
            }
            catch (Exception ex)
            {
                _dialogService.ShowError("No se han podido cargar las variantes: " + ex.Message);
            }
        }

        private void RefrescarComandosDeVariantes()
        {
            QuitarVarianteCommand.NotifyCanExecuteChanged();
            SubirVarianteCommand.NotifyCanExecuteChanged();
            BajarVarianteCommand.NotifyCanExecuteChanged();
        }

        public RelayCommand AnnadirVarianteCommand { get; private set; }
        private bool CanAnnadirVariante()
        {
            return !string.IsNullOrWhiteSpace(NuevaVarianteReferencia)
                && !string.IsNullOrWhiteSpace(NuevaVarianteAtributo)
                && !string.IsNullOrWhiteSpace(NuevaVarianteValor);
        }
        private async void OnAnnadirVariante()
        {
            try
            {
                string referencia = NuevaVarianteReferencia?.Trim();
                if (Variantes.Any(v => v.EsLaMisma(referencia)))
                {
                    _dialogService.ShowNotification($"La referencia {referencia} ya está en la familia");
                    return;
                }
                // Se lee la ficha para tener el nombre en la lista y para no añadir referencias que no existen.
                ProductoModel producto = await _servicio.LeerProducto(referencia);
                if (producto == null)
                {
                    _dialogService.ShowError($"La referencia {referencia} no existe");
                    return;
                }
                Variantes.Add(new VarianteModel
                {
                    Numero = referencia,
                    Principal = PrincipalVariantes,
                    Atributo = NuevaVarianteAtributo?.Trim(),
                    Valor = NuevaVarianteValor?.Trim(),
                    Nombre = producto.Nombre?.Trim(),
                    Orden = Variantes.Count + 1
                });
                NuevaVarianteReferencia = null;
                NuevaVarianteValor = null;
                RefrescarComandosDeVariantes();
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
        }

        public RelayCommand QuitarVarianteCommand { get; private set; }
        private void OnQuitarVariante()
        {
            if (VarianteSeleccionada == null)
            {
                return;
            }
            _ = Variantes.Remove(VarianteSeleccionada);
            VarianteSeleccionada = null;
        }

        public RelayCommand SubirVarianteCommand { get; private set; }
        private bool CanSubirVariante()
        {
            return VarianteSeleccionada != null && Variantes.IndexOf(VarianteSeleccionada) > 0;
        }
        private void OnSubirVariante()
        {
            MoverVariante(-1);
        }

        public RelayCommand BajarVarianteCommand { get; private set; }
        private bool CanBajarVariante()
        {
            int indice = VarianteSeleccionada == null ? -1 : Variantes.IndexOf(VarianteSeleccionada);
            return indice >= 0 && indice < Variantes.Count - 1;
        }
        private void OnBajarVariante()
        {
            MoverVariante(1);
        }

        private void MoverVariante(int desplazamiento)
        {
            VarianteModel seleccionada = VarianteSeleccionada;
            int indice = Variantes.IndexOf(seleccionada);
            int destino = indice + desplazamiento;
            if (indice < 0 || destino < 0 || destino >= Variantes.Count)
            {
                return;
            }
            Variantes.Move(indice, destino);
            VarianteSeleccionada = seleccionada;
            RefrescarComandosDeVariantes();
        }

        public RelayCommand GuardarVariantesCommand { get; private set; }
        private async void OnGuardarVariantes()
        {
            try
            {
                await _servicio.GuardarVariantes(PrincipalVariantes, Variantes.ToList());
                _dialogService.ShowNotification(Variantes.Any()
                    ? "Variantes guardadas. La familia se republica en la web en unos minutos"
                    : "La familia de variantes se ha deshecho: sus referencias vuelven a ser productos sueltos en la web");
                await CargarVariantesAsync(ProductoActual.Producto);
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
        }

        #endregion

        #region Códigos de barras (NestoAPI#605)

        // NestoAPI#605: un producto tiene varios códigos de barras (el proveedor lo cambia por lote, la caja
        // de 100 lleva el suyo...). El principal sigue siendo Productos.CodBarras y lo mantiene la API; aquí
        // se añaden, se cambia cuál es el principal y se dan de baja. Cada acción va directa a la API (no
        // hay "Guardar"): la lista se recarga después de cada una.
        public ObservableCollection<CodigoBarrasProductoModel> CodigosBarras { get; } = new();

        /// <summary>Mismo criterio que el resto de datos de la ficha que se editan aquí: el grupo Compras.</summary>
        public bool PuedeEditarCodigosBarras => EsDelGrupoCompras;

        private bool _hayCodigosBarras;
        /// <summary>False si la API publicada todavía no tiene el endpoint: entonces la pestaña no se ve.</summary>
        public bool HayCodigosBarras
        {
            get => _hayCodigosBarras;
            private set => SetProperty(ref _hayCodigosBarras, value);
        }

        private string _codigoBarrasPrincipal;
        public string CodigoBarrasPrincipal
        {
            get => _codigoBarrasPrincipal;
            private set => SetProperty(ref _codigoBarrasPrincipal, value);
        }

        private CodigoBarrasProductoModel _codigoBarrasSeleccionado;
        public CodigoBarrasProductoModel CodigoBarrasSeleccionado
        {
            get => _codigoBarrasSeleccionado;
            set
            {
                if (SetProperty(ref _codigoBarrasSeleccionado, value))
                {
                    RefrescarComandosDeCodigosBarras();
                }
            }
        }

        private string _nuevoCodigoBarras;
        public string NuevoCodigoBarras
        {
            get => _nuevoCodigoBarras;
            set
            {
                if (SetProperty(ref _nuevoCodigoBarras, value))
                {
                    AnnadirCodigoBarrasCommand.NotifyCanExecuteChanged();
                }
            }
        }

        private int _nuevaCantidadCodigoBarras = 1;
        public int NuevaCantidadCodigoBarras
        {
            get => _nuevaCantidadCodigoBarras;
            set
            {
                if (SetProperty(ref _nuevaCantidadCodigoBarras, value))
                {
                    AnnadirCodigoBarrasCommand.NotifyCanExecuteChanged();
                }
            }
        }

        private string _nuevoProveedorCodigoBarras;
        public string NuevoProveedorCodigoBarras
        {
            get => _nuevoProveedorCodigoBarras;
            set => SetProperty(ref _nuevoProveedorCodigoBarras, value);
        }

        internal async Task CargarCodigosBarrasAsync()
        {
            // En su propio try, como las variantes: que esto falle no puede impedir abrir la ficha.
            try
            {
                CodigosBarras.Clear();
                CodigoBarrasSeleccionado = null;
                string producto = ProductoActual?.Producto?.Trim();
                List<CodigoBarrasProductoModel> codigos = string.IsNullOrEmpty(producto)
                    ? null
                    : await _servicio.LeerCodigosBarras(producto);
                // TODO NestoAPI#605: con la API nueva en producción, null (404) ya no debería llegar nunca.
                HayCodigosBarras = codigos != null;
                foreach (CodigoBarrasProductoModel codigo in (codigos ?? new List<CodigoBarrasProductoModel>())
                    .Where(c => c.Activo)
                    .OrderByDescending(c => c.Principal)
                    .ThenBy(c => c.Cantidad)
                    .ThenByDescending(c => c.Fecha))
                {
                    CodigosBarras.Add(codigo);
                }
                CodigoBarrasPrincipal = CodigosBarras.FirstOrDefault(c => c.Principal)?.Codigo?.Trim()
                    ?? ProductoActual?.CodigoBarras?.Trim();
                if (ProductoActual != null && HayCodigosBarras)
                {
                    ProductoActual.CodigoBarras = CodigoBarrasPrincipal;
                }
            }
            catch (Exception ex)
            {
                HayCodigosBarras = false;
                _dialogService.ShowError("No se han podido cargar los códigos de barras: " + ex.Message);
            }
            finally
            {
                RefrescarComandosDeCodigosBarras();
            }
        }

        private void RefrescarComandosDeCodigosBarras()
        {
            AnnadirCodigoBarrasCommand.NotifyCanExecuteChanged();
            HacerPrincipalCodigoBarrasCommand.NotifyCanExecuteChanged();
            DarDeBajaCodigoBarrasCommand.NotifyCanExecuteChanged();
        }

        public RelayCommand AnnadirCodigoBarrasCommand { get; private set; }
        private bool CanAnnadirCodigoBarras()
        {
            return PuedeEditarCodigosBarras && HayCodigosBarras && ProductoActual != null
                && !string.IsNullOrWhiteSpace(NuevoCodigoBarras) && NuevaCantidadCodigoBarras > 0;
        }
        private async void OnAnnadirCodigoBarras()
        {
            try
            {
                string producto = ProductoActual.Producto.Trim();
                string codigo = NuevoCodigoBarras.Trim();
                RespuestaAnnadirCodigoBarras respuesta = await _servicio.AnnadirCodigoBarras(
                    producto, codigo, NuevaCantidadCodigoBarras, NuevoProveedorCodigoBarras, permitirCompartido: false);

                if (respuesta.Resultado == ResultadoAnnadirCodigoBarras.EnOtroProducto)
                {
                    // Caso guantes: el proveedor manda el mismo código para dos tallas. Se comparte solo si
                    // quien edita la ficha lo confirma expresamente.
                    if (!await _dialogService.ShowConfirmationAsync("Código de barras de otro producto", TextoCodigoEnOtroProducto(codigo, respuesta)))
                    {
                        return;
                    }
                    respuesta = await _servicio.AnnadirCodigoBarras(
                        producto, codigo, NuevaCantidadCodigoBarras, NuevoProveedorCodigoBarras, permitirCompartido: true);
                }

                if (respuesta.Resultado == ResultadoAnnadirCodigoBarras.YaEraDelProducto)
                {
                    _dialogService.ShowNotification($"El código {codigo} ya era de este producto");
                }
                NuevoCodigoBarras = null;
                NuevaCantidadCodigoBarras = 1;
                NuevoProveedorCodigoBarras = null;
                await CargarCodigosBarrasAsync();
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
        }

        internal static string TextoCodigoEnOtroProducto(string codigo, RespuestaAnnadirCodigoBarras respuesta)
        {
            List<ProductoDelCodigoBarrasModel> productos = respuesta?.Productos ?? new List<ProductoDelCodigoBarrasModel>();
            if (!productos.Any())
            {
                string mensaje = string.IsNullOrWhiteSpace(respuesta?.Mensaje)
                    ? $"El código {codigo} ya es de otro producto."
                    : respuesta.Mensaje.Trim();
                return mensaje + Environment.NewLine + Environment.NewLine + "¿Añadirlo también a este?";
            }
            string lista = string.Join(", ", productos.Select(p => $"{p.Producto?.Trim()} {p.Nombre?.Trim()}".Trim()));
            string de = productos.Count == 1 ? "del producto" : "de los productos";
            return $"Ese código ya es {de} {lista}." + Environment.NewLine + Environment.NewLine + "¿Añadirlo también a este?";
        }

        public RelayCommand HacerPrincipalCodigoBarrasCommand { get; private set; }
        public RelayCommand DarDeBajaCodigoBarrasCommand { get; private set; }
        private bool CanCambiarCodigoBarrasSeleccionado()
        {
            // El principal ni se vuelve a hacer principal ni se puede dar de baja (la API lo rechaza con 400):
            // para quitarlo, antes se hace principal otro.
            return PuedeEditarCodigosBarras && ProductoActual != null
                && CodigoBarrasSeleccionado != null && !CodigoBarrasSeleccionado.Principal;
        }

        private async void OnHacerPrincipalCodigoBarras()
        {
            try
            {
                CodigoBarrasProductoModel elegido = CodigoBarrasSeleccionado;
                await _servicio.HacerPrincipalCodigoBarras(ProductoActual.Producto.Trim(), elegido.Id);
                await CargarCodigosBarrasAsync();
                _dialogService.ShowNotification($"El código principal del producto es ahora el {elegido.Codigo?.Trim()}");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
        }

        private async void OnDarDeBajaCodigoBarras()
        {
            try
            {
                CodigoBarrasProductoModel elegido = CodigoBarrasSeleccionado;
                if (!await _dialogService.ShowConfirmationAsync("Dar de baja código de barras",
                    $"¿Dar de baja el código {elegido.Codigo?.Trim()}? Al leerlo con el escáner ya no saldrá este producto."))
                {
                    return;
                }
                await _servicio.DarDeBajaCodigoBarras(ProductoActual.Producto.Trim(), elegido.Id);
                await CargarCodigosBarrasAsync();
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
        }

        #endregion

        public RelayCommand ImprimirEtiquetasProductoCommand { get; private set; }
        private bool CanImprimirEtiquetasProducto()
        {
            return ProductosResultadoBusqueda != null && ProductosResultadoBusqueda.Lista != null && ProductosResultadoBusqueda.Lista.Any();
        }
        private async void OnImprimirEtiquetasProducto()
        {
            EstaCargandoProductos = true;
            try
            {
                List<string> listaDeProductos = ProductosResultadoBusqueda.Lista.Select(item => (item as ProductoModel).Producto).ToList();
                // Nesto#340 (Fase 2, 21/08/26): flag MotorPdfEtiquetasTienda retirado. El piloto
                // se comparó contra el papel precortado real y nadie tiene ya el parámetro en RDLC.
                // Era el ÚLTIMO informe que renderizaba en local: con esto se va ReportViewer.
                byte[] pdf = await _servicioInformes.DescargarEtiquetasTiendaPdf(listaDeProductos, EtiquetaPrimera);
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

        public RelayCommand MontarKitCommand { get; private set; }
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
                        await AbrirInformeMontarKitProductos(traspaso, _servicioInformes);
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

        // Nesto#340 (Fase 2 RDLC→QuestPDF): el PDF se genera en NestoAPI y se descarga, en vez de
        // renderizar MontarKitProductos.rdlc en local. Informe interno de almacén → switch directo
        // sin flag (la lección del flag aplica a los informes que salen al cliente/almacén externo).
        private static async Task AbrirInformeMontarKitProductos(int traspaso, Nesto.Infrastructure.Services.InformesService servicioInformes)
        {
            byte[] pdf = await servicioInformes.DescargarMontarKitProductosPdf(traspaso);
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
                _messenger.Send(new ProductoSeleccionadoMensaje(ProductoResultadoSeleccionado.Producto));
                // Elegido el producto, se cierra la ficha de Productos si es la pestaña activa. Se reconoce por
                // su ViewModel (el DataContext de su contenido), sin conocer el tipo de la vista (Nesto#490).
                object vista = _navegacion.VistaActiva("MainRegion");
                object vmActivo = (vista as FrameworkElement)?.DataContext
                    ?? ((vista as ContentControl)?.Content as FrameworkElement)?.DataContext;
                if (vmActivo is ProductoViewModel vm && vm.Titulo == Titulo)
                {
                    _navegacion.CerrarVistaActiva("MainRegion");
                }
            }
        }

        #endregion


        // Nesto#490 (4C.4): cada navegación abre una ficha de productos nueva (antes IsNavigationTarget = false).
        // Mismas claves que con Prism; GetValue<object> devuelve null si no vienen, como su indexador.
        public async void AlLlegar(ParametrosNavegacion parametros)
        {
            AlmacenDefecto = await _configuracion.leerParametro(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.AlmacenPedidoVta);

            // Issue #343: permitir llegar con un texto de búsqueda contextual (p. ej. desde
            // el diálogo "Reportar Error" de Videos) para lanzar directamente la búsqueda por
            // nombre, igual que un Alt+C.
            string busquedaContextual = parametros?.GetValue<object>("busquedaContextualParameter") as string;
            if (!string.IsNullOrWhiteSpace(busquedaContextual))
            {
                FiltroNombre = busquedaContextual;
                if (BuscarContextualCommand.CanExecute(busquedaContextual))
                {
                    BuscarContextualCommand.Execute(busquedaContextual);
                }
                return;
            }

            object parametro = parametros?.GetValue<object>("numeroProductoParameter");
            ReferenciaBuscar = parametro != null
                ? parametro.ToString()
                : await _configuracion.leerParametro(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.UltNumProducto);
        }

        private void ControlStockChanged(object sender, EventArgs e)
        {
            GuardarProductoCommand.NotifyCanExecuteChanged();
        }
    }

    public enum Pestannas
    {
        Filtros = 0,
        Clientes = 1,
        Videos = 2,
        Kits = 3
    }
}
