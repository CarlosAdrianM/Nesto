using ControlesUsuario.Services;
using Nesto.Infrastructure.Contracts;
using Nesto.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Nesto.Modulos.Cliente
{
    /// <summary>Novedades 1258: una línea del borrador que no pasa la validación, con su motivo.</summary>
    public class ErrorAutorizacionPedido
    {
        public string Producto { get; set; }
        public string Motivo { get; set; }
    }

    /// <summary>
    /// Novedades 1258: una solicitud de autorización de un pedido que no pasa la validación de ofertas y descuentos (lo que devuelven
    /// POST, GET y GET {id} de api/PedidosVenta/Autorizaciones). <see cref="Pedido"/> solo viene en el GET de una.
    /// </summary>
    public class AutorizacionPedidoModel
    {
        public const string ESTADO_PENDIENTE = "Pendiente";
        public const string ESTADO_AUTORIZADA = NotificacionBuzon.ESTADO_AUTORIZACION_PEDIDO_AUTORIZADA;
        public const string ESTADO_DENEGADA = NotificacionBuzon.ESTADO_AUTORIZACION_PEDIDO_DENEGADA;
        public const string ESTADO_ANULADA = "Anulada";

        public int Id { get; set; }
        public string Estado { get; set; }
        public string Empresa { get; set; }
        public string Cliente { get; set; }
        public string Contacto { get; set; }
        public string NombreCliente { get; set; }
        public string Solicitante { get; set; }
        public string NombreSolicitante { get; set; }
        public DateTime? FechaSolicitud { get; set; }
        public decimal? Importe { get; set; }
        public int? NumeroLineas { get; set; }
        public List<string> Motivos { get; set; } = new List<string>();
        public List<ErrorAutorizacionPedido> Errores { get; set; } = new List<ErrorAutorizacionPedido>();
        public string Comentario { get; set; }
        public string UsuarioResolucion { get; set; }
        public DateTime? FechaResolucion { get; set; }
        public string MotivoResolucion { get; set; }
        public int? PedidoCreado { get; set; }
        /// <summary>Solo en la respuesta del POST: a quién se ha pedido.</summary>
        public List<string> Destinatarios { get; set; } = new List<string>();
        /// <summary>Solo en el GET de una: el borrador tal cual se mandó.</summary>
        public PedidoVentaDTO Pedido { get; set; }

        public bool EstaPendiente => string.Equals(Estado?.Trim(), ESTADO_PENDIENTE, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Novedades 1258: la respuesta de POST …/{id}/Autorizar: el número del pedido creado.</summary>
    public class ResultadoAutorizarPedido
    {
        public int Id { get; set; }
        public string Estado { get; set; }
        public int? Pedido { get; set; }
    }

    /// <summary>
    /// Novedades 1258: pedir, ver, autorizar, denegar y anular autorizaciones de pedidos que no pasan la validación de ofertas y
    /// descuentos (api/PedidosVenta/Autorizaciones). Los errores llegan como <see cref="ApiRepartoException"/> con el «Message» de la
    /// API (o «Esto estará disponible cuando se actualice el servidor.» si el servidor aún no lo tiene).
    /// </summary>
    public interface IServicioAutorizacionesPedido
    {
        /// <summary>POST: 201 con la solicitud; 400 si en realidad pasa la validación; 409 YA_SOLICITADA si ya la había pedido.</summary>
        Task<AutorizacionPedidoModel> Pedir(PedidoVentaDTO pedido, string comentario);
        Task<List<AutorizacionPedidoModel>> Leer(bool pendientes, bool mias);
        /// <summary>GET {id}: la solicitud con el borrador (<see cref="AutorizacionPedidoModel.Pedido"/>).</summary>
        Task<AutorizacionPedidoModel> LeerUna(int id);
        Task<ResultadoAutorizarPedido> Autorizar(int id, string comentario);
        Task<AutorizacionPedidoModel> Denegar(int id, string motivo);
        Task<AutorizacionPedidoModel> Anular(int id);
    }

    public class ServicioAutorizacionesPedido : IServicioAutorizacionesPedido
    {
        public const string RUTA = "PedidosVenta/Autorizaciones";
        public const string CODIGO_YA_SOLICITADA = "YA_SOLICITADA";
        public const string MENSAJE_SIN_PERMISO = "No tiene permiso para ver o contestar esta autorización.";

        private readonly IClienteApiFactory _clienteApiFactory;

        public ServicioAutorizacionesPedido(IClienteApiFactory clienteApiFactory)
        {
            _clienteApiFactory = clienteApiFactory ?? throw new ArgumentNullException(nameof(clienteApiFactory));
        }

        /// <summary>PURA. El cuerpo del POST: el mismo PedidoVentaDTO que iba al POST de PedidosVenta y el comentario (null si no hay).</summary>
        public static string CuerpoPedir(PedidoVentaDTO pedido, string comentario)
            => JsonConvert.SerializeObject(new { Pedido = pedido, Comentario = string.IsNullOrWhiteSpace(comentario) ? null : comentario.Trim() });

        public static string RutaLista(bool pendientes, bool mias)
            => $"{RUTA}?pendientes={(pendientes ? "true" : "false")}&mias={(mias ? "true" : "false")}";

        public Task<AutorizacionPedidoModel> Pedir(PedidoVentaDTO pedido, string comentario)
            => Enviar<AutorizacionPedidoModel>(HttpMethod.Post, RUTA, CuerpoPedir(pedido, comentario));

        public async Task<List<AutorizacionPedidoModel>> Leer(bool pendientes, bool mias)
            => await Enviar<List<AutorizacionPedidoModel>>(HttpMethod.Get, RutaLista(pendientes, mias), null).ConfigureAwait(false)
               ?? new List<AutorizacionPedidoModel>();

        public Task<AutorizacionPedidoModel> LeerUna(int id)
            => Enviar<AutorizacionPedidoModel>(HttpMethod.Get, $"{RUTA}/{id}", null);

        public Task<ResultadoAutorizarPedido> Autorizar(int id, string comentario)
            => Enviar<ResultadoAutorizarPedido>(HttpMethod.Post, $"{RUTA}/{id}/Autorizar",
                JsonConvert.SerializeObject(new { Comentario = string.IsNullOrWhiteSpace(comentario) ? null : comentario.Trim() }));

        public Task<AutorizacionPedidoModel> Denegar(int id, string motivo)
            => Enviar<AutorizacionPedidoModel>(HttpMethod.Post, $"{RUTA}/{id}/Denegar", JsonConvert.SerializeObject(new { Motivo = motivo?.Trim() }));

        public Task<AutorizacionPedidoModel> Anular(int id)
            => Enviar<AutorizacionPedidoModel>(HttpMethod.Delete, $"{RUTA}/{id}", null);

        private async Task<T> Enviar<T>(HttpMethod metodo, string ruta, string cuerpoJson) where T : class
        {
            using (HttpClient client = _clienteApiFactory.Crear())
            using (var peticion = new HttpRequestMessage(metodo, ruta))
            {
                if (cuerpoJson != null)
                {
                    peticion.Content = new StringContent(cuerpoJson, Encoding.UTF8, "application/json");
                }
                using (HttpResponseMessage respuesta = await client.SendAsync(peticion).ConfigureAwait(false))
                {
                    string cuerpo = respuesta.Content == null ? string.Empty : await respuesta.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (!respuesta.IsSuccessStatusCode)
                    {
                        throw ErroresApiReparto.Excepcion((int)respuesta.StatusCode, cuerpo, MENSAJE_SIN_PERMISO);
                    }
                    return string.IsNullOrWhiteSpace(cuerpo) ? null : JsonConvert.DeserializeObject<T>(cuerpo);
                }
            }
        }
    }

    /// <summary>
    /// Novedades 1258: lo que ha pasado al ofrecer «Pedir autorización» en el aviso de validación.
    /// </summary>
    public enum ResultadoOfrecerAutorizacion
    {
        /// <summary>No se ha enseñado nada (sin servicio): quien llama enseña el error como siempre.</summary>
        NoOfrecida,
        /// <summary>Ha visto el aviso y ha cerrado (o ha cancelado el comentario): no se ha pedido nada.</summary>
        NoQuiso,
        Pedida,
        /// <summary>409 YA_SOLICITADA: ya la había pedido y sigue pendiente.</summary>
        YaPedida,
        /// <summary>La API no la ha aceptado (400, 503…): se ha enseñado su motivo.</summary>
        Error
    }

    /// <summary>
    /// Novedades 1258: el aviso de validación para quien NO puede guardar «de todos modos», con «Pedir autorización»: pide un comentario
    /// opcional, manda el borrador tal cual y dice a quién se ha pedido. No toca el borrador (la plantilla se queda como estaba).
    /// </summary>
    public class SolicitudAutorizacionPedido
    {
        public const string TITULO = "Pedido no válido";
        public const string TITULO_PEDIR = "Pedir autorización";
        public const string TEXTO_PEDIR = "Pedir autorización";
        public const string TEXTO_CERRAR = "Cerrar";
        public const int LONGITUD_MAXIMA_COMENTARIO = 500;
        public const string EXPLICACION = "Usted no puede crearlo sin pasar la validación. Puede pedir autorización a quien puede darla: " +
            "le llegará el pedido tal cual y, si lo autoriza, se creará.";
        public const string PREGUNTA_COMENTARIO = "Si quiere, escriba un comentario para quien autoriza (por qué lo necesita, lo hablado con el cliente…). Puede dejarlo en blanco.";
        public const string MENSAJE_YA_SOLICITADA = "Ya había pedido esta autorización; sigue pendiente.";
        public static readonly string MENSAJE_COMENTARIO_LARGO = $"El comentario no puede pasar de {LONGITUD_MAXIMA_COMENTARIO} caracteres.";

        private readonly IServicioAutorizacionesPedido _servicio;
        private readonly IServicioDialogos _dialogos;

        public SolicitudAutorizacionPedido(IServicioAutorizacionesPedido servicio, IServicioDialogos dialogos)
        {
            _servicio = servicio;
            _dialogos = dialogos;
        }

        /// <summary>PURA. «Se ha pedido autorización a Ana y Luis. Le avisaremos por la campana cuando contesten.»</summary>
        public static string TextoPedida(IEnumerable<string> destinatarios)
        {
            var nombres = new List<string>();
            foreach (string d in destinatarios ?? Array.Empty<string>())
            {
                if (!string.IsNullOrWhiteSpace(d))
                {
                    nombres.Add(d.Trim());
                }
            }
            string aQuien;
            if (nombres.Count == 0)
            {
                aQuien = string.Empty;
            }
            else if (nombres.Count == 1)
            {
                aQuien = " a " + nombres[0];
            }
            else
            {
                aQuien = " a " + string.Join(", ", nombres.GetRange(0, nombres.Count - 1)) + " y " + nombres[nombres.Count - 1];
            }
            return $"Se ha pedido autorización{aQuien}. Le avisaremos por la campana cuando contesten.";
        }

        public async Task<ResultadoOfrecerAutorizacion> OfrecerAsync(PedidoVentaDTO pedido, string mensajeValidacion)
        {
            if (_servicio == null || _dialogos == null || pedido == null)
            {
                return ResultadoOfrecerAutorizacion.NoOfrecida;
            }
            string mensaje = (string.IsNullOrWhiteSpace(mensajeValidacion) ? string.Empty : mensajeValidacion.Trim() + "\r\n\r\n") + EXPLICACION;
            if (!ControlesUsuario.Dialogs.ConfirmacionConBotones.Preguntar(_dialogos, TITULO, mensaje, TEXTO_PEDIR, TEXTO_CERRAR))
            {
                return ResultadoOfrecerAutorizacion.NoQuiso;
            }
            string comentario;
            while (true)
            {
                comentario = _dialogos.GetText(TITULO_PEDIR, PREGUNTA_COMENTARIO);
                if (comentario == null)
                {
                    return ResultadoOfrecerAutorizacion.NoQuiso; // ha cancelado
                }
                if (comentario.Trim().Length <= LONGITUD_MAXIMA_COMENTARIO)
                {
                    break;
                }
                _dialogos.ShowError(MENSAJE_COMENTARIO_LARGO);
            }
            try
            {
                AutorizacionPedidoModel solicitud = await _servicio.Pedir(pedido, comentario);
                _dialogos.ShowNotification(TITULO_PEDIR, TextoPedida(solicitud?.Destinatarios));
                return ResultadoOfrecerAutorizacion.Pedida;
            }
            catch (ApiRepartoException ex) when (ex.CodigoHttp == 409 && string.Equals(ex.CodigoApi, ServicioAutorizacionesPedido.CODIGO_YA_SOLICITADA, StringComparison.OrdinalIgnoreCase))
            {
                _dialogos.ShowNotification(TITULO_PEDIR, MENSAJE_YA_SOLICITADA);
                return ResultadoOfrecerAutorizacion.YaPedida;
            }
            catch (ApiRepartoException ex)
            {
                _dialogos.ShowError(ex.Message);
                return ResultadoOfrecerAutorizacion.Error;
            }
            catch (Exception ex)
            {
                _dialogos.ShowError("No se ha podido pedir la autorización: " + ex.Message);
                return ResultadoOfrecerAutorizacion.Error;
            }
        }
    }
}
