using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Modulos.Cliente.Models;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CP = Nesto.Infrastructure.Shared.CodigoPostal;

namespace Nesto.Modulos.Cliente
{
    /// <summary>
    /// Nesto#442: mantenimiento de códigos postales (NestoAPI#378). Para poner bien el país de
    /// los CPs extranjeros que se sigan creando sin él desde Nesto viejo y editar población,
    /// provincia, ruta, vendedor y los vendedores por grupo de producto. Acceso: Dirección y
    /// Tienda online (el menú ya lo filtra; el VM lo vuelve a comprobar por si acaso).
    /// </summary>
    public class MantenimientoCodigosPostalesViewModel : ObservableObject
    {
        private readonly ICodigosPostalesService _servicio;
        private readonly IServicioDialogos _dialogService;

        public MantenimientoCodigosPostalesViewModel(ICodigosPostalesService servicio,
            IConfiguracion configuracion, IServicioDialogos dialogService)
        {
            _servicio = servicio;
            Configuracion = configuracion;
            _dialogService = dialogService;
            Titulo = "Códigos Postales";
            BuscarCommand = new RelayCommand(async () => await BuscarAsync(), () => !string.IsNullOrWhiteSpace(Filtro));
            GuardarCommand = new RelayCommand(async () => await GuardarAsync(), () => Seleccionado != null);
            AnnadirVendedorGrupoCommand = new RelayCommand(OnAnnadirVendedorGrupo, () => Seleccionado != null);
            BorrarVendedorGrupoCommand = new RelayCommand<VendedorGrupoProductoCodigoPostalModel>(OnBorrarVendedorGrupo);
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
                    BuscarCommand.NotifyCanExecuteChanged();
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
                    GuardarCommand.NotifyCanExecuteChanged();
                    AnnadirVendedorGrupoCommand.NotifyCanExecuteChanged();
                    OnPropertyChanged(nameof(HaySeleccion));
                }
            }
        }

        public bool HaySeleccion => Seleccionado != null;

        // NestoAPI#596: aviso de códigos postales repetidos por formato («4430 999» y «4430-999»)
        private string _avisoFormato;
        public string AvisoFormato
        {
            get => _avisoFormato;
            private set
            {
                if (SetProperty(ref _avisoFormato, value))
                {
                    OnPropertyChanged(nameof(HayAvisoFormato));
                }
            }
        }
        public bool HayAvisoFormato => !string.IsNullOrEmpty(AvisoFormato);

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

        public RelayCommand BuscarCommand { get; }

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
                List<CodigoPostalModel> lista;
                // NestoAPI#596: un CP portugués completo se busca en cualquier formato («4430 999»,
                // «4430999», «4430-999»): se piden los de sus 4 primeras cifras y se quedan los que son
                // el mismo código. Lo demás (prefijos como «2800», poblaciones) se busca tal cual.
                if (CP.EsPortugues(Filtro, null))
                {
                    string canonico = CP.Normalizar(Filtro, CP.PORTUGAL);
                    Filtro = canonico;
                    lista = (await _servicio.Buscar(canonico.Substring(0, 4)))
                        .Where(c => CP.MismoCodigo(c.Numero, canonico, CP.PORTUGAL))
                        .ToList();
                }
                else
                {
                    lista = await _servicio.Buscar(Filtro.Trim());
                }
                Seleccionado = null;
                MarcarDuplicadosPorFormato(lista);
                Resultados = new ObservableCollection<CodigoPostalModel>(lista);
            }
            catch (Exception ex)
            {
                Resultados = new ObservableCollection<CodigoPostalModel>();
                AvisoFormato = null;
                _dialogService.ShowError(ex.Message);
            }
            finally
            {
                EstaOcupado = false;
            }
        }

        public RelayCommand GuardarCommand { get; }

        public async Task GuardarAsync()
        {
            if (Seleccionado == null)
            {
                return;
            }
            // NestoAPI#596: si esta fila es un duplicado por formato de otra que ya está en el
            // canónico, no se guarda: se corrige la buena y esta la fusiona el script de limpieza.
            string gemelo = await BuscarGemeloCanonico(Seleccionado);
            if (gemelo != null)
            {
                _dialogService.ShowError($"El código postal «{Seleccionado.Numero?.Trim()}» ya existe como «{gemelo}»: son el mismo. " +
                    $"Corrige «{gemelo}»; «{Seleccionado.Numero?.Trim()}» sobra y se fusionará con él.");
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

        private void MarcarDuplicadosPorFormato(List<CodigoPostalModel> lista)
        {
            List<string> avisos = new();
            // Todas las filas son de la misma empresa (la búsqueda es por empresa)
            foreach (IGrouping<string, CodigoPostalModel> grupo in lista
                .Where(c => !string.IsNullOrWhiteSpace(c.Numero))
                .GroupBy(c => c.NumeroCanonico)
                .Where(g => g.Count() > 1))
            {
                CodigoPostalModel canonico = grupo.FirstOrDefault(c => c.EnFormatoCanonico);
                foreach (CodigoPostalModel otro in grupo.Where(c => c != canonico))
                {
                    otro.DuplicadoPorFormato = canonico != null;
                }
                avisos.Add(string.Join(" y ", grupo.Select(c => $"«{c.Numero.Trim()}»")) + $" son el mismo código postal ({grupo.Key})");
            }
            AvisoFormato = avisos.Count == 0
                ? null
                : string.Join(". ", avisos) + ". Corrige solo el que está con guion; los otros se fusionarán con él.";
        }

        // El número canónico de otra fila de la tabla que es el mismo CP que este, escrito de otra forma.
        private async Task<string> BuscarGemeloCanonico(CodigoPostalModel cp)
        {
            if (cp.EnFormatoCanonico || !CP.EsPortugues(cp.Numero, cp.Pais))
            {
                return null;
            }
            string canonico = cp.NumeroCanonico;
            List<CodigoPostalModel> candidatos = await _servicio.Buscar(canonico.Substring(0, 4));
            return candidatos?.Any(c => c.Numero?.Trim() == canonico) == true ? canonico : null;
        }

        public RelayCommand AnnadirVendedorGrupoCommand { get; }
        private void OnAnnadirVendedorGrupo()
            => VendedoresGrupoProducto.Add(new VendedorGrupoProductoCodigoPostalModel());

        public RelayCommand<VendedorGrupoProductoCodigoPostalModel> BorrarVendedorGrupoCommand { get; }
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
