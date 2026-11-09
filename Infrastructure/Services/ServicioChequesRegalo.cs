using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Models;
using Nesto.Infrastructure.Shared;
using Newtonsoft.Json;
using System;
using System.Globalization;
using System.Net.Http;
using System.Threading.Tasks;

namespace Nesto.Infrastructure.Services
{
    /// <summary>
    /// NestoAPI#593 (c5): el cheque regalo del cliente, para ofrecerlo al meter el pedido (plantilla y detalle).
    /// </summary>
    public interface IServicioChequesRegalo
    {
        /// <summary>
        /// GET api/ChequesRegalo/Cliente?empresa=1&amp;cliente=X. Null si el cliente no tiene cheque en una campaña activa
        /// (404), si la API falla o si es anterior al endpoint: es una ayuda, nunca lanza ni enseña diálogos.
        /// </summary>
        Task<ChequeRegaloClienteDTO> LeerDelCliente(string empresa, string cliente);
        /// <summary>
        /// Sugerencia 1130 (Laura, 09/11/26): lo mismo, pero mirando el plazo con <paramref name="fecha"/> (la del pedido de la
        /// tienda online que se importa): <c>&amp;fecha=yyyy-MM-ddTHH:mm:ss</c>. La API la ignora si es posterior a ahora.
        /// Null = como la de dos parámetros.
        /// </summary>
        Task<ChequeRegaloClienteDTO> LeerDelCliente(string empresa, string cliente, DateTime? fecha);
    }

    public class ServicioChequesRegalo : IServicioChequesRegalo
    {
        private readonly IClienteApiFactory _clienteApiFactory;

        public ServicioChequesRegalo(IClienteApiFactory clienteApiFactory)
        {
            _clienteApiFactory = clienteApiFactory;
        }

        public Task<ChequeRegaloClienteDTO> LeerDelCliente(string empresa, string cliente) => LeerDelCliente(empresa, cliente, null);

        public async Task<ChequeRegaloClienteDTO> LeerDelCliente(string empresa, string cliente, DateTime? fecha)
        {
            if (string.IsNullOrWhiteSpace(cliente))
            {
                return null;
            }
            try
            {
                string empresaConsulta = string.IsNullOrWhiteSpace(empresa) ? Constantes.Empresas.EMPRESA_DEFECTO : empresa.Trim();
                using (HttpClient client = _clienteApiFactory.Crear())
                {
                    string url = $"ChequesRegalo/Cliente?empresa={Uri.EscapeDataString(empresaConsulta)}&cliente={Uri.EscapeDataString(cliente.Trim())}";
                    if (fecha.HasValue)
                    {
                        url += "&fecha=" + Uri.EscapeDataString(fecha.Value.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture));
                    }
                    HttpResponseMessage response = await client.GetAsync(url).ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode)
                    {
                        return null;
                    }
                    string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    ChequeRegaloClienteDTO cheque = string.IsNullOrWhiteSpace(json) ? null : JsonConvert.DeserializeObject<ChequeRegaloClienteDTO>(json);
                    return string.IsNullOrWhiteSpace(cheque?.Producto) ? null : cheque;
                }
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
