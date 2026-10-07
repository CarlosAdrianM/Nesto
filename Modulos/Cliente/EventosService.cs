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

        private async Task<string> Enviar(Func<HttpClient, Task<HttpResponseMessage>> llamada, string mensajeError)
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
                throw new Exception($"{mensajeError}: {ExtraerMensaje(body)}");
            }
            return body;
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
