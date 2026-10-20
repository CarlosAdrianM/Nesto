using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Nesto.Infrastructure.Services
{
    /// <summary>
    /// Nesto#537 (épica NestoAPI#669, API NestoAPI#670): los productos que el vendedor puede ofrecer desde la plantilla y lo
    /// que contesta en cada ficha. Es una ayuda: si la API falla, tarda o es anterior al endpoint, devuelve null y la
    /// plantilla sigue igual (sin diálogo ni ELMAH).
    /// </summary>
    public interface IServicioSugerenciasProductos
    {
        /// <summary>POST api/SugerenciasProductos/Calcular con el carrito de la plantilla. Null si no se puede.</summary>
        Task<SugerenciasProductosDTO> Calcular(CalcularSugerenciasProductosDTO peticion);

        /// <summary>
        /// POST api/SugerenciasProductos/{idSugerencia}/Respuesta («Anadido», «NoLeInteresa» o «DescartadoVendedor»). Con las dos
        /// últimas trae la sustituta. Null si no se puede.
        /// </summary>
        Task<ResultadoRespuestaSugerenciaProductoDTO> Responder(int idSugerencia, string respuesta, List<ProductoCarritoSugerenciaDTO> productos);
    }

    public class ServicioSugerenciasProductos : IServicioSugerenciasProductos
    {
        /// <summary>Si tarda más, el panel no sale: la plantilla no espera a nadie.</summary>
        public static readonly TimeSpan TIEMPO_MAXIMO = TimeSpan.FromSeconds(8);

        private readonly IClienteApiFactory _clienteApiFactory;

        public ServicioSugerenciasProductos(IClienteApiFactory clienteApiFactory)
        {
            _clienteApiFactory = clienteApiFactory;
        }

        public async Task<SugerenciasProductosDTO> Calcular(CalcularSugerenciasProductosDTO peticion)
        {
            if (peticion == null || string.IsNullOrWhiteSpace(peticion.Cliente))
            {
                return null;
            }
            return await Post<SugerenciasProductosDTO>("SugerenciasProductos/Calcular", peticion).ConfigureAwait(false);
        }

        public async Task<ResultadoRespuestaSugerenciaProductoDTO> Responder(int idSugerencia, string respuesta, List<ProductoCarritoSugerenciaDTO> productos)
        {
            if (idSugerencia <= 0 || string.IsNullOrWhiteSpace(respuesta))
            {
                return null;
            }
            var cuerpo = new RespuestaSugerenciaProductoDTO { Respuesta = respuesta, Productos = productos };
            return await Post<ResultadoRespuestaSugerenciaProductoDTO>($"SugerenciasProductos/{idSugerencia}/Respuesta", cuerpo).ConfigureAwait(false);
        }

        private async Task<T> Post<T>(string url, object cuerpo) where T : class
        {
            try
            {
                using (HttpClient client = _clienteApiFactory.Crear())
                {
                    client.Timeout = TIEMPO_MAXIMO;
                    var contenido = new StringContent(JsonConvert.SerializeObject(cuerpo), Encoding.UTF8, "application/json");
                    HttpResponseMessage response = await client.PostAsync(url, contenido).ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode)
                    {
                        return null;
                    }
                    string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    return JsonConvert.DeserializeObject<T>(json);
                }
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
