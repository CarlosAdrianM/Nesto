using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.Ganavisiones.Interfaces;
using Nesto.Modulos.Ganavisiones.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Nesto.Modulos.Ganavisiones.Services
{
    public class GanavisionesService : IGanavisionesService
    {
        private readonly IConfiguracion _configuracion;
        private readonly IServicioAutenticacion _servicioAutenticacion;

        public GanavisionesService(IConfiguracion configuracion, IServicioAutenticacion servicioAutenticacion)
        {
            _configuracion = configuracion;
            _servicioAutenticacion = servicioAutenticacion;
        }

        private async Task<HttpClient> CrearClienteAutenticado()
        {
            var client = new HttpClient
            {
                BaseAddress = new Uri(_configuracion.servidorAPI)
            };
            await _servicioAutenticacion.ConfigurarAutorizacion(client);
            return client;
        }

        public async Task<List<GanavisionModel>> GetGanavisiones(string empresa, string productoId = null, bool soloActivos = false)
        {
            using HttpClient client = await CrearClienteAutenticado();

            string urlConsulta = $"Ganavisiones?empresa={empresa}";
            if (!string.IsNullOrEmpty(productoId))
            {
                urlConsulta += $"&productoId={productoId}";
            }
            if (soloActivos)
            {
                urlConsulta += "&soloActivos=true";
            }

            HttpResponseMessage response = await client.GetAsync(urlConsulta);

            if (response.IsSuccessStatusCode)
            {
                string contenido = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<List<GanavisionModel>>(contenido);
            }
            else
            {
                throw new Exception(await GetErrorMessage(response));
            }
        }

        public async Task<GanavisionModel> GetGanavision(int id)
        {
            using HttpClient client = await CrearClienteAutenticado();

            HttpResponseMessage response = await client.GetAsync($"Ganavisiones/{id}");

            if (response.IsSuccessStatusCode)
            {
                string contenido = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<GanavisionModel>(contenido);
            }
            else
            {
                throw new Exception(await GetErrorMessage(response));
            }
        }

        public async Task<GanavisionModel> CreateGanavision(GanavisionCreateModel ganavision)
        {
            using HttpClient client = await CrearClienteAutenticado();

            string urlConsulta = $"Ganavisiones?usuario={_configuracion.usuario}";
            string jsonContent = JsonConvert.SerializeObject(ganavision);
            HttpContent content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            HttpResponseMessage response = await client.PostAsync(urlConsulta, content);

            if (response.IsSuccessStatusCode)
            {
                string contenido = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<GanavisionModel>(contenido);
            }
            else
            {
                throw new Exception(await GetErrorMessage(response));
            }
        }

        public async Task<GanavisionModel> UpdateGanavision(int id, GanavisionCreateModel ganavision)
        {
            using HttpClient client = await CrearClienteAutenticado();

            string urlConsulta = $"Ganavisiones/{id}?usuario={_configuracion.usuario}";
            string jsonContent = JsonConvert.SerializeObject(ganavision);
            HttpContent content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            HttpResponseMessage response = await client.PutAsync(urlConsulta, content);

            if (response.IsSuccessStatusCode)
            {
                string contenido = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<GanavisionModel>(contenido);
            }
            else
            {
                throw new Exception(await GetErrorMessage(response));
            }
        }

        public async Task<GanavisionModel> DeleteGanavision(int id)
        {
            using HttpClient client = await CrearClienteAutenticado();

            HttpResponseMessage response = await client.DeleteAsync($"Ganavisiones/{id}");

            if (response.IsSuccessStatusCode)
            {
                string contenido = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<GanavisionModel>(contenido);
            }
            else
            {
                throw new Exception(await GetErrorMessage(response));
            }
        }

        private static async Task<string> GetErrorMessage(HttpResponseMessage response)
        {
            string textoError = await response.Content.ReadAsStringAsync();
            try
            {
                JObject requestException = JsonConvert.DeserializeObject<JObject>(textoError);
                string errorMostrar = "Error: ";
                if (requestException?["Message"] != null)
                {
                    errorMostrar += requestException["Message"];
                }
                else if (requestException?["message"] != null)
                {
                    errorMostrar += requestException["message"];
                }
                else if (requestException?["exceptionMessage"] != null)
                {
                    errorMostrar += requestException["exceptionMessage"];
                }
                else
                {
                    errorMostrar += textoError;
                }
                return errorMostrar;
            }
            catch
            {
                return textoError;
            }
        }
    }
}
