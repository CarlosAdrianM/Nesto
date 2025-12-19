using ControlesUsuario;
using ControlesUsuario.Models;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Modules.Producto.Models;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static ControlesUsuario.Models.SelectorProveedorModel;

namespace Nesto.Modules.Producto.ViewModels
{
    public class ActualizarControlesStockPopupViewModel : BindableBase, IDialogAware
    {
        private readonly IProductoService _productoService;
        private readonly IConfiguracion _configuracion;
        private Action<IDialogResult> _requestClose;
        private SelectorProveedor _selectorProveedor;
        private CancellationTokenSource _cancellationTokenSource;

        public ActualizarControlesStockPopupViewModel(IProductoService productoService, IConfiguracion configuracion)
        {
            _productoService = productoService ?? throw new ArgumentNullException(nameof(productoService));
            _configuracion = configuracion ?? throw new ArgumentNullException(nameof(configuracion));
            Productos = new ObservableCollection<ProductoControlStockModel>();

            BuscarProductosCommand = new DelegateCommand(async () => await OnBuscarProductosAsync(), CanBuscarProductos);
            ActualizarCommand = new DelegateCommand(async () => await OnActualizarAsync(), CanActualizar);
        }

        #region IDialogAware

        event Action<IDialogResult> IDialogAware.RequestClose
        {
            add => _requestClose += value;
            remove => _requestClose -= value;
        }

        public string Title => "Actualizar Controles de Stock por Proveedor";

        public bool CanCloseDialog() => !EstaActualizando;

        public void OnDialogClosed() { }

        public async void OnDialogOpened(IDialogParameters parameters)
        {
            try
            {
                AlmacenSeleccionado = await _configuracion.leerParametro(
                    Constantes.Empresas.EMPRESA_DEFECTO,
                    Parametros.Claves.AlmacenPedidoVta);
            }
            catch
            {
                AlmacenSeleccionado = Constantes.Almacenes.ALMACEN_CENTRAL;
            }
        }

        public void CerrarDialogo(ButtonResult resultado)
        {
            // Cancelar operación en curso si existe
            _cancellationTokenSource?.Cancel();
            _requestClose?.Invoke(new DialogResult(resultado));
        }

        #endregion

        #region Propiedades

