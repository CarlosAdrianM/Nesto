using Nesto.Infrastructure.Contracts;
using Newtonsoft.Json;
using System;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Nesto.Modules.Producto
{
    /// <summary>
    /// Sugerencia 648 (Paloma, NestoAPI#658): el Excel con lo facturado en la tienda en un periodo (cursos incluidos) para pasar
    /// las cifras de la semana. Lo calcula, lo genera y lo MANDA POR CORREO la API: Nesto corre en el escritorio remoto, donde
    /// Office no tiene licencia, así que el Excel no se abre aquí. Qué tienda puede ver cada uno también lo decide la API.
    /// </summary>
    public interface IServicioFacturacionTienda
    {
        /// <summary>GET api/Informes/FacturacionTienda/Correo: el correo del usuario (su CorreoDefecto); null si no tiene.</summary>
        Task<string> LeerCorreoUsuario();

        /// <summary>
        /// POST api/Informes/FacturacionTienda/Correo. <paramref name="tienda"/> REI o ALC; null: la del usuario (su AlmacénPedidoVta).
        /// <paramref name="correo"/> null o vacío: el del usuario. Devuelve a qué correo se ha mandado y lo que llevaba.
        /// </summary>
        /// <exception cref="FacturacionTiendaException">403 (otra tienda o sin permiso), 400 (periodo, tienda o correo mal), 500 (no se ha podido mandar)…</exception>
        Task<ResultadoCorreoFacturacionTienda> MandarExcelPorCorreo(string tienda, DateTime desde, DateTime hasta, string correo);
    }

    /// <summary>Lo que contesta la API al mandar el Excel por correo.</summary>
    public class ResultadoCorreoFacturacionTienda
    {
        public string Correo { get; set; }
        public string Tienda { get; set; }
        public DateTime Desde { get; set; }
        public DateTime Hasta { get; set; }
        public int NumeroFacturas { get; set; }
        public decimal TotalBaseImponible { get; set; }
        public decimal Total { get; set; }
    }

    public class FacturacionTiendaException : Exception
    {
        public FacturacionTiendaException(string mensaje, int codigo) : base(mensaje)
        {
            Codigo = codigo;
        }

        public int Codigo { get; }
    }

    public class ServicioFacturacionTienda : IServicioFacturacionTienda
    {
        internal const string MENSAJE_SIN_PERMISO =
            "El informe de facturación de tienda solo lo pueden ver Dirección, Administración, Informática y cada tienda el suyo.";
        internal const string URL_CORREO = "Informes/FacturacionTienda/Correo";

        private readonly IClienteApiFactory _clienteApiFactory;

        public ServicioFacturacionTienda(IClienteApiFactory clienteApiFactory)
        {
            _clienteApiFactory = clienteApiFactory ?? throw new ArgumentNullException(nameof(clienteApiFactory));
        }

        /// <summary>El cuerpo del POST: las fechas sin hora; tienda y correo, null si no se dicen.</summary>
        internal static string Cuerpo(string tienda, DateTime desde, DateTime hasta, string correo)
        {
            return JsonConvert.SerializeObject(new
            {
                Tienda = string.IsNullOrWhiteSpace(tienda) ? null : tienda.Trim(),
                Desde = desde.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Hasta = hasta.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Correo = string.IsNullOrWhiteSpace(correo) ? null : correo.Trim()
            });
        }

        public async Task<string> LeerCorreoUsuario()
        {
            using (HttpClient client = _clienteApiFactory.Crear())
            {
                HttpResponseMessage response = await client.GetAsync(URL_CORREO).ConfigureAwait(false);
                await LanzarSiError(response).ConfigureAwait(false);
                string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                string correo = JsonConvert.DeserializeObject<ResultadoCorreoFacturacionTienda>(json)?.Correo;
                return string.IsNullOrWhiteSpace(correo) ? null : correo.Trim();
            }
        }

        public async Task<ResultadoCorreoFacturacionTienda> MandarExcelPorCorreo(string tienda, DateTime desde, DateTime hasta, string correo)
        {
            using (HttpClient client = _clienteApiFactory.Crear())
            {
                HttpContent contenido = new StringContent(Cuerpo(tienda, desde, hasta, correo), Encoding.UTF8, "application/json");
                HttpResponseMessage response = await client.PostAsync(URL_CORREO, contenido).ConfigureAwait(false);
                await LanzarSiError(response).ConfigureAwait(false);
                string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return JsonConvert.DeserializeObject<ResultadoCorreoFacturacionTienda>(json);
            }
        }

        private static async Task LanzarSiError(HttpResponseMessage response)
        {
            if (response.IsSuccessStatusCode)
            {
                return;
            }
            int codigo = (int)response.StatusCode;
            string cuerpo = response.Content == null ? null : await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            string motivo = ProductoService.MotivoDelErrorDeTexto(cuerpo);
            if (string.IsNullOrWhiteSpace(motivo))
            {
                motivo = codigo == 403 ? MENSAJE_SIN_PERMISO : $"El servidor ha contestado con el error {codigo}.";
            }
            throw new FacturacionTiendaException(motivo, codigo);
        }
    }
}
