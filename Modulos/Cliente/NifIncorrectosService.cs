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
    /// Nesto#417: clientes con NIF incorrecto por API (patrón ExtractoClienteService del
    /// mismo módulo).
    /// </summary>
    public class NifIncorrectosService : INifIncorrectosService
    {
        private readonly IConfiguracion configuracion;
        private readonly IServicioAutenticacion _servicioAutenticacion;

        public NifIncorrectosService(IConfiguracion configuracion, IServicioAutenticacion servicioAutenticacion)
        {
            this.configuracion = configuracion;
            _servicioAutenticacion = servicioAutenticacion;
        }

        public async Task<List<ClienteNifIncorrectoModel>> LeerNifIncorrectos(string vendedor)
        {
            using (HttpClient client = new HttpClient())
            {
                client.BaseAddress = new Uri(configuracion.servidorAPI);
                if (!await _servicioAutenticacion.ConfigurarAutorizacion(client))
                {
                    throw new UnauthorizedAccessException("No se pudo configurar la autorización");
                }

                string url = "Clientes/NifIncorrectos";
                if (!string.IsNullOrWhiteSpace(vendedor))
                {
                    url += $"?vendedor={Uri.EscapeDataString(vendedor.Trim())}";
                }
                HttpResponseMessage response = await client.GetAsync(url);
                string body = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"No se pudo cargar la lista de NIF incorrectos: {ExtraerMensaje(body)}");
                }
                return JsonConvert.DeserializeObject<List<ClienteNifIncorrectoModel>>(body)
                    ?? new List<ClienteNifIncorrectoModel>();
            }
        }

        public async Task<ResultadoCorreccionNifModel> CorregirNif(string cliente, string nif)
        {
            using (HttpClient client = new HttpClient())
            {
                client.BaseAddress = new Uri(configuracion.servidorAPI);
                if (!await _servicioAutenticacion.ConfigurarAutorizacion(client))
                {
                    throw new UnauthorizedAccessException("No se pudo configurar la autorización");
                }

                HttpContent contenido = new StringContent(
                    JsonConvert.SerializeObject(new { Cliente = cliente?.Trim(), Nif = nif?.Trim() }),
                    Encoding.UTF8, "application/json");
                HttpResponseMessage response = await client.PostAsync("Clientes/CorregirNif", contenido);
                string body = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    // Los BadRequest traen el motivo legible (p. ej. "La AEAT no reconoce el
                    // NIF X para 'NOMBRE'... No se ha modificado nada.")
                    throw new Exception(ExtraerMensaje(body));
                }
                return JsonConvert.DeserializeObject<ResultadoCorreccionNifModel>(body);
            }
        }

        // NestoAPI#339: pasaportes y demás identificaciones extranjeras.
        public async Task<ResultadoCorreccionNifModel> MarcarIdentificacionExtranjera(string cliente, string tipoIdentificacion, string pais, string nifNuevo = null)
        {
            using (HttpClient client = new HttpClient())
            {
                client.BaseAddress = new Uri(configuracion.servidorAPI);
                if (!await _servicioAutenticacion.ConfigurarAutorizacion(client))
                {
                    throw new UnauthorizedAccessException("No se pudo configurar la autorización");
                }

                HttpContent contenido = new StringContent(
                    JsonConvert.SerializeObject(new
                    {
                        Cliente = cliente?.Trim(),
                        TipoIdentificacion = tipoIdentificacion?.Trim(),
                        Pais = pais?.Trim(),
                        // NestoAPI#356: NIF-IVA extranjero completo (opcional): si se indica, se
                        // propaga a fichas y facturas sin declarar (el char(9) antiguo lo truncaba).
                        Nif = string.IsNullOrWhiteSpace(nifNuevo) ? null : nifNuevo.Trim()
                    }),
                    Encoding.UTF8, "application/json");
                HttpResponseMessage response = await client.PostAsync("Clientes/MarcarIdentificacionExtranjera", contenido);
                string body = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception(ExtraerMensaje(body));
                }
                return JsonConvert.DeserializeObject<ResultadoCorreccionNifModel>(body);
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
