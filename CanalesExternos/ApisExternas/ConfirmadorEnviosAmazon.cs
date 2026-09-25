using FikaAmazonAPI;
using FikaAmazonAPI.AmazonSpApiSDK.Models.Feeds;
using FikaAmazonAPI.ConstructFeed;
using FikaAmazonAPI.ConstructFeed.Messages;
using Nesto.Infrastructure.Contracts;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using static FikaAmazonAPI.Utils.Constants;

namespace Nesto.Modulos.CanalesExternos.ApisExternas
{
    /// <summary>Los datos de un envío que se confirma en Amazon.</summary>
    public class ConfirmacionEnvioAmazon
    {
        public string AmazonOrderId { get; set; }
        public int PedidoNesto { get; set; }
        public string CodigoAgencia { get; set; }
        public string NombreAgencia { get; set; }
        public string NombreServicio { get; set; }
        public string NumeroSeguimiento { get; set; }

        public string Descripcion =>
            $"transportista «{NombreAgencia}» (CarrierCode «{CodigoAgencia}»), servicio «{NombreServicio}», seguimiento {NumeroSeguimiento}";

        public string DescripcionPedido =>
            PedidoNesto > 0 ? $"{AmazonOrderId} (pedido {PedidoNesto} de Nesto)" : AmazonOrderId;
    }

    /// <summary>
    /// Nesto#499: lo que hace falta de la API de feeds de Amazon para confirmar un envío y comprobar
    /// cómo ha ido. Existe para poder probar la confirmación sin Amazon.
    /// </summary>
    public interface IClienteFeedsAmazon
    {
        /// <summary>Manda el feed POST_ORDER_FULFILLMENT_DATA y devuelve su id.</summary>
        Task<string> EnviarConfirmacionEnvio(ConfirmacionEnvioAmazon envio);
        Task<Feed> LeerFeed(string feedId);
        Task<ProcessingReportMessage> LeerInforme(string resultFeedDocumentId);
    }

    /// <summary>
    /// Nesto#499: avisa al usuario (sin bloquearle) de que una confirmación de envío que ya se dio por
    /// enviada ha ido mal en Amazon. Llega desde un hilo que no es el de la UI.
    /// </summary>
    public interface IAvisoConfirmacionAmazon
    {
        Task Avisar(string titulo, string mensaje, ConfirmacionEnvioAmazon envio, string feedId);
    }

    /// <summary>
    /// Nesto#499: confirma envíos en Amazon SIN hacer esperar al usuario al procesamiento del feed.
    ///
    /// El 24/09/26 (146f4654) se empezó a sondear el feed tras el SubmitFeed (cada 5 s, hasta 90 s) para
    /// leer el informe de procesamiento, y la confirmación pasó de un par de segundos a un minuto: Amazon
    /// tarda de 30 s a varios minutos en procesar un feed. Ahora <see cref="Confirmar"/> vuelve en cuanto
    /// Amazon acepta el feed y la comprobación sigue en segundo plano (async, sin Task.Run ni UI), con
    /// intervalo creciente hasta <see cref="ESPERA_MAXIMA"/>. Si Amazon lo rechaza, no lo termina a tiempo
    /// o no se puede leer, se avisa con <see cref="IAvisoConfirmacionAmazon"/>; si va bien, no se dice nada.
    /// Cada confirmación lleva su propia verificación: varias seguidas no se esperan unas a otras.
    /// </summary>
    public class ConfirmadorEnviosAmazon
    {
        internal static readonly TimeSpan ESPERA_MAXIMA = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan[] ESPERAS =
        {
            TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(20),
            TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(45), TimeSpan.FromSeconds(60)
        };

        internal const string TITULO_RECHAZADA = "Amazon ha rechazado la confirmación de un envío";
        internal const string TITULO_SIN_COMPROBAR = "No se ha podido comprobar una confirmación de envío en Amazon";

        private readonly Func<IClienteFeedsAmazon> _crearCliente;
        private readonly IAvisoConfirmacionAmazon _aviso;
        private readonly Func<TimeSpan, Task> _esperar;

