using ControlesUsuario.Models;
using Nesto.Infrastructure.Contracts;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using Prism.Ioc;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using Nesto.Infrastructure.Shared;

namespace ControlesUsuario.Services
{
    public class SelectorClienteService : ISelectorClienteService
    {
        public Task<ObservableCollection<ClienteDTO>> BuscarClientes(string empresa, string vendedor, string filtro)
            => BuscarClientes(empresa, vendedor, filtro, false);

        // Sugerencia 582: la API pone delante el contacto en estado 9 si el vendedor es telefónico
        public Task<ObservableCollection<ClienteDTO>> BuscarClientesContactoTelefonicoPrimero(string empresa, string vendedor, string filtro)
            => BuscarClientes(empresa, vendedor, filtro, true);

        private async Task<ObservableCollection<ClienteDTO>> BuscarClientes(string empresa, string vendedor, string filtro, bool contactoTelefonicoPrimero)
        {
            using (HttpClient client = new HttpClient())
            {
                IConfiguracion configuracion = ContainerLocator.Container.Resolve<IConfiguracion>();
                IServicioAutenticacion servicioAutenticacion = ContainerLocator.Container.Resolve<IServicioAutenticacion>();

                client.BaseAddress = new Uri(configuracion.servidorAPI);

                // Carlos 21/11/24: Agregar autenticación
                if (!await servicioAutenticacion.ConfigurarAutorizacion(client))
                {
                    throw new UnauthorizedAccessException("No se pudo configurar la autorización");
                }

                HttpResponseMessage response;

                try
                {
                    string urlConsulta;
                    if (configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ADMINISTRACION) || configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.DIRECCION))
                    {
                        urlConsulta = "Clientes?empresa=" + empresa + "&vendedor=&filtro=" + filtro;
                    }
                    else
                    {
                        urlConsulta = "Clientes?empresa=" + empresa + "&vendedor=" + vendedor + "&filtro=" + filtro;
                    }
                    if (contactoTelefonicoPrimero)
                    {
                        urlConsulta += "&contactoTelefonicoPrimero=true";
                    }



                    response = await client.GetAsync(urlConsulta);

                    if (response.IsSuccessStatusCode)
                    {
                        string resultado = await response.Content.ReadAsStringAsync();
                        return JsonConvert.DeserializeObject<ObservableCollection<ClienteDTO>>(resultado);
                    }
                    else
                    {
                        return null;
                    }
                }
                catch
                {
                    throw new Exception("No se encontró ningún cliente con el texto " + filtro);
                }
            }

        }

        public Task<ClienteDTO> CargarCliente(string empresa, string cliente, string contacto)
            => CargarCliente(empresa, cliente, contacto, null);

        // Sugerencia 582: sin contacto; si el vendedor es telefónico, la API devuelve el contacto en estado 9
        public Task<ClienteDTO> CargarClienteContactoTelefonico(string empresa, string cliente, string vendedor)
            => CargarCliente(empresa, cliente, null, vendedor);

        private async Task<ClienteDTO> CargarCliente(string empresa, string cliente, string contacto, string vendedorTelefonico)
        {
            using (HttpClient client = new HttpClient())
            {
                IConfiguracion configuracion = ContainerLocator.Container.Resolve<IConfiguracion>();
                IServicioAutenticacion servicioAutenticacion = ContainerLocator.Container.Resolve<IServicioAutenticacion>();

                client.BaseAddress = new Uri(configuracion.servidorAPI);

                // Carlos 21/11/24: Agregar autenticación
                if (!await servicioAutenticacion.ConfigurarAutorizacion(client))
                {
                    throw new UnauthorizedAccessException("No se pudo configurar la autorización");
                }

                HttpResponseMessage response;

                try
                {
                    string urlConsulta = "Clientes?empresa=" + empresa + "&cliente=" + cliente + "&contacto=" + contacto; //contacto en blanco para que coja clientePrincipal
                    if (!string.IsNullOrWhiteSpace(vendedorTelefonico))
                    {
                        urlConsulta += "&vendedorTelefonico=" + vendedorTelefonico;
                    }

                    response = await client.GetAsync(urlConsulta);

                    if (response.IsSuccessStatusCode)
                    {
                        string resultado = await response.Content.ReadAsStringAsync();
                        return JsonConvert.DeserializeObject<ClienteDTO>(resultado);
                    }
                    else
                    {
                        return null;
                    }
                }
                catch (Exception e)
                {
                    throw new Exception("No se pudo leer el cliente", e);
                }
            }
        }
    }
}
