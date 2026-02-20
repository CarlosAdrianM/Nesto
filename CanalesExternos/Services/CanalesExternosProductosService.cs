using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Modulos.CanalesExternos.Interfaces;
using Nesto.Modulos.CanalesExternos.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Nesto.Modulos.CanalesExternos.Services
{
    public class CanalesExternosProductosService : ICanalesExternosProductosService
    {
        private readonly IConfiguracion _configuracion;
        private readonly IServicioAutenticacion _servicioAutenticacion;

        public CanalesExternosProductosService(IConfiguracion configuracion, IServicioAutenticacion servicioAutenticacion)
        {
            _configuracion = configuracion;
            _servicioAutenticacion = servicioAutenticacion;
        }

        public async Task<ProductoCanalExterno> AddProductoAsync(string productoId)
        {
            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(_configuracion.servidorAPI);
                await _servicioAutenticacion.ConfigurarAutorizacion(client);

                var dto = new { ProductoId = productoId, VistoBueno = _configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.TIENDA_ON_LINE) };
                var json = JsonConvert.SerializeObject(dto);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await client.PostAsync("PrestashopProductos", content);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Error al añadir producto {productoId}: {error}");
                }

                var respuesta = await response.Content.ReadAsStringAsync();
                var resultado = JsonConvert.DeserializeObject<ProductoCanalExterno>(respuesta);
                resultado.IsDirty = false;
                return resultado;
            }
        }

        public async Task<ProductoCanalExterno> GetProductoAsync(string productoId)
        {
            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(_configuracion.servidorAPI);
                await _servicioAutenticacion.ConfigurarAutorizacion(client);

                var response = await client.GetAsync($"PrestashopProductos/{productoId}");

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return null;
                }

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Error al obtener producto {productoId}: {error}");
                }

                var respuesta = await response.Content.ReadAsStringAsync();
                var resultado = JsonConvert.DeserializeObject<ProductoCanalExterno>(respuesta);
                resultado.IsDirty = false;
                return resultado;
            }
        }

        public async Task<IEnumerable<ProductoCanalExterno>> GetProductosSinVistoBuenoNumeroAsync()
        {
            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(_configuracion.servidorAPI);
                await _servicioAutenticacion.ConfigurarAutorizacion(client);

                var response = await client.GetAsync("PrestashopProductos?sinVistoBueno=true");

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Error al obtener productos sin visto bueno: {error}");
                }

                var respuesta = await response.Content.ReadAsStringAsync();
                var resultado = JsonConvert.DeserializeObject<List<ProductoCanalExterno>>(respuesta);
                foreach (var producto in resultado)
                {
                    producto.IsDirty = false;
                }
                return resultado;
            }
        }

        public async Task SaveProductoAsync(ProductoCanalExterno producto)
        {
            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(_configuracion.servidorAPI);
                await _servicioAutenticacion.ConfigurarAutorizacion(client);

                var json = JsonConvert.SerializeObject(producto);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await client.PutAsync("PrestashopProductos", content);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Error al guardar producto {producto.ProductoId}: {error}");
                }

                producto.IsDirty = false;
            }
        }
    }
}
