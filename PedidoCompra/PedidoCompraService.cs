using Nesto.Infrastructure.Contracts;
using Nesto.Models.Nesto.Models;
using Nesto.Modulos.PedidoCompra.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using static Nesto.Infrastructure.Shared.Constantes;

namespace Nesto.Modulos.PedidoCompra
{
    public class PedidoCompraService : IPedidoCompraService
    {
        private readonly IConfiguracion configuracion;
        private readonly IServicioAutenticacion _servicioAutenticacion;

        public PedidoCompraService(IConfiguracion configuracion, IServicioAutenticacion servicioAutenticacion)
        {
            this.configuracion = configuracion;
            _servicioAutenticacion = servicioAutenticacion;
        }

        public async Task<PedidoCompraDTO> AmpliarHastaStockMaximo(PedidoCompraDTO pedido)
        {
            PedidoCompraDTO pedidoAmpliado;
            using (HttpClient client = new HttpClient())
            {
                client.BaseAddress = new Uri(configuracion.servidorAPI);

                // Carlos 21/11/24: Agregar autenticación
                if (!await _servicioAutenticacion.ConfigurarAutorizacion(client))
                {
                    throw new UnauthorizedAccessException("No se pudo configurar la autorización");
                }

                HttpResponseMessage response;
                HttpContent content = new StringContent(JsonConvert.SerializeObject(pedido), Encoding.UTF8, "application/json");

                try
                {
                    response = await client.PostAsync("PedidosCompra/AmpliarPedidoAlStockMaximo", content);

                    if (response.IsSuccessStatusCode)
                    {
                        string resultado = await response.Content.ReadAsStringAsync();
                        pedidoAmpliado = JsonConvert.DeserializeObject<PedidoCompraDTO>(resultado);
                    }
                    else
                    {
                        throw new Exception($"El pedido {pedido.Id} no se ha podido ampliar correctamente");
                    }
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }

            return pedidoAmpliado;
        }

        public async Task<PedidoCompraDTO> CargarPedido(string empresa, int pedido)
        {
            PedidoCompraDTO pedidoActual;
            using (HttpClient client = new HttpClient())
            {
                client.BaseAddress = new Uri(configuracion.servidorAPI);

                // Carlos 21/11/24: Agregar autenticación
                if (!await _servicioAutenticacion.ConfigurarAutorizacion(client))
                {
                    throw new UnauthorizedAccessException("No se pudo configurar la autorización");
                }

                HttpResponseMessage response;

                try
                {
                    string urlConsulta = $"PedidosCompra/PedidoCompra?empresa={empresa}&pedido={pedido}";

                    response = await client.GetAsync(urlConsulta);
                    string resultado = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {                        
                        pedidoActual = JsonConvert.DeserializeObject<PedidoCompraDTO>(resultado);
                    }
                    else
                    {
                        throw new Exception($"El pedido {pedido} no se ha podido cargar correctamente", new Exception(resultado));
                    }
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }

            return pedidoActual;
        }

        public async Task<ObservableCollection<PedidoCompraLookup>> CargarPedidos()
        {
            ObservableCollection<PedidoCompraLookup> pedidos;
            using (HttpClient client = new HttpClient())
            {
                client.BaseAddress = new Uri(configuracion.servidorAPI);

                // Carlos 21/11/24: Agregar autenticación
                if (!await _servicioAutenticacion.ConfigurarAutorizacion(client))
                {
                    throw new UnauthorizedAccessException("No se pudo configurar la autorización");
                }

                HttpResponseMessage response;

                try
                {
                    string urlConsulta = $"PedidosCompra/PedidosCompra";

                    response = await client.GetAsync(urlConsulta);

                    if (response.IsSuccessStatusCode)
                    {
                        string resultado = await response.Content.ReadAsStringAsync();
                        pedidos = JsonConvert.DeserializeObject<ObservableCollection<PedidoCompraLookup>>(resultado);
                    }
                    else
                    {
                        throw new Exception("Los pedidos no se han podido cargar correctamente");
                    }
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }

            return pedidos;
        }

        public async Task<List<PedidoCompraDTO>> CargarPedidosAutomaticos(string empresa)
        {
            List<PedidoCompraDTO> pedidos;
            using (HttpClient client = new HttpClient())
            {
                client.BaseAddress = new Uri(configuracion.servidorAPI);

                // Carlos 21/11/24: Agregar autenticación
                if (!await _servicioAutenticacion.ConfigurarAutorizacion(client))
                {
                    throw new UnauthorizedAccessException("No se pudo configurar la autorización");
                }

                HttpResponseMessage response;

                try
                {
                    string urlConsulta = $"PedidosCompra/PedidosCompraAutomaticos?empresa={empresa}";

                    response = await client.GetAsync(urlConsulta);

                    if (response.IsSuccessStatusCode)
                    {
                        string resultado = await response.Content.ReadAsStringAsync();
                        pedidos = JsonConvert.DeserializeObject<List<PedidoCompraDTO>>(resultado);
                    }
                    else
                    {
                        throw new Exception("Los pedidos no se han podido cargar correctamente");
                    }
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }

            return pedidos;
        }

        public async Task<CrearFacturaCmpResponse> CrearAlbaranYFactura(CrearFacturaCmpRequest request)
        {
            CrearFacturaCmpResponse respuesta;

            using (HttpClient client = new HttpClient())
            {
                client.BaseAddress = new Uri(configuracion.servidorAPI);

                // Carlos 21/11/24: Agregar autenticación
                if (!await _servicioAutenticacion.ConfigurarAutorizacion(client))
                {
                    throw new UnauthorizedAccessException("No se pudo configurar la autorización");
                }

                HttpResponseMessage response;

                try
                {
                    string urlConsulta = $"PedidosCompra/CrearAlbaranYFactura";
                    request.Pedido.Usuario = configuracion.usuario;

                    HttpContent content = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");
                    response = await client.PostAsync(urlConsulta, content);

                    if (response.IsSuccessStatusCode)
                    {
                        string resultado = await response.Content.ReadAsStringAsync();
                        respuesta = JsonConvert.DeserializeObject<CrearFacturaCmpResponse>(resultado);
                    }
                    else
                    {
                        string textoError = await response.Content.ReadAsStringAsync();
                        JObject requestException = JsonConvert.DeserializeObject<JObject>(textoError);

                        string errorMostrar = $"No se ha podido crear la factura o el albarán\n";
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
                    throw new Exception("No se han podido crear el albarán y la factura de compras", ex);
                }
            }

            return respuesta;
        }

        public async Task<int> CrearPedido(PedidoCompraDTO pedido)
        {
            int respuesta;

            using (HttpClient client = new HttpClient())
            {
                client.BaseAddress = new Uri(configuracion.servidorAPI);

                // Carlos 21/11/24: Agregar autenticación
                if (!await _servicioAutenticacion.ConfigurarAutorizacion(client))
                {
                    throw new UnauthorizedAccessException("No se pudo configurar la autorización");
                }

                HttpResponseMessage response;

                try
                {
                    string urlConsulta = $"PedidosCompra/PedidosCompra";

                    pedido.Usuario = configuracion.usuario;

                    HttpContent content = new StringContent(JsonConvert.SerializeObject(pedido), Encoding.UTF8, "application/json");
                    response = await client.PostAsync(urlConsulta, content);

                    if (response.IsSuccessStatusCode)
                    {
                        string resultado = await response.Content.ReadAsStringAsync();
                        respuesta = JsonConvert.DeserializeObject<int>(resultado);
                    }
                    else
                    {
                        string textoError = await response.Content.ReadAsStringAsync();
                        JObject requestException = JsonConvert.DeserializeObject<JObject>(textoError);

                        string errorMostrar = $"No se ha podido crear el pedido\n";
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

            return respuesta;
        }

        public async Task<LineaPedidoCompraDTO> LeerProducto(string empresa, string producto, string proveedor, string ivaCabecera)
        {
            LineaPedidoCompraDTO lineaProducto;
            using (HttpClient client = new HttpClient())
            {
                client.BaseAddress = new Uri(configuracion.servidorAPI);

                // Carlos 21/11/24: Agregar autenticación
                if (!await _servicioAutenticacion.ConfigurarAutorizacion(client))
                {
                    throw new UnauthorizedAccessException("No se pudo configurar la autorización");
                }

                HttpResponseMessage response;

                try
                {
                    string urlConsulta = $"PedidosCompra/ProductoCompra?empresa={empresa}&producto={producto}&proveedor={proveedor}&ivaCabecera={ivaCabecera}";

                    response = await client.GetAsync(urlConsulta);

                    if (response.IsSuccessStatusCode)
                    {
                        string resultado = await response.Content.ReadAsStringAsync();
                        lineaProducto = JsonConvert.DeserializeObject<LineaPedidoCompraDTO>(resultado);
                    }
                    else
                    {
                        throw new Exception($"El producto {producto} no se ha podido cargar correctamente");
                    }
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }

            return lineaProducto;
        }
        public async Task ModificarPedido(PedidoCompraDTO pedido)
        {
            using (HttpClient client = new HttpClient())
            {
                client.BaseAddress = new Uri(configuracion.servidorAPI);

                // Carlos 21/11/24: Agregar autenticación
                if (!await _servicioAutenticacion.ConfigurarAutorizacion(client))
                {
                    throw new UnauthorizedAccessException("No se pudo configurar la autorización");
                }

                HttpResponseMessage response;

                try
                {
                    string urlConsulta = $"PedidosCompra/PedidosCompra";

                    pedido.Usuario = configuracion.usuario;

                    HttpContent content = new StringContent(JsonConvert.SerializeObject(pedido), Encoding.UTF8, "application/json");
                    response = await client.PutAsync(urlConsulta, content);

                    if (response.IsSuccessStatusCode)
                    {
                        string resultado = await response.Content.ReadAsStringAsync();
                    }
                    else
                    {
                        string textoError = await response.Content.ReadAsStringAsync();
                        JObject requestException = JsonConvert.DeserializeObject<JObject>(textoError);

                        string errorMostrar = $"No se ha podido modificar el pedido\n";
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
        }
    }
}
