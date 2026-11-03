using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.Cliente.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Nesto.Modulos.Cliente
{
    /// <summary>Nesto#606: la API todavía no tiene api/Clientes/DiaReparto (404). La ficha esconde el control, sin error.</summary>
    public class DiaRepartoNoDisponibleException : Exception
    {
        public DiaRepartoNoDisponibleException() : base("La API todavía no tiene el día de reparto propio.") { }
    }

    /// <summary>
    /// Nesto#606 (NestoAPI#799): el «día de reparto propio» del cliente/contacto, excepción al día que le toca por su código postal.
    /// </summary>
    public interface IDiaRepartoClienteService
    {
        /// <summary>La excepción del contacto, o null si no tiene (204). Lanza <see cref="DiaRepartoNoDisponibleException"/> si es la API vieja.</summary>
        Task<DiaRepartoClienteModel> Leer(string empresa, string cliente, string contacto);
        /// <summary><paramref name="ruta"/> «01».. «05», o null para quitarla. Lanza con el texto de la API (400/403) o <see cref="DiaRepartoNoDisponibleException"/> (404).</summary>
        Task<DiaRepartoGuardadoModel> Guardar(string empresa, string cliente, string contacto, string ruta, string motivo);
    }

    /// <summary>Nesto#606: por HTTP contra NestoAPI (patrón HorarioPreferibleService del mismo módulo).</summary>
    public class DiaRepartoClienteService : IDiaRepartoClienteService
    {
        public const string MENSAJE_SIN_PERMISO = "No tiene permiso para cambiar el día de reparto propio: solo pueden hacerlo Dirección, Almacén y Administración.";

        private readonly IConfiguracion configuracion;
        private readonly IServicioAutenticacion _servicioAutenticacion;

        public DiaRepartoClienteService(IConfiguracion configuracion, IServicioAutenticacion servicioAutenticacion)
        {
            this.configuracion = configuracion;
            _servicioAutenticacion = servicioAutenticacion;
        }

        private async Task<HttpClient> Cliente()
        {
            var client = new HttpClient { BaseAddress = new Uri(configuracion.servidorAPI) };
            if (!await _servicioAutenticacion.ConfigurarAutorizacion(client))
            {
                client.Dispose();
                throw new UnauthorizedAccessException("No se pudo configurar la autorización");
            }
            return client;
        }

        internal static string UrlLeer(string empresa, string cliente, string contacto)
            => $"Clientes/DiaReparto?empresa={Uri.EscapeDataString(empresa?.Trim() ?? "1")}&cliente={Uri.EscapeDataString(cliente?.Trim() ?? string.Empty)}" +
               $"&contacto={Uri.EscapeDataString(string.IsNullOrWhiteSpace(contacto) ? "0" : contacto.Trim())}";

        public async Task<DiaRepartoClienteModel> Leer(string empresa, string cliente, string contacto)
        {
            using HttpClient client = await Cliente();
            HttpResponseMessage response = await client.GetAsync(UrlLeer(empresa, cliente, contacto));
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                throw new DiaRepartoNoDisponibleException();
            }
            if (response.StatusCode == HttpStatusCode.NoContent)
            {
                return null;
            }
            string body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"No se ha podido leer el día de reparto propio: {ExtraerMensaje(body)}");
            }
            return string.IsNullOrWhiteSpace(body) ? null : JsonConvert.DeserializeObject<DiaRepartoClienteModel>(body);
        }

        public async Task<DiaRepartoGuardadoModel> Guardar(string empresa, string cliente, string contacto, string ruta, string motivo)
        {
            using HttpClient client = await Cliente();
            var cuerpo = new GuardarDiaRepartoClienteModel
            {
                Empresa = empresa?.Trim(),
                Cliente = cliente?.Trim(),
                Contacto = string.IsNullOrWhiteSpace(contacto) ? "0" : contacto.Trim(),
                Ruta = string.IsNullOrWhiteSpace(ruta) ? null : ruta.Trim(),
                Motivo = string.IsNullOrWhiteSpace(motivo) ? null : motivo.Trim()
            };
            HttpResponseMessage response = await client.PutAsync("Clientes/DiaReparto",
                new StringContent(JsonConvert.SerializeObject(cuerpo), Encoding.UTF8, "application/json"));
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                throw new DiaRepartoNoDisponibleException();
            }
            string body = await response.Content.ReadAsStringAsync();
            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                string mensaje = ExtraerMensaje(body);
                throw new UnauthorizedAccessException(string.IsNullOrWhiteSpace(mensaje) ? MENSAJE_SIN_PERMISO : mensaje);
            }
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception(ExtraerMensaje(body));
            }
            return JsonConvert.DeserializeObject<DiaRepartoGuardadoModel>(body) ?? new DiaRepartoGuardadoModel { Ruta = cuerpo.Ruta };
        }

        // Los errores de Web API llegan como {"Message":"..."} o como una cadena JSON: extraer el texto legible.
        internal static string ExtraerMensaje(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return body;
            }
            try
            {
                JToken json = JToken.Parse(body);
                if (json.Type == JTokenType.String)
                {
                    return json.ToString();
                }
                string mensaje = json["Message"]?.ToString();
                if (!string.IsNullOrWhiteSpace(mensaje))
                {
                    return mensaje;
                }
            }
            catch
            {
            }
            return body;
        }
    }
}
