using ControlesUsuario.Dialogs;
using Nesto.Infrastructure.Shared;
using Nesto.Modules.Producto;
using Nesto.Modulos.CanalesExternos.Interfaces;
using Nesto.Modulos.CanalesExternos.Models;
using Nesto.Modulos.CanalesExternos.Services;
using Prism.Commands;
using Prism.Services.Dialogs;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace Nesto.Modulos.CanalesExternos.ViewModels
{
    public class CanalesExternosProductosViewModel : ViewModelBase
    {
        private readonly ICanalesExternosProductosService _servicio;
        private readonly IProductoService _servicioProducto;
        private readonly IDialogService _dialogService;
        public CanalesExternosProductosViewModel(ICanalesExternosProductosService servicio, IProductoService servicioProducto, IDialogService dialogService) {
            _dialogService = dialogService;
            Titulo = "Canales externos productos";

            ActualizarProductoCommand = new DelegateCommand<ProductoCanalExterno>(OnActualizarProducto, CanActualizarProducto);
            AnnadirProductoCommand = new DelegateCommand(OnAnnadirProducto, CanAnnadirProducto);
            BuscarProductoCommand = new DelegateCommand(OnBuscarProducto);
            GuardarCambiosCommand = new DelegateCommand<ProductoCanalExterno>(OnGuardarCambios, CanGuardarCambios);
            PonerVistoBuenoCommand = new DelegateCommand(OnPonerVistoBueno, CanPonerVistoBueno);
            _servicio = servicio;
            _servicioProducto = servicioProducto;
            var canalNuevaVision = new CanalExternoProductosNuevaVision(servicio);
            CanalesDisponibles =
            [
                canalNuevaVision,
                new CanalExternoProductosMiravia()
            ];
            _canalesSeleccionados = new() { canalNuevaVision };
            _canalesSeleccionados.CollectionChanged += (sender, args) =>
            {
                ActualizarProductoCommand.RaiseCanExecuteChanged();
            };
        }

        #region Propiedades
        public ObservableCollection<ICanalExternoProductos> CanalesDisponibles { get; }

        private ObservableCollection<ICanalExternoProductos> _canalesSeleccionados;
        public ObservableCollection<ICanalExternoProductos> CanalesSeleccionados
        {
            get => _canalesSeleccionados;
            set
            {
                if(SetProperty(ref _canalesSeleccionados, value))
                {
                    ActualizarProductoCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private ProductoCanalExterno _productoSeleccionado;
        public ProductoCanalExterno ProductoSeleccionado
        {
            get => _productoSeleccionado;
            set
            {
                if (_productoSeleccionado == value) return; // Salimos si es el mismo objeto

                // Nos desuscribimos del producto anterior si existe
                if (_productoSeleccionado != null)
                {
                    _productoSeleccionado.PropertyChanged -= ProductoSeleccionado_PropertyChanged;
                }

                if (SetProperty(ref _productoSeleccionado, value))
                {
                    // Nos suscribimos al nuevo producto si no es null
                    if (_productoSeleccionado != null)
                    {
                        _productoSeleccionado.PropertyChanged += ProductoSeleccionado_PropertyChanged;
                    }

                    GuardarCambiosCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private ProductoCanalExterno _productoSinVistoBuenoSeleccionado;
        public ProductoCanalExterno ProductoSinVistoBuenoSeleccionado
        {
            get => _productoSinVistoBuenoSeleccionado;
            set
            {
                if (SetProperty(ref _productoSinVistoBuenoSeleccionado, value))
                {
                    PonerVistoBuenoCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private int _pestannaSeleccionada;
        public int PestannaSeleccionada
        {
            get => _pestannaSeleccionada;
            set 
            { 
                if (SetProperty(ref _pestannaSeleccionada, value))
                {
                    if (_pestannaSeleccionada == 1)
                    {
                        LoadProductosAsync();
                    }
                }
            }
        }

        private ObservableCollection<ProductoCanalExterno> _productosSinVistoBueno;
        public ObservableCollection<ProductoCanalExterno> ProductosSinVistoBueno
        {
            get => _productosSinVistoBueno;
            set => SetProperty(ref _productosSinVistoBueno, value);
        }

        private string _productoBuscar;
        public string ProductoBuscar
        {
            get => _productoBuscar;
            set
            {
                if (SetProperty(ref _productoBuscar, value))
                {
                    AnnadirProductoCommand.RaiseCanExecuteChanged();
                }
            }
        }

        #endregion Propiedades


        #region Comandos

        public DelegateCommand<ProductoCanalExterno> ActualizarProductoCommand { get; private set; }
        public bool CanActualizarProducto(ProductoCanalExterno producto) => CanalesSeleccionados != null && CanalesSeleccionados.Any() && producto != null && !producto.IsDirty;
        private async void OnActualizarProducto(ProductoCanalExterno producto)
        {
            try
            {
                producto.ProductoCompleto ??= await _servicioProducto.LeerProducto(producto.ProductoId);
                foreach (var canal in CanalesSeleccionados)
                {
                    await canal.ActualizarProducto(producto);
                }
                var canales = string.Join(", ", CanalesSeleccionados.Select(c => c.Nombre));
                _dialogService.ShowNotification($"Producto {producto.ProductoId} actualizado en: {canales}");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
        }



        public DelegateCommand AnnadirProductoCommand { get; private set; }
        public bool CanAnnadirProducto() => !string.IsNullOrEmpty(ProductoBuscar);
        private async void OnAnnadirProducto()
        {
            var productoNuevo = await _servicio.AddProductoAsync(ProductoBuscar);
            if (ProductosSinVistoBueno == null)
            {
                ProductosSinVistoBueno = new ObservableCollection<ProductoCanalExterno>();
            }
            ProductosSinVistoBueno.Add(productoNuevo);
            ProductoSeleccionado = ProductosSinVistoBueno.First(p => p.ProductoId == ProductoBuscar);
        }

        public DelegateCommand BuscarProductoCommand { get; private set; }
        private async void OnBuscarProducto()
        {
            ProductoSeleccionado = await _servicio.GetProductoAsync(ProductoBuscar);
        }

        public DelegateCommand<ProductoCanalExterno> GuardarCambiosCommand { get; private set; }
        private bool CanGuardarCambios(ProductoCanalExterno producto) => producto?.IsDirty ?? false;
        private async void OnGuardarCambios(ProductoCanalExterno producto)
        {
            try
            {
                await _servicio.SaveProductoAsync(producto);
                _dialogService.ShowNotification($"Producto {producto.ProductoId} guardado correctamente");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
        }

        public DelegateCommand PonerVistoBuenoCommand { get; private set; }
        public bool CanPonerVistoBueno()
        {
            if (ProductoSeleccionado == null)
            {
                return false;
            }

            return ProductoSeleccionado.VistoBueno == false;
        }
        private void OnPonerVistoBueno()
        {
            ProductoSeleccionado.VistoBueno = true;
        }


        #endregion Comandos


        private async Task LoadProductosAsync()
        {
            ProductosSinVistoBueno = new ObservableCollection<ProductoCanalExterno>(await _servicio.GetProductosSinVistoBuenoNumeroAsync());
        }

        private void ProductoSeleccionado_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            ActualizarProductoCommand.RaiseCanExecuteChanged();
            GuardarCambiosCommand.RaiseCanExecuteChanged();
        }
    }
}
