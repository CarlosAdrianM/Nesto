using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ControlesUsuario.Dialogs
{
    /// <summary>
    /// Nesto#519 (NestoAPI#616): lo que necesitan las novedades para enseñar, abrir, subir y borrar adjuntos.
    /// Una instancia por ventana, compartida por todas las novedades.
    /// </summary>
    public class ContextoAdjuntosNovedades
    {
        internal const string TITULO_DIALOGO = "Novedades";

        public ContextoAdjuntosNovedades(IServicioAdjuntosNovedades servicio, bool puedeGestionar,
            Func<IList<string>> elegirFicheros, Action<string> abrirFichero, Func<string, bool> confirmar, string carpetaTemporal)
        {
            Servicio = servicio;
            PuedeGestionar = puedeGestionar;
            ElegirFicheros = elegirFicheros ?? (() => new List<string>());
            AbrirFichero = abrirFichero ?? (_ => { });
            Confirmar = confirmar ?? (_ => false);
            CarpetaTemporal = string.IsNullOrWhiteSpace(carpetaTemporal) ? CarpetaTemporalAdjuntos.CarpetaPorDefecto : carpetaTemporal;
        }

        public IServicioAdjuntosNovedades Servicio { get; }
        /// <summary>Dirección o Informática: pueden adjuntar y borrar (la API lo vuelve a comprobar).</summary>
        public bool PuedeGestionar { get; }
        /// <summary>Las rutas que elige el usuario (vacía si cancela).</summary>
        public Func<IList<string>> ElegirFicheros { get; }
        /// <summary>Abre un fichero con el visor del sistema.</summary>
        public Action<string> AbrirFichero { get; }
        public Func<string, bool> Confirmar { get; }
        public string CarpetaTemporal { get; }

        /// <summary>El de la aplicación: OpenFileDialog, visor del sistema y confirmación por el servicio de diálogos.</summary>
        public static ContextoAdjuntosNovedades Crear(IServicioAdjuntosNovedades servicio, IConfiguracion configuracion, IServicioDialogos dialogos)
        {
            return new ContextoAdjuntosNovedades(servicio, EsDireccionOInformatica(configuracion), ElegirFicherosWpf, AbrirConVisorDelSistema,
                pregunta => dialogos != null && dialogos.ShowConfirmationAnswer(TITULO_DIALOGO, pregunta), null);
        }

        /// <summary>Mismo criterio que la API (NestoAPI#616): solo Dirección e Informática gestionan adjuntos.</summary>
        internal static bool EsDireccionOInformatica(IConfiguracion configuracion)
        {
            if (configuracion == null)
            {
                return false;
            }
            try
            {
                return configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.DIRECCION)
                    || configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.INFORMATICA);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static IList<string> ElegirFicherosWpf()
        {
            var dialogo = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Adjuntar a la novedad",
                Multiselect = true,
                Filter = "PDF e imágenes|*.pdf;*.png;*.jpg;*.jpeg;*.gif;*.webp|PDF|*.pdf|Imágenes|*.png;*.jpg;*.jpeg;*.gif;*.webp"
            };
            return dialogo.ShowDialog() == true ? dialogo.FileNames : new string[0];
        }

        private static void AbrirConVisorDelSistema(string ruta)
            => _ = Process.Start(new ProcessStartInfo(ruta) { UseShellExecute = true });
    }

    /// <summary>Nesto#519: dónde se dejan los adjuntos descargados para abrirlos (nombre original, sin pisar otros).</summary>
    internal static class CarpetaTemporalAdjuntos
    {
        public static string CarpetaPorDefecto => Path.Combine(Path.GetTempPath(), "Nesto", "Adjuntos de novedades");

        /// <summary>Guarda <paramref name="contenido"/> con el nombre original; si ya existe, «Nombre (2).pdf», «Nombre (3).pdf»…</summary>
        public static string Guardar(string carpeta, string nombre, byte[] contenido)
        {
            Directory.CreateDirectory(carpeta);
            string limpio = NombreSeguro(nombre);
            string baseNombre = Path.GetFileNameWithoutExtension(limpio);
            string extension = Path.GetExtension(limpio);
            string ruta = Path.Combine(carpeta, limpio);
            for (int i = 2; File.Exists(ruta); i++)
            {
                ruta = Path.Combine(carpeta, $"{baseNombre} ({i}){extension}");
            }
            File.WriteAllBytes(ruta, contenido ?? new byte[0]);
            return ruta;
        }

        internal static string NombreSeguro(string nombre)
        {
            string soloNombre = Path.GetFileName((nombre ?? string.Empty).Replace('/', '\\'));
            char[] invalidos = Path.GetInvalidFileNameChars();
            string limpio = new string(soloNombre.Select(c => invalidos.Contains(c) ? '_' : c).ToArray()).Trim();
            return string.IsNullOrWhiteSpace(Path.GetFileNameWithoutExtension(limpio)) ? "adjunto" + limpio : limpio;
        }
    }

    /// <summary>
    /// Nesto#519 (NestoAPI#616): un adjunto de una novedad, como chip: icono por tipo, nombre y tamaño. Abrir lo
    /// descarga UNA vez a la carpeta temporal y lo abre con el visor del sistema; borrar pide confirmación.
    /// </summary>
    public class AdjuntoNovedadItem : ObservableObject
    {
        internal const string ICONO_PDF = "📄";
        internal const string ICONO_IMAGEN = "🖼";
        internal const string ICONO_OTRO = "📎";

        private static readonly CultureInfo Es = new CultureInfo("es-ES");

        private readonly AdjuntoNovedad _adjunto;
        private readonly ContextoAdjuntosNovedades _contexto;
        private readonly Action<AdjuntoNovedadItem> _alBorrar;
        private readonly Action<string> _avisar;
        private string _rutaTemporal;

        public AdjuntoNovedadItem(AdjuntoNovedad adjunto, ContextoAdjuntosNovedades contexto,
            Action<AdjuntoNovedadItem> alBorrar = null, Action<string> avisar = null)
        {
            _adjunto = adjunto ?? throw new ArgumentNullException(nameof(adjunto));
            _contexto = contexto ?? throw new ArgumentNullException(nameof(contexto));
            _alBorrar = alBorrar ?? (_ => { });
            _avisar = avisar ?? (_ => { });
            AbrirCommand = new AsyncRelayCommand(Abrir);
            BorrarCommand = new AsyncRelayCommand(Borrar, () => PuedeBorrar);
        }

        public int Id => _adjunto.Id;
        public string Nombre => string.IsNullOrWhiteSpace(_adjunto.Nombre) ? "adjunto" : _adjunto.Nombre.Trim();
        public string Tipo => _adjunto.Tipo;
        public long Tamano => _adjunto.Tamano;
        public string TamanoLegible => FormatearTamano(Tamano);
        /// <summary>«Normas cupones.pdf · 240 KB».</summary>
        public string Texto => $"{Nombre} · {TamanoLegible}";

        public bool EsPdf => string.Equals(TipoEfectivo, "application/pdf", StringComparison.OrdinalIgnoreCase);
        public bool EsImagen => (TipoEfectivo ?? string.Empty).StartsWith("image/", StringComparison.OrdinalIgnoreCase);
        public string Icono => EsPdf ? ICONO_PDF : EsImagen ? ICONO_IMAGEN : ICONO_OTRO;

        /// <summary>Si la API no manda el tipo, se deduce de la extensión del nombre.</summary>
        private string TipoEfectivo => string.IsNullOrWhiteSpace(Tipo) ? ServicioAdjuntosNovedades.TipoDeFichero(Nombre) : Tipo.Trim();

        public bool PuedeBorrar => _contexto.PuedeGestionar && _contexto.Servicio != null;

        private bool _descargando;
        public bool Descargando
        {
            get => _descargando;
            private set
            {
                if (SetProperty(ref _descargando, value))
                {
                    OnPropertyChanged(nameof(TextoAyuda));
                }
            }
        }

        public string TextoAyuda => Descargando ? $"Descargando {Nombre}…" : $"Abrir {Nombre} ({TamanoLegible})";

        public IAsyncRelayCommand AbrirCommand { get; }
        public IAsyncRelayCommand BorrarCommand { get; }

        /// <summary>Descarga a la carpeta temporal (solo la primera vez, mientras el fichero siga ahí) y lo abre.</summary>
        internal async Task Abrir()
        {
            if (_contexto.Servicio == null)
            {
                return;
            }
            try
            {
                if (_rutaTemporal == null || !File.Exists(_rutaTemporal))
                {
                    Descargando = true;
                    byte[] contenido = await _contexto.Servicio.Descargar(Id);
                    _rutaTemporal = CarpetaTemporalAdjuntos.Guardar(_contexto.CarpetaTemporal, Nombre, contenido);
                }
                _contexto.AbrirFichero(_rutaTemporal);
            }
            catch (Exception ex)
            {
                _avisar($"No se pudo abrir «{Nombre}»: {ex.Message}");
            }
            finally
            {
                Descargando = false;
            }
        }

        internal async Task Borrar()
        {
            if (!PuedeBorrar || !_contexto.Confirmar($"¿Borrar el adjunto «{Nombre}»? Dejará de verse en la novedad para todos."))
            {
                return;
            }
            try
            {
                await _contexto.Servicio.Borrar(Id);
                _alBorrar(this);
            }
            catch (Exception ex)
            {
                _avisar(ex.Message);
            }
        }

        /// <summary>«512 bytes», «240 KB», «1,5 MB».</summary>
        internal static string FormatearTamano(long bytes)
        {
            if (bytes < 1024)
            {
                return bytes == 1 ? "1 byte" : $"{Math.Max(0, bytes)} bytes";
            }
            long kb = (long)Math.Round(bytes / 1024d, MidpointRounding.AwayFromZero);
            if (kb < 1024)
            {
                return $"{kb} KB";
            }
            return (bytes / (1024d * 1024d)).ToString("0.#", Es) + " MB";
        }
    }
}
