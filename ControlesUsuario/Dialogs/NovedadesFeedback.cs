using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nesto.Infrastructure.Contracts;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace ControlesUsuario.Dialogs
{
    /// <summary>
    /// NestoAPI#520: acceso mínimo al portapapeles para adjuntar capturas sin guardar ficheros
    /// (Recortes → portapapeles → pegar). Detrás de una interfaz para poder probar el ViewModel.
    /// </summary>
    public interface IPortapapelesImagenes
    {
        bool HayImagen();
        /// <summary>La imagen del portapapeles en PNG, o null si no hay o no se puede leer.</summary>
        byte[] LeerImagenPng();
    }

    public class PortapapelesImagenesWpf : IPortapapelesImagenes
    {
        public bool HayImagen()
        {
            try
            {
                return Clipboard.ContainsImage();
            }
            catch (Exception)
            {
                // El portapapeles puede estar bloqueado por otra aplicación: como si no hubiera imagen.
                return false;
            }
        }

        public byte[] LeerImagenPng()
        {
            try
            {
                BitmapSource imagen = Clipboard.GetImage();
                if (imagen == null)
                {
                    return null;
                }
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(imagen));
                using (var ms = new MemoryStream())
                {
                    encoder.Save(ms);
                    return ms.ToArray();
                }
            }
            catch (Exception)
            {
                return null;
            }
        }
    }

    /// <summary>
    /// NestoAPI#520/#526: lo común a pegar una captura en un comentario o en una sugerencia: leerla del
    /// portapapeles y comprobar el límite de la API ANTES de enviar.
    /// </summary>
    internal static class CapturaPortapapeles
    {
        /// <summary>Mismo límite que la API (2 MB tras decodificar).</summary>
        public const int TAMANO_MAXIMO_IMAGEN = 2 * 1024 * 1024;

        /// <summary>
        /// True si lo copiado es una imagen (quepa o no). Si cabe, <paramref name="png"/> la trae; si no,
        /// <paramref name="aviso"/> explica por qué no se adjunta.
        /// </summary>
        public static bool Leer(IPortapapelesImagenes portapapeles, out byte[] png, out string aviso)
        {
            png = null;
            aviso = null;
            if (portapapeles == null || !portapapeles.HayImagen())
            {
                return false;
            }
            byte[] leida = portapapeles.LeerImagenPng();
            if (leida == null || leida.Length == 0)
            {
                return false;
            }
            if (leida.Length > TAMANO_MAXIMO_IMAGEN)
            {
                aviso = $"La imagen copiada ocupa {leida.Length / 1024.0 / 1024.0:0.0} MB y el máximo son 2 MB: haga un recorte más pequeño.";
                return true;
            }
            png = leida;
            return true;
        }
    }

    /// <summary>Nesto#578 (sugerencia 900): una captura pegada que aún no se ha enviado (miniatura con «Quitar»).</summary>
    public class CapturaPegadaItem
    {
        public CapturaPegadaItem(byte[] png, Action<CapturaPegadaItem> quitar)
        {
            Png = png;
            QuitarCommand = new RelayCommand(() => quitar?.Invoke(this));
        }

        public byte[] Png { get; }
        public IRelayCommand QuitarCommand { get; }
    }

    /// <summary>
    /// Nesto#578 (sugerencia 900): las capturas pegadas en un cuadro (comentario, sugerencia o aviso), en el orden en
    /// que se pegaron. Ctrl+V varias veces añade una más cada vez; cada miniatura tiene su «Quitar».
    /// </summary>
    public class CapturasPegadas : ObservableObject
    {
        /// <summary>Mismo máximo que la API (NestoAPI#754).</summary>
        public const int MAXIMO = 10;
        internal static readonly string AVISO_MAXIMO = $"Como máximo se pueden adjuntar {MAXIMO} capturas.";
        internal const string AVISO_REPETIDA = "Esa captura ya está adjunta.";

        public CapturasPegadas()
        {
            Items.CollectionChanged += (s, e) =>
            {
                OnPropertyChanged(nameof(HayCapturas));
                OnPropertyChanged(nameof(Numero));
                Cambiadas?.Invoke(this, EventArgs.Empty);
            };
        }

        public ObservableCollection<CapturaPegadaItem> Items { get; } = new ObservableCollection<CapturaPegadaItem>();
        public bool HayCapturas => Items.Count > 0;
        public int Numero => Items.Count;

        /// <summary>Se ha añadido o quitado alguna.</summary>
        public event EventHandler Cambiadas;

        /// <summary>Añade la captura al final. Devuelve null si se ha añadido o, si no, el aviso para el usuario.</summary>
        public string Anadir(byte[] png)
        {
            if (png == null || png.Length == 0)
            {
                return null;
            }
            if (Items.Count >= MAXIMO)
            {
                return AVISO_MAXIMO;
            }
            if (Items.Any(i => i.Png.SequenceEqual(png)))
            {
                return AVISO_REPETIDA;
            }
            Items.Add(new CapturaPegadaItem(png, i => Items.Remove(i)));
            return null;
        }

        /// <summary>Lo que se manda a la API: las capturas en orden, o null si no hay ninguna.</summary>
        public IReadOnlyList<byte[]> ParaEnviar() => Items.Count == 0 ? null : Items.Select(i => i.Png).ToList();

        public bool AlgunaDemasiadoGrande => Items.Any(i => i.Png.Length > CapturaPortapapeles.TAMANO_MAXIMO_IMAGEN);

        public void Vaciar() => Items.Clear();
    }

    /// <summary>Nesto#578: una captura que se enseña (de una sugerencia o de un comentario); llega aparte.</summary>
    public class CapturaVistaItem : ObservableObject
    {
        private byte[] _imagen;
        public byte[] Imagen { get => _imagen; set => SetProperty(ref _imagen, value); }

        private bool _noDisponible;
        public bool NoDisponible { get => _noDisponible; set => SetProperty(ref _noDisponible, value); }
    }

    /// <summary>
    /// Nesto#578 (NestoAPI#754): cómo se cargan las capturas de una sugerencia o de un comentario. Con la lista de la API,
    /// cada una por su Id; sin ella (API vieja o imagen de antes) y con <c>TieneImagen</c>, la única de siempre.
    /// </summary>
    internal static class CargaCapturas
    {
        public static async Task Cargar(ObservableCollection<CapturaVistaItem> destino, IReadOnlyList<CapturaNovedad> capturas,
            bool tieneImagen, Func<int, Task<byte[]>> leerCaptura, Func<Task<byte[]>> leerImagenUnica)
        {
            var pendientes = new List<(CapturaVistaItem Item, Func<Task<byte[]>> Leer)>();
            List<CapturaNovedad> lista = (capturas ?? new List<CapturaNovedad>()).Where(c => c != null).OrderBy(c => c.Orden).ThenBy(c => c.Id).ToList();
            if (lista.Count > 0)
            {
                foreach (CapturaNovedad captura in lista)
                {
                    int id = captura.Id;
                    pendientes.Add((new CapturaVistaItem(), () => leerCaptura(id)));
                }
            }
            else if (tieneImagen)
            {
                pendientes.Add((new CapturaVistaItem(), leerImagenUnica));
            }
            foreach (var (item, _) in pendientes)
            {
                destino.Add(item);
            }
            foreach (var (item, leer) in pendientes)
            {
                try
                {
                    item.Imagen = await leer();
                }
                catch (Exception)
                {
                    item.NoDisponible = true;
                }
            }
        }

        /// <summary>
        /// Lo recién enviado se enseña con los bytes que ya tenemos. Con una API que aún no conoce la lista (Capturas
        /// null), solo se ha guardado la primera.
        /// </summary>
        public static void MostrarEnviadas(ObservableCollection<CapturaVistaItem> destino, IReadOnlyList<byte[]> enviadas,
            bool tieneImagen, List<CapturaNovedad> capturasDeLaApi)
        {
            if (!tieneImagen || enviadas == null || enviadas.Count == 0)
            {
                return;
            }
            IEnumerable<byte[]> guardadas = capturasDeLaApi == null ? enviadas.Take(1) : enviadas;
            foreach (byte[] png in guardadas)
            {
                destino.Add(new CapturaVistaItem { Imagen = png });
            }
        }
    }

    /// <summary>
    /// NestoAPI#520: una novedad de la ventana, con su feedback (votos y comentarios). Expone los mismos
    /// datos que <see cref="NovedadUsuario"/> para la plantilla. Si la API no trae los contadores (tablas de
    /// feedback aún no creadas) <see cref="TieneFeedback"/> es false y la ventana se ve como siempre.
    /// </summary>
    public class NovedadItem : ObservableObject
    {
        /// <summary>Mismo límite que la API (2 MB tras decodificar): se avisa ANTES de enviar.</summary>
        public const int TAMANO_MAXIMO_IMAGEN = CapturaPortapapeles.TAMANO_MAXIMO_IMAGEN;

        private readonly NovedadUsuario _novedad;
        private readonly INovedadesService _servicio;
        private readonly IPortapapelesImagenes _portapapeles;
        private readonly Func<string, bool> _preguntar;

        public NovedadItem(NovedadUsuario novedad, INovedadesService servicio, IPortapapelesImagenes portapapeles, Func<string, bool> preguntar,
            ListaMencionables mencionables = null, ContextoAdjuntosNovedades adjuntos = null,
            ContextoVideosNovedades videos = null)
        {
            _novedad = novedad ?? throw new ArgumentNullException(nameof(novedad));
            _servicio = servicio;
            _adjuntos = adjuntos;
            // Sugerencia 551: a quién afecta (se pone por script; aquí solo se enseña)
            Perfiles = new PerfilesNovedadItem(novedad.Perfiles);
            // NestoAPI#711: el botón «▶ Ver vídeo», solo si la novedad lo tiene y hay con qué verlo (no descarga nada hasta pulsarlo)
            if (novedad.TieneVideo && videos?.Servicio != null)
            {
                Video = new VideoNovedadItem(novedad.Id, novedad.Titulo, novedad.VideoTamano, videos);
            }
            // Nesto#519: sin servicio de adjuntos o con una API que no los conoce (Adjuntos null), ni chips ni botón.
            if (adjuntos?.Servicio != null && novedad.Adjuntos != null)
            {
                foreach (AdjuntoNovedad adjunto in novedad.Adjuntos.Where(a => a != null))
                {
                    Adjuntos.Add(CrearAdjunto(adjunto));
                }
            }
            Adjuntos.CollectionChanged += (s, e) => NotificarAdjuntos();
            AdjuntarCommand = new AsyncRelayCommand(Adjuntar, () => PuedeAdjuntar && !SubiendoAdjuntos);
            _portapapeles = portapapeles;
            _preguntar = preguntar ?? (_ => false);
            _votosPositivos = novedad.VotosPositivos ?? 0;
            _votosNegativos = novedad.VotosNegativos ?? 0;
            _miVoto = novedad.MiVoto;
            _numeroComentarios = novedad.NumeroComentarios ?? 0;
            // Nesto#491: la lista de mencionables es de la ventana (una petición para todos los cuadros).
            MencionesComentario = new AutocompletadoMenciones(mencionables ?? new ListaMencionables(servicio));

            MeGustaCommand = new AsyncRelayCommand(() => Votar(1));
            NoMeGustaCommand = new AsyncRelayCommand(() => Votar(-1));
            AbrirComentariosCommand = new AsyncRelayCommand(AbrirOCerrarComentarios);
            EnviarComentarioCommand = new AsyncRelayCommand(EnviarComentario, () => !string.IsNullOrWhiteSpace(NuevoTexto) && !Enviando);
            PegarImagenCommand = new RelayCommand(() => PegarImagen());
            CapturasComentario.Cambiadas += (s, e) => OnPropertyChanged(nameof(TieneImagenAdjunta));
            BorrarComentarioCommand = new AsyncRelayCommand<ComentarioItem>(BorrarComentario);
        }

        /// <summary>Sugerencia 551: a quién afecta la novedad («Para: Almacén y Tiendas»).</summary>
        public PerfilesNovedadItem Perfiles { get; }

        /// <summary>NestoAPI#711: el vídeo de la novedad (null si no tiene).</summary>
        public VideoNovedadItem Video { get; }
        public bool TieneVideo => Video != null;

        #region Nesto#519 (NestoAPI#616): adjuntos (PDF e imágenes)

        private readonly ContextoAdjuntosNovedades _adjuntos;

        /// <summary>Los chips, debajo del texto.</summary>
        public ObservableCollection<AdjuntoNovedadItem> Adjuntos { get; } = new ObservableCollection<AdjuntoNovedadItem>();
        public bool HayAdjuntos => Adjuntos.Count > 0;

        /// <summary>Dirección / Informática, con una API que ya conoce los adjuntos.</summary>
        public bool PuedeAdjuntar => _adjuntos?.Servicio != null && _adjuntos.PuedeGestionar && _novedad.Adjuntos != null;

        /// <summary>La fila de los adjuntos (chips, «Adjuntar…» o su aviso) se ve si hay algo que enseñar.</summary>
        public bool MostrarAdjuntos => HayAdjuntos || PuedeAdjuntar || HayMensajeAdjuntos;

        private bool _subiendoAdjuntos;
        public bool SubiendoAdjuntos
        {
            get => _subiendoAdjuntos;
            private set
            {
                if (SetProperty(ref _subiendoAdjuntos, value))
                {
                    AdjuntarCommand.NotifyCanExecuteChanged();
                    OnPropertyChanged(nameof(TextoBotonAdjuntar));
                }
            }
        }

        public string TextoBotonAdjuntar => SubiendoAdjuntos ? "Subiendo…" : "Adjuntar…";

        private string _mensajeAdjuntos;
        /// <summary>Aviso discreto si no se pudo abrir, subir o borrar un adjunto.</summary>
        public string MensajeAdjuntos
        {
            get => _mensajeAdjuntos;
            private set
            {
                if (SetProperty(ref _mensajeAdjuntos, value))
                {
                    OnPropertyChanged(nameof(HayMensajeAdjuntos));
                    OnPropertyChanged(nameof(MostrarAdjuntos));
                }
            }
        }
        public bool HayMensajeAdjuntos => !string.IsNullOrWhiteSpace(MensajeAdjuntos);

        public IAsyncRelayCommand AdjuntarCommand { get; }

        private AdjuntoNovedadItem CrearAdjunto(AdjuntoNovedad adjunto)
            => new AdjuntoNovedadItem(adjunto, _adjuntos, a => Adjuntos.Remove(a), m => MensajeAdjuntos = m);

        private void NotificarAdjuntos()
        {
            OnPropertyChanged(nameof(HayAdjuntos));
            OnPropertyChanged(nameof(MostrarAdjuntos));
        }

        /// <summary>Elige ficheros (PDF e imágenes), los sube y añade sus chips.</summary>
        internal async Task Adjuntar()
        {
            if (!PuedeAdjuntar || SubiendoAdjuntos)
            {
                return;
            }
            IList<string> rutas;
            try
            {
                rutas = _adjuntos.ElegirFicheros() ?? new List<string>();
            }
            catch (Exception ex)
            {
                MensajeAdjuntos = ex.Message;
                return;
            }
            if (rutas.Count == 0)
            {
                return;
            }
            SubiendoAdjuntos = true;
            MensajeAdjuntos = null;
            try
            {
                List<AdjuntoNovedad> creados = await _adjuntos.Servicio.Subir(Id, rutas) ?? new List<AdjuntoNovedad>();
                foreach (AdjuntoNovedad creado in creados.Where(c => c != null && !Adjuntos.Any(a => a.Id == c.Id)))
                {
                    Adjuntos.Add(CrearAdjunto(creado));
                }
            }
            catch (Exception ex)
            {
                MensajeAdjuntos = ex.Message;
            }
            finally
            {
                SubiendoAdjuntos = false;
            }
        }

        #endregion

        // ---- Datos de la novedad (los que ya pintaba la ventana) ----
        public int Id => _novedad.Id;
        public string Version => _novedad.Version;

        /// <summary>
        /// Nesto#549: la página junta las revisiones de una misma versión (1.10.47.0, .1 y .2); cuando mezcla varias,
        /// cada tarjeta enseña la suya en pequeño (<see cref="TextoVersion"/>). Lo pone la ventana al montar la página.
        /// </summary>
        private bool _versionVisible;
        public bool VersionVisible
        {
            get => _versionVisible;
            set => SetProperty(ref _versionVisible, value);
        }

        /// <summary>Nesto#549: «1.10.47.2» (la versión exacta de la novedad, para la tarjeta).</summary>
        public string TextoVersion => (_novedad.Version ?? string.Empty).Trim();
        public DateTime Fecha => _novedad.Fecha;
        public string Categoria => _novedad.Categoria;
        public string Titulo => _novedad.Titulo;
        public string Descripcion => _novedad.Descripcion;
        public string Ambito => _novedad.Ambito;

        // ---- NestoAPI#526: sugerencias de los usuarios (novedades sin versión) ----
        public bool EsSugerencia => _novedad.EsSugerencia;
        /// <summary>Lo que escribió el usuario, tal cual.</summary>
        public string TextoOriginal => _novedad.TextoOriginal;
        public bool TieneTextoOriginal => EsSugerencia && !string.IsNullOrWhiteSpace(TextoOriginal);
        /// <summary>En una sugerencia, la descripción es la ampliada que redactamos nosotros: solo se pinta si existe.</summary>
        public bool TieneDescripcion => !string.IsNullOrWhiteSpace(Descripcion);
        public string Estado => _novedad.Estado;
        /// <summary>NestoAPI#558: aviso de «Algo no funciona» (sale con la etiqueta Incidencia).</summary>
        public bool EsIncidencia => _novedad.EsIncidencia;
        /// <summary>NestoAPI#558: versión, pantalla y errores de ELMAH. Solo llega a Dirección / Informática.</summary>
        public string Contexto => _novedad.Contexto;
        public bool TieneContexto => !string.IsNullOrWhiteSpace(Contexto);
        public string SugeridaPor
        {
            get
            {
                if (!EsSugerencia)
                {
                    return null;
                }
                string verbo = EsIncidencia ? "Avisado" : "Sugerida";
                string quien = string.IsNullOrWhiteSpace(_novedad.SugeridaNombre) ? verbo : $"{verbo} por {_novedad.SugeridaNombre.Trim()}";
                DateTime? cuando = _novedad.SugeridaFecha;
                string estado = string.IsNullOrWhiteSpace(Estado) ? string.Empty : $" · {Estado.Trim()}";
                return cuando.HasValue ? $"{quien} el {cuando.Value:dd/MM/yyyy}{estado}" : quien + estado;
            }
        }
        public bool TieneImagenNovedad => _novedad.TieneImagen;

        // ---- Nesto#532 (NestoAPI#650): sugerencias privadas ----
        internal const string TEXTO_PRIVADA = "🔒 Privada: solo la ven quien la envió, Dirección e Informática";
        internal const string TEXTO_PRIVADA_SIN_VOTOS = "Las sugerencias privadas no admiten «Me gusta».";

        /// <summary>Solo la ven quien la envió y Dirección / Informática (la API no se la manda a nadie más).</summary>
        public bool EsPrivada => _novedad.Privada;
        public string TextoPrivada => EsPrivada ? TEXTO_PRIVADA : null;
        /// <summary>Los botones de voto: en una privada no se enseñan (la API no admite «Me gusta»).</summary>
        public bool PuedeVotar => !EsPrivada;

        /// <summary>
        /// Nesto#578: las capturas de la sugerencia (una o varias; se piden al enseñar la página de sugerencias). Cada una
        /// dice si no se ha podido cargar.
        /// </summary>
        public ObservableCollection<CapturaVistaItem> ImagenesNovedad { get; } = new ObservableCollection<CapturaVistaItem>();

        /// <summary>Pide las capturas una sola vez; si alguna falla se dice, sin romper nada.</summary>
        internal async Task CargarImagenNovedad()
        {
            if (!TieneImagenNovedad || _servicio == null || ImagenesNovedad.Count > 0)
            {
                return;
            }
            await CargaCapturas.Cargar(ImagenesNovedad, _novedad.Capturas, _novedad.TieneImagen,
                _servicio.LeerCaptura, () => _servicio.LeerImagenNovedad(Id));
        }

        /// <summary>Nesto#578: la sugerencia que se acaba de enviar enseña sus capturas sin volver a pedirlas.</summary>
        internal void MostrarCapturasEnviadas(IReadOnlyList<byte[]> enviadas)
        {
            if (ImagenesNovedad.Count == 0)
            {
                CargaCapturas.MostrarEnviadas(ImagenesNovedad, enviadas, _novedad.TieneImagen, _novedad.Capturas);
            }
        }

        private bool _destacada;
        /// <summary>NestoAPI#527: la que se eligió en el buscador (se resalta y se hace visible).</summary>
        public bool Destacada { get => _destacada; internal set => SetProperty(ref _destacada, value); }

        /// <summary>La API trae feedback (tablas creadas) y hay servicio con el que hablar.</summary>
        public bool TieneFeedback => _servicio != null && _novedad.VotosPositivos.HasValue;

        // ---- Votos ----
        private int _votosPositivos;
        public int VotosPositivos { get => _votosPositivos; private set => SetProperty(ref _votosPositivos, value); }

        private int _votosNegativos;
        public int VotosNegativos { get => _votosNegativos; private set => SetProperty(ref _votosNegativos, value); }

        private short? _miVoto;
        public short? MiVoto
        {
            get => _miVoto;
            private set
            {
                if (SetProperty(ref _miVoto, value))
                {
                    OnPropertyChanged(nameof(EsMeGusta));
                    OnPropertyChanged(nameof(EsNoMeGusta));
                }
            }
        }
        public bool EsMeGusta => MiVoto == 1;
        public bool EsNoMeGusta => MiVoto == -1;

        public IAsyncRelayCommand MeGustaCommand { get; }
        public IAsyncRelayCommand NoMeGustaCommand { get; }

        /// <summary>Pulsar el mismo voto lo quita; el otro lo cambia. Se pinta al momento y se deshace si la API falla.</summary>
        internal async Task Votar(short voto)
        {
            if (!TieneFeedback)
            {
                return;
            }
            short? anterior = MiVoto;
            short? nuevo = anterior == voto ? (short?)null : voto;
            if (EsPrivada && nuevo.HasValue)
            {
                // Nesto#532: la API no admite «Me gusta» en las privadas (quitar un voto antiguo, sí)
                Mensaje = TEXTO_PRIVADA_SIN_VOTOS;
                return;
            }
            AplicarVoto(anterior, nuevo);
            Mensaje = null;
            try
            {
                await _servicio.VotarNovedad(Id, nuevo ?? 0);
            }
            catch (Exception ex)
            {
                AplicarVoto(nuevo, anterior);
                Mensaje = ex.Message;
            }
        }

        private void AplicarVoto(short? desde, short? hasta)
        {
            if (desde == 1) VotosPositivos--;
            if (desde == -1) VotosNegativos--;
            if (hasta == 1) VotosPositivos++;
            if (hasta == -1) VotosNegativos++;
            MiVoto = hasta;
        }

        // ---- Comentarios ----
        private int _numeroComentarios;
        public int NumeroComentarios
        {
            get => _numeroComentarios;
            private set
            {
                if (SetProperty(ref _numeroComentarios, value))
                {
                    OnPropertyChanged(nameof(TextoComentarios));
                }
            }
        }
        public string TextoComentarios => $"Comentarios ({NumeroComentarios})";

        private bool _comentariosAbiertos;
        public bool ComentariosAbiertos { get => _comentariosAbiertos; private set => SetProperty(ref _comentariosAbiertos, value); }

        public ObservableCollection<ComentarioItem> Comentarios { get; } = new ObservableCollection<ComentarioItem>();

        private string _nuevoTexto;
        public string NuevoTexto
        {
            get => _nuevoTexto;
            set
            {
                if (SetProperty(ref _nuevoTexto, value))
                {
                    EnviarComentarioCommand.NotifyCanExecuteChanged();
                }
            }
        }

        /// <summary>Nesto#578 (sugerencia 900): las capturas pegadas en el cuadro del comentario (una o varias).</summary>
        public CapturasPegadas CapturasComentario { get; } = new CapturasPegadas();
        public bool TieneImagenAdjunta => CapturasComentario.HayCapturas;

        private bool _enviando;
        public bool Enviando
        {
            get => _enviando;
            private set
            {
                if (SetProperty(ref _enviando, value))
                {
                    EnviarComentarioCommand.NotifyCanExecuteChanged();
                }
            }
        }

        /// <summary>Aviso o error para el usuario, bajo la novedad. La ventana nunca se cierra por un fallo del feedback.</summary>
        private string _mensaje;
        public string Mensaje
        {
            get => _mensaje;
            set
            {
                if (SetProperty(ref _mensaje, value))
                {
                    OnPropertyChanged(nameof(HayMensaje));
                }
            }
        }
        public bool HayMensaje => !string.IsNullOrWhiteSpace(Mensaje);

        /// <summary>Nesto#491: el desplegable de @menciones del cuadro de comentario.</summary>
        public AutocompletadoMenciones MencionesComentario { get; }

        public IAsyncRelayCommand AbrirComentariosCommand { get; }
        public IAsyncRelayCommand EnviarComentarioCommand { get; }
        public IRelayCommand PegarImagenCommand { get; }
        public IAsyncRelayCommand<ComentarioItem> BorrarComentarioCommand { get; }

        internal async Task AbrirOCerrarComentarios()
        {
            if (!TieneFeedback)
            {
                return;
            }
            if (ComentariosAbiertos)
            {
                ComentariosAbiertos = false;
                return;
            }
            ComentariosAbiertos = true;
            OfrecerImagenDelPortapapeles();
            await CargarComentarios();
        }

        /// <summary>Al abrir el cuadro, UNA vez por apertura: si hay una imagen copiada, se ofrece adjuntarla.</summary>
        private void OfrecerImagenDelPortapapeles()
        {
            if (CapturasComentario.HayCapturas || _portapapeles == null || !_portapapeles.HayImagen())
            {
                return;
            }
            if (_preguntar("Tiene una imagen copiada en el portapapeles. ¿Quiere adjuntarla a su comentario?"))
            {
                PegarImagen();
            }
        }

        /// <summary>
        /// Añade la imagen del portapapeles a las capturas del comentario (Nesto#578: cada Ctrl+V, una más). Devuelve
        /// true si había imagen (se haya adjuntado o no por tamaño): así Ctrl+V en el cuadro solo se «come» la tecla
        /// cuando lo copiado es una imagen.
        /// </summary>
        internal bool PegarImagen()
        {
            if (!CapturaPortapapeles.Leer(_portapapeles, out byte[] png, out string aviso))
            {
                return false;
            }
            Mensaje = png == null ? aviso : CapturasComentario.Anadir(png);
            return true;
        }

        private ComentarioItem _comentarioDestacado;
        /// <summary>Nesto#477: el comentario al que lleva la notificación (se resalta y la vista lo hace visible).</summary>
        public ComentarioItem ComentarioDestacado { get => _comentarioDestacado; private set => SetProperty(ref _comentarioDestacado, value); }

        /// <summary>
        /// Nesto#477: abre los comentarios (sin plegarlos si ya estaban abiertos ni ofrecer el portapapeles:
        /// se viene a leer) y resalta <paramref name="comentarioADestacar"/> en cuanto llega la lista.
        /// </summary>
        internal async Task AbrirComentarios(int? comentarioADestacar)
        {
            if (!TieneFeedback)
            {
                return;
            }
            ComentariosAbiertos = true;
            await CargarComentarios(comentarioADestacar);
        }

        private async Task CargarComentarios(int? comentarioADestacar = null)
        {
            try
            {
                var lista = await _servicio.LeerComentarios(Id);
                Comentarios.Clear();
                foreach (ComentarioNovedad c in lista)
                {
                    Comentarios.Add(new ComentarioItem(c));
                }
                NumeroComentarios = Comentarios.Count;
                if (comentarioADestacar.HasValue)
                {
                    ComentarioItem destacado = null;
                    foreach (ComentarioItem c in Comentarios)
                    {
                        if (c.Id == comentarioADestacar.Value)
                        {
                            destacado = c;
                            c.Destacado = true;
                        }
                    }
                    ComentarioDestacado = destacado;
                }
                // Las miniaturas se piden al desplegar, no antes (no cargar imágenes que nadie mira).
                foreach (ComentarioItem c in Comentarios.ToList())
                {
                    int id = c.Id;
                    await CargaCapturas.Cargar(c.Imagenes, c.Capturas, c.TieneImagen,
                        _servicio.LeerCaptura, () => _servicio.LeerImagenComentario(id));
                }
            }
            catch (Exception ex)
            {
                Mensaje = ex.Message;
            }
        }

        internal async Task EnviarComentario()
        {
            if (!TieneFeedback || string.IsNullOrWhiteSpace(NuevoTexto))
            {
                return;
            }
            if (CapturasComentario.AlgunaDemasiadoGrande)
            {
                Mensaje = "Alguna captura supera los 2 MB: quítela o haga un recorte más pequeño.";
                return;
            }
            Enviando = true;
            Mensaje = null;
            try
            {
                IReadOnlyList<byte[]> capturas = CapturasComentario.ParaEnviar();
                ComentarioNovedad creado = await _servicio.Comentar(Id, NuevoTexto.Trim(), capturas);
                var item = new ComentarioItem(creado);
                CargaCapturas.MostrarEnviadas(item.Imagenes, capturas, creado.TieneImagen, creado.Capturas);
                Comentarios.Add(item);
                NumeroComentarios++;
                NuevoTexto = null;
                CapturasComentario.Vaciar();
            }
            catch (Exception ex)
            {
                Mensaje = ex.Message;
            }
            finally
            {
                Enviando = false;
            }
        }

        internal async Task BorrarComentario(ComentarioItem comentario)
        {
            if (comentario == null || !comentario.EsMio)
            {
                return;
            }
            if (!_preguntar("¿Borrar tu comentario?"))
            {
                return;
            }
            try
            {
                await _servicio.BorrarComentario(comentario.Id);
                Comentarios.Remove(comentario);
                NumeroComentarios = Math.Max(0, NumeroComentarios - 1);
            }
            catch (Exception ex)
            {
                Mensaje = ex.Message;
            }
        }
    }

    /// <summary>NestoAPI#520: un comentario tal cual se pinta (la miniatura llega aparte).</summary>
    public class ComentarioItem : ObservableObject
    {
        public ComentarioItem(ComentarioNovedad c)
        {
            Id = c.Id;
            NombreVisible = c.NombreVisible;
            Fecha = c.Fecha;
            Texto = c.Texto;
            TieneImagen = c.TieneImagen;
            EsMio = c.EsMio;
            Capturas = c.Capturas;
        }

        public int Id { get; }
        public string NombreVisible { get; }
        public DateTime Fecha { get; }
        public string Texto { get; }
        public bool TieneImagen { get; }
        public bool EsMio { get; }
        public string Cabecera => $"{NombreVisible} · {Fecha:dd/MM/yyyy HH:mm}";

        /// <summary>Nesto#578: lo que dice la API de sus capturas (null = imagen única de antes, si TieneImagen).</summary>
        internal List<CapturaNovedad> Capturas { get; }

        /// <summary>Nesto#578: sus capturas (una o varias), según llegan.</summary>
        public ObservableCollection<CapturaVistaItem> Imagenes { get; } = new ObservableCollection<CapturaVistaItem>();

        private bool _destacado;
        /// <summary>Nesto#477: el comentario al que lleva la notificación de la campana.</summary>
        public bool Destacado { get => _destacado; internal set => SetProperty(ref _destacado, value); }
    }

    /// <summary>NestoAPI#520: bytes (PNG/JPEG) → imagen para la miniatura. Null o bytes rotos → sin imagen.</summary>
    public class BytesAImagenConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is byte[] bytes) || bytes.Length == 0)
            {
                return null;
            }
            try
            {
                var imagen = new BitmapImage();
                using (var ms = new MemoryStream(bytes))
                {
                    imagen.BeginInit();
                    imagen.CacheOption = BitmapCacheOption.OnLoad;
                    imagen.StreamSource = ms;
                    imagen.EndInit();
                }
                imagen.Freeze();
                return imagen;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
