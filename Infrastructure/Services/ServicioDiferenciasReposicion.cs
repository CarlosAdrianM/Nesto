using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Nesto.Infrastructure.Services
{
    /// <summary>
    /// Nesto#526 (NestoAPI#634): las diferencias entre lo enviado y lo recibido en las reposiciones. El ajuste del stock
    /// del origen lo hace la API al terminar la recepción; aquí solo se leen y se resuelven. Los permisos los decide la API
    /// (403 con su mensaje).
    /// </summary>
    public interface IServicioDiferenciasReposicion
    {
        /// <param name="estado">Pendiente, Resuelta o Todas.</param>
        /// <param name="almacen">El de origen (donde se comprueba); null o vacío, todos los que puede ver el usuario.</param>
        /// <exception cref="DiferenciasReposicionException">La API no las ha devuelto.</exception>
        Task<List<DiferenciaReposicion>> Leer(string empresa, string estado, string almacen);

        /// <summary>Cuántas quedan por comprobar en <paramref name="almacen"/> (null: todas las que puede ver).</summary>
        /// <exception cref="DiferenciasReposicionException">La API no lo ha devuelto.</exception>
        Task<ContadorDiferenciasReposicion> ContarPendientes(string empresa, string almacen);

        /// <exception cref="DiferenciasReposicionException">400 si no vale esa resolución, 403, 404, 409 si ya estaba resuelta.</exception>
        Task<ResultadoResolverDiferenciaReposicion> Resolver(string empresa, int id, ResolverDiferenciaReposicion peticion);

        /// <summary>Se ha resuelto una diferencia (para que el contador de la cinta se ponga al día).</summary>
        event EventHandler DiferenciasCambiadas;
    }

    public class ServicioDiferenciasReposicion : IServicioDiferenciasReposicion
    {
        private readonly IClienteApiFactory _clienteApiFactory;

        public ServicioDiferenciasReposicion(IClienteApiFactory clienteApiFactory)
        {
            _clienteApiFactory = clienteApiFactory;
        }

        public event EventHandler DiferenciasCambiadas;

        public async Task<List<DiferenciaReposicion>> Leer(string empresa, string estado, string almacen)
        {
            string url = "Reposiciones/Diferencias?estado=" + Uri.EscapeDataString(estado?.Trim() ?? DiferenciaReposicion.ESTADO_PENDIENTE)
                + ParametroAlmacen(almacen) + "&empresa=" + Uri.EscapeDataString(empresa?.Trim() ?? string.Empty);
            using (HttpClient client = _clienteApiFactory.Crear())
            {
                HttpResponseMessage response = await client.GetAsync(url).ConfigureAwait(false);
                return await Leer<List<DiferenciaReposicion>>(response).ConfigureAwait(false) ?? new List<DiferenciaReposicion>();
            }
        }

        public async Task<ContadorDiferenciasReposicion> ContarPendientes(string empresa, string almacen)
        {
            string url = "Reposiciones/Diferencias/Contador?empresa=" + Uri.EscapeDataString(empresa?.Trim() ?? string.Empty) + ParametroAlmacen(almacen);
            using (HttpClient client = _clienteApiFactory.Crear())
            {
                HttpResponseMessage response = await client.GetAsync(url).ConfigureAwait(false);
                return await Leer<ContadorDiferenciasReposicion>(response).ConfigureAwait(false) ?? new ContadorDiferenciasReposicion();
            }
        }

        public async Task<ResultadoResolverDiferenciaReposicion> Resolver(string empresa, int id, ResolverDiferenciaReposicion peticion)
        {
            string url = $"Reposiciones/Diferencias/{id}/Resolver?empresa=" + Uri.EscapeDataString(empresa?.Trim() ?? string.Empty);
            ResultadoResolverDiferenciaReposicion resultado;
            using (HttpClient client = _clienteApiFactory.Crear())
            {
                var contenido = new StringContent(JsonConvert.SerializeObject(peticion), Encoding.UTF8, "application/json");
                HttpResponseMessage response = await client.PostAsync(url, contenido).ConfigureAwait(false);
                resultado = await Leer<ResultadoResolverDiferenciaReposicion>(response).ConfigureAwait(false) ?? new ResultadoResolverDiferenciaReposicion();
            }
            DiferenciasCambiadas?.Invoke(this, EventArgs.Empty);
            return resultado;
        }

        private static string ParametroAlmacen(string almacen)
            => string.IsNullOrWhiteSpace(almacen) ? string.Empty : "&almacen=" + Uri.EscapeDataString(almacen.Trim());

        private static async Task<T> Leer<T>(HttpResponseMessage response)
        {
            string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new DiferenciasReposicionException(ServicioEtiquetasHueco.Motivo(json, (int)response.StatusCode), (int)response.StatusCode);
            }
            return JsonConvert.DeserializeObject<T>(json);
        }
    }
}
