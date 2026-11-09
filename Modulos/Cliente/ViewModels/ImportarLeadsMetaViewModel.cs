using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Modulos.Cliente.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Nesto.Modulos.Cliente
{
    /// <summary>
    /// NestoAPI#623 corte 4: «Importar leads de Meta». Se elige el fichero que exporta Meta Ads y el origen; la API hace primero un
    /// ensayo (no graba nada) y se ve lead a lead qué pasará. «Importar» lo hace de verdad y enseña los números de cliente.
    /// </summary>
    public class ImportarLeadsMetaViewModel : ObservableObject
    {
        public const string ORIGEN_POR_DEFECTO = "META-HARMONIA-0926";
        public const string GRUPO_VENDEDORES = "Vendedores";

        private readonly IOrigenesClienteService _origenes;
        private readonly IImportarLeadsMetaService _servicio;
        private readonly IServicioDialogos _dialogos;

        public ImportarLeadsMetaViewModel(IOrigenesClienteService origenes, IImportarLeadsMetaService servicio, IServicioDialogos dialogos)
        {
            _origenes = origenes;
            _servicio = servicio;
            _dialogos = dialogos;
            ElegirFicheroCommand = new AsyncRelayCommand(ElegirFicheroAsync, () => !EstaOcupado);
            EnsayarCommand = new AsyncRelayCommand(EnsayarAsync, () => PuedeEnsayar);
            ImportarCommand = new AsyncRelayCommand(ImportarAsync, () => PuedeImportar);
        }

        /// <summary>Los mismos grupos que el endpoint (la API lo vuelve a comprobar).</summary>
        public static bool PuedeVer(IConfiguracion configuracion) =>
            configuracion != null && (configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.DIRECCION)
                || configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ADMINISTRACION)
                || configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.INFORMATICA)
                || configuracion.UsuarioEnGrupo(GRUPO_VENDEDORES));

        public string Titulo => "Importar leads de Meta";

        /// <summary>Elige el fichero (null = cancelado). Sustituible en los tests.</summary>
        public Func<string> ElegirFichero { get; set; } = ElegirFicheroWpf;
        /// <summary>Lee el fichero. Sustituible en los tests.</summary>
        public Func<string, byte[]> LeerFichero { get; set; } = File.ReadAllBytes;

        private static string ElegirFicheroWpf()
        {
            var dialogo = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Fichero de leads exportado de Meta",
                Filter = "Leads de Meta (*.xml;*.csv)|*.xml;*.csv|Todos los ficheros (*.*)|*.*"
            };
            return dialogo.ShowDialog() == true ? dialogo.FileName : null;
        }

        public AsyncRelayCommand ElegirFicheroCommand { get; }
        public AsyncRelayCommand EnsayarCommand { get; }
        public AsyncRelayCommand ImportarCommand { get; }

        private List<OrigenClienteModel> _listaOrigenes = new();
        public List<OrigenClienteModel> Origenes { get => _listaOrigenes; private set => SetProperty(ref _listaOrigenes, value); }

        private OrigenClienteModel _origenSeleccionado;
        public OrigenClienteModel OrigenSeleccionado
        {
            get => _origenSeleccionado;
            set
            {
                if (SetProperty(ref _origenSeleccionado, value))
                {
                    // Otro origen = otro ensayo.
                    Resultado = null;
                }
            }
        }

        private string _rutaFichero;
        public string RutaFichero
        {
            get => _rutaFichero;
            private set
            {
                if (SetProperty(ref _rutaFichero, value))
                {
                    OnPropertyChanged(nameof(NombreFichero));
                }
            }
        }
        public string NombreFichero => string.IsNullOrWhiteSpace(RutaFichero) ? "(ningún fichero)" : Path.GetFileName(RutaFichero);

        private ResultadoImportacionLeadsModel _resultado;
        public ResultadoImportacionLeadsModel Resultado
        {
            get => _resultado;
            private set
            {
                if (SetProperty(ref _resultado, value))
                {
                    Leads = new ObservableCollection<LeadImportadoModel>(value?.Leads ?? new List<LeadImportadoModel>());
                    OnPropertyChanged(nameof(Resumen));
                    Refrescar();
                }
            }
        }

        private ObservableCollection<LeadImportadoModel> _leads = new();
        public ObservableCollection<LeadImportadoModel> Leads { get => _leads; private set => SetProperty(ref _leads, value); }

        /// <summary>«Ensayo: 5 leads; se crearán 4, ya existía 1, con error 0. Pulse «Importar» para hacerlo de verdad.»</summary>
        public string Resumen
        {
            get
            {
                ResultadoImportacionLeadsModel r = Resultado;
                if (r == null)
                {
                    return "Elija el fichero que exporta Meta (Excel XML o CSV). Primero se hace un ensayo: no se graba nada hasta que pulse «Importar».";
                }
                return r.Ensayo
                    ? $"Ensayo: {r.Leidos} leads; se crearán {r.Creados}, ya existían {r.YaExistian}, con error {r.Errores}. No se ha grabado nada: pulse «Importar» para hacerlo de verdad."
                    : $"Importado: {r.Leidos} leads; creados {r.Creados}, ya existían {r.YaExistian}, con error {r.Errores}. Los vendedores ya los tienen en «Clientes para contactar».";
            }
        }

        private bool _estaOcupado;
        public bool EstaOcupado
        {
            get => _estaOcupado;
            private set
            {
                if (SetProperty(ref _estaOcupado, value))
                {
                    Refrescar();
                }
            }
        }

        private byte[] _contenido;

        public bool PuedeEnsayar => !EstaOcupado && _contenido != null && OrigenSeleccionado != null;

        /// <summary>Solo tras un ensayo de este fichero y este origen, y si hay algún lead sin error (repetir es seguro: la API no duplica).</summary>
        public bool PuedeImportar => PuedeEnsayar && Resultado?.Ensayo == true && Resultado.Leads.Any(l => l.Codigo != "ERROR");

        private void Refrescar()
        {
            ElegirFicheroCommand?.NotifyCanExecuteChanged();
            EnsayarCommand?.NotifyCanExecuteChanged();
            ImportarCommand?.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(PuedeImportar));
        }

        /// <summary>Lee los orígenes una vez (la vista vuelve a cargarse al cambiar de pestaña y no debe perder el resultado).</summary>
        public async Task CargarAsync()
        {
            if (Origenes.Count > 0)
            {
                return;
            }
            try
            {
                // NestoAPI#848: solo los de campaña; los de canal (web, Amazon, tiendas, cursos) no son leads (la API los rechaza).
                List<OrigenClienteModel> origenes = (await _origenes.LeerOrigenes() ?? new List<OrigenClienteModel>())
                    .Where(o => o != null && o.EsLead != false).ToList();
                Origenes = origenes;
                OrigenSeleccionado = origenes.FirstOrDefault(o => string.Equals(o.Codigo?.Trim(), ORIGEN_POR_DEFECTO, StringComparison.OrdinalIgnoreCase))
                    ?? origenes.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _dialogos.ShowError(ex.Message);
            }
            Refrescar();
        }

        public async Task ElegirFicheroAsync()
        {
            string ruta = ElegirFichero?.Invoke();
            if (string.IsNullOrWhiteSpace(ruta))
            {
                return;
            }
            try
            {
                _contenido = LeerFichero(ruta);
            }
            catch (Exception ex)
            {
                _dialogos.ShowError($"No se ha podido leer el fichero: {ex.Message}");
                return;
            }
            RutaFichero = ruta;
            Resultado = null;
            await EnsayarAsync();
        }

        public Task EnsayarAsync() => Lanzar(ensayo: true);

        public async Task ImportarAsync()
        {
            if (!PuedeImportar)
            {
                return;
            }
            if (!_dialogos.ShowConfirmationAnswer(Titulo,
                $"Se van a dar de alta {Resultado.Creados} clientes y a revisar {Resultado.YaExistian} que ya existían, con el origen {OrigenSeleccionado.Codigo}. " +
                "Sus vendedores los verán en «Clientes para contactar». ¿Desea continuar?"))
            {
                return;
            }
            await Lanzar(ensayo: false);
        }

        private async Task Lanzar(bool ensayo)
        {
            if (_contenido == null || OrigenSeleccionado == null)
            {
                return;
            }
            EstaOcupado = true;
            try
            {
                Resultado = await _servicio.Importar(OrigenSeleccionado.Codigo, ensayo, _contenido, RutaFichero);
            }
            catch (Exception ex)
            {
                _dialogos.ShowError(ex.Message);
            }
            finally
            {
                EstaOcupado = false;
            }
        }
    }
}
