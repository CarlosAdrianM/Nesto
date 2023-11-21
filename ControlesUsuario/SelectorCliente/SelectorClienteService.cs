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
        public async Task<ObservableCollection<ClienteDTO>> BuscarClientes(string empresa, string vendedor, string filtro)
        {
            using (HttpClient client = new HttpClient())
            {
                IConfiguracion configuracion = ContainerLocator.Container.Resolve<IConfiguracion>();
                client.BaseAddress = new Uri(configuracion.servidorAPI);
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

        public async Task<ClienteDTO> CargarCliente(string empresa, string cliente, string contacto)
        {
            using (HttpClient client = new HttpClient())
            {
                IConfiguracion configuracion = ContainerLocator.Container.Resolve<IConfiguracion>();
                client.BaseAddress = new Uri(configuracion.servidorAPI);
                HttpResponseMessage response;

                try
                {
                    string urlConsulta = "Clientes?empresa=" + empresa + "&cliente=" + cliente + "&contacto=" + contacto; //contacto en blanco para que coja clientePrincipal

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
