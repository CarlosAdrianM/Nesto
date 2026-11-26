using Nesto.Infrastructure.Contracts;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace ControlesUsuario.BultosPendientes
{
    /// <summary>NestoAPI#880 (corte 2): un bulto que faltó en una entrega incompleta del reparto propio (GET api/RepartoPropio/BultosPendientes).</summary>
    public class BultoPendienteModel
    {
        public const string ESTADO_PENDIENTE = "Pendiente";

        public int Id { get; set; }
        public string Empresa { get; set; }
        public int EnvioId { get; set; }
        public int? Pedido { get; set; }
        public string Cliente { get; set; }
        public string Contacto { get; set; }
        public string Nombre { get; set; }
        public string Codigo { get; set; }
        public int Bulto { get; set; }
        public int Total { get; set; }
        public string SituacionAlEntregar { get; set; }
        public string MotivoFalta { get; set; }
        public string Repartidor { get; set; }
        public DateTime? FechaEntrega { get; set; }
        public string Estado { get; set; }
        public string Cierre { get; set; }
        public string MotivoCierre { get; set; }
        public string UsuarioCierre { get; set; }
        public DateTime? FechaCierre { get; set; }
        public string DondeEsta { get; set; }
        public string Texto { get; set; }

        public bool EsPendiente => string.Equals(Estado?.Trim(), ESTADO_PENDIENTE, StringComparison.OrdinalIgnoreCase);

        /// <summary>«Bulto 5 de 5».</summary>
        public string TextoBulto => Total > 0 ? $"{Bulto} de {Total}" : Bulto.ToString();

        /// <summary>«Pendiente» o «Entregado en mano: se lo llevó Alfredo el jueves».</summary>
        public string TextoEstado
        {
            get
            {
                if (EsPendiente || string.IsNullOrWhiteSpace(Cierre))
                {
                    return Estado;
                }
                string cierre = ReglasBultosPendientes.NombreCierre(Cierre);
                return string.IsNullOrWhiteSpace(MotivoCierre) ? cierre : $"{cierre}: {MotivoCierre.Trim()}";
            }
        }
    }

    /// <summary>NestoAPI#880: un error de la API con su mensaje (403 de usted, 503 sin el script…). <see cref="Estado"/> = el código HTTP.</summary>
    public class BultosPendientesApiException : Exception
    {
        public BultosPendientesApiException(int estado, string mensaje) : base(mensaje)
        {
            Estado = estado;
        }

        public int Estado { get; }
    }

    /// <summary>NestoAPI#880 (corte 2): los bultos pendientes y su cierre a mano.</summary>
    public interface IServicioBultosPendientes
    {
        Task<List<BultoPendienteModel>> Listar(bool soloPendientes, string empresa, int? pedido, int? envio);
        Task<BultoPendienteModel> Cerrar(int id, string cierre, string motivo);
    }

    public class ServicioBultosPendientes : IServicioBultosPendientes
    {
        public const string MENSAJE_SIN_TABLA = "Los bultos pendientes todavía no están activados en el servidor.";
        public const string MENSAJE_SIN_PERMISO = "Solo Almacén, Dirección e Informática pueden cerrar un bulto pendiente.";

        private readonly IConfiguracion _configuracion;
        private readonly IServicioAutenticacion _autenticacion;

        public ServicioBultosPendientes(IConfiguracion configuracion, IServicioAutenticacion autenticacion)
        {
            _configuracion = configuracion;
            _autenticacion = autenticacion;
        }

        private async Task<HttpClient> Cliente()
        {
            var client = new HttpClient { BaseAddress = new Uri(_configuracion.servidorAPI) };
            if (!await _autenticacion.ConfigurarAutorizacion(client))
            {
                client.Dispose();
                throw new UnauthorizedAccessException("No se pudo configurar la autorización");
            }
            return client;
        }

        internal static string UrlListar(bool soloPendientes, string empresa, int? pedido, int? envio)
        {
            string url = $"RepartoPropio/BultosPendientes?soloPendientes={(soloPendientes ? "true" : "false")}";
            if (!string.IsNullOrWhiteSpace(empresa))
            {
                url += "&empresa=" + Uri.EscapeDataString(empresa.Trim());
            }
            if (pedido.HasValue)
            {
                url += "&pedido=" + pedido.Value;
            }
            if (envio.HasValue)
            {
                url += "&envio=" + envio.Value;
            }
            return url;
        }

        public async Task<List<BultoPendienteModel>> Listar(bool soloPendientes, string empresa, int? pedido, int? envio)
        {
            using HttpClient client = await Cliente();
            HttpResponseMessage response = await client.GetAsync(UrlListar(soloPendientes, empresa, pedido, envio));
            string body = await response.Content.ReadAsStringAsync();
            Comprobar(response.StatusCode, body);
            return JsonConvert.DeserializeObject<List<BultoPendienteModel>>(body) ?? new List<BultoPendienteModel>();
        }

        public async Task<BultoPendienteModel> Cerrar(int id, string cierre, string motivo)
        {
            using HttpClient client = await Cliente();
            var cuerpo = new { Cierre = cierre, Motivo = motivo?.Trim() };
            HttpResponseMessage response = await client.PostAsync($"RepartoPropio/BultosPendientes/{id}/Cerrar",
                new StringContent(JsonConvert.SerializeObject(cuerpo), Encoding.UTF8, "application/json"));
            string body = await response.Content.ReadAsStringAsync();
            Comprobar(response.StatusCode, body);
            return JsonConvert.DeserializeObject<BultoPendienteModel>(body);
        }

        internal static void Comprobar(HttpStatusCode estado, string body)
        {
            if ((int)estado >= 200 && (int)estado < 300)
            {
                return;
            }
            string mensaje = LeerMensaje(body);
            if (estado == HttpStatusCode.ServiceUnavailable)
            {
                mensaje = MENSAJE_SIN_TABLA;
            }
            else if (estado == HttpStatusCode.Forbidden && string.IsNullOrWhiteSpace(mensaje))
            {
                mensaje = MENSAJE_SIN_PERMISO;
            }
            else if (string.IsNullOrWhiteSpace(mensaje))
            {
                mensaje = $"El servidor ha contestado {(int)estado} con los bultos pendientes.";
            }
            throw new BultosPendientesApiException((int)estado, mensaje);
        }

        internal static string LeerMensaje(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return null;
            }
            try
            {
                JToken json = JToken.Parse(body);
                if (json.Type == JTokenType.String)
                {
                    return json.ToString();
                }
                return json["Message"]?.ToString();
            }
            catch
            {
                return body;
            }
        }
    }

    /// <summary>NestoAPI#880: lo que decide la pantalla (puro).</summary>
    public static class ReglasBultosPendientes
    {
        public const string CIERRE_ENTREGADO_EN_MANO = "EntregadoEnMano";
        public const string CIERRE_PERDIDO = "Perdido";
        public const string CIERRE_ABONADO = "Abonado";
        public const int LONGITUD_MOTIVO = 500;
        public const string MOTIVO_FALTA_CIERRE = "Elija cómo se cierra el bulto.";
        public const string MOTIVO_FALTA_MOTIVO = "Escriba el motivo del cierre.";

        public static IReadOnlyList<string> CIERRES { get; } = new[] { CIERRE_ENTREGADO_EN_MANO, CIERRE_PERDIDO, CIERRE_ABONADO };

        public static string NombreCierre(string cierre) => cierre?.Trim() switch
        {
            CIERRE_ENTREGADO_EN_MANO => "Entregado en mano",
            CIERRE_PERDIDO => "Perdido",
            CIERRE_ABONADO => "Abonado",
            _ => cierre
        };

        /// <summary>Por qué no se puede cerrar todavía (null = se puede).</summary>
        public static string MotivoNoSePuedeCerrar(BultoPendienteModel bulto, string cierre, string motivo)
        {
            if (bulto == null || !bulto.EsPendiente)
            {
                return "Elija un bulto pendiente.";
            }
            if (string.IsNullOrWhiteSpace(cierre))
            {
                return MOTIVO_FALTA_CIERRE;
            }
            if (string.IsNullOrWhiteSpace(motivo))
            {
                return MOTIVO_FALTA_MOTIVO;
            }
            if (motivo.Trim().Length > LONGITUD_MOTIVO)
            {
                return $"El motivo no puede pasar de {LONGITUD_MOTIVO} caracteres.";
            }
            return null;
        }
    }
}
