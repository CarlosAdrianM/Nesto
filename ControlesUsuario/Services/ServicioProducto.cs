using ControlesUsuario.Models;
using Nesto.Infrastructure.Contracts;
using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace ControlesUsuario.Services
{
    /// <summary>
    /// Implementación del servicio de productos que consulta la API de Nesto.
    /// Issue #258 - Carlos 11/12/25
    /// </summary>
    public class ServicioProducto : IServicioProducto
    {
        private readonly IConfiguracion _configuracion;

        /// <summary>
        /// Constructor con inyección de dependencias.
        /// </summary>
        /// <param name="configuracion">Configuración con el servidor API.</param>
        public ServicioProducto(IConfiguracion configuracion)
        {
            _configuracion = configuracion ?? throw new ArgumentNullException(nameof(configuracion));
        }

        /// <inheritdoc/>
        public async Task<ProductoDTO> BuscarProducto(string empresa, string producto, string cliente, string contacto, short cantidad)
        {
            if (string.IsNullOrWhiteSpace(producto))
            {
                return null;
            }

            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(_configuracion.servidorAPI);

                try
                {
                    var urlConsulta = $"Productos?empresa={empresa}&id={producto}&cliente={cliente}&contacto={contacto}&cantidad={cantidad}";

                    client.DefaultRequestHeaders.Accept.Clear();
                    client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                    var response = await client.GetAsync(urlConsulta);

                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadAsStringAsync();
                        var productoApi = JsonConvert.DeserializeObject<ProductoApiResponse>(json);

                        if (productoApi != null)
                        {
                            return new ProductoDTO
                            {
                                Producto = productoApi.producto,
                                Nombre = productoApi.nombre,
                                Precio = productoApi.precio,
                                AplicarDescuento = productoApi.aplicarDescuento,
                                Descuento = productoApi.descuento,
                                Iva = productoApi.iva,
                                Stock = productoApi.Stock,
                                CantidadReservada = productoApi.CantidadReservada,
                                CantidadDisponible = productoApi.CantidadDisponible
                            };
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ServicioProducto] Error buscando producto: {ex.Message}");
                }

                return null;
            }
        }

        /// <summary>
        /// Clase interna para deserializar la respuesta de la API.
        /// Mantiene compatibilidad con los nombres de propiedades de la API.
        /// </summary>
        private class ProductoApiResponse
        {
            public string producto { get; set; }
            public string nombre { get; set; }
            public decimal precio { get; set; }
            public bool aplicarDescuento { get; set; }
            public int Stock { get; set; }
            public int CantidadReservada { get; set; }
            public int CantidadDisponible { get; set; }
            public decimal descuento { get; set; }
            public string iva { get; set; }
        }
    }
}