        public ConfirmadorEnviosAmazon(IAvisoConfirmacionAmazon aviso)
            : this(() => new ClienteFeedsAmazon(), aviso, t => Task.Delay(t)) { }

        internal ConfirmadorEnviosAmazon(Func<IClienteFeedsAmazon> crearCliente, IAvisoConfirmacionAmazon aviso, Func<TimeSpan, Task> esperar)
        {
            _crearCliente = crearCliente ?? throw new ArgumentNullException(nameof(crearCliente));
            _aviso = aviso;
            _esperar = esperar ?? (t => Task.Delay(t));
        }

        /// <summary>La verificación en segundo plano de la última confirmación (para los tests).</summary>
        internal Task UltimaVerificacion { get; private set; } = Task.CompletedTask;

        /// <summary>Lo que se espera antes de cada lectura del feed: 10 s, 15 s, 20 s, 30 s, 45 s y luego cada minuto.</summary>
        internal static TimeSpan EsperaAntesDelIntento(int intento) => ESPERAS[Math.Min(Math.Max(intento, 0), ESPERAS.Length - 1)];

        /// <summary>
        /// Manda la confirmación y vuelve en cuanto Amazon acepta el feed. Lanza si Amazon no lo acepta.
        /// </summary>
        public async Task<string> Confirmar(ConfirmacionEnvioAmazon envio)
        {
            if (envio == null)
            {
                throw new ArgumentNullException(nameof(envio));
            }
            IClienteFeedsAmazon cliente;
            string feedId;
            try
            {
                cliente = _crearCliente();
                feedId = await cliente.EnviarConfirmacionEnvio(envio).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // 22/09/26: antes se tragaba la excepción y devolvía un texto: nadie se enteraba (ni ELMAH).
                throw new Exception($"Amazon no ha aceptado la confirmación del pedido {envio.AmazonOrderId} ({envio.Descripcion}): {ex.Message}", ex);
            }
            if (string.IsNullOrWhiteSpace(feedId))
            {
                throw new Exception($"Amazon no ha devuelto el identificador del feed de la confirmación del pedido {envio.AmazonOrderId} ({envio.Descripcion}). Revisa en Seller Central si aparece como enviado.");
            }

            // Sin await a propósito: el resultado lo comprueba la verificación en segundo plano, que nunca lanza.
            UltimaVerificacion = VerificarEnSegundoPlano(cliente, envio, feedId);

            return $"Se ha enviado a Amazon la confirmación del pedido {envio.AmazonOrderId} con {envio.NombreAgencia} ({envio.NombreServicio}) y seguimiento {envio.NumeroSeguimiento}. " +
                "Amazon tarda unos minutos en procesarla: si la rechaza, te avisaremos.";
        }

