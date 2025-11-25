using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
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
        private readonly IServicioAutenticacion _servicioAutenticacion;

        public ClienteService(IConfiguracion configuracion, IServicioAutenticacion servicioAutenticacion)
        {
            this.configuracion = configuracion;
            _servicioAutenticacion = servicioAutenticacion;
        }

        public async Task<Clientes> CrearCliente(ClienteCrear cliente)
        {
            Clientes respuesta;

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
                        // Carlos 24/11/24: Usar HttpErrorHelper para parsear errores correctamente
                        string textoError = await response.Content.ReadAsStringAsync();
                        JObject requestException = JsonConvert.DeserializeObject<JObject>(textoError);
                        string mensajeError = HttpErrorHelper.ParsearErrorHttp(requestException);
                        throw new Exception("No se ha podido crear el cliente " + cliente.Nombre + "\n" + mensajeError);
                    }
                }
                catch
                {
                    // Carlos 24/11/24: Solo 'throw' para preservar el stack trace completo
                    throw;
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

                // Carlos 21/11/24: Agregar autenticación
                if (!await _servicioAutenticacion.ConfigurarAutorizacion(client))
                {
                    throw new UnauthorizedAccessException("No se pudo configurar la autorización");
                }

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
                        // Carlos 24/11/24: Usar HttpErrorHelper para parsear errores correctamente
                        string textoError = await response.Content.ReadAsStringAsync();
                        JObject requestException = JsonConvert.DeserializeObject<JObject>(textoError);
                        string mensajeError = HttpErrorHelper.ParsearErrorHttp(requestException);
                        throw new Exception(String.Format("No existe el cliente {0}/{1}/{2}\n{3}", empresa, cliente, contacto, mensajeError));
                    }
                }
                catch
                {
                    // Carlos 24/11/24: Solo 'throw' para preservar el stack trace completo
                    throw;
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

                // Carlos 21/11/24: Agregar autenticación
                if (!await _servicioAutenticacion.ConfigurarAutorizacion(client))
                {
                    throw new UnauthorizedAccessException("No se pudo configurar la autorización");
                }

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
                        // Carlos 24/11/24: Usar HttpErrorHelper para parsear errores correctamente
                        string textoError = await response.Content.ReadAsStringAsync();
                        JObject requestException = JsonConvert.DeserializeObject<JObject>(textoError);
                        string mensajeError = HttpErrorHelper.ParsearErrorHttp(requestException);
                        throw new Exception("No se ha podido modificar el cliente " + cliente.Nombre + "\n" + mensajeError);
                    }
                }
                catch
                {
                    // Carlos 24/11/24: Solo 'throw' para preservar el stack trace completo
                    throw;
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

                // Carlos 21/11/24: Agregar autenticación
                if (!await _servicioAutenticacion.ConfigurarAutorizacion(client))
                {
                    throw new UnauthorizedAccessException("No se pudo configurar la autorización");
                }

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
                        // Carlos 24/11/24: Usar HttpErrorHelper para parsear errores correctamente
                        string textoError = await response.Content.ReadAsStringAsync();
                        JObject requestException = JsonConvert.DeserializeObject<JObject>(textoError);
                        string mensajeError = HttpErrorHelper.ParsearErrorHttp(requestException);
                        throw new Exception("No se ha podido validar la dirección en el código postal " + codigoPostal + "\n" + mensajeError);
                    }
                }
                catch
                {
                    // Carlos 24/11/24: Solo 'throw' para preservar el stack trace completo
                    throw;
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

                // Carlos 21/11/24: Agregar autenticación
                if (!await _servicioAutenticacion.ConfigurarAutorizacion(client))
                {
                    throw new UnauthorizedAccessException("No se pudo configurar la autorización");
                }

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
                        // Carlos 24/11/24: Usar HttpErrorHelper para parsear errores correctamente
                        string textoError = await response.Content.ReadAsStringAsync();
                        JObject requestException = JsonConvert.DeserializeObject<JObject>(textoError);
                        string mensajeError = HttpErrorHelper.ParsearErrorHttp(requestException);
                        throw new Exception("No se ha podido validar la forma de pago " + formaPago + "\n" + mensajeError);
                    }
                }
                catch
                {
                    // Carlos 24/11/24: Solo 'throw' para preservar el stack trace completo
                    throw;
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

                // Carlos 21/11/24: Agregar autenticación
                if (!await _servicioAutenticacion.ConfigurarAutorizacion(client))
                {
                    throw new UnauthorizedAccessException("No se pudo configurar la autorización");
                }

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
                        // Carlos 24/11/24: Usar HttpErrorHelper para parsear errores correctamente
                        string textoError = await response.Content.ReadAsStringAsync();
                        JObject requestException = JsonConvert.DeserializeObject<JObject>(textoError);
                        string mensajeError = HttpErrorHelper.ParsearErrorHttp(requestException);
                        throw new Exception("No se ha podido validar el NIF " + nif + "\n" + mensajeError);
                    }
                }
                catch
                {
                    // Carlos 24/11/24: Solo 'throw' para preservar el stack trace completo
                    throw;
                }
            }

            return respuesta;
        }
        
    }
}
