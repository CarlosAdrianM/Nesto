using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Modules.Producto.Models;
using Nesto.Modules.Producto.ViewModels;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Nesto.Modules.Producto
{
    public class ProductoService : IProductoService
    {
        private readonly IConfiguracion _configuracion;
        private readonly IServicioAutenticacion _servicioAutenticacion;
        private readonly string EmpresaDefecto = "1";

        public ProductoService(IConfiguracion configuracion, IServicioAutenticacion servicioAutenticacion)
        {
            _configuracion = configuracion;
            _servicioAutenticacion = servicioAutenticacion;
        }

        public async Task<ICollection<ProductoClienteModel>> BuscarClientes(string producto)
        {
            ICollection<ProductoClienteModel> clientes;
            using (HttpClient client = new())
            {
                client.BaseAddress = new Uri(_configuracion.servidorAPI);
                HttpResponseMessage response;

                try
                {
                    string vendedor = await _configuracion.leerParametro(EmpresaDefecto, Parametros.Claves.Vendedor);
                    string todosLosClientes = await _configuracion.leerParametro(EmpresaDefecto, Parametros.Claves.PermitirVerClientesTodosLosVendedores);
                    string urlConsulta = todosLosClientes == "1"
                        ? "Productos?empresa=" + EmpresaDefecto + "&id=" + producto + "&vendedor="
                        : "Productos?empresa=" + EmpresaDefecto + "&id=" + producto + "&vendedor=" + vendedor;
                    response = await client.GetAsync(urlConsulta);

                    if (response.IsSuccessStatusCode)
                    {
                        string resultado = await response.Content.ReadAsStringAsync();
                        clientes = JsonConvert.DeserializeObject<ICollection<ProductoClienteModel>>(resultado);
                    }
                    else
                    {
                        throw new Exception("No se han podido cargar los clientes que han comprado el producto " + producto);
                    }
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }

            return clientes;
        }

        public async Task<ICollection<ProductoModel>> BuscarProductos(string filtroNombre, string filtroFamilia, string filtroSubgrupo)
        {
            ICollection<ProductoModel> productos;
            var almacen = await _configuracion.leerParametro(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.AlmacenPedidoVta);
            using (HttpClient client = new())
            {
                client.BaseAddress = new Uri(_configuracion.servidorAPI);
                HttpResponseMessage response;

                try
                {
                    string urlConsulta = $"Productos?empresa={EmpresaDefecto}&filtroNombre={filtroNombre}&filtroFamilia={filtroFamilia}&filtroSubgrupo={filtroSubgrupo}&almacen={almacen}";


                    response = await client.GetAsync(urlConsulta);

                    if (response.IsSuccessStatusCode)
                    {
                        string resultado = await response.Content.ReadAsStringAsync();
                        productos = JsonConvert.DeserializeObject<ICollection<ProductoModel>>(resultado);
                    }
                    else
                    {
                        throw new Exception("El resultado de la búsqueda no se ha podido cargar correctamente");
                    }
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }

            return productos;
        }

        /// <summary>
        /// Issue #341: búsqueda contextual de productos (Lucene) estilo PlantillaVenta.
        /// Reutiliza el endpoint PlantillaVentas/Buscar ya existente y el PonerStock para
        /// obtener el stock por almacén, y mapea el resultado a ProductoModel para que la
        /// ficha de Producto pueda mostrarlo en la misma lista que la búsqueda por filtros.
        /// La ficha completa (UrlFoto, PrecioPublicoFinal, kits, etc.) se carga al seleccionar
        /// un resultado vía LeerProducto, igual que con la búsqueda por filtros.
        /// </summary>
        public async Task<ICollection<ProductoModel>> BuscarProductosContextual(string filtro, bool usarBusquedaConAND)
        {
            if (string.IsNullOrWhiteSpace(filtro))
            {
                return new List<ProductoModel>();
            }

            var almacen = await _configuracion.leerParametro(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.AlmacenPedidoVta);

            using (HttpClient client = new())
            {
                client.BaseAddress = new Uri(_configuracion.servidorAPI);

                // 1. Búsqueda Lucene
                string urlBuscar = $"PlantillaVentas/Buscar?q={Uri.EscapeDataString(filtro)}&usarBusquedaConAND={usarBusquedaConAND.ToString().ToLower()}";
                HttpResponseMessage responseBuscar = await client.GetAsync(urlBuscar);
                if (!responseBuscar.IsSuccessStatusCode)
                {
                    throw new Exception("No se han podido buscar los productos por texto contextual");
                }
                string jsonBuscar = await responseBuscar.Content.ReadAsStringAsync();
                var lineas = JsonConvert.DeserializeObject<List<LineaPlantillaVentaLite>>(jsonBuscar) ?? new List<LineaPlantillaVentaLite>();

                if (lineas.Count == 0)
                {
                    return new List<ProductoModel>();
                }

                // 2. Enriquecer con stocks (mismo patrón que PlantillaVentaService.PonerStocks)
                var paramStocks = new PonerStockParamLite
                {
                    Lineas = lineas,
                    Almacen = almacen,
                    Almacenes = Constantes.Almacenes.ALMACENES_STOCK.ToList()
                };
                var contentStocks = new StringContent(JsonConvert.SerializeObject(paramStocks), Encoding.UTF8, "application/json");
                HttpResponseMessage responseStock = await client.PostAsync("PlantillaVentas/PonerStock", contentStocks);
                if (responseStock.IsSuccessStatusCode)
                {
                    string jsonStock = await responseStock.Content.ReadAsStringAsync();
                    lineas = JsonConvert.DeserializeObject<List<LineaPlantillaVentaLite>>(jsonStock) ?? lineas;
                }
                // Si falla PonerStock, seguimos sin stocks antes que fallar toda la búsqueda.

                // 3. Mapear a ProductoModel (solo los campos que muestra el ListView; la ficha
                //    completa se carga después al seleccionar el producto).
                return lineas.Select(l => new ProductoModel
                {
                    Producto = l.producto,
                    Nombre = l.texto,
                    Tamanno = (short?)l.tamanno,
                    UnidadMedida = l.unidadMedida,
                    Familia = l.familia,
                    Subgrupo = l.subGrupo,
                    Estado = (short)l.estado,
                    PrecioProfesional = l.precio,
                    CodigoBarras = l.codigoBarras,
                    Stocks = l.stocks?.Select(s => new ProductoModel.StockProducto
                    {
                        Almacen = s.almacen,
                        Stock = s.stock,
                        CantidadDisponible = s.cantidadDisponible
                    }).ToList() ?? new List<ProductoModel.StockProducto>()
                }).ToList();
            }
        }

        // DTOs "lite" para comunicarnos con el endpoint PlantillaVentas sin acoplar Producto a PlantillaVenta.
        // Los nombres de propiedades coinciden con LineaPlantillaVenta / PonerStockParam / StockAlmacenDTO
        // para que Newtonsoft.Json serialice/deserialice correctamente.
        private class LineaPlantillaVentaLite
        {
            public string producto { get; set; }
            public string texto { get; set; }
            public int? tamanno { get; set; }
            public string unidadMedida { get; set; }
            public string familia { get; set; }
            public string subGrupo { get; set; }
            public int estado { get; set; }
            public decimal precio { get; set; }
            public string codigoBarras { get; set; }
            public List<StockAlmacenLite> stocks { get; set; }
        }

        private class StockAlmacenLite
        {
            public string almacen { get; set; }
            public int stock { get; set; }
            public int cantidadDisponible { get; set; }
        }

        private class PonerStockParamLite
        {
            public List<LineaPlantillaVentaLite> Lineas { get; set; }
            public string Almacen { get; set; }
            public List<string> Almacenes { get; set; }
        }

        public async Task<List<VideoLookupModel>> BuscarVideosRelacionados(string producto)
        {
            List<VideoLookupModel> videos;
            using (HttpClient client = new())
            {
                client.BaseAddress = new Uri(_configuracion.servidorAPI);
                HttpResponseMessage response;

                try
                {
                    string urlConsulta = $"Videos/Producto/{producto}";

                    response = await client.GetAsync(urlConsulta);

                    if (response.IsSuccessStatusCode)
                    {
                        string resultado = await response.Content.ReadAsStringAsync();
                        videos = JsonConvert.DeserializeObject<List<VideoLookupModel>>(resultado);
                    }
                    else
                    {
                        throw new Exception("No se han podido cargar los videos relacionados");
                    }
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }

            return videos;
        }

        public async Task<VideoModel> CargarVideoCompleto(int videoId)
        {
            using HttpClient client = new();
            client.BaseAddress = new Uri(_configuracion.servidorAPI);

            // Configurar autorización
            if (!await _servicioAutenticacion.ConfigurarAutorizacion(client))
            {
                throw new UnauthorizedAccessException("No se pudo configurar la autorización");
            }

            try
            {
                string urlConsulta = $"Videos/{videoId}";
                var response = await client.GetAsync(urlConsulta);

                if (response.IsSuccessStatusCode)
                {
                    string resultado = await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<VideoModel>(resultado);
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    // Token expirado o inválido, limpiar y reintentar una vez
                    _servicioAutenticacion.LimpiarToken();
                    throw new UnauthorizedAccessException("Token de autenticación inválido");
                }
                else
                {
                    throw new Exception($"Error al cargar el video: {response.StatusCode}");
                }
            }
            catch (UnauthorizedAccessException)
            {
                throw; // Re-lanzar excepciones de autorización
            }
            catch (Exception ex)
            {
                throw new Exception("No se ha podido cargar el video completo", ex);
            }
        }

        public async Task CrearControlStock(ControlStock controlStock)
        {
            using HttpClient client = new();
            client.BaseAddress = new Uri(_configuracion.servidorAPI);
            HttpResponseMessage response;

            if (!await _servicioAutenticacion.ConfigurarAutorizacion(client))
            {
                throw new Exception("No se pudo configurar la autorización para crear el control de stock");
            }

            try
            {
                string urlConsulta = "ControlesStock";

                HttpContent content = new StringContent(JsonConvert.SerializeObject(controlStock), Encoding.UTF8, "application/json");
                response = await client.PostAsync(urlConsulta, content);

                if (response.IsSuccessStatusCode)
                {
                    string resultado = await response.Content.ReadAsStringAsync();
                }
                else
                {
                    string textoError = await response.Content.ReadAsStringAsync();
                    JObject requestException = JsonConvert.DeserializeObject<JObject>(textoError);

                    string errorMostrar = $"No se ha podido modificar el control de stock\n";
                    if (requestException != null && requestException["exceptionMessage"] != null)
                    {
                        errorMostrar += requestException["exceptionMessage"] + "\n";
                    }
                    if (requestException != null && requestException["ModelState"] != null)
                    {
                        var firstError = requestException["ModelState"];
                        var nodoError = firstError.LastOrDefault();
                        errorMostrar += nodoError.FirstOrDefault()[0];
                    }
                    var innerException = requestException?["InnerException"];
                    while (innerException != null)
                    {
                        errorMostrar += "\n" + innerException["ExceptionMessage"];
                        innerException = innerException["InnerException"];
                    }
                    throw new Exception(errorMostrar);
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public async Task GuardarControlStock(ControlStock controlStock)
        {
            using HttpClient client = new();
            client.BaseAddress = new Uri(_configuracion.servidorAPI);
            HttpResponseMessage response;

            if (!await _servicioAutenticacion.ConfigurarAutorizacion(client))
            {
                throw new Exception("No se pudo configurar la autorización para guardar el control de stock");
            }

            try
            {
                string urlConsulta = "ControlesStock";

                HttpContent content = new StringContent(JsonConvert.SerializeObject(controlStock), Encoding.UTF8, "application/json");
                response = await client.PutAsync(urlConsulta, content);

                if (response.IsSuccessStatusCode)
                {
                    string resultado = await response.Content.ReadAsStringAsync();
                }
                else
                {
                    string textoError = await response.Content.ReadAsStringAsync();
                    JObject requestException = JsonConvert.DeserializeObject<JObject>(textoError);

                    string errorMostrar = $"No se ha podido modificar el control de stock\n";
                    if (requestException["exceptionMessage"] != null)
                    {
                        errorMostrar += requestException["exceptionMessage"] + "\n";
                    }
                    if (requestException["ModelState"] != null)
                    {
                        var firstError = requestException["ModelState"];
                        var nodoError = firstError.LastOrDefault();
                        errorMostrar += nodoError.FirstOrDefault()[0];
                    }
                    var innerException = requestException["InnerException"];
                    while (innerException != null)
                    {
                        errorMostrar += "\n" + innerException["ExceptionMessage"];
                        innerException = innerException["InnerException"];
                    }
                    throw new Exception(errorMostrar);
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public async Task<ControlStockProductoModel> LeerControlStock(string producto)
        {
            ControlStockProductoModel controlStock;
            using (HttpClient client = new())
            {
                client.BaseAddress = new Uri(_configuracion.servidorAPI);
                HttpResponseMessage response;

                try
                {
                    string urlConsulta = $"ControlesStock?productoId={producto}";

                    response = await client.GetAsync(urlConsulta).ConfigureAwait(true);

                    if (response.IsSuccessStatusCode)
                    {
                        string resultado = await response.Content.ReadAsStringAsync().ConfigureAwait(true);
                        controlStock = JsonConvert.DeserializeObject<ControlStockProductoModel>(resultado);
                    }
                    else
                    {
                        throw new Exception("El stock del producto " + producto + " no se ha podido cargar correctamente");
                    }
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }
            return controlStock;
        }

        public async Task<List<DiarioProductoModel>> LeerDiariosProducto()
        {
            using HttpClient client = new();
            client.BaseAddress = new Uri(_configuracion.servidorAPI);
            HttpResponseMessage response;

            try
            {
                string urlConsulta = "DiariosProductos";

                response = await client.GetAsync(urlConsulta).ConfigureAwait(true);

                if (response.IsSuccessStatusCode)
                {
                    string resultado = await response.Content.ReadAsStringAsync().ConfigureAwait(true);
                    return JsonConvert.DeserializeObject<List<DiarioProductoModel>>(resultado);
                }
                else
                {
                    throw new Exception("Se ha producido un error al cargar los diarios de producto");
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public async Task<List<KitContienePerteneceModel>> LeerKitsContienePertenece(string producto)
        {
            if (string.IsNullOrEmpty(producto))
            {
                return null;
            }
            using HttpClient client = new();
            client.BaseAddress = new Uri(_configuracion.servidorAPI);
            HttpResponseMessage response;

            try
            {
                string urlConsulta = $"Productos/KitsProducto?empresa={EmpresaDefecto}&producto={producto}";


                response = await client.GetAsync(urlConsulta);

                if (response.IsSuccessStatusCode)
                {
                    string resultado = await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<List<KitContienePerteneceModel>>(resultado);
                }
                else
                {
                    throw new Exception("El producto " + producto + " no se ha podido cargar correctamente");
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public async Task<ProductoModel> LeerProducto(string producto)
        {
            ProductoModel productoActual;
            if (producto is null or "")
            {
                //producto = await configuracion.leerParametro(EmpresaDefecto, "UltNumProducto");
                return null;
            }
            using (HttpClient client = new())
            {
                client.BaseAddress = new Uri(_configuracion.servidorAPI);
                HttpResponseMessage response;

                try
                {
                    string urlConsulta = "Productos?empresa=" + EmpresaDefecto + "&id=" + producto + "&fichaCompleta=true";


                    response = await client.GetAsync(urlConsulta);

                    if (response.IsSuccessStatusCode)
                    {
                        string resultado = await response.Content.ReadAsStringAsync();
                        productoActual = JsonConvert.DeserializeObject<ProductoModel>(resultado);
                    }
                    else
                    {
                        throw new Exception("El producto " + producto + " no se ha podido cargar correctamente");
                    }
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }

            return productoActual;
        }

        public async Task<int> MontarKit(string almacen, string producto, int cantidad)
        {
            using HttpClient client = new();
            client.BaseAddress = new Uri(_configuracion.servidorAPI);
            HttpResponseMessage response;

            try
            {
                string urlConsulta = "Productos/MontarKit";

                var parametros = new
                {
                    empresa = EmpresaDefecto,
                    almacen,
                    producto,
                    cantidad,
                    _configuracion.usuario
                };

                string jsonParametros = JsonConvert.SerializeObject(parametros);

                HttpContent content = new StringContent(jsonParametros, Encoding.UTF8, "application/json");
                //var content = new StringContent("{\"empresa\":\"1\",\"almacen\":\"ALG\",\"producto\":\"38697\",\"cantidad\":1}", Encoding.UTF8, "application/json");

                response = await client.PostAsync(urlConsulta, content);

                if (response.IsSuccessStatusCode)
                {
                    string contenido = await response.Content.ReadAsStringAsync();
                    int traspaso = JsonConvert.DeserializeObject<int>(contenido);
                    return traspaso;
                }
                else
                {
                    string textoError = await response.Content.ReadAsStringAsync();
                    JObject requestException = JsonConvert.DeserializeObject<JObject>(textoError);

                    string errorMostrar = $"No se han podido traspasar los movimientos de producto\n";
                    if (requestException != null && requestException["exceptionMessage"] != null)
                    {
                        errorMostrar += requestException["exceptionMessage"] + "\n";
                    }
                    if (requestException != null && requestException["ModelState"] != null)
                    {
                        var firstError = requestException["ModelState"];
                        var nodoError = firstError.LastOrDefault();
                        errorMostrar += nodoError.FirstOrDefault()[0];
                    }
                    var innerException = requestException?["InnerException"];
                    while (innerException != null)
                    {
                        errorMostrar += "\n" + innerException["ExceptionMessage"];
                        innerException = innerException["InnerException"];
                    }
                    throw new Exception(errorMostrar);
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public async Task<bool> TraspasarDiario(string diarioOrigen, string diarioDestino, string almacenOrigen)
        {
            using HttpClient client = new();
            client.BaseAddress = new Uri(_configuracion.servidorAPI);
            HttpResponseMessage response;

            try
            {
                string urlConsulta = "DiariosProductos";

                var parametros = new ParametrosDiarioProducto
                {
                    diarioOrigen = diarioOrigen,
                    diarioDestino = diarioDestino,
                    almacen = almacenOrigen
                };

                string jsonParametros = JsonConvert.SerializeObject(parametros);

                HttpContent content = new StringContent(jsonParametros, Encoding.UTF8, "application/json");
                response = await client.PostAsync(urlConsulta, content);

                if (response.IsSuccessStatusCode)
                {
                    string contenido = await response.Content.ReadAsStringAsync();
                    bool resultado = JsonConvert.DeserializeObject<bool>(contenido);
                    return resultado;
                }
                else
                {
                    string textoError = await response.Content.ReadAsStringAsync();
                    JObject requestException = JsonConvert.DeserializeObject<JObject>(textoError);

                    string errorMostrar = $"No se han podido traspasar los movimientos de producto\n";
                    if (requestException != null && requestException["exceptionMessage"] != null)
                    {
                        errorMostrar += requestException["exceptionMessage"] + "\n";
                    }
                    if (requestException != null && requestException["ModelState"] != null)
                    {
                        var firstError = requestException["ModelState"];
                        var nodoError = firstError.LastOrDefault();
                        errorMostrar += nodoError.FirstOrDefault()[0];
                    }
                    var innerException = requestException?["InnerException"];
                    while (innerException != null)
                    {
                        errorMostrar += "\n" + innerException["ExceptionMessage"];
                        innerException = innerException["InnerException"];
                    }
                    throw new Exception(errorMostrar);
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public async Task ActualizarVideoProducto(int id, ActualizacionVideoProductoDto dto)
        {
            using HttpClient client = new();
            client.BaseAddress = new Uri(_configuracion.servidorAPI);

            if (!await _servicioAutenticacion.ConfigurarAutorizacion(client))
            {
                throw new UnauthorizedAccessException("No se pudo configurar la autorización");
            }

            try
            {
                string url = $"VideosProductos/{id}";
                var content = new StringContent(JsonConvert.SerializeObject(dto), Encoding.UTF8, "application/json");
                var response = await client.PutAsync(url, content);

                if (!response.IsSuccessStatusCode)
                {
                    string error = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Error al actualizar: {response.StatusCode} - {error}");
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        public async Task EliminarVideoProducto(int id, string observaciones = null)
        {
            using HttpClient client = new();
            client.BaseAddress = new Uri(_configuracion.servidorAPI);

            if (!await _servicioAutenticacion.ConfigurarAutorizacion(client))
            {
                throw new UnauthorizedAccessException("No se pudo configurar la autorización");
            }

            try
            {
                string url = $"VideosProductos/{id}";
                if (!string.IsNullOrEmpty(observaciones))
                {
                    url += $"?observaciones={Uri.EscapeDataString(observaciones)}";
                }

                var response = await client.DeleteAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    string error = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Error al eliminar: {response.StatusCode} - {error}");
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        // LO HAREMOS MÁS ADELANTE
        //public async Task<List<HistorialCambioDto>> ObtenerHistorialCambios(int videoProductoId)
        //{
        //    using HttpClient client = new();
        //    client.BaseAddress = new Uri(_configuracion.servidorAPI);

        //    if (!await _servicioAutenticacion.ConfigurarAutorizacion(client))
        //    {
        //        throw new UnauthorizedAccessException("No se pudo configurar la autorización");
        //    }

        //    try
        //    {
        //        string url = $"VideosProductos/{videoProductoId}/historial";
        //        var response = await client.GetAsync(url);

        //        if (response.IsSuccessStatusCode)
        //        {
        //            string resultado = await response.Content.ReadAsStringAsync();
        //            return JsonConvert.DeserializeObject<List<HistorialCambioDto>>(resultado);
        //        }
        //        else
        //        {
        //            throw new Exception($"Error al obtener historial: {response.StatusCode}");
        //        }
        //    }
        //    catch (Exception)
        //    {
        //        throw;
        //    }
        //}

        public async Task DeshacerCambio(int videoProductoId, int logId, string observaciones = null)
        {
            using HttpClient client = new();
            client.BaseAddress = new Uri(_configuracion.servidorAPI);

            if (!await _servicioAutenticacion.ConfigurarAutorizacion(client))
            {
                throw new UnauthorizedAccessException("No se pudo configurar la autorización");
            }

            try
            {
                string url = $"VideosProductos/{videoProductoId}/deshacer/{logId}";
                if (!string.IsNullOrEmpty(observaciones))
                {
                    url += $"?observaciones={Uri.EscapeDataString(observaciones)}";
                }

                var response = await client.PostAsync(url, null);

                if (!response.IsSuccessStatusCode)
                {
                    string error = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Error al deshacer: {response.StatusCode} - {error}");
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        public async Task<List<ProductoControlStockModel>> LeerProductosProveedorControlStock(string proveedorId, string almacen)
        {
            using HttpClient client = new();
            client.BaseAddress = new Uri(_configuracion.servidorAPI);

            try
            {
                string urlConsulta = $"ControlesStock/ProductosProveedor?proveedorId={proveedorId}&almacen={almacen}";
                var response = await client.GetAsync(urlConsulta).ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                {
                    string resultado = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    return JsonConvert.DeserializeObject<List<ProductoControlStockModel>>(resultado);
                }
                else
                {
                    string error = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    throw new Exception($"Error al obtener productos del proveedor: {response.StatusCode} - {error}");
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        public async Task<List<VideoLookupModel>> CargarVideos(int skip, int take)
        {
            using HttpClient client = new();
            client.BaseAddress = new Uri(_configuracion.servidorAPI);

            if (!await _servicioAutenticacion.ConfigurarAutorizacion(client))
            {
                throw new UnauthorizedAccessException("No se pudo configurar la autorización");
            }

            try
            {
                string urlConsulta = $"Videos?skip={skip}&take={take}";
                var response = await client.GetAsync(urlConsulta);

                if (response.IsSuccessStatusCode)
                {
                    string resultado = await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<List<VideoLookupModel>>(resultado);
                }
                else
                {
                    throw new Exception($"Error al cargar los videos: {response.StatusCode}");
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        public async Task<List<VideoLookupModel>> BuscarVideos(string busqueda, int skip, int take)
        {
            using HttpClient client = new();
            client.BaseAddress = new Uri(_configuracion.servidorAPI);

            if (!await _servicioAutenticacion.ConfigurarAutorizacion(client))
            {
                throw new UnauthorizedAccessException("No se pudo configurar la autorización");
            }

            try
            {
                string urlConsulta = $"Videos/Buscar?q={Uri.EscapeDataString(busqueda)}&skip={skip}&take={take}";
                var response = await client.GetAsync(urlConsulta);

                if (response.IsSuccessStatusCode)
                {
                    string resultado = await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<List<VideoLookupModel>>(resultado);
                }
                else
                {
                    throw new Exception($"Error al buscar videos: {response.StatusCode}");
                }
            }
            catch (Exception)
            {
                throw;
            }
        }
    }
}