        /// <summary>Espera el resultado del feed y avisa si no ha ido bien. Nunca lanza.</summary>
        internal async Task VerificarEnSegundoPlano(IClienteFeedsAmazon cliente, ConfirmacionEnvioAmazon envio, string feedId)
        {
            try
            {
                AmazonApiOrdersService.ResultadoFeedAmazon resultado = await EsperarResultado(cliente, feedId).ConfigureAwait(false);
                (string titulo, string mensaje)? aviso = ConstruirAviso(envio, feedId, resultado);
                if (aviso == null)
                {
                    AmazonApiOrdersService.LogDiag($"ConfirmarPedido {envio.AmazonOrderId}: Amazon ha procesado bien el feed {feedId}" +
                        (string.IsNullOrWhiteSpace(resultado.Detalle) ? string.Empty : $" (avisos: {resultado.Detalle})"));
                    return;
                }
                AmazonApiOrdersService.LogDiag($"ConfirmarPedido {envio.AmazonOrderId}: {aviso.Value.mensaje}");
                if (_aviso != null)
                {
                    await _aviso.Avisar(aviso.Value.titulo, aviso.Value.mensaje, envio, feedId).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                AmazonApiOrdersService.LogDiag($"ConfirmarPedido {envio?.AmazonOrderId}: fallo al verificar el feed {feedId}: {AmazonApiOrdersService.DescribirCadenaError(ex)}");
            }
        }

        internal async Task<AmazonApiOrdersService.ResultadoFeedAmazon> EsperarResultado(IClienteFeedsAmazon cliente, string feedId)
        {
            TimeSpan esperado = TimeSpan.Zero;
            Feed feed = null;
            string ultimoError = null;
            for (int intento = 0; esperado < ESPERA_MAXIMA; intento++)
            {
                TimeSpan espera = EsperaAntesDelIntento(intento);
                await _esperar(espera).ConfigureAwait(false);
                esperado += espera;
                try
                {
                    feed = await cliente.LeerFeed(feedId).ConfigureAwait(false);
                    ultimoError = null;
                }
                catch (Exception ex)
                {
                    // Un fallo puntual (rate limit, red) no decide nada: se reintenta hasta el tope.
                    ultimoError = ex.Message;
                    AmazonApiOrdersService.LogDiag($"Feed {feedId}: no se pudo leer su estado: {AmazonApiOrdersService.DescribirCadenaError(ex)}");
                    continue;
                }
                if (!AmazonApiOrdersService.FeedEnCurso(feed?.ProcessingStatus))
                {
                    break;
                }
            }

            if (AmazonApiOrdersService.FeedEnCurso(feed?.ProcessingStatus))
            {
                return new AmazonApiOrdersService.ResultadoFeedAmazon
                {
                    Estado = AmazonApiOrdersService.EstadoFeedAmazon.SinConfirmar,
                    Detalle = ultimoError != null && feed == null
                        ? "No se ha podido leer el estado del feed: " + ultimoError
                        : $"Amazon no lo ha terminado de procesar en {ESPERA_MAXIMA.TotalMinutes:0} minutos."
                };
            }

            ProcessingReportMessage informe = null;
            if (!string.IsNullOrWhiteSpace(feed.ResultFeedDocumentId))
            {
                try
                {
                    informe = await cliente.LeerInforme(feed.ResultFeedDocumentId).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    // Sin informe se decide solo por el estado (FATAL/CANCELLED = rechazado; DONE = sin verificar).
                    AmazonApiOrdersService.LogDiag($"Feed {feedId}: no se pudo leer el informe de procesamiento: {AmazonApiOrdersService.DescribirCadenaError(ex)}");
                }
            }
            return AmazonApiOrdersService.InterpretarResultadoFeed(feed.ProcessingStatus, informe);
        }

        /// <summary>El aviso para el usuario, o null si Amazon lo ha dado por bueno (entonces no se avisa).</summary>
        internal static (string titulo, string mensaje)? ConstruirAviso(ConfirmacionEnvioAmazon envio, string feedId, AmazonApiOrdersService.ResultadoFeedAmazon resultado)
        {
            switch (resultado?.Estado)
            {
                case AmazonApiOrdersService.EstadoFeedAmazon.Confirmado:
                    return null;
                case AmazonApiOrdersService.EstadoFeedAmazon.Rechazado:
                    return (TITULO_RECHAZADA,
                        $"Amazon ha rechazado la confirmación del envío del pedido {envio.DescripcionPedido}.\n\n" +
                        $"Motivo de Amazon: {resultado.Detalle}\n\n" +
                        $"El pedido NO consta como enviado en Amazon: revísalo en Seller Central.\n" +
                        $"Envío: {envio.Descripcion}. Feed {feedId}.");
                default:
                    return (TITULO_SIN_COMPROBAR,
                        $"Se envió a Amazon la confirmación del envío del pedido {envio.DescripcionPedido}, pero no se ha podido comprobar que la haya aceptado.\n\n" +
                        $"Motivo: {resultado?.Detalle ?? "desconocido"}\n\n" +
                        $"Revisa en Seller Central que el pedido aparece como enviado.\n" +
                        $"Envío: {envio.Descripcion}. Feed {feedId}.");
            }
        }
    }

    /// <summary>Nesto#499: el cliente de feeds real, sobre FikaAmazonAPI. Una instancia por confirmación.</summary>
    public class ClienteFeedsAmazon : IClienteFeedsAmazon
    {
        private AmazonConnection _conexion;
        private AmazonConnection Conexion => _conexion ??= AmazonApiOrdersService.ConexionAmazon();

