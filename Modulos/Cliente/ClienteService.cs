using Nesto.Contratos;
using Nesto.Models;
using Nesto.Models.Nesto.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Nesto.Modulos.Cliente
{
    public class ClienteService : IClienteService
    {
        private readonly IConfiguracion configuracion;

        public ClienteService(IConfiguracion configuracion)
        {
            this.configuracion = configuracion;
        }

        public async Task<Clientes> CrearCliente(ClienteCrear cliente)
        {
            Clientes respuesta;

            using (HttpClient client = new HttpClient())
            {
                client.BaseAddress = new Uri(configuracion.servidorAPI);
                HttpResponseMessage response;

                try
                {
                    string urlConsulta = "Clientes";

                    HttpContent content = new StringContent(JsonConvert.SerializeObject(cliente), Encoding.UTF8, "application/json");
                    response = await client.PostAsync(urlConsulta, content);

                    if (response.IsSuccessStatusCode)
                    {
                        string resultado = await response.Content.ReadAsStringAsync();
                        respuesta = JsonConvert.DeserializeObject<Clientes>(resultado);
                    }
                    else
                    {
                        string textoError = await response.Content.ReadAsStringAsync();
                        JObject requestException = JsonConvert.DeserializeObject<JObject>(textoError);

                        string errorMostrar = "No se ha podido crear el cliente " + cliente.Nombre + "\n";
                        if (requestException["exceptionMessage"]!=null)
                        {
                            errorMostrar += requestException["exceptionMessage"] + "\n";
                        }
                        if (requestException["ModelState"]!=null)
                        {
                            var firstError = requestException["ModelState"];
                            var nodoError = firstError.LastOrDefault();
                            errorMostrar += nodoError.FirstOrDefault()[0];
                        }
                        var innerException = requestException["InnerException"];
                        while (innerException!=null)
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

        public async Task<ClienteCrear> LeerClienteCrear(string empresa, string cliente, string contacto)
        {
            ClienteCrear respuesta;

            using (HttpClient client = new HttpClient())
            {
                client.BaseAddress = new Uri(configuracion.servidorAPI);
                HttpResponseMessage response;

                try
                {
                    string urlConsulta = "Clientes/GetClienteCrear?empresa=" + empresa + 
                        "&cliente=" + cliente + "&contacto=" + contacto;
                    
                    response = await client.GetAsync(urlConsulta);

                    if (response.IsSuccessStatusCode)
                    {
                        string resultado = await response.Content.ReadAsStringAsync();
                        respuesta = JsonConvert.DeserializeObject<ClienteCrear>(resultado);
                    }
                    else
                    {
                        throw new Exception(String.Format("No existe el cliente {0}/{1}/{2}", empresa, cliente, contacto));
                    }
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }

            return respuesta;
        }

        public async Task<Clientes> ModificarCliente(ClienteCrear cliente)
        {
            Clientes respuesta;

            using (HttpClient client = new HttpClient())
            {
                client.BaseAddress = new Uri(configuracion.servidorAPI);
                HttpResponseMessage response;

                try
                {
                    string urlConsulta = "Clientes";

                    HttpContent content = new StringContent(JsonConvert.SerializeObject(cliente), Encoding.UTF8, "application/json");
                    response = await client.PutAsync(urlConsulta, content);

                    if (response.IsSuccessStatusCode)
                    {
                        string resultado = await response.Content.ReadAsStringAsync();
                        respuesta = JsonConvert.DeserializeObject<Clientes>(resultado);
                    }
                    else
                    {
                        string textoError = await response.Content.ReadAsStringAsync();
                        JObject requestException = JsonConvert.DeserializeObject<JObject>(textoError);

                        string errorMostrar = "No se ha podido modificar el cliente " + cliente.Nombre + "\n";
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

        public async Task<RespuestaDatosGeneralesClientes> ValidarDatosGenerales(string direccion, string codigoPostal, string telefono)
        {
            RespuestaDatosGeneralesClientes respuesta;

            using (HttpClient client = new HttpClient())
            {
                client.BaseAddress = new Uri(configuracion.servidorAPI);
                HttpResponseMessage response;

                try
                {
                    string urlConsulta = "Clientes/ComprobarDatosGenerales?direccion=" + direccion
                        + "&codigoPostal=" + codigoPostal
                        + "&telefono="+telefono;


                    response = await client.GetAsync(urlConsulta);

                    if (response.IsSuccessStatusCode)
                    {
                        string resultado = await response.Content.ReadAsStringAsync();
                        respuesta = JsonConvert.DeserializeObject<RespuestaDatosGeneralesClientes>(resultado);
                    }
                    else
                    {
                        string textoError = await response.Content.ReadAsStringAsync();
                        dynamic requestException = JsonConvert.DeserializeObject<dynamic>(textoError);
                        throw new Exception("No se ha podido validar la dirección en el código postal " + codigoPostal + "\n" +
                            requestException["exceptionMessage"]);
                    }
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }

            return respuesta;
        }

        public async Task<RespuestaDatosBancoCliente> ValidarDatosPago(string formaPago, string plazosPago, string iban)
        {
            RespuestaDatosBancoCliente respuesta;

            using (HttpClient client = new HttpClient())
            {
                client.BaseAddress = new Uri(configuracion.servidorAPI);
                HttpResponseMessage response;

                try
                {
                    string urlConsulta = "Clientes/ComprobarDatosBanco?formaPago=" + formaPago + 
                        "&plazosPago=" + plazosPago +
                        "&iban="+ iban;


                    response = await client.GetAsync(urlConsulta);

                    if (response.IsSuccessStatusCode)
                    {
                        string resultado = await response.Content.ReadAsStringAsync();
                        respuesta = JsonConvert.DeserializeObject<RespuestaDatosBancoCliente>(resultado);
                    }
                    else
                    {
                        throw new Exception("No se ha podido validar la forma de pago " + formaPago);
                    }
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }

            return respuesta;
        }

        public async Task<RespuestaNifNombreCliente> ValidarNif(string nif, string nombre)
        {
            RespuestaNifNombreCliente respuesta;
            
            using (HttpClient client = new HttpClient())
            {
                client.BaseAddress = new Uri(configuracion.servidorAPI);
                HttpResponseMessage response;

                try
                {
                    string urlConsulta = "Clientes/ComprobarNifNombre?nif="+nif+"&nombre="+nombre;


                    response = await client.GetAsync(urlConsulta);

                    if (response.IsSuccessStatusCode)
                    {
                        string resultado = await response.Content.ReadAsStringAsync();
                        respuesta = JsonConvert.DeserializeObject<RespuestaNifNombreCliente>(resultado);
                    }
                    else
                    {
                        throw new Exception("No se ha podido validar el NIF " + nif);
                    }
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }

            return respuesta;
        }
        
    }
}
