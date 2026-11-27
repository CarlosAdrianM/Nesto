using ControlesUsuario.Models;
using Nesto.Infrastructure.Contracts;
using System;
using System.Net.Http;
using System.Threading.Tasks;
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
                IConfiguracion configuracion = Contenedor.Actual.Resolver<IConfiguracion>();
                IServicioAutenticacion servicioAutenticacion = Contenedor.Actual.Resolver<IServicioAutenticacion>();

                client.BaseAddress = new Uri(configuracion.servidorAPI);

                // Carlos 21/11/24: Agregar autenticación
                if (!await servicioAutenticacion.ConfigurarAutorizacion(client))
                {
                    throw new UnauthorizedAccessException("No se pudo configurar la autorización");
                }

                HttpResponseMessage response;

                try
                {
                    string urlConsulta = await ConstruirUrlBusqueda(configuracion, empresa, vendedor, filtro, contactoTelefonicoPrimero);

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

        /// <summary>
        /// La URL de la búsqueda. Sin vendedor (todos los clientes) para Administración, Dirección y quien tenga el
        /// parámetro PermitirVerClientesTodosLosVendedores (incidencia 1506: tiendas y centralita, que crean pedidos a
        /// clientes de otros vendedores); al resto, solo los clientes de su vendedor (la API filtra por su equipo).
        /// Se decide aquí, y no en cada pantalla, para que lo hereden todas las que usan el SelectorCliente.
        /// Con contactoTelefonicoPrimero (sugerencia 582, Rapports) el parámetro no se mira: la API necesita el
        /// vendedor para saber si es telefónico, y sin él el contacto en estado 9 dejaría de salir el primero.
        /// </summary>
        internal async Task<string> ConstruirUrlBusqueda(IConfiguracion configuracion, string empresa, string vendedor, string filtro, bool contactoTelefonicoPrimero)
        {
            bool todosLosVendedores = configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ADMINISTRACION)
                || configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.DIRECCION)
                || (!contactoTelefonicoPrimero && await PuedeVerClientesTodosLosVendedores(configuracion));

            string urlConsulta = "Clientes?empresa=" + empresa + "&vendedor=" + (todosLosVendedores ? string.Empty : vendedor) + "&filtro=" + filtro;
            if (contactoTelefonicoPrimero)
            {
                urlConsulta += "&contactoTelefonicoPrimero=true";
            }
            return urlConsulta;
        }

        // Incidencia 1506: se lee la primera vez que se busca y vale para toda la vida del selector (no en cada tecla;
        // leerParametro no cachea). Solo se guarda una lectura buena: si falla, se busca como siempre (con el vendedor)
        // y se vuelve a intentar en la búsqueda siguiente.
        private bool? _verClientesTodosLosVendedores;
        private async Task<bool> PuedeVerClientesTodosLosVendedores(IConfiguracion configuracion)
        {
            if (_verClientesTodosLosVendedores.HasValue)
            {
                return _verClientesTodosLosVendedores.Value;
            }
            try
            {
                string valor = await configuracion.leerParametro(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.PermitirVerClientesTodosLosVendedores);
                _verClientesTodosLosVendedores = Parametros.EsSi(valor);
                return _verClientesTodosLosVendedores.Value;
            }
            catch
            {
                return false;
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
                IConfiguracion configuracion = Contenedor.Actual.Resolver<IConfiguracion>();
                IServicioAutenticacion servicioAutenticacion = Contenedor.Actual.Resolver<IServicioAutenticacion>();

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