        public async Task<string> EnviarConfirmacionEnvio(ConfirmacionEnvioAmazon envio)
        {
            ConstructFeedService createDocument = new ConstructFeedService(Conexion.GetCurrentSellerID, "1.02");
            var list = new List<OrderFulfillmentMessage>
            {
                new OrderFulfillmentMessage()
                {
                    AmazonOrderID = envio.AmazonOrderId,
                    FulfillmentDate = DateTime.Now.ToString("yyyy-MM-dd'T'HH:mm:ss.fffK"),
                    FulfillmentData = new FulfillmentData()
                    {
                        // 24/09/26: desde 2021 Amazon exige CarrierCode en España. Sin él acepta el feed pero
                        // no marca el pedido como enviado (lo que pasaba con CTT). Con "Other", CarrierName manda.
                        CarrierCode = envio.CodigoAgencia,
                        CarrierName = envio.NombreAgencia, // "Correos Express",
                        ShippingMethod = envio.NombreServicio, // "ePaq",
                        ShipperTrackingNumber = envio.NumeroSeguimiento
                    }
                }
            };
            createDocument.AddOrderFulfillmentMessage(list);
            var xml = createDocument.GetXML();
            return await Conexion.Feed.SubmitFeedAsync(xml, FeedType.POST_ORDER_FULFILLMENT_DATA).ConfigureAwait(false);
        }

        public Task<Feed> LeerFeed(string feedId) => Conexion.Feed.GetFeedAsync(feedId, CancellationToken.None);

        public async Task<ProcessingReportMessage> LeerInforme(string resultFeedDocumentId)
        {
            var documento = await Conexion.Feed.GetFeedDocumentAsync(resultFeedDocumentId, CancellationToken.None).ConfigureAwait(false);
            return await Conexion.Feed.GetFeedDocumentProcessingReportAsync(documento, CancellationToken.None).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Nesto#499: el aviso de una confirmación que ha ido mal. Queda en ELMAH (POST api/Errores) y se
    /// enseña con el diálogo de notificación NO modal (Show, no ShowDialog) en el hilo de la UI, para no
    /// interrumpir lo que el usuario esté haciendo.
    /// TODO: el sitio ideal es el buzón/campana (#477), pero hoy Nesto no puede crear un aviso en su propio
    /// buzón (falta un POST api/Notificaciones/Buzon en NestoAPI); bastará con otra implementación de
    /// <see cref="IAvisoConfirmacionAmazon"/>.
    /// </summary>
    public class AvisoConfirmacionAmazonNoModal : IAvisoConfirmacionAmazon
    {
        internal const string DIALOGO = "NotificationDialog";

        private readonly IDialogService _dialogService;
        private readonly Func<IServicioRegistroErrores> _registroErrores;

        /// <param name="dialogService">Debe abrir en el hilo de UI (p. ej. ControlesUsuario.Dialogs.DialogServiceEnHiloUi).</param>
        public AvisoConfirmacionAmazonNoModal(IDialogService dialogService, Func<IServicioRegistroErrores> registroErrores)
        {
            _dialogService = dialogService;
            _registroErrores = registroErrores;
        }

        public async Task Avisar(string titulo, string mensaje, ConfirmacionEnvioAmazon envio, string feedId)
        {
            try
            {
                IServicioRegistroErrores registro = _registroErrores?.Invoke();
                if (registro != null)
                {
                    await registro.RegistrarErrorAsync(new Exception(mensaje),
                        $"CanalesExternos.ConfirmarEnvioAmazon pedidoAmazon={envio?.AmazonOrderId} pedido={envio?.PedidoNesto} feed={feedId}").ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                AmazonApiOrdersService.LogDiag($"No se pudo registrar en ELMAH el aviso del feed {feedId}: {ex.Message}");
            }

            try
            {
                _dialogService?.Show(DIALOGO, new DialogParameters
                {
                    { "title", titulo },
                    { "message", mensaje }
                }, null);
            }
            catch (Exception ex)
            {
                AmazonApiOrdersService.LogDiag($"No se pudo enseñar el aviso del feed {feedId}: {ex.Message}");
            }
        }
    }
}
