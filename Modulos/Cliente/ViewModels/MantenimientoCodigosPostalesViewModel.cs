using ControlesUsuario.Dialogs;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Modulos.Cliente.Models;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Nesto.Modulos.Cliente
{
    /// <summary>
    /// Nesto#442: mantenimiento de códigos postales (NestoAPI#378). Para poner bien el país de
    /// los CPs extranjeros que se sigan creando sin él desde Nesto viejo y editar población,
    /// provincia, ruta, vendedor y los vendedores por grupo de producto. Acceso: Dirección y
    /// Tienda online (el menú ya lo filtra; el VM lo vuelve a comprobar por si acaso).
    /// </summary>
    public class MantenimientoCodigosPostalesViewModel : BindableBase
    {
        private readonly ICodigosPostalesService _servicio;
        private readonly IDialogService _dialogService;

        public MantenimientoCodigosPostalesViewModel(ICodigosPostalesService servicio,
            IConfiguracion configuracion, IDialogService dialogService)
        {
            _servicio = servicio;
            Configuracion = configuracion;
            _dialogService = dialogService;
            Titulo = "Códigos Postales";
            BuscarCommand = new DelegateCommand(async () => await BuscarAsync(), () => !string.IsNullOrWhiteSpace(Filtro));
            GuardarCommand = new DelegateCommand(async () => await GuardarAsync(), () => Seleccionado != null);
            AnnadirVendedorGrupoCommand = new DelegateCommand(OnAnnadirVendedorGrupo, () => Seleccionado != null);
            BorrarVendedorGrupoCommand = new DelegateCommand<VendedorGrupoProductoCodigoPostalModel>(OnBorrarVendedorGrupo);
        }

        // Público para que el SelectorVendedor de la vista pueda leer la configuración.
        public IConfiguracion Configuracion { get; }

        public string Titulo { get; }

        // Para el SelectorVendedor de la vista
        public string Empresa => Constantes.Empresas.EMPRESA_DEFECTO;

        public bool TieneAcceso => Configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.DIRECCION)
            || Configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.TIENDA_ON_LINE);

        private string _filtro;
        public string Filtro
        {
            get => _filtro;
            set
            {
                if (SetProperty(ref _filtro, value))
                {
                    BuscarCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private ObservableCollection<CodigoPostalModel> _resultados = new();
        public ObservableCollection<CodigoPostalModel> Resultados
        {
            get => _resultados;
            private set => SetProperty(ref _resultados, value);
        }

        private CodigoPostalModel _seleccionado;
        public CodigoPostalModel Seleccionado
        {
            get => _seleccionado;
            set
            {
                if (SetProperty(ref _seleccionado, value))
                {
                    CargarEdicion(value);
                    GuardarCommand.RaiseCanExecuteChanged();
                    AnnadirVendedorGrupoCommand.RaiseCanExecuteChanged();
                    RaisePropertyChanged(nameof(HaySeleccion));
                }
            }
        }

        public bool HaySeleccion => Seleccionado != null;

        // Campos de edición (copia del seleccionado: no se toca la fila hasta guardar con éxito)
        private string _poblacionEdicion;
        public string PoblacionEdicion { get => _poblacionEdicion; set => SetProperty(ref _poblacionEdicion, value); }

        private string _provinciaEdicion;
        public string ProvinciaEdicion { get => _provinciaEdicion; set => SetProperty(ref _provinciaEdicion, value); }

        private string _rutaEdicion;
        public string RutaEdicion { get => _rutaEdicion; set => SetProperty(ref _rutaEdicion, value); }

        private string _vendedorEdicion;
        public string VendedorEdicion { get => _vendedorEdicion; set => SetProperty(ref _vendedorEdicion, value); }

        private string _paisEdicion;
        public string PaisEdicion { get => _paisEdicion; set => SetProperty(ref _paisEdicion, value); }

        private ObservableCollection<VendedorGrupoProductoCodigoPostalModel> _vendedoresGrupoProducto = new();
        public ObservableCollection<VendedorGrupoProductoCodigoPostalModel> VendedoresGrupoProducto
        {
            get => _vendedoresGrupoProducto;
            private set => SetProperty(ref _vendedoresGrupoProducto, value);
        }

        private bool _estaOcupado;
        public bool EstaOcupado
        {
            get => _estaOcupado;
            set => SetProperty(ref _estaOcupado, value);
        }

        public DelegateCommand BuscarCommand { get; }

        // Function As Task para poder esperarla en los tests (patrón Fase 1C).
        public async Task BuscarAsync()
        {
            if (!TieneAcceso)
            {
                _dialogService.ShowError("Esta ventana es solo para Dirección y Tienda online");
                return;
            }
            if (string.IsNullOrWhiteSpace(Filtro))
            {
                return;
            }
            try
            {
                EstaOcupado = true;
                List<CodigoPostalModel> lista = await _servicio.Buscar(Filtro.Trim());
                Seleccionado = null;
                Resultados = new ObservableCollection<CodigoPostalModel>(lista);
            }
            catch (Exception ex)
            {
                Resultados = new ObservableCollection<CodigoPostalModel>();
                _dialogService.ShowError(ex.Message);
            }
            finally
            {
                EstaOcupado = false;
            }
        }

        public DelegateCommand GuardarCommand { get; }

        public async Task GuardarAsync()
        {
            if (Seleccionado == null)
            {
                return;
            }
            CodigoPostalModel aGuardar = new()
            {
                Empresa = Seleccionado.Empresa,
                Numero = Seleccionado.Numero,
                Poblacion = PoblacionEdicion?.Trim(),
                Provincia = ProvinciaEdicion?.Trim(),
                Ruta = RutaEdicion?.Trim(),
                Vendedor = VendedorEdicion?.Trim(),
                Pais = PaisEdicion?.Trim(),
                VendedoresGrupoProducto = VendedoresGrupoProducto
                    .Where(v => !string.IsNullOrWhiteSpace(v.GrupoProducto) && !string.IsNullOrWhiteSpace(v.Vendedor))
                    .ToList()
            };
            try
            {
                EstaOcupado = true;
                CodigoPostalModel guardado = await _servicio.Guardar(aGuardar);
                // Refrescar la fila del grid con lo que devuelve el servidor
                int indice = Resultados.IndexOf(Seleccionado);
                if (indice >= 0 && guardado != null)
                {
                    Resultados[indice] = guardado;
                    Seleccionado = guardado;
                }
                _dialogService.ShowNotification("Código postal guardado",
                    $"Guardado el código postal {guardado?.Numero}" +
                    (string.IsNullOrWhiteSpace(guardado?.Pais) ? " (sin país)" : $" con país {guardado.Pais}"));
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
            finally
            {
                EstaOcupado = false;
            }
        }

        public DelegateCommand AnnadirVendedorGrupoCommand { get; }
        private void OnAnnadirVendedorGrupo()
            => VendedoresGrupoProducto.Add(new VendedorGrupoProductoCodigoPostalModel());

        public DelegateCommand<VendedorGrupoProductoCodigoPostalModel> BorrarVendedorGrupoCommand { get; }
        private void OnBorrarVendedorGrupo(VendedorGrupoProductoCodigoPostalModel fila)
        {
            if (fila != null)
            {
                _ = VendedoresGrupoProducto.Remove(fila);
            }
        }

        private void CargarEdicion(CodigoPostalModel seleccionado)
        {
            PoblacionEdicion = seleccionado?.Poblacion;
            ProvinciaEdicion = seleccionado?.Provincia;
            RutaEdicion = seleccionado?.Ruta;
            VendedorEdicion = seleccionado?.Vendedor;
            PaisEdicion = seleccionado?.Pais;
            VendedoresGrupoProducto = new ObservableCollection<VendedorGrupoProductoCodigoPostalModel>(
                (seleccionado?.VendedoresGrupoProducto ?? new List<VendedorGrupoProductoCodigoPostalModel>())
                .Select(v => new VendedorGrupoProductoCodigoPostalModel
                {
                    GrupoProducto = v.GrupoProducto,
                    Vendedor = v.Vendedor
                }));
        }
    }
}
