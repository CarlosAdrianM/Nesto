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
    /// Nesto#419: extracto de cliente por API (patrón ClienteService del mismo módulo).
    /// </summary>
    public class ExtractoClienteService : IExtractoClienteService
    {
        private readonly IConfiguracion configuracion;
        private readonly IServicioAutenticacion _servicioAutenticacion;

        public ExtractoClienteService(IConfiguracion configuracion, IServicioAutenticacion servicioAutenticacion)
        {
            this.configuracion = configuracion;
            _servicioAutenticacion = servicioAutenticacion;
        }

        public async Task<List<ExtractoClienteModel>> LeerExtractoPendiente(string cliente)
        {
            using (HttpClient client = new HttpClient())
            {
                client.BaseAddress = new Uri(configuracion.servidorAPI);
                if (!await _servicioAutenticacion.ConfigurarAutorizacion(client))
                {
                    throw new UnauthorizedAccessException("No se pudo configurar la autorización");
                }

                HttpResponseMessage response = await client.GetAsync(
                    $"ExtractosCliente?cliente={Uri.EscapeDataString(cliente?.Trim() ?? string.Empty)}");
                string body = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"No se pudo cargar el extracto del cliente {cliente?.Trim()}: {ExtraerMensaje(body)}");
                }
                return JsonConvert.DeserializeObject<List<ExtractoClienteModel>>(body)
                    ?? new List<ExtractoClienteModel>();
            }
        }

        public async Task<ResultadoLiquidacionModel> LiquidarEfectos(string empresa, int origen, int destino)
        {
            using (HttpClient client = new HttpClient())
            {
                client.BaseAddress = new Uri(configuracion.servidorAPI);
                if (!await _servicioAutenticacion.ConfigurarAutorizacion(client))
                {
                    throw new UnauthorizedAccessException("No se pudo configurar la autorización");
                }

                HttpContent contenido = new StringContent(
                    JsonConvert.SerializeObject(new { Empresa = empresa, Origen = origen, Destino = destino }),
                    Encoding.UTF8, "application/json");
                HttpResponseMessage response = await client.PostAsync("ExtractosCliente/Liquidar", contenido);
                string body = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    // Los BadRequest del endpoint traen el motivo de negocio legible
                    // (validaciones de prdLiquidar adelantadas en C#, NestoAPI#333).
                    throw new Exception(ExtraerMensaje(body));
                }
                return JsonConvert.DeserializeObject<ResultadoLiquidacionModel>(body);
            }
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
