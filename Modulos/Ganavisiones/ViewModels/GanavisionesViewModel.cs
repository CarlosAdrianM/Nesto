using ControlesUsuario.Dialogs;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Modulos.Ganavisiones.Interfaces;
using Nesto.Modulos.Ganavisiones.Models;
using Nesto.Modulos.Ganavisiones.ViewModels;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

public delegate void NuevoGanavisionCreadoHandler(GanavisionWrapper nuevoItem);

namespace Nesto.Modulos.Ganavisiones.ViewModels
{
    public class GanavisionesViewModel : ViewModelBase
    {
        private readonly IGanavisionesService _ganavisionesService;
        private readonly IConfiguracion _configuracion;
        private readonly IDialogService _dialogService;

        public GanavisionesViewModel(IGanavisionesService ganavisionesService, IConfiguracion configuracion, IDialogService dialogService)
        {
            _ganavisionesService = ganavisionesService;
            _configuracion = configuracion;
            _dialogService = dialogService;

            Ganavisiones = new ObservableCollection<GanavisionWrapper>();

            CargarGanavisionesCommand = new DelegateCommand(async () => await OnCargarGanavisiones());
            NuevoCommand = new DelegateCommand(OnNuevo);
            // Quitamos CanGuardar - validamos dentro de OnGuardar para evitar problemas de binding
            GuardarCommand = new DelegateCommand<object>(async (g) => await OnGuardar(g as GanavisionWrapper));
            EliminarCommand = new DelegateCommand<object>(async (g) => await OnEliminar(g as GanavisionWrapper), CanEliminar);

            Titulo = "Ganavisiones";
            Empresa = Constantes.Empresas.EMPRESA_DEFECTO;

            // Cargar datos automáticamente al inicializar
            _ = CargarDatosIniciales();
        }

        private async Task CargarDatosIniciales()
        {
            await OnCargarGanavisiones(mostrarConfirmacion: false);
        }

        #region Propiedades

        private string _empresa;
        public string Empresa
        {
            get => _empresa;
            set => SetProperty(ref _empresa, value);
        }

        private ObservableCollection<GanavisionWrapper> _ganavisiones;
        public ObservableCollection<GanavisionWrapper> Ganavisiones
        {
            get => _ganavisiones;
            set => SetProperty(ref _ganavisiones, value);
        }

        private GanavisionWrapper _ganavisionSeleccionada;
        public GanavisionWrapper GanavisionSeleccionada
        {
            get => _ganavisionSeleccionada;
            set
            {
                if (SetProperty(ref _ganavisionSeleccionada, value))
                {
                    ((DelegateCommand<object>)EliminarCommand).RaiseCanExecuteChanged();
                }
            }
        }

        private bool _estaCargando;
        public bool EstaCargando
        {
            get => _estaCargando;
            set => SetProperty(ref _estaCargando, value);
        }

        private bool _soloActivos = true;
        public bool SoloActivos
        {
            get => _soloActivos;
            set
            {
                if (SetProperty(ref _soloActivos, value))
                {
                    // Recargar automáticamente al cambiar el filtro
                    _ = OnCargarGanavisiones(mostrarConfirmacion: true);
                }
            }
        }

        public bool EsUsuarioCompras => _configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.COMPRAS);

        #endregion

        #region Comandos

        public ICommand CargarGanavisionesCommand { get; }
        public ICommand NuevoCommand { get; }
        public ICommand GuardarCommand { get; }
        public ICommand EliminarCommand { get; }

        /// <summary>
        /// Evento que se dispara cuando se crea un nuevo Ganavision para que la View pueda poner el foco en la celda de edición.
        /// </summary>
        public event NuevoGanavisionCreadoHandler NuevoGanavisionCreado;

