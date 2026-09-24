using Nesto.Infrastructure.Contracts;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace ControlesUsuario.Dialogs
{
    /// <summary>
    /// Nesto#372: muestra el changelog de novedades en lenguaje de usuario. El llamante carga las
    /// novedades (popup tras actualizar o menú Ayuda → Novedades) y las pasa por parámetro.
    /// Las novedades pueden abarcar VARIAS versiones (al actualizar saltándose versiones); para no
    /// mezclarlas, se agrupan por versión y se muestra SOLO una a la vez, empezando por la más nueva,
    /// con navegación anterior/siguiente entre versiones.
    /// Nesto#487 (NestoAPI#526/#527): por delante de la versión más nueva está la página «Sugerencias
    /// pendientes» (las pide la propia ventana al llegar a ella), con el botón para sugerir una
    /// característica nueva; y un buscador que salta a la versión (o a Sugerencias) de lo encontrado.
    /// </summary>
    public class NovedadesDialogViewModel : ObservableObject, IDialogAware
    {
        /// <summary>Índice de página de las sugerencias: por delante de la versión más nueva (índice 0).</summary>
        internal const int INDICE_SUGERENCIAS = -1;
        internal const string TITULO_SUGERENCIAS = "Sugerencias pendientes";

        // NestoAPI#520: feedback de los usuarios (votos y comentarios). Sin servicio (tests antiguos) o si la
        // API no trae los contadores, la ventana se ve exactamente como antes.
        private readonly INovedadesService _servicio;
        private readonly IPortapapelesImagenes _portapapeles;
        private readonly Func<string, bool> _preguntar;

        public NovedadesDialogViewModel() : this(null, null, null) { }

        /// <summary>El que usa el contenedor (Prism/Unity elige el constructor con más parámetros resolubles).</summary>
        public NovedadesDialogViewModel(INovedadesService servicio)
            : this(servicio, new PortapapelesImagenesWpf(), PreguntarConMessageBox) { }

        internal NovedadesDialogViewModel(INovedadesService servicio, IPortapapelesImagenes portapapeles, Func<string, bool> preguntar)
        {
            _servicio = servicio;
            _portapapeles = portapapeles;
            _preguntar = preguntar ?? (_ => false);

            AbrirSugerenciaCommand = new AsyncRelayCommand(AbrirOCerrarSugerencia, () => PuedeSugerir);
            EnviarSugerenciaCommand = new AsyncRelayCommand(EnviarSugerencia, () => PuedeSugerir && !string.IsNullOrWhiteSpace(TextoSugerencia) && !EnviandoSugerencia);
            PegarImagenSugerenciaCommand = new RelayCommand(() => PegarImagenSugerencia());
            QuitarImagenSugerenciaCommand = new RelayCommand(() => ImagenSugerencia = null, () => ImagenSugerencia != null);
            BuscarCommand = new AsyncRelayCommand(Buscar, () => _servicio != null && !string.IsNullOrWhiteSpace(TextoBusqueda));
            CerrarBusquedaCommand = new RelayCommand(CerrarBusqueda);
        }

        private static bool PreguntarConMessageBox(string pregunta)
            => MessageBox.Show(pregunta, "Novedades", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

        private RelayCommand _closeDialogCommand;
        public RelayCommand CloseDialogCommand =>
            _closeDialogCommand ?? (_closeDialogCommand = new RelayCommand(() => RequestClose?.Invoke(new DialogResult(ButtonResult.OK))));

        // Todas las novedades cargadas (envueltas UNA vez: al navegar se conserva su estado) y agrupadas
        // por versión, de la más NUEVA (índice 0) a la más antigua.
        private List<NovedadItem> _items = new List<NovedadItem>();
        private List<IGrouping<string, NovedadItem>> _porVersion = new List<IGrouping<string, NovedadItem>>();
        private int _indice;
        // Nesto#487: null = aún sin pedir a la API (o falló: se reintenta al volver).
        private List<NovedadItem> _sugerencias;

        private string _title = "Novedades de Nesto";
        public string Title
        {
            get { return _title; }
            set { SetProperty(ref _title, value); }
        }

        private List<NovedadItem> _novedades = new List<NovedadItem>();
        public List<NovedadItem> Novedades
        {
            get { return _novedades; }
            set { SetProperty(ref _novedades, value); }
        }

        // Texto de la versión que se está mostrando (p. ej. "Versión 1.10.8.0").
        private string _versionActual;
        public string VersionActual
        {
            get { return _versionActual; }
            set { SetProperty(ref _versionActual, value); }
        }

        // "Versión anterior" = una más antigua (índice mayor, porque están de nueva a antigua).
        private RelayCommand _versionAnteriorCommand;
        public RelayCommand VersionAnteriorCommand =>
            _versionAnteriorCommand ?? (_versionAnteriorCommand = new RelayCommand(
                () => MostrarVersion(_indice + 1), () => _indice < _porVersion.Count - 1));

        // "Versión siguiente" = una más nueva (índice menor). Nesto#487: desde la más nueva, a Sugerencias.
        private RelayCommand _versionSiguienteCommand;
        public RelayCommand VersionSiguienteCommand =>
            _versionSiguienteCommand ?? (_versionSiguienteCommand = new RelayCommand(
                () =>
                {
                    if (_indice - 1 == INDICE_SUGERENCIAS)
                    {
                        _ = IrASugerencias();
                    }
                    else
                    {
                        MostrarVersion(_indice - 1);
                    }
                },
                () => _indice > 0 || (_indice == 0 && PuedeSugerir)));

        public event Action<IDialogResult> RequestClose;

        public bool CanCloseDialog() => true;

        public void OnDialogClosed() { }

        public void OnDialogOpened(IDialogParameters parameters)
        {
            if (parameters.ContainsKey("title"))
            {
                Title = parameters.GetValue<string>("title");
            }

            List<NovedadUsuario> todas = parameters.ContainsKey("novedades")
                ? (parameters.GetValue<List<NovedadUsuario>>("novedades") ?? new List<NovedadUsuario>())
                : new List<NovedadUsuario>();

            _items = todas.Where(n => n != null).Select(CrearItem).ToList();
            Reagrupar();
            MostrarVersion(0);

            // Nesto#477: desde la campana (te han contestado): directos a la novedad y al comentario.
            if (parameters.ContainsKey(PARAMETRO_NOVEDAD_ID))
            {
                int? comentarioId = parameters.ContainsKey(PARAMETRO_COMENTARIO_ID)
                    ? parameters.GetValue<int>(PARAMETRO_COMENTARIO_ID)
                    : (int?)null;
                _ = IrANovedadComentario(parameters.GetValue<int>(PARAMETRO_NOVEDAD_ID), comentarioId);
            }
        }

        /// <summary>Nesto#477: parámetros del diálogo para abrirlo en una novedad y en uno de sus comentarios.</summary>
        public const string PARAMETRO_NOVEDAD_ID = "novedadId";
        public const string PARAMETRO_COMENTARIO_ID = "comentarioId";

        private NovedadItem CrearItem(NovedadUsuario n) => new NovedadItem(n, _servicio, _portapapeles, _preguntar);

        // Agrupar por versión y ordenar de la más nueva a la más antigua (por System.Version si
        // parsea; si no, por texto, para no romper con versiones con formato raro).
        private void Reagrupar()
        {
            _porVersion = _items
                .GroupBy(n => (n.Version ?? string.Empty).Trim())
                .OrderByDescending(g => ParsearVersion(g.Key))
                .ThenByDescending(g => g.Key, StringComparer.Ordinal)
                .ToList();
        }

        private void MostrarVersion(int indice)
        {
            EnSugerencias = false;
            if (_porVersion.Count == 0)
            {
                _indice = 0;
                Novedades = new List<NovedadItem>();
                VersionActual = null;
            }
            else
            {
                _indice = Math.Max(0, Math.Min(indice, _porVersion.Count - 1));
                IGrouping<string, NovedadItem> grupo = _porVersion[_indice];
                Novedades = grupo.ToList();
                VersionActual = string.IsNullOrWhiteSpace(grupo.Key) ? "Novedades" : $"Versión {grupo.Key}";
            }
            NotificarNavegacion();
        }

        private void NotificarNavegacion()
        {
            VersionAnteriorCommand.NotifyCanExecuteChanged();
            VersionSiguienteCommand.NotifyCanExecuteChanged();
        }

        private static Version ParsearVersion(string version)
            => Version.TryParse(version, out Version v) ? v : new Version(0, 0);

        #region Nesto#487: sugerencias de los usuarios (NestoAPI#526)

        /// <summary>Hay API con la que hablar: existe la página de sugerencias y se puede sugerir.</summary>
        public bool PuedeSugerir => _servicio != null;

        private bool _enSugerencias;
        public bool EnSugerencias { get => _enSugerencias; private set => SetProperty(ref _enSugerencias, value); }

        private bool _cargandoSugerencias;
        public bool CargandoSugerencias { get => _cargandoSugerencias; private set => SetProperty(ref _cargandoSugerencias, value); }

        private string _mensajeSugerencias;
        /// <summary>Aviso de la página de sugerencias (error al cargarlas, lista vacía, gracias por sugerir...).</summary>
        public string MensajeSugerencias
        {
            get => _mensajeSugerencias;
            private set
            {
                if (SetProperty(ref _mensajeSugerencias, value))
                {
                    OnPropertyChanged(nameof(HayMensajeSugerencias));
                }
            }
        }
        public bool HayMensajeSugerencias => !string.IsNullOrWhiteSpace(MensajeSugerencias);

        /// <summary>Página de sugerencias: la lista se pide a la API la primera vez que se llega a ella.</summary>
        internal async Task IrASugerencias()
        {
            if (!PuedeSugerir)
            {
                return;
            }
            _indice = INDICE_SUGERENCIAS;
            EnSugerencias = true;
            VersionActual = TITULO_SUGERENCIAS;
            Novedades = _sugerencias?.ToList() ?? new List<NovedadItem>();
            NotificarNavegacion();
            if (_sugerencias == null && !CargandoSugerencias)
            {
                await CargarSugerencias();
            }
        }

        private async Task CargarSugerencias()
        {
            CargandoSugerencias = true;
            MensajeSugerencias = null;
            try
            {
                List<NovedadUsuario> lista = await _servicio.LeerSugerencias() ?? new List<NovedadUsuario>();
                _sugerencias = lista.Where(n => n != null).Select(CrearItem).ToList();
                if (_sugerencias.Count == 0)
                {
                    MensajeSugerencias = "Todavía no hay sugerencias. ¿Echas algo en falta? Cuéntanoslo con «Sugerir nueva característica».";
                }
            }
            catch (Exception ex)
            {
                MensajeSugerencias = ex.Message;
            }
            finally
            {
                CargandoSugerencias = false;
            }
            if (EnSugerencias && _sugerencias != null)
            {
                Novedades = _sugerencias.ToList();
            }
            // Las capturas, después de pintar la lista (no la retrasan).
            foreach (NovedadItem sugerencia in _sugerencias ?? new List<NovedadItem>())
            {
                await sugerencia.CargarImagenNovedad();
            }
        }

        private bool _formularioSugerenciaAbierto;
        public bool FormularioSugerenciaAbierto { get => _formularioSugerenciaAbierto; private set => SetProperty(ref _formularioSugerenciaAbierto, value); }

        private string _textoSugerencia;
        public string TextoSugerencia
        {
            get => _textoSugerencia;
            set
            {
                if (SetProperty(ref _textoSugerencia, value))
                {
                    EnviarSugerenciaCommand.NotifyCanExecuteChanged();
                }
            }
        }

        private byte[] _imagenSugerencia;
        public byte[] ImagenSugerencia
        {
            get => _imagenSugerencia;
            set
            {
                if (SetProperty(ref _imagenSugerencia, value))
                {
                    OnPropertyChanged(nameof(TieneImagenSugerencia));
                    QuitarImagenSugerenciaCommand.NotifyCanExecuteChanged();
                }
            }
        }
        public bool TieneImagenSugerencia => ImagenSugerencia != null;

        private bool _enviandoSugerencia;
        public bool EnviandoSugerencia
        {
            get => _enviandoSugerencia;
            private set
            {
                if (SetProperty(ref _enviandoSugerencia, value))
                {
                    EnviarSugerenciaCommand.NotifyCanExecuteChanged();
                }
            }
        }

        public IAsyncRelayCommand AbrirSugerenciaCommand { get; }
        public IAsyncRelayCommand EnviarSugerenciaCommand { get; }
        public IRelayCommand PegarImagenSugerenciaCommand { get; }
        public IRelayCommand QuitarImagenSugerenciaCommand { get; }

        /// <summary>«Sugerir nueva característica»: lleva a Sugerencias y abre (o cierra) el cuadro.</summary>
        internal async Task AbrirOCerrarSugerencia()
        {
            if (!PuedeSugerir)
            {
                return;
            }
            if (FormularioSugerenciaAbierto)
            {
                FormularioSugerenciaAbierto = false;
                return;
            }
            FormularioSugerenciaAbierto = true;
            // Igual que al comentar: si hay una imagen copiada, se ofrece adjuntarla una vez por apertura.
            if (ImagenSugerencia == null && _portapapeles != null && _portapapeles.HayImagen()
                && _preguntar("Tienes una imagen copiada en el portapapeles. ¿Quieres adjuntarla a tu sugerencia?"))
            {
                PegarImagenSugerencia();
            }
            if (!EnSugerencias)
            {
                await IrASugerencias();
            }
        }

        /// <summary>Mismo mecanismo que en los comentarios: true si lo copiado era una imagen (así Ctrl+V solo se «come» la tecla entonces).</summary>
        internal bool PegarImagenSugerencia()
        {
            if (!CapturaPortapapeles.Leer(_portapapeles, out byte[] png, out string aviso))
            {
                return false;
            }
            if (png == null)
            {
                MensajeSugerencias = aviso;
                return true;
            }
            ImagenSugerencia = png;
            MensajeSugerencias = null;
            return true;
        }

        internal async Task EnviarSugerencia()
        {
            if (!PuedeSugerir || string.IsNullOrWhiteSpace(TextoSugerencia))
            {
                return;
            }
            if (ImagenSugerencia != null && ImagenSugerencia.Length > CapturaPortapapeles.TAMANO_MAXIMO_IMAGEN)
            {
                MensajeSugerencias = "La imagen supera los 2 MB: quítala o haz un recorte más pequeño.";
                return;
            }
            EnviandoSugerencia = true;
            MensajeSugerencias = null;
            try
            {
                byte[] imagen = ImagenSugerencia;
                NovedadUsuario creada = await _servicio.Sugerir(TextoSugerencia.Trim(), imagen);
                TextoSugerencia = null;
                ImagenSugerencia = null;
                FormularioSugerenciaAbierto = false;
                if (!EnSugerencias || _sugerencias == null)
                {
                    // Si la lista aún no estaba cargada, al cargarla ya viene la nueva.
                    await IrASugerencias();
                }
                NovedadItem item = _sugerencias?.FirstOrDefault(s => s.Id == creada.Id);
                if (item == null)
                {
                    item = CrearItem(creada);
                    if (_sugerencias == null)
                    {
                        _sugerencias = new List<NovedadItem>();
                    }
                    // Arriba del todo: acaba de llegar y así se ve que ya está.
                    _sugerencias.Insert(0, item);
                }
                if (creada.TieneImagen && item.ImagenNovedad == null)
                {
                    item.ImagenNovedad = imagen;
                }
                Novedades = _sugerencias.ToList();
                Destacar(item);
                MensajeSugerencias = "¡Gracias! Tu sugerencia ya está en la lista: los demás pueden votarla y comentarla.";
            }
            catch (Exception ex)
            {
                MensajeSugerencias = ex.Message;
            }
            finally
            {
                EnviandoSugerencia = false;
            }
        }

        #endregion

        #region Nesto#487: buscador de novedades (NestoAPI#527)

        private string _textoBusqueda;
        public string TextoBusqueda
        {
            get => _textoBusqueda;
            set
            {
                if (SetProperty(ref _textoBusqueda, value))
                {
                    BuscarCommand.NotifyCanExecuteChanged();
                }
            }
        }

        private List<ResultadoBusquedaNovedad> _resultadosBusqueda;
        public List<ResultadoBusquedaNovedad> ResultadosBusqueda
        {
            get => _resultadosBusqueda;
            private set
            {
                if (SetProperty(ref _resultadosBusqueda, value))
                {
                    OnPropertyChanged(nameof(HayResultadosBusqueda));
                }
            }
        }
        public bool HayResultadosBusqueda => ResultadosBusqueda != null && ResultadosBusqueda.Count > 0;

        private string _mensajeBusqueda;
        public string MensajeBusqueda
        {
            get => _mensajeBusqueda;
            private set
            {
                if (SetProperty(ref _mensajeBusqueda, value))
                {
                    OnPropertyChanged(nameof(HayMensajeBusqueda));
                }
            }
        }
        public bool HayMensajeBusqueda => !string.IsNullOrWhiteSpace(MensajeBusqueda);

        private ResultadoBusquedaNovedad _resultadoSeleccionado;
        /// <summary>Al elegir un resultado se salta a su versión (o a Sugerencias) con la novedad resaltada.</summary>
        public ResultadoBusquedaNovedad ResultadoSeleccionado
        {
            get => _resultadoSeleccionado;
            set
            {
                if (SetProperty(ref _resultadoSeleccionado, value) && value != null)
                {
                    _ = IrAResultado(value.Novedad);
                }
            }
        }

        private NovedadItem _novedadDestacada;
        /// <summary>La novedad elegida en el buscador (o la sugerencia recién creada): la vista la hace visible.</summary>
        public NovedadItem NovedadDestacada { get => _novedadDestacada; private set => SetProperty(ref _novedadDestacada, value); }

        public IAsyncRelayCommand BuscarCommand { get; }
        public IRelayCommand CerrarBusquedaCommand { get; }

        internal async Task Buscar()
        {
            if (_servicio == null || string.IsNullOrWhiteSpace(TextoBusqueda))
            {
                return;
            }
            MensajeBusqueda = null;
            try
            {
                List<NovedadUsuario> encontradas = await _servicio.Buscar(TextoBusqueda.Trim()) ?? new List<NovedadUsuario>();
                ResultadosBusqueda = encontradas.Where(n => n != null)
                    .Select(n => new ResultadoBusquedaNovedad(n))
                    .ToList();
                if (ResultadosBusqueda.Count == 0)
                {
                    MensajeBusqueda = "No hay ninguna novedad ni sugerencia con esas palabras.";
                }
            }
            catch (Exception ex)
            {
                ResultadosBusqueda = null;
                MensajeBusqueda = ex.Message;
            }
        }

        private void CerrarBusqueda()
        {
            ResultadosBusqueda = null;
            MensajeBusqueda = null;
            TextoBusqueda = null;
        }

        /// <summary>
        /// Salta a la página de la novedad encontrada y la resalta. Si su versión no está entre las cargadas
        /// (p. ej. en el popup de arranque solo vienen las nuevas), se piden todas a la API.
        /// </summary>
        internal async Task IrAResultado(NovedadUsuario encontrada)
        {
            if (encontrada == null)
            {
                return;
            }

            if (encontrada.EsSugerencia)
            {
                await IrASugerencias();
                NovedadItem sugerencia = _sugerencias?.FirstOrDefault(s => s.Id == encontrada.Id);
                if (sugerencia == null)
                {
                    // Cerrada (o recién creada por otro): se enseña igual para poder comentarla.
                    sugerencia = CrearItem(encontrada);
                    if (_sugerencias == null)
                    {
                        _sugerencias = new List<NovedadItem>();
                    }
                    _sugerencias.Add(sugerencia);
                    Novedades = _sugerencias.ToList();
                    await sugerencia.CargarImagenNovedad();
                }
                Destacar(sugerencia);
                return;
            }

            if (!_items.Any(i => i.Id == encontrada.Id))
            {
                // Si falla, se sigue con la novedad del propio buscador.
                await CargarTodasLasNovedades();
            }
            NovedadItem item = _items.FirstOrDefault(i => i.Id == encontrada.Id);
            if (item == null)
            {
                item = CrearItem(encontrada);
                _items.Add(item);
            }
            MostrarConVersion(item);
        }

        /// <summary>Añade a las cargadas todas las publicadas (en el popup de arranque solo vienen las nuevas).</summary>
        private async Task CargarTodasLasNovedades()
        {
            if (_servicio == null)
            {
                return;
            }
            // ObtenerNovedades nunca lanza: si falla, se sigue con lo que hay.
            List<NovedadUsuario> todas = await _servicio.ObtenerNovedades() ?? new List<NovedadUsuario>();
            var cargadas = new HashSet<int>(_items.Select(i => i.Id));
            _items.AddRange(todas.Where(n => n != null && !n.EsSugerencia && cargadas.Add(n.Id)).Select(CrearItem));
        }

        private void MostrarConVersion(NovedadItem item)
        {
            Reagrupar();
            string version = (item.Version ?? string.Empty).Trim();
            MostrarVersion(_porVersion.FindIndex(g => g.Key == version));
            Destacar(item);
        }

        #endregion

        #region Nesto#477: abrir en una novedad y un comentario (la campana de notificaciones)

        internal const string MENSAJE_NOVEDAD_NO_ENCONTRADA = "No encuentro la novedad de la notificación: puede que ya no esté publicada.";

        /// <summary>
        /// Salta a la novedad <paramref name="novedadId"/> (con versión: a su versión; sin ella es una
        /// sugerencia: a Sugerencias), la resalta, le abre los comentarios y resalta el comentario
        /// <paramref name="comentarioId"/>. De la notificación solo se tiene el id: se busca en las cargadas,
        /// luego en todas las publicadas y, si no está, en las sugerencias. Reutiliza el salto del buscador (#487).
        /// </summary>
        internal async Task IrANovedadComentario(int novedadId, int? comentarioId)
        {
            NovedadItem item = await IrANovedad(novedadId);
            if (item != null)
            {
                await item.AbrirComentarios(comentarioId);
            }
        }

        private async Task<NovedadItem> IrANovedad(int novedadId)
        {
            NovedadItem item = _items.FirstOrDefault(i => i.Id == novedadId);
            if (item == null)
            {
                await CargarTodasLasNovedades();
                item = _items.FirstOrDefault(i => i.Id == novedadId);
            }
            if (item != null)
            {
                MostrarConVersion(item);
                return item;
            }

            await IrASugerencias();
            NovedadItem sugerencia = _sugerencias?.FirstOrDefault(s => s.Id == novedadId);
            if (sugerencia == null)
            {
                MensajeSugerencias = MENSAJE_NOVEDAD_NO_ENCONTRADA;
                return null;
            }
            Destacar(sugerencia);
            return sugerencia;
        }

        private void Destacar(NovedadItem item)
        {
            if (NovedadDestacada != null)
            {
                NovedadDestacada.Destacada = false;
            }
            if (item != null)
            {
                item.Destacada = true;
            }
            NovedadDestacada = item;
        }

        #endregion
    }

    /// <summary>Nesto#487: una línea de resultados del buscador de novedades.</summary>
    public class ResultadoBusquedaNovedad
    {
        public ResultadoBusquedaNovedad(NovedadUsuario novedad)
        {
            Novedad = novedad ?? throw new ArgumentNullException(nameof(novedad));
        }

        public NovedadUsuario Novedad { get; }
        public string Titulo => Novedad.Titulo;
        /// <summary>Dónde está: «Versión 1.10.30.0» o «Sugerencia».</summary>
        public string Donde => Novedad.EsSugerencia ? "Sugerencia" : $"Versión {Novedad.Version.Trim()}";
    }
}
