using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nesto.Infrastructure.Contracts;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ControlesUsuario.Dialogs
{
    /// <summary>
    /// NestoAPI#711: reproduce un vídeo que ya está en un fichero local, dentro de Nesto, y no vuelve hasta que el
    /// usuario lo cierra. Interfaz para que el ViewModel no conozca la ventana (y para probarlo sin ella).
    /// </summary>
    public interface IReproductorVideoNovedad
    {
        void Reproducir(string rutaFichero, string titulo);
    }

    /// <summary>
    /// NestoAPI#711: lo que necesitan las novedades para enseñar su vídeo. Una instancia por ventana, compartida por
    /// todas las novedades.
    /// </summary>
    public class ContextoVideosNovedades
    {
        public ContextoVideosNovedades(IServicioVideosNovedades servicio, IReproductorVideoNovedad reproductor, string carpetaTemporal = null)
        {
            Servicio = servicio;
            Reproductor = reproductor;
            CarpetaTemporal = string.IsNullOrWhiteSpace(carpetaTemporal) ? CarpetaPorDefecto : carpetaTemporal;
        }

        public IServicioVideosNovedades Servicio { get; }
        public IReproductorVideoNovedad Reproductor { get; }
        public string CarpetaTemporal { get; }

        public static string CarpetaPorDefecto => Path.Combine(Path.GetTempPath(), "Nesto", "Vídeos de novedades");

        /// <summary>El de la aplicación: la ventana con el MediaElement.</summary>
        public static ContextoVideosNovedades Crear(IServicioVideosNovedades servicio)
            => servicio == null ? null : new ContextoVideosNovedades(servicio, new ReproductorVideoNovedadWpf());

        /// <summary>Guarda el vídeo con un nombre que no pisa a otro (dos ventanas pueden ver el mismo a la vez).</summary>
        internal string GuardarTemporal(int novedadId, byte[] contenido)
        {
            Directory.CreateDirectory(CarpetaTemporal);
            LimpiarAntiguos();
            string ruta = Path.Combine(CarpetaTemporal, $"novedad-{novedadId}-{Guid.NewGuid():N}.mp4");
            File.WriteAllBytes(ruta, contenido ?? new byte[0]);
            return ruta;
        }

        /// <summary>Al cerrar el reproductor. Si el fichero sigue cogido, se queda y lo borra la siguiente vez <see cref="LimpiarAntiguos"/>.</summary>
        internal static void BorrarTemporal(string ruta)
        {
            if (string.IsNullOrEmpty(ruta))
            {
                return;
            }
            try
            {
                File.Delete(ruta);
            }
            catch (Exception)
            {
                // Lo recoge LimpiarAntiguos
            }
        }

        /// <summary>Los que se quedaron de otras veces (Nesto cerrado con el vídeo abierto, fichero cogido…).</summary>
        private void LimpiarAntiguos()
        {
            try
            {
                foreach (string viejo in Directory.EnumerateFiles(CarpetaTemporal, "novedad-*.mp4")
                    .Where(f => File.GetLastWriteTime(f) < DateTime.Now.AddDays(-1)))
                {
                    BorrarTemporal(viejo);
                }
            }
            catch (Exception)
            {
                // Limpiar es opcional
            }
        }
    }

    /// <summary>
    /// NestoAPI#711: el botón «▶ Ver vídeo» de una novedad. NO descarga nada hasta que se pulsa: entonces lo baja por la
    /// API (con el token), lo deja en un fichero temporal, apunta la reproducción, lo reproduce dentro de Nesto y, al
    /// cerrar, borra el fichero.
    /// </summary>
    public class VideoNovedadItem : ObservableObject
    {
        internal const string TEXTO_BOTON = "▶ Ver vídeo";
        internal const string TEXTO_DESCARGANDO = "Descargando el vídeo…";

        private readonly int _novedadId;
        private readonly string _titulo;
        private readonly ContextoVideosNovedades _contexto;

        public VideoNovedadItem(int novedadId, string titulo, long? tamano, ContextoVideosNovedades contexto)
        {
            _novedadId = novedadId;
            _titulo = string.IsNullOrWhiteSpace(titulo) ? "Novedad" : titulo.Trim();
            Tamano = tamano;
            _contexto = contexto ?? throw new ArgumentNullException(nameof(contexto));
            VerCommand = new AsyncRelayCommand(Ver, () => PuedeVer && !Descargando);
        }

        public long? Tamano { get; }

        public bool PuedeVer => _contexto.Servicio != null && _contexto.Reproductor != null;

        public IAsyncRelayCommand VerCommand { get; }

        public string TextoBoton => Descargando ? TEXTO_DESCARGANDO : TEXTO_BOTON;

        public string TextoAyuda => Tamano > 0
            ? $"Ver el vídeo de esta novedad dentro de Nesto ({AdjuntoNovedadItem.FormatearTamano(Tamano.Value)})"
            : "Ver el vídeo de esta novedad dentro de Nesto";

        private bool _descargando;
        public bool Descargando
        {
            get => _descargando;
            private set
            {
                if (SetProperty(ref _descargando, value))
                {
                    OnPropertyChanged(nameof(TextoBoton));
                    VerCommand.NotifyCanExecuteChanged();
                }
            }
        }

        private string _mensaje;
        /// <summary>Lo que ha ido mal al descargarlo o reproducirlo (null si nada).</summary>
        public string Mensaje
        {
            get => _mensaje;
            private set
            {
                if (SetProperty(ref _mensaje, value))
                {
                    OnPropertyChanged(nameof(HayMensaje));
                }
            }
        }

        public bool HayMensaje => !string.IsNullOrWhiteSpace(Mensaje);

        internal async Task Ver()
        {
            if (!PuedeVer || Descargando)
            {
                return;
            }
            Mensaje = null;
            string ruta;
            Descargando = true;
            try
            {
                byte[] contenido = await _contexto.Servicio.Descargar(_novedadId);
                if (contenido == null || contenido.Length == 0)
                {
                    Mensaje = "El vídeo no está disponible en este momento.";
                    return;
                }
                ruta = _contexto.GuardarTemporal(_novedadId, contenido);
            }
            catch (Exception ex)
            {
                Mensaje = $"No se pudo descargar el vídeo: {ex.Message}";
                return;
            }
            finally
            {
                Descargando = false;
            }

            _ = RegistrarReproduccionSinRomper();
            try
            {
                _contexto.Reproductor.Reproducir(ruta, _titulo);
            }
            catch (Exception ex)
            {
                Mensaje = $"No se pudo reproducir el vídeo: {ex.Message}";
            }
            finally
            {
                ContextoVideosNovedades.BorrarTemporal(ruta);
            }
        }

        private async Task RegistrarReproduccionSinRomper()
        {
            try
            {
                await _contexto.Servicio.RegistrarReproduccion(_novedadId);
            }
            catch (Exception)
            {
                // Contar nunca estorba al que lo está viendo
            }
        }
    }
}
