using CommunityToolkit.Mvvm.Input;
using Nesto.Infrastructure.Contracts;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace ControlesUsuario.Dialogs
{
    /// <summary>
    /// Nesto#571 (NestoAPI#739): la prueba de entrega de un envío. Desde el 10/11/26 de cualquier agencia que la dé (reparto propio,
    /// CTT, GLS), por GET api/EnviosAgencias/{id}/PruebaEntrega: quién lo recibió, su DNI, cuándo y el documento (la firma o el
    /// albarán firmado en imagen, el justificante en PDF o un enlace de la agencia). En el reparto propio, además, cómo se cobró el
    /// reembolso y quién registró la entrega. Solo lectura. La imagen llega en bytes (<see cref="Firma"/>) y la vista la pinta; el
    /// PDF y el enlace se abren fuera con <see cref="AbrirDocumentoCommand"/>: el ViewModel no conoce tipos de WPF.
    /// </summary>
    public class PruebaEntregaDialogViewModel : DialogoNestoBase
    {
        public const string NOMBRE = "PruebaEntregaDialog";
        public const string PARAMETRO_ENVIO = "envio";
        public const string PARAMETRO_PEDIDO = "pedido";
        public const string PARAMETRO_AGENCIA = "agencia";
        public const string PARAMETRO_FECHA_HORA = "fechaHora";
        public const string PARAMETRO_RECEPTOR = "receptor";
        public const string PARAMETRO_DNI = "dni";
        public const string PARAMETRO_COBRO_REEMBOLSO = "cobroReembolso";
        public const string PARAMETRO_IMPORTE_COBRADO = "importeCobrado";
        public const string PARAMETRO_USUARIO = "usuario";
        public const string PARAMETRO_TIENE_FIRMA = "tieneFirma";
        /// <summary>La imagen que se pinta (firma del reparto propio o albarán firmado de GLS).</summary>
        public const string PARAMETRO_FIRMA = "firma";
        /// <summary>El documento tal cual (PDF o imagen) con su tipo, para abrirlo fuera.</summary>
        public const string PARAMETRO_DOCUMENTO = "documento";
        public const string PARAMETRO_TIPO_DOCUMENTO = "tipoDocumento";
        public const string PARAMETRO_URL_DOCUMENTO = "urlDocumento";
        /// <summary>La nota de la API (p. ej. «Sin firma (se entregó con DNI).»).</summary>
        public const string PARAMETRO_AVISO = "aviso";

        public const string COBRO_EFECTIVO = "Efectivo";
        public const string COBRO_TARJETA = "Tarjeta";
        public const string COBRO_NO_COBRADO = "NoCobrado";

        private static readonly CultureInfo Espanol = CultureInfo.GetCultureInfo("es-ES");

        private string _cabecera;
        private string _agencia;
        private string _receptor;
        private string _dni;
        private string _fechaHora;
        private string _cobro;
        private string _usuario;
        private byte[] _firma;
        private string _textoSinFirma;
        private bool _hayDatosReparto;
        private int _envio;
        private byte[] _documento;
        private string _tipoDocumento;
        private string _urlDocumento;
        private string _textoAbrirDocumento;

        public PruebaEntregaDialogViewModel()
        {
            Title = "Prueba de entrega";
            CerrarCommand = new RelayCommand(() => RaiseRequestClose(ResultadoBoton.OK));
            AbrirDocumentoCommand = new RelayCommand(AbrirDocumento, () => HayDocumentoParaAbrir);
        }

        public string Cabecera { get => _cabecera; set => SetProperty(ref _cabecera, value); }
        public string Agencia { get => _agencia; set => SetProperty(ref _agencia, value); }
        public string Receptor { get => _receptor; set => SetProperty(ref _receptor, value); }
        public string Dni { get => _dni; set => SetProperty(ref _dni, value); }
        public string FechaHora { get => _fechaHora; set => SetProperty(ref _fechaHora, value); }
        public string Cobro { get => _cobro; set => SetProperty(ref _cobro, value); }
        public string Usuario { get => _usuario; set => SetProperty(ref _usuario, value); }

        /// <summary>El reembolso y quién lo registró: solo los da el reparto propio.</summary>
        public bool HayDatosReparto { get => _hayDatosReparto; set => SetProperty(ref _hayDatosReparto, value); }

        /// <summary>La imagen que se pinta (firma o albarán firmado); null si no hay (o no se ha podido leer).</summary>
        public byte[] Firma
        {
            get => _firma;
            set
            {
                if (SetProperty(ref _firma, value))
                {
                    OnPropertyChanged(nameof(HayFirma));
                }
            }
        }

        public bool HayFirma => Firma != null && Firma.Length > 0;

        /// <summary>Lo que se ve en el hueco de la imagen cuando no la hay.</summary>
        public string TextoSinFirma { get => _textoSinFirma; set => SetProperty(ref _textoSinFirma, value); }

        /// <summary>Hay un PDF, una imagen o un enlace de la agencia que se puede abrir fuera de Nesto.</summary>
        public bool HayDocumentoParaAbrir => (_documento != null && _documento.Length > 0) || !string.IsNullOrWhiteSpace(_urlDocumento);

        public string TextoAbrirDocumento { get => _textoAbrirDocumento; set => SetProperty(ref _textoAbrirDocumento, value); }

        public RelayCommand CerrarCommand { get; }
        public RelayCommand AbrirDocumentoCommand { get; }

        /// <summary>Abre un fichero o una URL con el programa del sistema (sustituible en los tests).</summary>
        public Action<string> AbrirConElSistema { get; set; } = ruta => _ = Process.Start(new ProcessStartInfo(ruta) { UseShellExecute = true });

        /// <summary>Dónde se deja el documento para abrirlo (sustituible en los tests).</summary>
        public Func<string, byte[], string> GuardarTemporal { get; set; } = GuardarEnTemporal;

        public override void OnDialogOpened(ParametrosDialogo parameters)
        {
            _envio = parameters.GetValue<int>(PARAMETRO_ENVIO);
            string pedido = parameters.GetValue<string>(PARAMETRO_PEDIDO);
            Cabecera = string.IsNullOrWhiteSpace(pedido) ? $"Envío {_envio}" : $"Envío {_envio} (pedido {pedido.Trim()})";
            Title = $"Prueba de entrega del envío {_envio}";
            Agencia = TextoOGuion(parameters.GetValue<string>(PARAMETRO_AGENCIA));
            Receptor = TextoOGuion(parameters.GetValue<string>(PARAMETRO_RECEPTOR));
            Dni = TextoOGuion(parameters.GetValue<string>(PARAMETRO_DNI));
            DateTime fecha = parameters.GetValue<DateTime>(PARAMETRO_FECHA_HORA);
            FechaHora = fecha == default ? "-" : fecha.ToString("dd/MM/yyyy 'a las' HH:mm", Espanol);
            string cobro = parameters.GetValue<string>(PARAMETRO_COBRO_REEMBOLSO);
            decimal? importe = parameters.GetValue<decimal?>(PARAMETRO_IMPORTE_COBRADO);
            string usuario = parameters.GetValue<string>(PARAMETRO_USUARIO);
            Cobro = TextoCobro(cobro, importe);
            Usuario = TextoOGuion(usuario);
            HayDatosReparto = !string.IsNullOrWhiteSpace(cobro) || !string.IsNullOrWhiteSpace(usuario);
            Firma = parameters.GetValue<byte[]>(PARAMETRO_FIRMA);

            _documento = parameters.GetValue<byte[]>(PARAMETRO_DOCUMENTO) ?? Firma;
            _tipoDocumento = parameters.GetValue<string>(PARAMETRO_TIPO_DOCUMENTO) ?? (_documento != null ? "image/png" : null);
            _urlDocumento = parameters.GetValue<string>(PARAMETRO_URL_DOCUMENTO);
            OnPropertyChanged(nameof(HayDocumentoParaAbrir));
            AbrirDocumentoCommand.NotifyCanExecuteChanged();
            bool esPdf = _documento != null && _documento.Length > 0 && !EsImagen(_tipoDocumento);
            TextoAbrirDocumento = esPdf ? "Abrir el justificante (PDF)"
                : !HayFirma && !string.IsNullOrWhiteSpace(_urlDocumento) ? "Abrir en el navegador"
                : "Abrir la imagen";

            string aviso = parameters.GetValue<string>(PARAMETRO_AVISO);
            if (esPdf)
            {
                TextoSinFirma = "El justificante de entrega es un PDF: ábralo con el botón de abajo.";
            }
            else if (!HayFirma && !string.IsNullOrWhiteSpace(_urlDocumento))
            {
                TextoSinFirma = "La agencia da el justificante en su web: ábralo con el botón de abajo.";
            }
            else if (!string.IsNullOrWhiteSpace(aviso))
            {
                TextoSinFirma = aviso.Trim();
            }
            else
            {
                TextoSinFirma = parameters.GetValue<bool>(PARAMETRO_TIENE_FIRMA)
                    ? "No se ha podido cargar la firma. Inténtelo de nuevo más tarde."
                    : "Sin firma (se entregó con DNI).";
            }
        }

        private void AbrirDocumento()
        {
            if (_documento != null && _documento.Length > 0)
            {
                string nombre = $"PruebaEntrega_{_envio}{Extension(_tipoDocumento)}";
                AbrirConElSistema(GuardarTemporal(nombre, _documento));
            }
            else if (!string.IsNullOrWhiteSpace(_urlDocumento))
            {
                // El enlace viene de la agencia: con UseShellExecute abriría cualquier cosa (un fichero, un programa…). Solo http/https.
                if (EsEnlaceWeb(_urlDocumento))
                {
                    AbrirConElSistema(_urlDocumento.Trim());
                }
                else
                {
                    TextoSinFirma = MENSAJE_ENLACE_NO_VALIDO;
                }
            }
        }

        public const string MENSAJE_ENLACE_NO_VALIDO = "No se puede abrir el enlace del justificante.";

        /// <summary>Una URI absoluta http o https (lo único que se abre en el navegador).</summary>
        public static bool EsEnlaceWeb(string url)
            => !string.IsNullOrWhiteSpace(url)
               && Uri.TryCreate(url.Trim(), UriKind.Absolute, out Uri uri)
               && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

        /// <summary>La extensión del fichero por su tipo MIME (PDF si no se sabe: es lo que da CTT).</summary>
        public static string Extension(string tipo)
        {
            switch ((tipo ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "image/png": return ".png";
                case "image/jpeg":
                case "image/jpg": return ".jpg";
                case "image/gif": return ".gif";
                case "image/tiff": return ".tif";
                case "image/bmp": return ".bmp";
                default: return ".pdf";
            }
        }

        private static bool EsImagen(string tipo) => (tipo ?? string.Empty).StartsWith("image/", StringComparison.OrdinalIgnoreCase);

        private static string GuardarEnTemporal(string nombre, byte[] contenido)
        {
            string carpeta = Path.Combine(Path.GetTempPath(), "Nesto");
            _ = Directory.CreateDirectory(carpeta);
            string ruta = Path.Combine(carpeta, nombre);
            File.WriteAllBytes(ruta, contenido);
            return ruta;
        }

        /// <summary>Cómo se cobró el reembolso, con el importe.</summary>
        public static string TextoCobro(string cobroReembolso, decimal? importeCobrado)
        {
            string importe = importeCobrado.HasValue ? importeCobrado.Value.ToString("C", Espanol) : null;
            switch (cobroReembolso?.Trim())
            {
                case null:
                case "":
                    return "Sin reembolso";
                case COBRO_EFECTIVO:
                    return importe == null ? "Cobrado en efectivo" : $"Cobrado en efectivo: {importe}";
                case COBRO_TARJETA:
                    return importe == null ? "Cobrado con tarjeta" : $"Cobrado con tarjeta: {importe}";
                case COBRO_NO_COBRADO:
                    return "No se cobró el reembolso";
                default:
                    return importe == null ? cobroReembolso.Trim() : $"{cobroReembolso.Trim()}: {importe}";
            }
        }

        private static string TextoOGuion(string texto) => string.IsNullOrWhiteSpace(texto) ? "-" : texto.Trim();
    }
}