        private async Task OnCargarGanavisiones(bool mostrarConfirmacion = true)
        {
            // Verificar si hay cambios sin guardar (solo si se solicita confirmación)
            if (mostrarConfirmacion && TieneCambiosSinGuardar())
            {
                var confirmacion = _dialogService.ShowConfirmationAnswer(
                    "Cambios sin guardar",
                    "Hay cambios sin guardar que se perderán. ¿Desea continuar?");

                if (!confirmacion) return;
            }

            try
            {
                EstaCargando = true;
                Ganavisiones.Clear();

                var lista = await _ganavisionesService.GetGanavisiones(Empresa, soloActivos: SoloActivos);
                foreach (var item in lista)
                {
                    Ganavisiones.Add(new GanavisionWrapper(item));
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
            finally
            {
                EstaCargando = false;
            }
        }

        private bool TieneCambiosSinGuardar()
        {
            foreach (var ganavision in Ganavisiones)
            {
                if (ganavision.HaCambiado || ganavision.Id == 0)
                {
                    return true;
                }
            }
            return false;
        }

        private void OnNuevo()
        {
            var nuevo = new GanavisionWrapper();
            Ganavisiones.Add(nuevo);
            GanavisionSeleccionada = nuevo;
            NuevoGanavisionCreado?.Invoke(nuevo);
        }

        private async Task OnGuardar(GanavisionWrapper ganavision)
        {
            if (ganavision == null) return;

            // Validar que tenga producto
            if (string.IsNullOrWhiteSpace(ganavision.ProductoId))
            {
                _dialogService.ShowError("Debe introducir un código de producto.");
                return;
            }

            // Validar duplicados localmente antes de llamar a la API
            if (ganavision.Id == 0)
            {
                var productoTrimmed = ganavision.ProductoId.Trim();
                var existeLocal = Ganavisiones.Any(g => g.Id > 0 &&
                    string.Equals(g.ProductoId?.Trim(), productoTrimmed, StringComparison.OrdinalIgnoreCase));

                if (existeLocal)
                {
                    _dialogService.ShowError($"Ya existe un registro de Ganavisiones para el producto '{productoTrimmed}'. " +
                        "Modifique directamente el registro existente en la lista.");
                    return;
                }
            }

            try
            {
                EstaCargando = true;

                var createModel = new GanavisionCreateModel
                {
                    Empresa = Empresa,
                    ProductoId = ganavision.ProductoId?.Trim(),
                    Ganavisiones = ganavision.Ganavisiones,
                    FechaDesde = ganavision.FechaDesde,
                    FechaHasta = ganavision.FechaHasta
                };

                GanavisionModel resultado;
                if (ganavision.Id == 0)
                {
                    // Crear nuevo
                    resultado = await _ganavisionesService.CreateGanavision(createModel);
                    _dialogService.ShowNotification($"Ganavisión creado para el producto {resultado.ProductoId}");
                }
                else
                {
                    // Actualizar existente
                    resultado = await _ganavisionesService.UpdateGanavision(ganavision.Id, createModel);
                    _dialogService.ShowNotification($"Ganavisión actualizado para el producto {resultado.ProductoId}");
                }

                // Actualizar todos los campos desde la respuesta del servidor sin marcar como cambiado
                ganavision.ActualizarDesdeServidor(resultado);
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
            finally
            {
                EstaCargando = false;
            }
        }

        private bool CanEliminar(object parameter)
        {
            var ganavision = parameter as GanavisionWrapper;
            return ganavision != null && ganavision.Id > 0;
        }

        private async Task OnEliminar(GanavisionWrapper ganavision)
        {
            if (ganavision == null) return;

            var confirmacion = _dialogService.ShowConfirmationAnswer(
                "Eliminar Ganavisión",
                $"¿Está seguro de eliminar el Ganavisión del producto {ganavision.ProductoId}?");

            if (!confirmacion) return;

            try
            {
                EstaCargando = true;
                await _ganavisionesService.DeleteGanavision(ganavision.Id);
                Ganavisiones.Remove(ganavision);
                _dialogService.ShowNotification("Ganavisión eliminado correctamente");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
            finally
            {
                EstaCargando = false;
            }
        }

        #endregion
    }

    /// <summary>
    /// Wrapper para GanavisionModel que permite detectar cambios en la UI
    /// </summary>
    public class GanavisionWrapper : BindableBase
    {
        private bool _rastreandoCambios = true;

        public GanavisionWrapper()
        {
            FechaDesde = DateTime.Today;
        }

        public GanavisionWrapper(GanavisionModel model)
        {
            _rastreandoCambios = false;
            Id = model.Id;
            Empresa = model.Empresa;
            ProductoId = model.ProductoId;
            _productoIdAnterior = model.ProductoId; // Guardar para detectar cambios
            ProductoNombre = model.ProductoNombre;
            Ganavisiones = model.Ganavisiones;
            FechaDesde = model.FechaDesde;
            FechaHasta = model.FechaHasta;
            FechaCreacion = model.FechaCreacion;
            FechaModificacion = model.FechaModificacion;
            Usuario = model.Usuario;
            _rastreandoCambios = true;
            HaCambiado = false;
        }

        /// <summary>
        /// Actualiza los datos desde la respuesta del servidor sin marcar como cambiado.
        /// </summary>
        public void ActualizarDesdeServidor(GanavisionModel model)
        {
            _rastreandoCambios = false;
            Id = model.Id;
            ProductoId = model.ProductoId;
            _productoIdAnterior = model.ProductoId; // Actualizar para detectar futuros cambios
            ProductoNombre = model.ProductoNombre;
            Ganavisiones = model.Ganavisiones;
            FechaDesde = model.FechaDesde;
            FechaHasta = model.FechaHasta;
            FechaCreacion = model.FechaCreacion;
            FechaModificacion = model.FechaModificacion;
            Usuario = model.Usuario;
            _rastreandoCambios = true;
            HaCambiado = false;
        }

        public int Id { get; set; }
        public string Empresa { get; set; }

        private string _productoId;
        private string _productoIdAnterior;
        public string ProductoId
        {
            get => _productoId;
            set
            {
                if (SetProperty(ref _productoId, value))
                {
                    if (_rastreandoCambios) HaCambiado = true;
                    RaisePropertyChanged(nameof(Producto));
                }
            }
        }

        /// <summary>
        /// Alias de ProductoId para compatibilidad con ProductoBehavior
        /// </summary>
        public string Producto
        {
            get => ProductoId;
            set => ProductoId = value;
        }

        private string _productoNombre;
        public string ProductoNombre
        {
            get => _productoNombre;
            set
            {
                if (SetProperty(ref _productoNombre, value))
                {
                    RaisePropertyChanged(nameof(texto));
                }
            }
        }

        /// <summary>
        /// Alias de ProductoNombre para compatibilidad con ProductoBehavior
        /// </summary>
        public string texto
        {
            get => ProductoNombre;
            set => ProductoNombre = value;
        }

        private int _ganavisiones;
        public int Ganavisiones
        {
            get => _ganavisiones;
            set
            {
                if (SetProperty(ref _ganavisiones, value))
                {
                    if (_rastreandoCambios) HaCambiado = true;
                }
            }
        }

        private decimal _precioProducto;
        /// <summary>
        /// PVP del producto. Al establecerse, calcula automáticamente los Ganavisiones
        /// solo si el producto ha cambiado respecto al valor anterior.
        /// </summary>
        public decimal PrecioProducto
        {
            get => _precioProducto;
            set
            {
                if (SetProperty(ref _precioProducto, value))
                {
                    // Solo calcular Ganavisiones si el producto realmente cambió
                    var productoActual = ProductoId?.Trim();
                    var productoAnterior = _productoIdAnterior?.Trim();

                    if (!string.Equals(productoActual, productoAnterior, StringComparison.OrdinalIgnoreCase))
                    {
                        // Calcular Ganavisiones: 1 Ganavisión = 1 EUR de PVP (redondeado hacia arriba)
                        Ganavisiones = (int)Math.Ceiling(value);
                        _productoIdAnterior = productoActual;
                    }
                }
            }
        }

        private DateTime _fechaDesde;
        public DateTime FechaDesde
        {
            get => _fechaDesde;
            set
            {
                if (SetProperty(ref _fechaDesde, value))
                {
                    if (_rastreandoCambios) HaCambiado = true;
                }
            }
        }

        private DateTime? _fechaHasta;
        public DateTime? FechaHasta
        {
            get => _fechaHasta;
            set
            {
                if (SetProperty(ref _fechaHasta, value))
                {
                    if (_rastreandoCambios) HaCambiado = true;
                }
            }
        }

        public DateTime FechaCreacion { get; set; }
        public DateTime FechaModificacion { get; set; }
        public string Usuario { get; set; }

        private bool _haCambiado;
        public bool HaCambiado
        {
            get => _haCambiado;
            set => SetProperty(ref _haCambiado, value);
        }
    }
}
