using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Models;
using Nesto.Infrastructure.Shared;
using Newtonsoft.Json;
using System;
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
    }

    public class ServicioChequesRegalo : IServicioChequesRegalo
    {
        private readonly IClienteApiFactory _clienteApiFactory;

        public ServicioChequesRegalo(IClienteApiFactory clienteApiFactory)
        {
            _clienteApiFactory = clienteApiFactory;
        }

        public async Task<ChequeRegaloClienteDTO> LeerDelCliente(string empresa, string cliente)
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
                    HttpResponseMessage response = await client.GetAsync(
                        $"ChequesRegalo/Cliente?empresa={Uri.EscapeDataString(empresaConsulta)}&cliente={Uri.EscapeDataString(cliente.Trim())}").ConfigureAwait(false);
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
