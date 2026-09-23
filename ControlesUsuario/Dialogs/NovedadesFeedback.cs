using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nesto.Infrastructure.Contracts;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
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
    /// NestoAPI#520: una novedad de la ventana, con su feedback (votos y comentarios). Expone los mismos
    /// datos que <see cref="NovedadUsuario"/> para la plantilla. Si la API no trae los contadores (tablas de
    /// feedback aún no creadas) <see cref="TieneFeedback"/> es false y la ventana se ve como siempre.
    /// </summary>
    public class NovedadItem : ObservableObject
    {
        /// <summary>Mismo límite que la API (2 MB tras decodificar): se avisa ANTES de enviar.</summary>
        public const int TAMANO_MAXIMO_IMAGEN = 2 * 1024 * 1024;

        private readonly NovedadUsuario _novedad;
        private readonly INovedadesService _servicio;
        private readonly IPortapapelesImagenes _portapapeles;
        private readonly Func<string, bool> _preguntar;

        public NovedadItem(NovedadUsuario novedad, INovedadesService servicio, IPortapapelesImagenes portapapeles, Func<string, bool> preguntar)
        {
            _novedad = novedad ?? throw new ArgumentNullException(nameof(novedad));
            _servicio = servicio;
            _portapapeles = portapapeles;
            _preguntar = preguntar ?? (_ => false);
            _votosPositivos = novedad.VotosPositivos ?? 0;
            _votosNegativos = novedad.VotosNegativos ?? 0;
            _miVoto = novedad.MiVoto;
            _numeroComentarios = novedad.NumeroComentarios ?? 0;

            MeGustaCommand = new AsyncRelayCommand(() => Votar(1));
            NoMeGustaCommand = new AsyncRelayCommand(() => Votar(-1));
            AbrirComentariosCommand = new AsyncRelayCommand(AbrirOCerrarComentarios);
            EnviarComentarioCommand = new AsyncRelayCommand(EnviarComentario, () => !string.IsNullOrWhiteSpace(NuevoTexto) && !Enviando);
            PegarImagenCommand = new RelayCommand(() => PegarImagen());
            QuitarImagenCommand = new RelayCommand(() => ImagenAdjunta = null, () => ImagenAdjunta != null);
            BorrarComentarioCommand = new AsyncRelayCommand<ComentarioItem>(BorrarComentario);
        }

        // ---- Datos de la novedad (los que ya pintaba la ventana) ----
        public int Id => _novedad.Id;
        public string Version => _novedad.Version;
        public DateTime Fecha => _novedad.Fecha;
        public string Categoria => _novedad.Categoria;
        public string Titulo => _novedad.Titulo;
        public string Descripcion => _novedad.Descripcion;
        public string Ambito => _novedad.Ambito;

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

        private byte[] _imagenAdjunta;
        public byte[] ImagenAdjunta
        {
            get => _imagenAdjunta;
            set
            {
                if (SetProperty(ref _imagenAdjunta, value))
                {
                    OnPropertyChanged(nameof(TieneImagenAdjunta));
                    QuitarImagenCommand.NotifyCanExecuteChanged();
                }
            }
        }
        public bool TieneImagenAdjunta => ImagenAdjunta != null;

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

        public IAsyncRelayCommand AbrirComentariosCommand { get; }
        public IAsyncRelayCommand EnviarComentarioCommand { get; }
        public IRelayCommand PegarImagenCommand { get; }
        public IRelayCommand QuitarImagenCommand { get; }
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
            if (ImagenAdjunta != null || _portapapeles == null || !_portapapeles.HayImagen())
            {
                return;
            }
            if (_preguntar("Tienes una imagen copiada en el portapapeles. ¿Quieres adjuntarla a tu comentario?"))
            {
                PegarImagen();
            }
        }

        /// <summary>
        /// Adjunta la imagen del portapapeles. Devuelve true si había imagen (se haya adjuntado o no por
        /// tamaño): así Ctrl+V en el cuadro solo se «come» la tecla cuando lo copiado es una imagen.
        /// </summary>
        internal bool PegarImagen()
        {
            if (_portapapeles == null || !_portapapeles.HayImagen())
            {
                return false;
            }
            byte[] png = _portapapeles.LeerImagenPng();
            if (png == null || png.Length == 0)
            {
                return false;
            }
            if (png.Length > TAMANO_MAXIMO_IMAGEN)
            {
                Mensaje = $"La imagen copiada ocupa {png.Length / 1024.0 / 1024.0:0.0} MB y el máximo son 2 MB: haz un recorte más pequeño.";
                return true;
            }
            ImagenAdjunta = png;
            Mensaje = null;
            return true;
        }

        private async Task CargarComentarios()
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
                // Las miniaturas se piden al desplegar, no antes (no cargar imágenes que nadie mira).
                foreach (ComentarioItem c in Comentarios)
                {
                    if (!c.TieneImagen)
                    {
                        continue;
                    }
                    try
                    {
                        c.Imagen = await _servicio.LeerImagenComentario(c.Id);
                    }
                    catch (Exception)
                    {
                        c.ImagenNoDisponible = true;
                    }
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
            if (ImagenAdjunta != null && ImagenAdjunta.Length > TAMANO_MAXIMO_IMAGEN)
            {
                Mensaje = "La imagen supera los 2 MB: quítala o haz un recorte más pequeño.";
                return;
            }
            Enviando = true;
            Mensaje = null;
            try
            {
                ComentarioNovedad creado = await _servicio.Comentar(Id, NuevoTexto.Trim(), ImagenAdjunta);
                var item = new ComentarioItem(creado) { Imagen = creado.TieneImagen ? ImagenAdjunta : null };
                Comentarios.Add(item);
                NumeroComentarios++;
                NuevoTexto = null;
                ImagenAdjunta = null;
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
        }

        public int Id { get; }
        public string NombreVisible { get; }
        public DateTime Fecha { get; }
        public string Texto { get; }
        public bool TieneImagen { get; }
        public bool EsMio { get; }
        public string Cabecera => $"{NombreVisible} · {Fecha:dd/MM/yyyy HH:mm}";

        private byte[] _imagen;
        public byte[] Imagen { get => _imagen; set => SetProperty(ref _imagen, value); }

        private bool _imagenNoDisponible;
        public bool ImagenNoDisponible { get => _imagenNoDisponible; set => SetProperty(ref _imagenNoDisponible, value); }
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
