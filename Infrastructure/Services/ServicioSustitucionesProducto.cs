using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Models;
using Nesto.Infrastructure.Shared;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Nesto.Infrastructure.Services
{
    /// <summary>
    /// NestoAPI#581: sustitución temporal de referencias («mientras tanto, servid la 45685 en lugar de la 25539»).
    /// </summary>
    public interface IServicioSustitucionesProducto
    {
        /// <summary>
        /// GET api/Productos/{producto}/Sustitucion: la que hay que avisar ahora al pedir esa cantidad, o null. Es una
        /// ayuda al meter el pedido: si la API falla o es anterior al endpoint (404), null y no se avisa (sin diálogo).
        /// </summary>
        Task<SustitucionProductoDTO> LeerVigente(string empresa, string producto, int cantidad);

        /// <summary>GET api/Productos/{producto}/Sustituciones: todas, la activa primero. Null si la API aún no lo tiene.</summary>
        Task<List<SustitucionProductoDTO>> Listar(string empresa, string producto);

        /// <summary>POST api/Productos/{producto}/Sustituciones. Lanza con el motivo de la API si no vale.</summary>
        Task<SustitucionProductoDTO> Crear(string producto, NuevaSustitucionProductoDTO nueva);

        /// <summary>DELETE api/Productos/{producto}/Sustituciones/{id}. Lanza con el motivo de la API si no se puede.</summary>
        Task Anular(string empresa, string producto, int id);
    }

    public class ServicioSustitucionesProducto : IServicioSustitucionesProducto
    {
        private readonly IClienteApiFactory _clienteApiFactory;

        public ServicioSustitucionesProducto(IClienteApiFactory clienteApiFactory)
        {
            _clienteApiFactory = clienteApiFactory;
        }

        public async Task<SustitucionProductoDTO> LeerVigente(string empresa, string producto, int cantidad)
        {
            if (string.IsNullOrWhiteSpace(producto))
            {
                return null;
            }
            try
            {
                using (HttpClient client = _clienteApiFactory.Crear())
                {
                    HttpResponseMessage response = await client.GetAsync(
                        $"{Ruta(producto)}/Sustitucion?empresa={Empresa(empresa)}&cantidad={Math.Max(cantidad, 1)}").ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode)
                    {
                        return null;
                    }
                    string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    return string.IsNullOrWhiteSpace(json) ? null : JsonConvert.DeserializeObject<SustitucionProductoDTO>(json);
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        public async Task<List<SustitucionProductoDTO>> Listar(string empresa, string producto)
        {
            using (HttpClient client = _clienteApiFactory.Crear())
            {
                HttpResponseMessage response = await client.GetAsync($"{Ruta(producto)}/Sustituciones?empresa={Empresa(empresa)}").ConfigureAwait(false);
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return null;
                }
                await LanzarSiError(response, "No se han podido cargar las sustituciones del producto").ConfigureAwait(false);
                string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return JsonConvert.DeserializeObject<List<SustitucionProductoDTO>>(json) ?? new List<SustitucionProductoDTO>();
            }
        }

        public async Task<SustitucionProductoDTO> Crear(string producto, NuevaSustitucionProductoDTO nueva)
        {
            using (HttpClient client = _clienteApiFactory.Crear())
            {
                var contenido = new StringContent(JsonConvert.SerializeObject(nueva), Encoding.UTF8, "application/json");
                HttpResponseMessage response = await client.PostAsync($"{Ruta(producto)}/Sustituciones", contenido).ConfigureAwait(false);
                await LanzarSiError(response, "No se ha podido guardar la sustitución").ConfigureAwait(false);
                string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return JsonConvert.DeserializeObject<SustitucionProductoDTO>(json);
            }
        }

        public async Task Anular(string empresa, string producto, int id)
        {
            using (HttpClient client = _clienteApiFactory.Crear())
            {
                HttpResponseMessage response = await client.DeleteAsync($"{Ruta(producto)}/Sustituciones/{id}?empresa={Empresa(empresa)}").ConfigureAwait(false);
                await LanzarSiError(response, "No se ha podido anular la sustitución").ConfigureAwait(false);
            }
        }

        private static string Ruta(string producto)
        {
            return $"Productos/{Uri.EscapeDataString((producto ?? string.Empty).Trim())}";
        }

        private static string Empresa(string empresa)
        {
            return Uri.EscapeDataString(string.IsNullOrWhiteSpace(empresa) ? Constantes.Empresas.EMPRESA_DEFECTO : empresa.Trim());
        }

        private static async Task LanzarSiError(HttpResponseMessage response, string queFallo)
        {
            if (response.IsSuccessStatusCode)
            {
                return;
            }
            string detalle = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            throw new Exception($"{queFallo}: {HttpErrorHelper.ParsearErrorHttp(detalle)}");
        }
    }

    /// <summary>
    /// NestoAPI#581: lo que comparten la plantilla y el detalle del pedido al meter un producto: preguntar a la API si
    /// hay que avisar y no volver a dar la lata con el mismo producto si el usuario ya ha dicho que no en este pedido.
    /// </summary>
    public class ComprobadorSustitucionProducto
    {
        private readonly IServicioSustitucionesProducto _servicio;
        private readonly HashSet<string> _rechazadas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public ComprobadorSustitucionProducto(IServicioSustitucionesProducto servicio)
        {
            _servicio = servicio;
        }

        /// <summary>La sustitución que hay que ofrecer al meter esa cantidad del producto, o null.</summary>
        public async Task<SustitucionProductoDTO> SustitucionAOfrecer(string empresa, string producto, int cantidad)
        {
            string numero = producto?.Trim();
            if (_servicio == null || string.IsNullOrEmpty(numero) || cantidad <= 0 || _rechazadas.Contains(numero))
            {
                return null;
            }
            SustitucionProductoDTO sustitucion = await _servicio.LeerVigente(empresa, numero, cantidad).ConfigureAwait(true);
            if (sustitucion == null || string.IsNullOrWhiteSpace(sustitucion.ProductoSustituto)
                || string.Equals(sustitucion.ProductoSustituto.Trim(), numero, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
            return sustitucion;
        }

        /// <summary>El usuario ha dicho que no: en este pedido no se vuelve a preguntar por ese producto.</summary>
        public void Rechazar(string producto)
        {
            if (!string.IsNullOrWhiteSpace(producto))
            {
                _ = _rechazadas.Add(producto.Trim());
            }
        }

        /// <summary>Pedido nuevo: se vuelve a avisar de todo.</summary>
        public void Olvidar()
        {
            _rechazadas.Clear();
        }

        public const string TITULO = "Producto sustituido";

        /// <summary>«Compras pide servir la 45685 (…) en lugar de la 25539 … ¿Poner la 45685 en su lugar?»</summary>
        public static string Pregunta(SustitucionProductoDTO sustitucion)
        {
            string aviso = string.IsNullOrWhiteSpace(sustitucion.Aviso)
                ? $"Compras pide servir la {sustitucion.ProductoSustituto?.Trim()} en lugar de la {sustitucion.Producto?.Trim()}."
                : sustitucion.Aviso.Trim();
            return $"{aviso}{Environment.NewLine}{Environment.NewLine}¿Poner la {sustitucion.ProductoSustituto?.Trim()} en su lugar?";
        }
    }
}
