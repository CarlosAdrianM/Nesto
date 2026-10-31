using Nesto.Infrastructure.Contracts;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Nesto.Infrastructure.Shared
{
    /// <summary>Nesto#592: ver <see cref="IServicioGlobos"/>.</summary>
    public class ServicioGlobos : IServicioGlobos
    {
        public const int MAXIMO_SEGUIDOS = 2;

        private readonly IClienteGlobos _cliente;
        private readonly object _cerrojo = new object();
        // Por pantalla: los pendientes que aún no se han enseñado en esta sesión
        private readonly Dictionary<string, List<GloboNesto>> _pendientes = new Dictionary<string, List<GloboNesto>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Task<List<GloboNesto>>> _pidiendo = new Dictionary<string, Task<List<GloboNesto>>>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<int> _enPantalla = new HashSet<int>();
        private readonly HashSet<int> _retirados = new HashSet<int>();

        public ServicioGlobos(IClienteGlobos cliente, int maximoSeguidos = MAXIMO_SEGUIDOS)
        {
            _cliente = cliente ?? throw new ArgumentNullException(nameof(cliente));
            MaximoSeguidos = Math.Max(1, maximoSeguidos);
        }

        public int MaximoSeguidos { get; }

        public async Task<IReadOnlyList<GloboAEnsenar>> QueEnsenarAsync(string pantalla, Func<string, bool> anclaVisible)
        {
            string clave = pantalla?.Trim();
            if (string.IsNullOrEmpty(clave))
            {
                return Array.Empty<GloboAEnsenar>();
            }
            // Sin ConfigureAwait(false): anclaVisible mira controles de WPF y tiene que correr en el hilo de quien pregunta
            List<GloboNesto> pendientes = await Pendientes(clave);
            lock (_cerrojo)
            {
                if (_enPantalla.Count > 0)
                {
                    return Array.Empty<GloboAEnsenar>();
                }
                List<GloboAEnsenar> tanda = pendientes
                    .Where(g => !_retirados.Contains(g.Id))
                    .Take(MaximoSeguidos)
                    .Select(g => new GloboAEnsenar(g, EsCentrado(g, anclaVisible)))
                    .ToList();
                foreach (GloboAEnsenar g in tanda)
                {
                    _ = _enPantalla.Add(g.Globo.Id);
                }
                return tanda;
            }
        }

        public async Task EntendidoAsync(GloboNesto globo)
        {
            if (globo == null)
            {
                return;
            }
            Retirar(globo);
            try
            {
                await _cliente.MarcarVistoAsync(globo.Id).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Un globo nunca molesta: si no se pudo marcar, como mucho vuelve a salir en otra sesión
                Debug.WriteLine($"[ServicioGlobos] No se pudo marcar visto el {globo.Id}: {ex.Message}");
            }
        }

        public void Aplazar(GloboNesto globo)
        {
            if (globo != null)
            {
                Retirar(globo);
            }
        }

        public void Soltar(GloboNesto globo)
        {
            if (globo == null)
            {
                return;
            }
            lock (_cerrojo)
            {
                _ = _enPantalla.Remove(globo.Id);
            }
        }

        private void Retirar(GloboNesto globo)
        {
            lock (_cerrojo)
            {
                _ = _enPantalla.Remove(globo.Id);
                _ = _retirados.Add(globo.Id);
            }
        }

        private static bool EsCentrado(GloboNesto globo, Func<string, bool> anclaVisible)
        {
            if (string.IsNullOrWhiteSpace(globo.Ancla) || anclaVisible == null)
            {
                return true;
            }
            try
            {
                return !anclaVisible(globo.Ancla.Trim());
            }
            catch (Exception)
            {
                return true;
            }
        }

        private Task<List<GloboNesto>> Pendientes(string pantalla)
        {
            lock (_cerrojo)
            {
                if (_pendientes.TryGetValue(pantalla, out List<GloboNesto> ya))
                {
                    return Task.FromResult(ya);
                }
                if (!_pidiendo.TryGetValue(pantalla, out Task<List<GloboNesto>> enCurso))
                {
                    enCurso = Pedir(pantalla);
                    // Si ya ha terminado (falló o respondió sin esperar), Pedir ya ha dejado su resultado: no se guarda la tarea
                    if (!enCurso.IsCompleted)
                    {
                        _pidiendo[pantalla] = enCurso;
                    }
                }
                return enCurso;
            }
        }

        private async Task<List<GloboNesto>> Pedir(string pantalla)
        {
            List<GloboNesto> leidos = null;
            try
            {
                leidos = await _cliente.LeerPendientesAsync(pantalla).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ServicioGlobos] No se pudieron leer los de {pantalla}: {ex.Message}");
            }
            lock (_cerrojo)
            {
                _ = _pidiendo.Remove(pantalla);
                if (leidos == null)
                {
                    // Sin caché: se vuelve a pedir la próxima vez que se entre en la pantalla (no hay sondeo)
                    return new List<GloboNesto>();
                }
                List<GloboNesto> validos = leidos.Where(g => g != null && !string.IsNullOrWhiteSpace(g.Texto)).ToList();
                _pendientes[pantalla] = validos;
                return validos;
            }
        }
    }

    /// <summary>Nesto#592: el cliente HTTP de api/Globos (con el JWT de <see cref="IClienteApiFactory"/>).</summary>
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

        public async Task<List<GloboNesto>> LeerPendientesAsync(string pantalla)
        {
            string url = $"Globos?aplicacion={APLICACION}&pantalla={Uri.EscapeDataString(pantalla ?? string.Empty)}";
            string version = _version();
            if (!string.IsNullOrWhiteSpace(version))
            {
                url += "&version=" + Uri.EscapeDataString(version);
            }
            using (HttpClient client = _clienteApiFactory.Crear())
            {
                HttpResponseMessage response = await client.GetAsync(url).ConfigureAwait(false);
                _ = response.EnsureSuccessStatusCode();
                string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return JsonConvert.DeserializeObject<List<GloboNesto>>(json) ?? new List<GloboNesto>();
            }
        }

        public async Task MarcarVistoAsync(int id)
        {
            using (HttpClient client = _clienteApiFactory.Crear())
            {
                HttpResponseMessage response = await client.PostAsync($"Globos/{id}/Visto", new StringContent(string.Empty)).ConfigureAwait(false);
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
