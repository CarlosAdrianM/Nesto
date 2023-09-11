using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Modules.Producto.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using static Nesto.Infrastructure.Shared.Constantes;

namespace Nesto.Modules.Producto
{
    public class ProductoService : IProductoService
    {
        private readonly IConfiguracion configuracion;
        private readonly string EmpresaDefecto = "1";


        public ProductoService(IConfiguracion configuracion)
        {
            this.configuracion = configuracion;
        }

        public async Task<ICollection<ProductoClienteModel>> BuscarClientes(string producto)
        {
            ICollection<ProductoClienteModel> clientes;
            using (HttpClient client = new HttpClient())
            {
                client.BaseAddress = new Uri(configuracion.servidorAPI);
                HttpResponseMessage response;

                try
                {
                    string vendedor = await configuracion.leerParametro(EmpresaDefecto, Parametros.Claves.Vendedor);
                    string todosLosClientes = await configuracion.leerParametro(EmpresaDefecto, Parametros.Claves.PermitirVerClientesTodosLosVendedores);
                    string urlConsulta;
                    if (todosLosClientes == "1")
                    {
                        urlConsulta = "Productos?empresa=" + EmpresaDefecto + "&id=" + producto + "&vendedor=";
                    }
                    else
                    {
                        urlConsulta = "Productos?empresa=" + EmpresaDefecto + "&id=" + producto + "&vendedor=" + vendedor;
                    }
                        


                    response = await client.GetAsync(urlConsulta);

                    if (response.IsSuccessStatusCode)
                    {
                        string resultado = await response.Content.ReadAsStringAsync();
                        clientes = JsonConvert.DeserializeObject<ICollection<ProductoClienteModel>>(resultado);
                    }
                    else
                    {
                        throw new Exception("No se han podido cargar los clientes que han comprado el producto "+ producto);
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
            using (HttpClient client = new HttpClient())
            {
                client.BaseAddress = new Uri(configuracion.servidorAPI);
                HttpResponseMessage response;

                try
                {
                    string urlConsulta = "Productos?empresa=" + EmpresaDefecto + "&filtroNombre=" + filtroNombre + "&filtroFamilia=" + filtroFamilia + "&filtroSubgrupo=" + filtroSubgrupo;


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

        public async Task CrearControlStock(ControlStock controlStock)
        {
            using (HttpClient client = new HttpClient())
            {
                client.BaseAddress = new Uri(configuracion.servidorAPI);
                HttpResponseMessage response;

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
                        var innerException = requestException != null ? requestException["InnerException"] : null;
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

        public async Task GuardarControlStock(ControlStock controlStock)
        {
            using (HttpClient client = new HttpClient())
            {
                client.BaseAddress = new Uri(configuracion.servidorAPI);
                HttpResponseMessage response;

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
        }

        public async Task<ControlStockProductoModel> LeerControlStock(string producto)
        {
            ControlStockProductoModel controlStock;
            using (HttpClient client = new HttpClient())
            {
                client.BaseAddress = new Uri(configuracion.servidorAPI);
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

        public async Task<ProductoModel> LeerProducto(string producto)
        {
            ProductoModel productoActual;
            if (producto == null || producto == "")
            {
                //producto = await configuracion.leerParametro(EmpresaDefecto, "UltNumProducto");
                return null;
            }
            using (HttpClient client = new HttpClient())
            {
                client.BaseAddress = new Uri(configuracion.servidorAPI);
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
    }
}
