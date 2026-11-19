using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.Cliente.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Nesto.Modulos.Cliente
{
    /// <summary>NestoAPI#879: la API todavía no tiene PUT api/Clientes/ContactoDefecto (404).</summary>
    public class ContactoDefectoNoDisponibleException : Exception
    {
        public ContactoDefectoNoDisponibleException()
            : base("El servidor todavía no permite cambiar la dirección de entrega por defecto. Inténtelo cuando se actualice.") { }
    }

    /// <summary>
    /// NestoAPI#879 (corte 3): las direcciones (contactos) del cliente con cuál es la de facturación y cuál la de entrega por defecto,
    /// y el cambio de la de entrega por defecto.
    /// </summary>
    public interface IDireccionesClienteService
    {
        Task<List<DireccionClienteModel>> Leer(string empresa, string cliente);
        /// <summary>Lanza con el texto de la API (400) o <see cref="ContactoDefectoNoDisponibleException"/> (404 de la API vieja).</summary>
        Task<ContactoDefectoClienteModel> CambiarEntregaPorDefecto(string empresa, string cliente, string contacto);
    }

    /// <summary>Por HTTP contra NestoAPI (patrón DiaRepartoClienteService del mismo módulo).</summary>
    public class DireccionesClienteService : IDireccionesClienteService
    {
        private readonly IConfiguracion configuracion;
        private readonly IServicioAutenticacion _servicioAutenticacion;

        public DireccionesClienteService(IConfiguracion configuracion, IServicioAutenticacion servicioAutenticacion)
        {
            this.configuracion = configuracion;
            _servicioAutenticacion = servicioAutenticacion;
        }

        private async Task<HttpClient> Cliente()
        {
            var client = new HttpClient { BaseAddress = new Uri(configuracion.servidorAPI) };
            if (!await _servicioAutenticacion.ConfigurarAutorizacion(client))
            {
                client.Dispose();
                throw new UnauthorizedAccessException("No se pudo configurar la autorización");
            }
            return client;
        }

        internal static string UrlLeer(string empresa, string cliente)
            => $"PlantillaVentas/DireccionesEntrega?empresa={Uri.EscapeDataString(empresa?.Trim() ?? "1")}&clienteDirecciones={Uri.EscapeDataString(cliente?.Trim() ?? string.Empty)}";

        public async Task<List<DireccionClienteModel>> Leer(string empresa, string cliente)
        {
            using HttpClient client = await Cliente();
            HttpResponseMessage response = await client.GetAsync(UrlLeer(empresa, cliente));
            string body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"No se han podido leer las direcciones del cliente: {DiaRepartoClienteService.ExtraerMensaje(body)}");
            }
            return JsonConvert.DeserializeObject<List<DireccionClienteModel>>(body) ?? new List<DireccionClienteModel>();
        }

        public async Task<ContactoDefectoClienteModel> CambiarEntregaPorDefecto(string empresa, string cliente, string contacto)
        {
            using HttpClient client = await Cliente();
            var cuerpo = new CambiarContactoDefectoModel { Empresa = empresa?.Trim(), Cliente = cliente?.Trim(), Contacto = contacto?.Trim() };
            HttpResponseMessage response = await client.PutAsync("Clientes/ContactoDefecto",
                new StringContent(JsonConvert.SerializeObject(cuerpo), Encoding.UTF8, "application/json"));
            string body = await response.Content.ReadAsStringAsync();
            // 404 sin texto = la API vieja (no conoce la ruta); con texto = «No existe el cliente …»
            if (response.StatusCode == HttpStatusCode.NotFound && !EsMensajeDeLaApi(body))
            {
                throw new ContactoDefectoNoDisponibleException();
            }
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception(DiaRepartoClienteService.ExtraerMensaje(body));
            }
            return JsonConvert.DeserializeObject<ContactoDefectoClienteModel>(body) ?? new ContactoDefectoClienteModel { ContactoDefecto = cuerpo.Contacto };
        }

        /// <summary>El 404 del cliente que no existe trae el texto como cadena JSON; el de la ruta desconocida, un objeto con «No HTTP resource».</summary>
        internal static bool EsMensajeDeLaApi(string body)
        {
            string mensaje = DiaRepartoClienteService.ExtraerMensaje(body);
            return !string.IsNullOrWhiteSpace(mensaje) && mensaje.StartsWith("No existe el cliente", StringComparison.Ordinal);
        }
    }
}
