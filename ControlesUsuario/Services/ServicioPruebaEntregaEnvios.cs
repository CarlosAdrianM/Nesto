using ControlesUsuario.Dialogs;
using Nesto.Infrastructure.Contracts;
using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace ControlesUsuario.Services
{
    /// <summary>
    /// Respuesta de GET api/EnviosAgencias/{id}/PruebaEntrega (la misma para todas las agencias). Mismos nombres que el servidor
    /// (PascalCase): si uno deja de casar, Newtonsoft lo deja por defecto sin avisar. <see cref="Documento"/> llega en base64.
    /// </summary>
    public class PruebaEntregaEnvio
    {
        public int Numero { get; set; }
        public int Agencia { get; set; }
        public string NombreAgencia { get; set; }
        public int? Pedido { get; set; }
        /// <summary>False: no hay prueba que ver y <see cref="Mensaje"/> dice por qué.</summary>
        public bool Disponible { get; set; }
        public string Mensaje { get; set; }
        public DateTime? FechaHora { get; set; }
        public string Receptor { get; set; }
        public string DniReceptor { get; set; }
        public byte[] Documento { get; set; }
        /// <summary>«application/pdf», «image/png», «image/jpeg»…</summary>
        public string TipoDocumento { get; set; }
        /// <summary>Cuando la agencia solo da un enlace: se abre en el navegador.</summary>
        public string UrlDocumento { get; set; }
        /// <summary>Solo el reparto propio.</summary>
        public string CobroReembolso { get; set; }
        public decimal? ImporteCobrado { get; set; }
        public string Usuario { get; set; }
    }

    /// <summary>La prueba de entrega de un envío, de cualquier agencia (la API la pide a la agencia en el momento).</summary>
    public interface IServicioPruebaEntregaEnvios
    {
        /// <summary>Lanza con el motivo de la API si no contesta 200 (403 sin permiso, 404 si el envío no existe).</summary>
        Task<PruebaEntregaEnvio> Leer(int numeroEnvio);
    }

    public class ServicioPruebaEntregaEnvios : IServicioPruebaEntregaEnvios
    {
        private readonly IClienteApiFactory _clienteApiFactory;

        public ServicioPruebaEntregaEnvios(IClienteApiFactory clienteApiFactory)
        {
            _clienteApiFactory = clienteApiFactory ?? throw new ArgumentNullException(nameof(clienteApiFactory));
        }

        public static string Ruta(int numeroEnvio) => $"EnviosAgencias/{numeroEnvio}/PruebaEntrega";

        public static PruebaEntregaEnvio DeRespuesta(string cuerpo)
            => string.IsNullOrWhiteSpace(cuerpo) ? null : JsonConvert.DeserializeObject<PruebaEntregaEnvio>(cuerpo);

        public async Task<PruebaEntregaEnvio> Leer(int numeroEnvio)
        {
            using (HttpClient client = _clienteApiFactory.Crear())
            using (HttpResponseMessage respuesta = await client.GetAsync(Ruta(numeroEnvio)).ConfigureAwait(false))
            {
                string cuerpo = respuesta.Content == null ? string.Empty : await respuesta.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!respuesta.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException(ServicioEnvioCorreoDocumentos.MensajeDeError(cuerpo));
                }
                return DeRespuesta(cuerpo);
            }
        }
    }

    /// <summary>
    /// «Ver prueba de entrega»: el mismo paso en Agencias › Tramitados y en el detalle del pedido. Pide la prueba a la API, y
    /// enseña el diálogo (<see cref="PruebaEntregaDialogViewModel"/>), el motivo si no la hay o el error si falla.
    /// </summary>
    public interface IMostradorPruebaEntrega
    {
        Task Mostrar(int numeroEnvio, string pedido);
    }

    public class MostradorPruebaEntrega : IMostradorPruebaEntrega
    {
        public const string TITULO = "Prueba de entrega";

        private readonly Func<int, Task<PruebaEntregaEnvio>> _leer;
        private readonly IServicioDialogos _dialogos;
        private readonly Action<Exception> _registrarError;
        private readonly Action<bool> _ocupado;

        /// <param name="registrarError">Para ELMAH (opcional): el error no se calla, también se le enseña al usuario.</param>
        /// <param name="ocupado">La pantalla está ocupada mientras se pide a la API (no mientras se ve el diálogo).</param>
        public MostradorPruebaEntrega(Func<int, Task<PruebaEntregaEnvio>> leer, IServicioDialogos dialogos, Action<Exception> registrarError = null,
            Action<bool> ocupado = null)
        {
            _leer = leer ?? throw new ArgumentNullException(nameof(leer));
            _dialogos = dialogos ?? throw new ArgumentNullException(nameof(dialogos));
            _registrarError = registrarError;
            _ocupado = ocupado;
        }

        public MostradorPruebaEntrega(IServicioPruebaEntregaEnvios servicio, IServicioDialogos dialogos, Action<Exception> registrarError = null,
            Action<bool> ocupado = null)
            : this(numero => (servicio ?? throw new ArgumentNullException(nameof(servicio))).Leer(numero), dialogos, registrarError, ocupado)
        {
        }

        public async Task Mostrar(int numeroEnvio, string pedido)
        {
            PruebaEntregaEnvio prueba;
            try
            {
                _ocupado?.Invoke(true);
                prueba = await _leer(numeroEnvio);
            }
            catch (Exception ex)
            {
                _ocupado?.Invoke(false);
                try
                {
                    _registrarError?.Invoke(ex);
                }
                catch
                {
                    // el registro nunca tapa el error al usuario
                }
                _dialogos.ShowError($"No se ha podido leer la prueba de entrega del envío {numeroEnvio}:\r{ex.InnerException?.Message ?? ex.Message}");
                return;
            }
            _ocupado?.Invoke(false);
            if (prueba == null || !prueba.Disponible)
            {
                _dialogos.ShowNotification(TITULO, string.IsNullOrWhiteSpace(prueba?.Mensaje) ? "Este envío no tiene prueba de entrega." : prueba.Mensaje.Trim());
                return;
            }
            _dialogos.ShowDialog(PruebaEntregaDialogViewModel.NOMBRE, Parametros(numeroEnvio, pedido, prueba), r => { });
        }

        public static ParametrosDialogo Parametros(int numeroEnvio, string pedido, PruebaEntregaEnvio prueba)
        {
            bool esImagen = prueba.Documento != null && prueba.Documento.Length > 0
                && (prueba.TipoDocumento ?? string.Empty).StartsWith("image/", StringComparison.OrdinalIgnoreCase);
            return new ParametrosDialogo
            {
                { PruebaEntregaDialogViewModel.PARAMETRO_ENVIO, numeroEnvio },
                { PruebaEntregaDialogViewModel.PARAMETRO_PEDIDO, string.IsNullOrWhiteSpace(pedido) ? prueba.Pedido?.ToString() ?? string.Empty : pedido },
                { PruebaEntregaDialogViewModel.PARAMETRO_AGENCIA, prueba.NombreAgencia },
                { PruebaEntregaDialogViewModel.PARAMETRO_FECHA_HORA, prueba.FechaHora ?? default(DateTime) },
                { PruebaEntregaDialogViewModel.PARAMETRO_RECEPTOR, prueba.Receptor },
                { PruebaEntregaDialogViewModel.PARAMETRO_DNI, prueba.DniReceptor },
                { PruebaEntregaDialogViewModel.PARAMETRO_COBRO_REEMBOLSO, prueba.CobroReembolso },
                { PruebaEntregaDialogViewModel.PARAMETRO_IMPORTE_COBRADO, prueba.ImporteCobrado },
                { PruebaEntregaDialogViewModel.PARAMETRO_USUARIO, prueba.Usuario },
                { PruebaEntregaDialogViewModel.PARAMETRO_TIENE_FIRMA, esImagen },
                { PruebaEntregaDialogViewModel.PARAMETRO_FIRMA, esImagen ? prueba.Documento : null },
                { PruebaEntregaDialogViewModel.PARAMETRO_DOCUMENTO, prueba.Documento },
                { PruebaEntregaDialogViewModel.PARAMETRO_TIPO_DOCUMENTO, prueba.TipoDocumento },
                { PruebaEntregaDialogViewModel.PARAMETRO_URL_DOCUMENTO, prueba.UrlDocumento },
                { PruebaEntregaDialogViewModel.PARAMETRO_AVISO, prueba.Mensaje }
            };
        }
    }
}
