using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.Cliente.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Nesto.Modulos.Cliente
{
    /// <summary>Nesto#618 (NestoAPI#786): POST api/NotasEntrega/Cobro y GET api/RepartoPropio/Repartidores.</summary>
    public class NotaCobroService : INotaCobroService
    {
        private readonly IConfiguracion _configuracion;
        private readonly IServicioAutenticacion _servicioAutenticacion;

        public NotaCobroService(IConfiguracion configuracion, IServicioAutenticacion servicioAutenticacion)
        {
            _configuracion = configuracion;
            _servicioAutenticacion = servicioAutenticacion;
        }

        public async Task<NotaCobroCreadaModel> CrearNotaCobro(CrearNotaCobroModel nota)
        {
            using (HttpClient client = new HttpClient())
            {
                client.BaseAddress = new Uri(_configuracion.servidorAPI);
                if (!await _servicioAutenticacion.ConfigurarAutorizacion(client))
                {
                    throw new UnauthorizedAccessException("No se pudo configurar la autorización");
                }

                HttpContent contenido = new StringContent(JsonConvert.SerializeObject(nota), Encoding.UTF8, "application/json");
                HttpResponseMessage response = await client.PostAsync("NotasEntrega/Cobro", contenido);
                string body = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    string mensaje = ExtraerMensaje(body);
                    if (string.IsNullOrWhiteSpace(mensaje))
                    {
                        mensaje = response.StatusCode == HttpStatusCode.Forbidden
                            ? "No tiene permiso para crear notas de cobro."
                            : $"No se ha podido crear la nota de cobro ({(int)response.StatusCode}).";
                    }
                    throw new Exception(mensaje);
                }
                return JsonConvert.DeserializeObject<NotaCobroCreadaModel>(body);
            }
        }

        public async Task<List<RepartidorModel>> LeerRepartidores()
        {
            using (HttpClient client = new HttpClient())
            {
                client.BaseAddress = new Uri(_configuracion.servidorAPI);
                if (!await _servicioAutenticacion.ConfigurarAutorizacion(client))
                {
                    throw new UnauthorizedAccessException("No se pudo configurar la autorización");
                }

                HttpResponseMessage response = await client.GetAsync("RepartoPropio/Repartidores");
                string body = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    string mensaje = ExtraerMensaje(body);
                    throw new Exception(string.IsNullOrWhiteSpace(mensaje)
                        ? $"No se han podido leer los repartidores ({(int)response.StatusCode})."
                        : mensaje);
                }
                return JsonConvert.DeserializeObject<List<RepartidorModel>>(body) ?? new List<RepartidorModel>();
            }
        }

        /// <summary>Los errores de la API llegan como {"Message": "…"} (o {"error": {"message": "…"}}): el texto legible, tal cual.</summary>
        internal static string ExtraerMensaje(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return string.Empty;
            }
            try
            {
                JObject json = JsonConvert.DeserializeObject<JObject>(body);
                string mensaje = json?["Message"]?.ToString();
                if (string.IsNullOrWhiteSpace(mensaje))
                {
                    mensaje = (json?["error"] as JObject)?["message"]?.ToString();
                }
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
