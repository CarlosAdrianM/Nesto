using Nesto.Infrastructure.Contracts;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Threading.Tasks;

namespace Nesto.Modules.Producto
{
    /// <summary>
    /// Sugerencia 648 (Paloma, NestoAPI#658): el Excel con lo facturado en la tienda en un periodo (cursos incluidos) para pasar
    /// las cifras de la semana. Lo calcula y lo genera la API; qué tienda puede ver cada uno también lo decide la API.
    /// </summary>
    public interface IServicioFacturacionTienda
    {
        /// <summary>
        /// GET api/Informes/FacturacionTienda/Excel. <paramref name="tienda"/> REI o ALC; null: la del usuario (su AlmacénPedidoVta).
        /// </summary>
        /// <exception cref="FacturacionTiendaException">403 (otra tienda o sin permiso), 400 (periodo o tienda mal)…</exception>
        Task<byte[]> DescargarExcel(string tienda, DateTime desde, DateTime hasta);
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

        private readonly IClienteApiFactory _clienteApiFactory;

        public ServicioFacturacionTienda(IClienteApiFactory clienteApiFactory)
        {
            _clienteApiFactory = clienteApiFactory ?? throw new ArgumentNullException(nameof(clienteApiFactory));
        }

        internal static string Url(string tienda, DateTime desde, DateTime hasta)
        {
            var parametros = new List<string>
            {
                "desde=" + desde.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                "hasta=" + hasta.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            };
            if (!string.IsNullOrWhiteSpace(tienda))
            {
                parametros.Insert(0, "tienda=" + Uri.EscapeDataString(tienda.Trim()));
            }
            return "Informes/FacturacionTienda/Excel?" + string.Join("&", parametros);
        }

        public async Task<byte[]> DescargarExcel(string tienda, DateTime desde, DateTime hasta)
        {
            using (HttpClient client = _clienteApiFactory.Crear())
            {
                HttpResponseMessage response = await client.GetAsync(Url(tienda, desde, hasta)).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    int codigo = (int)response.StatusCode;
                    string cuerpo = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    string motivo = ProductoService.MotivoDelErrorDeTexto(cuerpo);
                    if (string.IsNullOrWhiteSpace(motivo))
                    {
                        motivo = codigo == 403 ? MENSAJE_SIN_PERMISO : $"El servidor ha contestado con el error {codigo}.";
                    }
                    throw new FacturacionTiendaException(motivo, codigo);
                }
                return await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            }
        }
    }
}
