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
        private readonly IServicioAutenticacion _servicioAutenticacion;

        /// <summary>
        /// Constructor con inyección de dependencias.
        /// </summary>
        /// <param name="configuracion">Configuración con el servidor API.</param>
        /// <param name="servicioAutenticacion">Servicio de autenticación para obtener tokens.</param>
        public ServicioProducto(IConfiguracion configuracion, IServicioAutenticacion servicioAutenticacion)
        {
            _configuracion = configuracion ?? throw new ArgumentNullException(nameof(configuracion));
            _servicioAutenticacion = servicioAutenticacion;
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

                // Configurar autenticación
                if (_servicioAutenticacion != null)
                {
                    await _servicioAutenticacion.ConfigurarAutorizacion(client);
                }

                try
                {
                    // Si no hay cliente/contacto, usar el endpoint simple que solo devuelve nombre y PVP
                    string urlConsulta;
                    if (string.IsNullOrWhiteSpace(cliente))
                    {
                        urlConsulta = $"Productos?empresa={empresa}&id={producto}";
                    }
                    else
                    {
                        urlConsulta = $"Productos?empresa={empresa}&id={producto}&cliente={cliente}&contacto={contacto}&cantidad={cantidad}";
                    }

                    client.DefaultRequestHeaders.Accept.Clear();
                    client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                    System.Diagnostics.Debug.WriteLine($"[ServicioProducto] URL: {urlConsulta}");
                    System.Diagnostics.Debug.WriteLine($"[ServicioProducto] Auth configurada: {_servicioAutenticacion != null}");

                    var response = await client.GetAsync(urlConsulta);

                    System.Diagnostics.Debug.WriteLine($"[ServicioProducto] HTTP Status: {response.StatusCode}");

                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadAsStringAsync();
                        System.Diagnostics.Debug.WriteLine($"[ServicioProducto] Respuesta: {json.Substring(0, Math.Min(200, json.Length))}...");

                        var productoApi = JsonConvert.DeserializeObject<ProductoApiResponse>(json);

                        if (productoApi != null)
                        {
                            System.Diagnostics.Debug.WriteLine($"[ServicioProducto] Producto deserializado: {productoApi.producto} - {productoApi.nombre}");
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
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"[ServicioProducto] productoApi es null después de deserializar");
                        }
                    }
                    else
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        System.Diagnostics.Debug.WriteLine($"[ServicioProducto] Error HTTP: {errorContent}");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ServicioProducto] Excepción: {ex.Message}");
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