        private string _almacenSeleccionado;
        public string AlmacenSeleccionado
        {
            get => _almacenSeleccionado;
            set
            {
                if (SetProperty(ref _almacenSeleccionado, value))
                {
                    BuscarProductosCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private ObservableCollection<ProductoControlStockModel> _productos;
        public ObservableCollection<ProductoControlStockModel> Productos
        {
            get => _productos;
            set => SetProperty(ref _productos, value);
        }

        private bool _estaCargando;
        public bool EstaCargando
        {
            get => _estaCargando;
            set
            {
                if (SetProperty(ref _estaCargando, value))
                {
                    RaisePropertyChanged(nameof(PuedeInteractuar));
                    RaisePropertyChanged(nameof(PuedeActualizar));
                    BuscarProductosCommand.RaiseCanExecuteChanged();
                    ActualizarCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private bool _estaActualizando;
        public bool EstaActualizando
        {
            get => _estaActualizando;
            set
            {
                if (SetProperty(ref _estaActualizando, value))
                {
                    RaisePropertyChanged(nameof(PuedeInteractuar));
                    RaisePropertyChanged(nameof(PuedeActualizar));
                    BuscarProductosCommand.RaiseCanExecuteChanged();
                    ActualizarCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private int _progresoActual;
        public int ProgresoActual
        {
            get => _progresoActual;
            set => SetProperty(ref _progresoActual, value);
        }

        private int _totalAActualizar;
        public int TotalAActualizar
        {
            get => _totalAActualizar;
            set => SetProperty(ref _totalAActualizar, value);
        }

        private string _mensajeProgreso;
        public string MensajeProgreso
        {
            get => _mensajeProgreso;
            set => SetProperty(ref _mensajeProgreso, value);
        }

        private string _mensajeResultado;
        public string MensajeResultado
        {
            get => _mensajeResultado;
            set
            {
                if (SetProperty(ref _mensajeResultado, value))
                {
                    RaisePropertyChanged(nameof(HayResultado));
                }
            }
        }

        public bool HayResultado => !string.IsNullOrEmpty(MensajeResultado);
        public bool HayProductos => Productos?.Any() == true;
        public bool PuedeInteractuar => !EstaCargando && !EstaActualizando;
        public bool PuedeActualizar => PuedeInteractuar && ProductosParaActualizar > 0;

        public int TotalProductos => Productos?.Count ?? 0;
        public int ProductosAActualizar => Productos?.Count(p => !p.Actualizado && p.TieneCambios && p.YaExiste) ?? 0;
        public int ProductosACrear => Productos?.Count(p => !p.Actualizado && p.RequiereActualizacion && !p.YaExiste) ?? 0;
        public int ProductosActualizados => Productos?.Count(p => p.Actualizado) ?? 0;
        public int ProductosConError => Productos?.Count(p => p.TieneError) ?? 0;
        public int ProductosSinCambios => TotalProductos - ProductosAActualizar - ProductosACrear - ProductosActualizados - ProductosConError;
        private int ProductosParaActualizar => ProductosAActualizar + ProductosACrear;

        #endregion

        #region Comandos

        public DelegateCommand BuscarProductosCommand { get; }
        public DelegateCommand ActualizarCommand { get; }

        private bool CanBuscarProductos()
        {
            return !EstaCargando && !EstaActualizando &&
                   !string.IsNullOrEmpty(AlmacenSeleccionado) &&
                   AlmacenSeleccionado != SelectorAlmacen.VALOR_VARIOS &&
                   ProveedorSeleccionado != null;
        }

        private async Task OnBuscarProductosAsync()
        {
            if (!CanBuscarProductos()) return;

            try
            {
                EstaCargando = true;
                MensajeResultado = string.Empty;
                Productos.Clear();

                var productos = await _productoService.LeerProductosProveedorControlStock(
                    ProveedorSeleccionado.Proveedor,
                    AlmacenSeleccionado);

                foreach (var producto in productos)
                {
                    Productos.Add(producto);
                }

                RaisePropertyChanged(nameof(HayProductos));
                RaisePropertyChanged(nameof(TotalProductos));
                RaisePropertyChanged(nameof(ProductosAActualizar));
                RaisePropertyChanged(nameof(ProductosACrear));
                RaisePropertyChanged(nameof(ProductosSinCambios));
                RaisePropertyChanged(nameof(PuedeActualizar));
                ActualizarCommand.RaiseCanExecuteChanged();
            }
            catch (Exception ex)
            {
                MensajeResultado = $"Error: {ex.Message}";
            }
            finally
            {
                EstaCargando = false;
            }
        }

        private bool CanActualizar()
        {
            return PuedeActualizar;
        }

        private async Task OnActualizarAsync()
        {
            if (!CanActualizar()) return;

            _cancellationTokenSource = new CancellationTokenSource();
            var token = _cancellationTokenSource.Token;

            try
            {
                EstaActualizando = true;
                MensajeResultado = string.Empty;

                var productosParaActualizar = Productos
                    .Where(p => !p.Actualizado && ((p.TieneCambios && p.YaExiste) || (p.RequiereActualizacion && !p.YaExiste)))
                    .ToList();

                TotalAActualizar = productosParaActualizar.Count;
                ProgresoActual = 0;

                int actualizados = 0;
                int creados = 0;
                int errores = 0;

                foreach (var producto in productosParaActualizar)
                {
                    if (token.IsCancellationRequested)
                    {
                        MensajeResultado = $"Cancelado: {actualizados} actualizados, {creados} creados, {errores} errores";
                        break;
                    }

                    MensajeProgreso = $"Procesando {ProgresoActual + 1} de {TotalAActualizar}: {producto.ProductoId}";

                    try
                    {
                        var controlStock = new ControlStock
                        {
                            Empresa = "1",
                            Almacén = AlmacenSeleccionado,
                            Número = producto.ProductoId,
                            StockMínimo = producto.StockMinimoCalculado,
                            StockMáximo = producto.StockMaximoCalculado,
                            Categoria = producto.Categoria ?? string.Empty,
                            Estacionalidad = producto.Estacionalidad ?? string.Empty,
                            Múltiplos = producto.Multiplos > 0 ? producto.Multiplos : 1,
                            Fecha_Modificación = DateTime.Now
                        };

                        if (producto.YaExiste)
                        {
                            await _productoService.GuardarControlStock(controlStock);
                            actualizados++;
                        }
                        else
                        {
                            await _productoService.CrearControlStock(controlStock);
                            creados++;
                        }

                        // Actualizar los valores actuales en el modelo para reflejar el cambio
                        producto.StockMinimoActual = producto.StockMinimoCalculado;
                        producto.StockMaximoActual = producto.StockMaximoCalculado;
                        producto.Actualizado = true;
                        producto.TieneError = false;
                    }
                    catch (Exception ex)
                    {
                        errores++;
                        producto.TieneError = true;
                        producto.MensajeError = ex.Message;
                    }

                    ProgresoActual++;
                }

                MensajeProgreso = string.Empty;
                if (!token.IsCancellationRequested)
                {
                    MensajeResultado = $"Completado: {actualizados} actualizados, {creados} creados";
                    if (errores > 0)
                    {
                        MensajeResultado += $", {errores} errores";
                    }
                }

                // Refrescar la lista para actualizar los estados
                RaisePropertyChanged(nameof(ProductosAActualizar));
                RaisePropertyChanged(nameof(ProductosACrear));
                RaisePropertyChanged(nameof(ProductosActualizados));
                RaisePropertyChanged(nameof(ProductosConError));
                RaisePropertyChanged(nameof(ProductosSinCambios));
                RaisePropertyChanged(nameof(PuedeActualizar));
                ActualizarCommand.RaiseCanExecuteChanged();
            }
            finally
            {
                EstaActualizando = false;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        #endregion

        #region Métodos públicos

        public void SetSelectorProveedor(SelectorProveedor selector)
        {
            _selectorProveedor = selector;
            if (_selectorProveedor != null)
            {
                // Suscribirse al cambio de la DependencyProperty ProveedorCompleto
                var dpd = System.ComponentModel.DependencyPropertyDescriptor.FromProperty(
                    SelectorProveedor.ProveedorCompletoProperty, typeof(SelectorProveedor));
                dpd?.AddValueChanged(_selectorProveedor, (s, e) =>
                {
                    RaisePropertyChanged(nameof(ProveedorSeleccionado));
                    BuscarProductosCommand.RaiseCanExecuteChanged();
                });
            }
        }

        private ProveedorDTO ProveedorSeleccionado => _selectorProveedor?.ProveedorCompleto;

        #endregion
    }
}
