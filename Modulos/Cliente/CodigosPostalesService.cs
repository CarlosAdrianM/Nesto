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
    /// Nesto#442: mantenimiento de códigos postales por API (patrón NifIncorrectosService del
    /// mismo módulo).
    /// </summary>
    public class CodigosPostalesService : ICodigosPostalesService
    {
        private readonly IConfiguracion configuracion;
        private readonly IServicioAutenticacion _servicioAutenticacion;

        public CodigosPostalesService(IConfiguracion configuracion, IServicioAutenticacion servicioAutenticacion)
        {
            this.configuracion = configuracion;
            _servicioAutenticacion = servicioAutenticacion;
        }

        public async Task<List<CodigoPostalModel>> Buscar(string filtro, string empresa = null)
        {
            using HttpClient client = new();
            client.BaseAddress = new Uri(configuracion.servidorAPI);
            if (!await _servicioAutenticacion.ConfigurarAutorizacion(client))
            {
                throw new UnauthorizedAccessException("No se pudo configurar la autorización");
            }

            string url = $"CodigosPostales?filtro={Uri.EscapeDataString(filtro?.Trim() ?? string.Empty)}";
            if (!string.IsNullOrWhiteSpace(empresa))
            {
                url += $"&empresa={Uri.EscapeDataString(empresa.Trim())}";
            }
            HttpResponseMessage response = await client.GetAsync(url);
            string body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"No se pudieron cargar los códigos postales: {ExtraerMensaje(body)}");
            }
            return JsonConvert.DeserializeObject<List<CodigoPostalModel>>(body) ?? new List<CodigoPostalModel>();
        }

        public async Task<CodigoPostalModel> Guardar(CodigoPostalModel codigoPostal)
        {
            using HttpClient client = new();
            client.BaseAddress = new Uri(configuracion.servidorAPI);
            if (!await _servicioAutenticacion.ConfigurarAutorizacion(client))
            {
                throw new UnauthorizedAccessException("No se pudo configurar la autorización");
            }

            HttpContent contenido = new StringContent(
                JsonConvert.SerializeObject(codigoPostal), Encoding.UTF8, "application/json");
            HttpResponseMessage response = await client.PutAsync("CodigosPostales", contenido);
            string body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"No se pudo guardar el código postal: {ExtraerMensaje(body)}");
            }
            return JsonConvert.DeserializeObject<CodigoPostalModel>(body);
        }

        // Los errores de Web API llegan como {"Message":"..."}: extraer el texto legible.
        private static string ExtraerMensaje(string body)
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
