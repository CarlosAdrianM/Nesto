using Nesto.Contratos;
using Newtonsoft.Json;
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
        private readonly IConfiguracion configuracion;
        private readonly string EmpresaDefecto = "1";


        public ProductoService(IConfiguracion configuracion)
        {
            this.configuracion = configuracion;
        }

        public async Task<ProductoModel> LeerProducto(string producto)
        {
            ProductoModel productoActual;
            if (producto == null || producto == "")
            {
                producto = await configuracion.leerParametro(EmpresaDefecto, "UltNumProducto");
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

                return productoActual;
            }
        }
    }
}
