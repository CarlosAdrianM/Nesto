using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nesto.Infrastructure.Contracts;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace ControlesUsuario.Dialogs
{
    /// <summary>
    /// Sugerencia 551 (Alberto Sancho): lo que necesitan las novedades para saber a quién afectan. Una instancia por
    /// ventana, compartida por todas las novedades. Los perfiles del usuario llegan de la API después de abrir la
    /// ventana: hasta entonces (o si la API no los conoce) todas cuentan como suyas y nadie puede editarlos.
    /// </summary>
    public class ContextoPerfilesNovedades
    {
        /// <summary>Los de la API si no manda la lista (mismo orden).</summary>
        internal static readonly IReadOnlyList<string> DISPONIBLES_POR_DEFECTO = new[] { "Vendedores", "Almacén", "Tiendas", "Administración" };

        public ContextoPerfilesNovedades(INovedadesService servicio)
        {
            Servicio = servicio;
        }

        public INovedadesService Servicio { get; }

        /// <summary>null = todavía sin saber (o la API no lo sabe): no se filtra nada.</summary>
        public PerfilesUsuarioNovedades MisPerfiles { get; internal set; }

        /// <summary>Se le filtra algo: tiene perfiles y no las ve todas por su puesto.</summary>
        public bool TieneFiltro => MisPerfiles != null && !MisPerfiles.VeTodas && (MisPerfiles.Perfiles?.Count ?? 0) > 0;

        /// <summary>Dirección o Informática (la API lo vuelve a comprobar).</summary>
        public bool PuedeEditar => Servicio != null && MisPerfiles?.PuedeEditar == true;

        public IReadOnlyList<string> Disponibles =>
            MisPerfiles?.Disponibles != null && MisPerfiles.Disponibles.Count > 0 ? MisPerfiles.Disponibles : DISPONIBLES_POR_DEFECTO;

        /// <summary>Si una novedad con esos perfiles sale por defecto (sin perfiles = para todos).</summary>
        public bool EsParaMi(IReadOnlyCollection<string> perfilesNovedad)
        {
            if (!TieneFiltro || perfilesNovedad == null || perfilesNovedad.Count == 0)
            {
                return true;
            }
            return perfilesNovedad.Any(p => MisPerfiles.Perfiles.Any(m => string.Equals(m, p, StringComparison.OrdinalIgnoreCase)));
        }

        /// <summary>«Almacén», «Almacén y Tiendas», «Vendedores, Almacén y Tiendas».</summary>
        internal static string Unir(IReadOnlyList<string> perfiles)
        {
            if (perfiles == null || perfiles.Count == 0)
            {
                return string.Empty;
            }
            return perfiles.Count == 1
                ? perfiles[0]
                : string.Join(", ", perfiles.Take(perfiles.Count - 1)) + " y " + perfiles[perfiles.Count - 1];
        }
    }

    /// <summary>Sugerencia 551: una casilla del editor de a quién afecta una novedad.</summary>
    public class OpcionPerfilNovedad : ObservableObject
    {
        public OpcionPerfilNovedad(string nombre, bool marcado)
        {
            Nombre = nombre;
            _marcado = marcado;
        }

        public string Nombre { get; }

        private bool _marcado;
        public bool Marcado { get => _marcado; set => SetProperty(ref _marcado, value); }
    }

    /// <summary>
    /// Sugerencia 551: a quién afecta una novedad («Para: Almacén y Tiendas») y, para Dirección e Informática, el
    /// editor con una casilla por perfil. Las sugerencias no llevan perfiles (no se filtran).
    /// </summary>
    public class PerfilesNovedadItem : ObservableObject
    {
        internal const string TEXTO_PARA_TODOS = "Para todos";

        private readonly int _novedadId;
        private readonly bool _esSugerencia;
        private readonly ContextoPerfilesNovedades _contexto;

        public PerfilesNovedadItem(int novedadId, IEnumerable<string> perfiles, bool esSugerencia, ContextoPerfilesNovedades contexto)
        {
            _novedadId = novedadId;
            _esSugerencia = esSugerencia;
            _contexto = contexto;
            _actuales = Limpiar(perfiles);
            EditarCommand = new RelayCommand(AbrirOCerrarEditor, () => PuedeEditar && !Guardando);
            GuardarCommand = new AsyncRelayCommand(Guardar, () => PuedeEditar && Editando && !Guardando);
        }

        private List<string> _actuales;
        /// <summary>Vacía = para todos.</summary>
        public IReadOnlyList<string> Actuales => _actuales;

        /// <summary>«Para: Almacén y Tiendas». Sin perfiles, nada (salvo a quien puede editarlos: «Para todos»).</summary>
        public string Texto => _actuales.Count > 0
            ? "Para: " + ContextoPerfilesNovedades.Unir(_actuales)
            : PuedeEditar ? TEXTO_PARA_TODOS : null;

        public bool HayTexto => !string.IsNullOrEmpty(Texto);

        public bool PuedeEditar => !_esSugerencia && _contexto != null && _contexto.PuedeEditar;

        /// <summary>La fila se ve si hay algo que enseñar.</summary>
        public bool Mostrar => HayTexto || PuedeEditar || HayMensaje;

        private bool _editando;
        public bool Editando
        {
            get => _editando;
            private set
            {
                if (SetProperty(ref _editando, value))
                {
                    GuardarCommand.NotifyCanExecuteChanged();
                    OnPropertyChanged(nameof(TextoBotonEditar));
                }
            }
        }

        public string TextoBotonEditar => Editando ? "Cancelar" : "¿A quién afecta?";

        private bool _guardando;
        public bool Guardando
        {
            get => _guardando;
            private set
            {
                if (SetProperty(ref _guardando, value))
                {
                    EditarCommand.NotifyCanExecuteChanged();
                    GuardarCommand.NotifyCanExecuteChanged();
                }
            }
        }

        private string _mensaje;
        /// <summary>Por qué no se pudo guardar (el mensaje de la API).</summary>
        public string Mensaje
        {
            get => _mensaje;
            private set
            {
                if (SetProperty(ref _mensaje, value))
                {
                    OnPropertyChanged(nameof(HayMensaje));
                    OnPropertyChanged(nameof(Mostrar));
                }
            }
        }

        public bool HayMensaje => !string.IsNullOrWhiteSpace(Mensaje);

        public ObservableCollection<OpcionPerfilNovedad> Opciones { get; } = new ObservableCollection<OpcionPerfilNovedad>();

        public IRelayCommand EditarCommand { get; }
        public IAsyncRelayCommand GuardarCommand { get; }

        /// <summary>Lo llama la ventana cuando llegan los perfiles del usuario (cambia quién puede editar).</summary>
        internal void Refrescar()
        {
            OnPropertyChanged(nameof(PuedeEditar));
            OnPropertyChanged(nameof(Texto));
            OnPropertyChanged(nameof(HayTexto));
            OnPropertyChanged(nameof(Mostrar));
            EditarCommand.NotifyCanExecuteChanged();
            GuardarCommand.NotifyCanExecuteChanged();
        }

        internal void AbrirOCerrarEditor()
        {
            if (Editando)
            {
                Editando = false;
                return;
            }
            Opciones.Clear();
            foreach (string perfil in _contexto.Disponibles)
            {
                Opciones.Add(new OpcionPerfilNovedad(perfil, _actuales.Any(a => string.Equals(a, perfil, StringComparison.OrdinalIgnoreCase))));
            }
            Mensaje = null;
            Editando = true;
        }

        internal async Task Guardar()
        {
            if (!PuedeEditar || !Editando)
            {
                return;
            }
            List<string> elegidos = Opciones.Where(o => o.Marcado).Select(o => o.Nombre).ToList();
            // Todos marcados es lo mismo que ninguno: para todos (la API lo guarda igual).
            if (elegidos.Count == Opciones.Count)
            {
                elegidos = new List<string>();
            }
            Guardando = true;
            Mensaje = null;
            try
            {
                await _contexto.Servicio.CambiarPerfiles(_novedadId, elegidos);
                _actuales = elegidos;
                Editando = false;
                Refrescar();
            }
            catch (Exception ex)
            {
                Mensaje = ex.Message;
            }
            finally
            {
                Guardando = false;
            }
        }

        private static List<string> Limpiar(IEnumerable<string> perfiles) =>
            (perfiles ?? Enumerable.Empty<string>())
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => p.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
    }
}
