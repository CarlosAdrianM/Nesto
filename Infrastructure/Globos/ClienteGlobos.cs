using Nesto.Infrastructure.Contracts;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Nesto.Infrastructure.Globos
{
    /// <summary>
    /// Nesto#592 (NestoAPI#774): GET api/Globos?aplicacion=Nesto&amp;pantalla=…&amp;version=… y POST api/Globos/{id}/Visto, con el JWT de
    /// <see cref="IClienteApiFactory"/>. El núcleo (qué se enseña) está en NucleoGlobos.cs, igual que en Ariadna.
    /// </summary>
    public class ClienteGlobos : IClienteGlobos
    {
        public const string APLICACION = "Nesto";

        private readonly IClienteApiFactory _clienteApiFactory;
        private readonly Func<string> _version;

        public ClienteGlobos(IClienteApiFactory clienteApiFactory, Func<string> version = null)
        {
            _clienteApiFactory = clienteApiFactory ?? throw new ArgumentNullException(nameof(clienteApiFactory));
            _version = version ?? VersionNesto;
        }

        public async Task<List<DatosGlobo>> LeerPendientesAsync(string pantalla, CancellationToken cancelacion = default)
        {
            string url = $"Globos?aplicacion={APLICACION}&pantalla={Uri.EscapeDataString(pantalla ?? string.Empty)}";
            string version = _version();
            if (!string.IsNullOrWhiteSpace(version))
            {
                url += "&version=" + Uri.EscapeDataString(version.Trim());
            }
            using (HttpClient client = _clienteApiFactory.Crear())
            {
                HttpResponseMessage response = await client.GetAsync(url, cancelacion).ConfigureAwait(false);
                _ = response.EnsureSuccessStatusCode();
                string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return JsonConvert.DeserializeObject<List<DatosGlobo>>(json) ?? new List<DatosGlobo>();
            }
        }

        public async Task MarcarVistoAsync(int id, CancellationToken cancelacion = default)
        {
            using (HttpClient client = _clienteApiFactory.Crear())
            {
                HttpResponseMessage response = await client.PostAsync($"Globos/{id}/Visto", new StringContent(string.Empty), cancelacion).ConfigureAwait(false);
                _ = response.EnsureSuccessStatusCode();
            }
        }

        /// <summary>La de ClickOnce; fuera de ClickOnce, la del ensamblado.</summary>
        internal static string VersionNesto()
        {
            string clickOnce = Environment.GetEnvironmentVariable("ClickOnce_CurrentVersion");
            if (!string.IsNullOrWhiteSpace(clickOnce))
            {
                return clickOnce.Trim();
            }
            return System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString();
        }
    }
}
