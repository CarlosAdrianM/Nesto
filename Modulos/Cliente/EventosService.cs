using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Modulos.Cliente.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Nesto.Modulos.Cliente
{
    /// <summary>NestoAPI#591: eventos y señales por API (patrón CodigosPostalesService del mismo módulo).</summary>
    public class EventosService : IEventosService
    {
        private readonly IConfiguracion configuracion;
        private readonly IServicioAutenticacion _servicioAutenticacion;

        public EventosService(IConfiguracion configuracion, IServicioAutenticacion servicioAutenticacion)
        {
            this.configuracion = configuracion;
            _servicioAutenticacion = servicioAutenticacion;
        }

        public async Task<List<EventoModel>> LeerEventos(bool soloActivos)
        {
            string body = await Enviar(c => c.GetAsync($"Eventos?empresa={Constantes.Empresas.EMPRESA_DEFECTO}&soloActivos={soloActivos.ToString().ToLowerInvariant()}"),
                "No se pudieron cargar los eventos");
            return JsonConvert.DeserializeObject<List<EventoModel>>(body) ?? new List<EventoModel>();
        }

        public async Task<EventoModel> GuardarEvento(EventoModel evento)
        {
            evento.Empresa ??= Constantes.Empresas.EMPRESA_DEFECTO;
            string body = await Enviar(c => evento.Id > 0
                    ? c.PutAsync($"Eventos/{evento.Id}", Json(evento))
                    : c.PostAsync("Eventos", Json(evento)),
                "No se pudo guardar el evento");
            return JsonConvert.DeserializeObject<EventoModel>(body);
        }

        public async Task<List<SenalEventoModel>> LeerSenales(string estado, int? eventoId)
        {
            string url = $"Eventos/Senales?empresa={Constantes.Empresas.EMPRESA_DEFECTO}";
            if (!string.IsNullOrWhiteSpace(estado))
            {
                url += $"&estado={Uri.EscapeDataString(estado)}";
            }
            if (eventoId.HasValue)
            {
                url += $"&eventoId={eventoId.Value}";
            }
            string body = await Enviar(c => c.GetAsync(url), "No se pudieron cargar las señales de los eventos");
            return JsonConvert.DeserializeObject<List<SenalEventoModel>>(body) ?? new List<SenalEventoModel>();
        }

        public async Task<List<SenalEventoModel>> LeerSenalesCliente(string cliente)
        {
            string body = await Enviar(c => c.GetAsync(
                    $"Eventos/Senales/Cliente?empresa={Constantes.Empresas.EMPRESA_DEFECTO}&cliente={Uri.EscapeDataString(cliente?.Trim() ?? string.Empty)}"),
                "No se pudieron cargar las señales del cliente");
            return JsonConvert.DeserializeObject<List<SenalEventoModel>>(body) ?? new List<SenalEventoModel>();
        }

        public async Task<SenalEventoModel> MarcarSenal(int eventoId, MarcarSenalEventoModel peticion)
        {
            string body = await Enviar(c => c.PostAsync($"Eventos/{eventoId}/Senales", Json(peticion)), "No se pudo marcar la señal");
            return JsonConvert.DeserializeObject<SenalEventoModel>(body);
        }

        public async Task QuitarSenal(int id)
        {
            _ = await Enviar(c => c.DeleteAsync($"Eventos/Senales/{id}"), "No se pudo quitar la señal");
        }

        // ---------------- Nesto#517: inscripciones ----------------

        public async Task<List<InscripcionEventoModel>> LeerInscripciones(int eventoId)
        {
            string body = await Enviar(c => c.GetAsync($"Eventos/{eventoId}/Inscripciones"),
                "No se pudieron cargar los inscritos del evento", vacioSiApiVieja: true);
            return Lista<InscripcionEventoModel>(body);
        }

        public async Task<List<InscripcionEventoModel>> LeerInscripcionesCliente(string empresa, string cliente)
        {
            string body = await Enviar(c => c.GetAsync(UrlInscripcionesCliente(empresa, cliente)),
                "No se pudieron cargar las inscripciones del cliente", vacioSiApiVieja: true);
            return Lista<InscripcionEventoModel>(body);
        }

        /// <summary>«Clientes/1/15191/Inscripciones» (sin empresa, la de por defecto).</summary>
        public static string UrlInscripcionesCliente(string empresa, string cliente)
        {
            string empresaUrl = Uri.EscapeDataString(string.IsNullOrWhiteSpace(empresa) ? Constantes.Empresas.EMPRESA_DEFECTO : empresa.Trim());
            string clienteUrl = Uri.EscapeDataString(cliente?.Trim() ?? string.Empty);
            return $"Clientes/{empresaUrl}/{clienteUrl}/Inscripciones";
        }

        public async Task<InscripcionEventoModel> Inscribir(int eventoId, InscribirEventoModel peticion)
        {
            peticion.Empresa ??= Constantes.Empresas.EMPRESA_DEFECTO;
            string body = await Enviar(c => c.PostAsync($"Eventos/{eventoId}/Inscripciones", Json(peticion)), "No se pudo inscribir al cliente");
            return JsonConvert.DeserializeObject<InscripcionEventoModel>(body);
        }

        public async Task<InscripcionEventoModel> AnularInscripcion(int id)
        {
            string body = await Enviar(c => c.DeleteAsync($"Eventos/Inscripciones/{id}"), "No se pudo anular la inscripción");
            return JsonConvert.DeserializeObject<InscripcionEventoModel>(body);
        }

        public async Task<InscripcionEventoModel> MarcarPagada(int id, MarcarPagoInscripcionModel peticion)
        {
            string body = await Enviar(c => c.PostAsync($"Eventos/Inscripciones/{id}/Pago", Json(peticion)),
                "No se pudo marcar la inscripción como pagada");
            return JsonConvert.DeserializeObject<InscripcionEventoModel>(body);
        }

        public async Task<List<InscripcionEventoModel>> PasarLista(int eventoId, IEnumerable<int> asistentes)
        {
            var peticion = new AsistenciaEventoModel { Asistentes = new List<int>(asistentes ?? Array.Empty<int>()) };
            string body = await Enviar(c => c.PostAsync($"Eventos/{eventoId}/Asistencia", Json(peticion)), "No se pudo guardar la lista de asistencia");
            return Lista<InscripcionEventoModel>(body);
        }

        public async Task<InscripcionEventoModel> FacturarCurso(int id)
        {
            string body = await Enviar(c => c.PostAsync($"Eventos/Inscripciones/{id}/Facturar", Json(new { })), "No se pudo facturar el curso");
            return JsonConvert.DeserializeObject<InscripcionEventoModel>(body);
        }

        public async Task<ProductoCursoModel> LeerProductoCurso(string producto)
        {
            if (string.IsNullOrWhiteSpace(producto))
            {
                return null;
            }
            string body = await Enviar(c => c.GetAsync(
                    $"Productos?empresa={Constantes.Empresas.EMPRESA_DEFECTO}&id={Uri.EscapeDataString(producto.Trim())}"),
                "No se pudo leer la referencia del curso", vacioSiNoExiste: true);
            return string.IsNullOrWhiteSpace(body) ? null : JsonConvert.DeserializeObject<ProductoCursoModel>(body);
        }

        private static List<T> Lista<T>(string body) =>
            string.IsNullOrWhiteSpace(body) ? new List<T>() : JsonConvert.DeserializeObject<List<T>>(body) ?? new List<T>();

        /// <param name="vacioSiApiVieja">Nesto#517: con una API sin la ruta (el 404 de Web API) devuelve "" en vez de fallar.</param>
        /// <param name="vacioSiNoExiste">Cualquier 404 devuelve "" (lo que se busca no existe).</param>
        private async Task<string> Enviar(Func<HttpClient, Task<HttpResponseMessage>> llamada, string mensajeError,
            bool vacioSiApiVieja = false, bool vacioSiNoExiste = false)
        {
            using HttpClient client = new();
            client.BaseAddress = new Uri(configuracion.servidorAPI);
            if (!await _servicioAutenticacion.ConfigurarAutorizacion(client))
            {
                throw new UnauthorizedAccessException("No se pudo configurar la autorización");
            }
            HttpResponseMessage response = await llamada(client);
            string body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                int estado = (int)response.StatusCode;
                bool apiVieja = EsApiSinLaRuta(estado, body);
                if (estado == 404 && (vacioSiNoExiste || (vacioSiApiVieja && apiVieja)))
                {
                    return string.Empty;
                }
                if (apiVieja)
                {
                    throw new Exception($"{mensajeError}: la API todavía no tiene las inscripciones a eventos. Avise a Informática.");
                }
                throw new Exception($"{mensajeError}: {ExtraerMensaje(body)}");
            }
            return body;
        }

        /// <summary>
        /// Nesto#517: ¿es el 404 de Web API de una ruta que no existe (una API anterior a NestoAPI#611)? Un 404 de negocio
        /// («No existe el evento 5») trae su mensaje y no cuenta.
        /// </summary>
        public static bool EsApiSinLaRuta(int estado, string body)
        {
            if (estado != 404)
            {
                return false;
            }
            if (string.IsNullOrWhiteSpace(body))
            {
                return true;
            }
            return body.IndexOf("No HTTP resource was found", StringComparison.OrdinalIgnoreCase) >= 0
                || body.IndexOf("No se encontró ningún recurso HTTP", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static StringContent Json(object objeto)
            => new(JsonConvert.SerializeObject(objeto), Encoding.UTF8, "application/json");

        // Errores de negocio: {"error":{"message":"..."}}; 403 y BadRequest de Web API: {"Message":"..."}.
        internal static string ExtraerMensaje(string body)
        {
            try
            {
                JObject json = JsonConvert.DeserializeObject<JObject>(body);
                string mensaje = (json?["error"] as JObject)?["message"]?.ToString() ?? json?["Message"]?.ToString();
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
