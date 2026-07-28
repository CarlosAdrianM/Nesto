using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.CanalesExternos.Interfaces;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Nesto.Modulos.CanalesExternos.Services
{
    /// <summary>
    /// Nesto#434: cliente de los endpoints de facturas Amazon de NestoAPI (#366). El servidor
    /// factura, genera el PDF y lo sube por SP-API; aquí solo se llama con JWT.
    /// </summary>
    public class FacturasAmazonService : IFacturasAmazonService
    {
        private readonly IConfiguracion _configuracion;
        private readonly IServicioAutenticacion _servicioAutenticacion;

        public FacturasAmazonService(IConfiguracion configuracion, IServicioAutenticacion servicioAutenticacion)
        {
            _configuracion = configuracion;
            _servicioAutenticacion = servicioAutenticacion;
        }

        public async Task<SubirFacturaAmazonResponse> FacturarYSubirAsync(string empresa, int pedido)
        {
            using HttpClient client = await CrearClienteAsync();
            StringContent contenido = new(
                JsonConvert.SerializeObject(new { Empresa = empresa, Pedido = pedido }),
                Encoding.UTF8, "application/json");
            HttpResponseMessage respuesta = await client.PostAsync("CanalesExternos/Amazon/SubirFactura", contenido);
            string cuerpo = await respuesta.Content.ReadAsStringAsync();
            if (!respuesta.IsSuccessStatusCode)
            {
                throw new Exception(ExtraerMensajeError(cuerpo, (int)respuesta.StatusCode));
            }
            return JsonConvert.DeserializeObject<SubirFacturaAmazonResponse>(cuerpo);
        }

        public async Task<Dictionary<int, FacturaSubidaAmazon>> ConsultarSubidasAsync(string empresa, IEnumerable<int> pedidos)
        {
            List<int> lista = pedidos?.Distinct().ToList() ?? new List<int>();
            if (lista.Count == 0)
            {
                return new Dictionary<int, FacturaSubidaAmazon>();
            }
            using HttpClient client = await CrearClienteAsync();
            HttpResponseMessage respuesta = await client.GetAsync(
                $"CanalesExternos/Amazon/FacturasSubidas?empresa={Uri.EscapeDataString(empresa)}&pedidos={string.Join(",", lista)}");
            string cuerpo = await respuesta.Content.ReadAsStringAsync();
            if (!respuesta.IsSuccessStatusCode)
            {
                throw new Exception(ExtraerMensajeError(cuerpo, (int)respuesta.StatusCode));
            }
            List<FacturaSubidaAmazon> subidas = JsonConvert.DeserializeObject<List<FacturaSubidaAmazon>>(cuerpo);
            return subidas.GroupBy(s => s.Pedido).ToDictionary(g => g.Key, g => g.First());
        }

        private async Task<HttpClient> CrearClienteAsync()
        {
            HttpClient client = new()
            {
                BaseAddress = new Uri(_configuracion.servidorAPI)
            };
            if (!await _servicioAutenticacion.ConfigurarAutorizacion(client))
            {
                client.Dispose();
                throw new UnauthorizedAccessException("No se pudo configurar la autorización");
            }
            return client;
        }

        // Web API devuelve los BadRequest como {"Message":"..."}; si no se puede parsear, se
        // muestra el cuerpo crudo con el código.
        private static string ExtraerMensajeError(string cuerpo, int codigo)
        {
            try
            {
                string mensaje = (string)JObject.Parse(cuerpo)["Message"];
                if (!string.IsNullOrWhiteSpace(mensaje))
                {
                    return mensaje;
                }
            }
            catch
            {
                // cuerpo no JSON: caemos al mensaje genérico
            }
            return $"Error {codigo} al llamar a la API de facturas de Amazon: {cuerpo}";
        }
    }
}
