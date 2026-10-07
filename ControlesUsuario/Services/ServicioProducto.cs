using ControlesUsuario.Models;
using Nesto.Infrastructure.Contracts;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
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
        private readonly IClienteApiFactory _clienteApiFactory;

        /// <summary>
        /// Constructor con inyección de dependencias.
        /// </summary>
        /// <param name="configuracion">Configuración con el servidor API.</param>
        /// <param name="servicioAutenticacion">Servicio de autenticación para obtener tokens.</param>
        /// <param name="clienteApiFactory">
        /// Factory de HttpClient ya configurado con JWT (Nesto#369: así el usuario sale en ELMAH).
        /// Opcional: si no se inyecta, se cae al cliente con autenticación manual de siempre.
        /// </param>
        public ServicioProducto(IConfiguracion configuracion, IServicioAutenticacion servicioAutenticacion, IClienteApiFactory clienteApiFactory = null)
        {
            _configuracion = configuracion ?? throw new ArgumentNullException(nameof(configuracion));
            _servicioAutenticacion = servicioAutenticacion;
            _clienteApiFactory = clienteApiFactory;
        }

        // Nesto#369: prioriza el HttpClient de la factory (base + JWT centralizados). Si la factory no
        // está disponible, mantiene EXACTAMENTE el comportamiento anterior (cliente propio + auth manual),
        // de modo que la migración nunca degrada la autenticación.
        private async Task<HttpClient> CrearClienteAsync()
        {
            if (_clienteApiFactory != null)
            {
                return _clienteApiFactory.Crear();
            }

            var client = new HttpClient { BaseAddress = new Uri(_configuracion.servidorAPI) };
            if (_servicioAutenticacion != null)
            {
                await _servicioAutenticacion.ConfigurarAutorizacion(client);
            }
            return client;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// NestoAPI#605: un producto puede tener varios códigos de barras y un código puede estar (si se
        /// marcó expresamente) en varios productos. Lo que se teclea o se lee se busca primero como hasta
        /// ahora (número de producto o código principal). Si no era un número de producto, se pregunta a
        /// GET api/Productos/PorCodigoBarras, que mira TODOS los códigos: uno → ese producto; varios →
        /// <see cref="CodigoBarrasDuplicadoException"/> para que el behavior deje elegir (el mismo selector
        /// de Nesto#368). Si la API publicada no tiene aún ese endpoint (404), se queda el resultado de siempre.
        /// </remarks>
        public async Task<ProductoDTO> BuscarProducto(string empresa, string producto, string cliente, string contacto, short cantidad)
        {
            if (string.IsNullOrWhiteSpace(producto))
            {
                return null;
            }

            ProductoDTO directo = null;
            CodigoBarrasDuplicadoException duplicado = null;
            try
            {
                directo = await BuscarProductoDirecto(empresa, producto, cliente, contacto, cantidad);
            }
            catch (CodigoBarrasDuplicadoException ex)
            {
                duplicado = ex;
            }

            string texto = producto.Trim();
            if (directo != null && string.Equals(directo.Producto?.Trim(), texto, StringComparison.OrdinalIgnoreCase))
            {
                // Era un número de producto: no hay código de barras que buscar.
                return directo;
            }

            List<ProductoPorCodigoBarras> porCodigo = await BuscarPorCodigoBarras(empresa, texto);
            if (porCodigo == null || porCodigo.Count == 0)
            {
                // TODO NestoAPI#605: con la API nueva en producción, el null (404) desaparece.
                if (duplicado != null)
                {
                    throw duplicado;
                }
                return directo;
            }

            List<ProductoPorCodigoBarras> distintos = porCodigo
                .Where(p => !string.IsNullOrWhiteSpace(p.Producto))
                .GroupBy(p => p.Producto.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();
            if (distintos.Count > 1)
            {
                throw new CodigoBarrasDuplicadoException(distintos
                    .Select(p => new ProductoCodigoBarrasDuplicado { Producto = p.Producto.Trim(), Nombre = p.Nombre?.Trim() })
                    .ToList());
            }
            if (distintos.Count == 0)
            {
                return directo;
            }

            string unico = distintos[0].Producto.Trim();
            if (directo != null && string.Equals(directo.Producto?.Trim(), unico, StringComparison.OrdinalIgnoreCase))
            {
                return directo;
            }
            return await BuscarProductoDirecto(empresa, unico, cliente, contacto, cantidad);
        }

        /// <summary>
        /// NestoAPI#605: productos que tienen ese código entre sus códigos de barras activos. Null si la API
        /// no tiene el endpoint (404) o falla: entonces manda la búsqueda de siempre.
        /// </summary>
        internal async Task<List<ProductoPorCodigoBarras>> BuscarPorCodigoBarras(string empresa, string codigo)
        {
            try
            {
                using (var client = await CrearClienteAsync())
                {
                    var response = await client.GetAsync($"Productos/PorCodigoBarras?codigo={Uri.EscapeDataString(codigo)}&empresa={Uri.EscapeDataString(empresa ?? "1")}");
                    if (!response.IsSuccessStatusCode)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ServicioProducto] PorCodigoBarras: {response.StatusCode}");
                        return null;
                    }
                    var json = await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<List<ProductoPorCodigoBarras>>(json) ?? new List<ProductoPorCodigoBarras>();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ServicioProducto] PorCodigoBarras excepción: {ex.Message}");
                return null;
            }
        }

        /// <summary>NestoAPI#605: una fila de GET api/Productos/PorCodigoBarras.</summary>
        internal class ProductoPorCodigoBarras
        {
            public string Producto { get; set; }
            public string Nombre { get; set; }
            public int Cantidad { get; set; }
            public bool Principal { get; set; }
        }

        private async Task<ProductoDTO> BuscarProductoDirecto(string empresa, string producto, string cliente, string contacto, short cantidad)
        {
            using (var client = await CrearClienteAsync())
            {
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

                    // Nesto#368: el código de barras puede corresponder a varios productos.
                    // En ese caso la API devuelve 409 con la lista de candidatos para que el
                    // cliente muestre un selector y reintente con el Número elegido.
                    if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
                    {
                        var jsonConflicto = await response.Content.ReadAsStringAsync();
                        var candidatos = JsonConvert.DeserializeObject<List<ProductoCodigoBarrasDuplicado>>(jsonConflicto)
                                         ?? new List<ProductoCodigoBarrasDuplicado>();
                        throw new CodigoBarrasDuplicadoException(candidatos);
                    }

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
                catch (CodigoBarrasDuplicadoException)
                {
                    // No la tragamos: el behavior la captura para mostrar el selector (Nesto#368).
                    throw;
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
