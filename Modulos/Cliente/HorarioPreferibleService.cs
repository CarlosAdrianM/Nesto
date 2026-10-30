using Nesto.Infrastructure.Contracts;
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
    /// <summary>
    /// Nesto#584 (NestoAPI#763): el «horario preferible» de entrega del cliente/contacto y las propuestas sacadas de los
    /// comentarios. Se intenta respetar, pero sin compromiso.
    /// </summary>
    public interface IHorarioPreferibleService
    {
        Task<HorarioPreferibleModel> Leer(string empresa, string cliente, string contacto);
        /// <summary>Sustituye el horario (sin franjas, lo quita). Lanza con el mensaje de la API si no se puede.</summary>
        Task<HorarioPreferibleModel> Guardar(string empresa, string cliente, string contacto, List<FranjaHorarioPreferibleModel> franjas);
        Task<List<PropuestaHorarioPreferibleModel>> Propuestas();
        /// <summary>Acepta la propuesta con el horario <paramref name="texto"/> («L-V 10:00-14:00»), o con lo propuesto si va vacío.</summary>
        Task<HorarioPreferibleModel> Aceptar(int id, string texto);
        Task Descartar(int id);
    }

    /// <summary>Nesto#584: por HTTP contra NestoAPI (patrón CodigosPostalesService del mismo módulo).</summary>
    public class HorarioPreferibleService : IHorarioPreferibleService
    {
        private readonly IConfiguracion configuracion;
        private readonly IServicioAutenticacion _servicioAutenticacion;

        public HorarioPreferibleService(IConfiguracion configuracion, IServicioAutenticacion servicioAutenticacion)
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

        internal static string Url(string empresa, string cliente, string contacto)
            => $"Clientes/{Uri.EscapeDataString(empresa?.Trim() ?? "1")}/{Uri.EscapeDataString(cliente?.Trim() ?? string.Empty)}/" +
               $"{Uri.EscapeDataString(string.IsNullOrWhiteSpace(contacto) ? "0" : contacto.Trim())}/HorarioPreferible";

        public async Task<HorarioPreferibleModel> Leer(string empresa, string cliente, string contacto)
        {
            using HttpClient client = await Cliente();
            HttpResponseMessage response = await client.GetAsync(Url(empresa, cliente, contacto));
            string body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"No se ha podido leer el horario preferible: {ExtraerMensaje(body)}");
            }
            return JsonConvert.DeserializeObject<HorarioPreferibleModel>(body);
        }

        public async Task<HorarioPreferibleModel> Guardar(string empresa, string cliente, string contacto, List<FranjaHorarioPreferibleModel> franjas)
        {
            using HttpClient client = await Cliente();
            var cuerpo = new HorarioPreferibleModel { Franjas = franjas ?? new List<FranjaHorarioPreferibleModel>() };
            HttpResponseMessage response = await client.PutAsync(Url(empresa, cliente, contacto),
                new StringContent(JsonConvert.SerializeObject(cuerpo), Encoding.UTF8, "application/json"));
            string body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception(ExtraerMensaje(body));
            }
            return JsonConvert.DeserializeObject<HorarioPreferibleModel>(body);
        }

        public async Task<List<PropuestaHorarioPreferibleModel>> Propuestas()
        {
            using HttpClient client = await Cliente();
            HttpResponseMessage response = await client.GetAsync("Clientes/HorariosPreferibles/Propuestas");
            string body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"No se han podido leer los horarios por revisar: {ExtraerMensaje(body)}");
            }
            return JsonConvert.DeserializeObject<List<PropuestaHorarioPreferibleModel>>(body) ?? new List<PropuestaHorarioPreferibleModel>();
        }

        public async Task<HorarioPreferibleModel> Aceptar(int id, string texto)
        {
            using HttpClient client = await Cliente();
            string json = JsonConvert.SerializeObject(new { Texto = texto?.Trim() });
            HttpResponseMessage response = await client.PostAsync($"Clientes/HorariosPreferibles/Propuestas/{id}/Aceptar",
                new StringContent(json, Encoding.UTF8, "application/json"));
            string body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception(ExtraerMensaje(body));
            }
            return JsonConvert.DeserializeObject<HorarioPreferibleModel>(body);
        }

        public async Task Descartar(int id)
        {
            using HttpClient client = await Cliente();
            HttpResponseMessage response = await client.PostAsync($"Clientes/HorariosPreferibles/Propuestas/{id}/Descartar",
                new StringContent(string.Empty, Encoding.UTF8, "application/json"));
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception(ExtraerMensaje(await response.Content.ReadAsStringAsync()));
            }
        }

        // Los errores de Web API llegan como {"Message":"..."}: extraer el texto legible.
        internal static string ExtraerMensaje(string body)
        {
            try
            {
                JObject json = JsonConvert.DeserializeObject<JObject>(body);
                string mensaje = json?["Message"]?.ToString();
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
