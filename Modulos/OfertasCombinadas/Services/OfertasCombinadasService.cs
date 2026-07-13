using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.OfertasCombinadas.Interfaces;
using Nesto.Modulos.OfertasCombinadas.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Nesto.Modulos.OfertasCombinadas.Services
{
    public class OfertasCombinadasService : IOfertasCombinadasService
    {
        private readonly IConfiguracion _configuracion;
        private readonly IServicioAutenticacion _servicioAutenticacion;
        private readonly IClienteApiFactory _clienteApiFactory;

        public OfertasCombinadasService(IConfiguracion configuracion, IServicioAutenticacion servicioAutenticacion, IClienteApiFactory clienteApiFactory = null)
        {
            _configuracion = configuracion;
            _servicioAutenticacion = servicioAutenticacion;
            _clienteApiFactory = clienteApiFactory;
        }

        // Nesto#369: usa el HttpClient de la factory (base + JWT centralizados, usuario en ELMAH).
        // Si la factory no está disponible, conserva EXACTAMENTE la autenticación manual de antes.
        private async Task<HttpClient> CrearClienteAutenticado()
        {
            if (_clienteApiFactory != null)
            {
                return _clienteApiFactory.Crear();
            }

            var client = new HttpClient
            {
                BaseAddress = new Uri(_configuracion.servidorAPI)
            };
            await _servicioAutenticacion.ConfigurarAutorizacion(client);
            return client;
        }

        #region Ofertas Combinadas

        public async Task<List<OfertaCombinadaModel>> GetOfertasCombinadas(string empresa, bool soloActivas = false)
        {
            using HttpClient client = await CrearClienteAutenticado();

            string urlConsulta = $"OfertasCombinadas?empresa={empresa}";
            if (soloActivas)
            {
                urlConsulta += "&soloActivas=true";
            }

            HttpResponseMessage response = await client.GetAsync(urlConsulta);

            if (response.IsSuccessStatusCode)
            {
                string contenido = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<List<OfertaCombinadaModel>>(contenido);
            }
            else
            {
                throw new Exception(await GetErrorMessage(response));
            }
        }

        public async Task<OfertaCombinadaModel> CreateOfertaCombinada(OfertaCombinadaCreateModel oferta)
        {
            using HttpClient client = await CrearClienteAutenticado();

            string urlConsulta = $"OfertasCombinadas?usuario={_configuracion.usuario}";
            string jsonContent = JsonConvert.SerializeObject(oferta);
            HttpContent content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            HttpResponseMessage response = await client.PostAsync(urlConsulta, content);

            if (response.IsSuccessStatusCode)
            {
                string contenido = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<OfertaCombinadaModel>(contenido);
            }
            else
            {
                throw new Exception(await GetErrorMessage(response));
            }
        }

        public async Task<OfertaCombinadaModel> UpdateOfertaCombinada(int id, OfertaCombinadaCreateModel oferta)
        {
            using HttpClient client = await CrearClienteAutenticado();

            string urlConsulta = $"OfertasCombinadas/{id}?usuario={_configuracion.usuario}";
            string jsonContent = JsonConvert.SerializeObject(oferta);
            HttpContent content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            HttpResponseMessage response = await client.PutAsync(urlConsulta, content);

            if (response.IsSuccessStatusCode)
            {
                string contenido = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<OfertaCombinadaModel>(contenido);
            }
            else
            {
                throw new Exception(await GetErrorMessage(response));
            }
        }

        public async Task<OfertaCombinadaModel> DeleteOfertaCombinada(int id)
        {
            using HttpClient client = await CrearClienteAutenticado();

            HttpResponseMessage response = await client.DeleteAsync($"OfertasCombinadas/{id}");

            if (response.IsSuccessStatusCode)
            {
                string contenido = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<OfertaCombinadaModel>(contenido);
            }
            else
            {
                throw new Exception(await GetErrorMessage(response));
            }
        }

        // NestoAPI#289: subgrupos para el combo del detalle (el endpoint ya los devuelve
        // ordenados por grupo + descripción).
        public async Task<List<SubgrupoComboModel>> GetSubgrupos()
        {
            using HttpClient client = await CrearClienteAutenticado();

            HttpResponseMessage response = await client.GetAsync("Productos/Subgrupos");

            if (response.IsSuccessStatusCode)
            {
                string contenido = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<List<SubgrupoComboModel>>(contenido);
            }
            else
            {
                throw new Exception(await GetErrorMessage(response));
            }
        }

        #endregion

        #region Ofertas Escalonadas

        public async Task<List<OfertaEscalonadaModel>> GetOfertasEscalonadas(string empresa, bool soloActivas = false)
        {
            using HttpClient client = await CrearClienteAutenticado();

            string urlConsulta = $"OfertasEscalonadas?empresa={empresa}";
            if (soloActivas)
            {
                urlConsulta += "&soloActivas=true";
            }

            HttpResponseMessage response = await client.GetAsync(urlConsulta);

            if (response.IsSuccessStatusCode)
            {
                string contenido = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<List<OfertaEscalonadaModel>>(contenido);
            }
            else
            {
                throw new Exception(await GetErrorMessage(response));
            }
        }

        public async Task<OfertaEscalonadaModel> CreateOfertaEscalonada(OfertaEscalonadaCreateModel oferta)
        {
            using HttpClient client = await CrearClienteAutenticado();

            string urlConsulta = $"OfertasEscalonadas?usuario={_configuracion.usuario}";
            string jsonContent = JsonConvert.SerializeObject(oferta);
            HttpContent content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            HttpResponseMessage response = await client.PostAsync(urlConsulta, content);

            if (response.IsSuccessStatusCode)
            {
                string contenido = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<OfertaEscalonadaModel>(contenido);
            }
            else
            {
                throw new Exception(await GetErrorMessage(response));
            }
        }

        public async Task<OfertaEscalonadaModel> UpdateOfertaEscalonada(int id, OfertaEscalonadaCreateModel oferta)
        {
            using HttpClient client = await CrearClienteAutenticado();

            string urlConsulta = $"OfertasEscalonadas/{id}?usuario={_configuracion.usuario}";
            string jsonContent = JsonConvert.SerializeObject(oferta);
            HttpContent content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            HttpResponseMessage response = await client.PutAsync(urlConsulta, content);

            if (response.IsSuccessStatusCode)
            {
                string contenido = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<OfertaEscalonadaModel>(contenido);
            }
            else
            {
                throw new Exception(await GetErrorMessage(response));
            }
        }

        public async Task<OfertaEscalonadaModel> DeleteOfertaEscalonada(int id)
        {
            using HttpClient client = await CrearClienteAutenticado();

            HttpResponseMessage response = await client.DeleteAsync($"OfertasEscalonadas/{id}");

            if (response.IsSuccessStatusCode)
            {
                string contenido = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<OfertaEscalonadaModel>(contenido);
            }
            else
            {
                throw new Exception(await GetErrorMessage(response));
            }
        }

        #endregion

        #region Ofertas Permitidas por Familia

        public async Task<List<OfertaPermitidaFamiliaModel>> GetOfertasPermitidasFamilia(string empresa)
        {
            using HttpClient client = await CrearClienteAutenticado();

            HttpResponseMessage response = await client.GetAsync($"OfertasPermitidasFamilia?empresa={empresa}");

            if (response.IsSuccessStatusCode)
            {
                string contenido = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<List<OfertaPermitidaFamiliaModel>>(contenido);
            }
            else
            {
                throw new Exception(await GetErrorMessage(response));
            }
        }

        public async Task<OfertaPermitidaFamiliaModel> CreateOfertaPermitidaFamilia(OfertaPermitidaFamiliaCreateModel oferta)
        {
            using HttpClient client = await CrearClienteAutenticado();

            string urlConsulta = $"OfertasPermitidasFamilia?usuario={_configuracion.usuario}";
            string jsonContent = JsonConvert.SerializeObject(oferta);
            HttpContent content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            HttpResponseMessage response = await client.PostAsync(urlConsulta, content);

            if (response.IsSuccessStatusCode)
            {
                string contenido = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<OfertaPermitidaFamiliaModel>(contenido);
            }
            else
            {
                throw new Exception(await GetErrorMessage(response));
            }
        }

        public async Task<OfertaPermitidaFamiliaModel> UpdateOfertaPermitidaFamilia(int nOrden, OfertaPermitidaFamiliaCreateModel oferta)
        {
            using HttpClient client = await CrearClienteAutenticado();

            string urlConsulta = $"OfertasPermitidasFamilia/{nOrden}?usuario={_configuracion.usuario}";
            string jsonContent = JsonConvert.SerializeObject(oferta);
            HttpContent content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            HttpResponseMessage response = await client.PutAsync(urlConsulta, content);

            if (response.IsSuccessStatusCode)
            {
                string contenido = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<OfertaPermitidaFamiliaModel>(contenido);
            }
            else
            {
                throw new Exception(await GetErrorMessage(response));
            }
        }

        public async Task<OfertaPermitidaFamiliaModel> DeleteOfertaPermitidaFamilia(int nOrden)
        {
            using HttpClient client = await CrearClienteAutenticado();

            HttpResponseMessage response = await client.DeleteAsync($"OfertasPermitidasFamilia/{nOrden}");

            if (response.IsSuccessStatusCode)
            {
                string contenido = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<OfertaPermitidaFamiliaModel>(contenido);
            }
            else
            {
                throw new Exception(await GetErrorMessage(response));
            }
        }

        #endregion

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
