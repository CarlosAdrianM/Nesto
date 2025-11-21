using ControlesUsuario.Models;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Prism.Ioc;
using static ControlesUsuario.Models.SelectorProveedorModel;

namespace ControlesUsuario.Services
{
    public class SelectorProveedorService : ISelectorProveedorService
    {
        public async Task<IEnumerable<IFiltrableItem>> BuscarProveedores(string empresa, string filtro)
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
                    urlConsulta = $"Proveedores?empresa={empresa}&filtro={filtro}";

                    response = await client.GetAsync(urlConsulta);

                    if (response.IsSuccessStatusCode)
                    {
                        string resultado = await response.Content.ReadAsStringAsync();
                        return JsonConvert.DeserializeObject<ObservableCollection<ProveedorDTO>>(resultado);
                    }
                    else
                    {
                        return null;
                    }
                }
                catch
                {
                    throw new Exception("No se encontró ningún proveedor con el texto " + filtro);
                }
            }
        }

        public async Task<ProveedorDTO> CargarProveedor(string empresa, string proveedor, string contacto)
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
                    string urlConsulta = $"Proveedores?empresa={empresa}&proveedor={proveedor}&contacto={contacto}"; //contacto en blanco para que coja ProveedorPrincipal

                    response = await client.GetAsync(urlConsulta);

                    if (response.IsSuccessStatusCode)
                    {
                        string resultado = await response.Content.ReadAsStringAsync();
                        return JsonConvert.DeserializeObject<ProveedorDTO>(resultado);
                    }
                    else
                    {
                        return null;
                    }
                }
                catch (Exception e)
                {
                    throw new Exception("No se pudo leer el proveedor", e);
                }
            }
        }
    }
}
