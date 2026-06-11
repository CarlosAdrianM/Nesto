using Nesto.Infrastructure.Contracts;
using Newtonsoft.Json;
using Prism.Ioc;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace Nesto.Modulos.CanalesExternos.ApisExternas
{
    /// <summary>
    /// Credencial LWA vigente servida por NestoAPI (GET api/CredencialesAmazon, NestoAPI#225).
    /// </summary>
    public class CredencialAmazonRotada
    {
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
        public string RefreshToken { get; set; }
        public DateTime? SecretExpiry { get; set; }
    }

    /// <summary>
    /// NestoAPI#225: obtiene de la API la credencial LWA que el job de rotación mantiene en BD,
    /// para que Nesto no dependa del secreto de clavesSecretas.config (que muere a los 7 días de
    /// cada rotación). Se consulta UNA vez por sesión y se cachea; si la API no responde o el
    /// usuario no está en los grupos autorizados (TiendaOnline/Administración), devuelve null y
    /// el llamante cae al secreto del config (comportamiento previo).
    /// </summary>
    public static class CredencialAmazonProvider
    {
        private static readonly object _candado = new();
        private static CredencialAmazonRotada _credencial;
        private static bool _consultada;

        public static CredencialAmazonRotada Obtener()
        {
            if (_consultada)
            {
                return _credencial;
            }
            lock (_candado)
            {
                if (_consultada)
                {
                    return _credencial;
                }
                try
                {
                    // Task.Run evita bloquear con el SynchronizationContext de WPF si algún
                    // llamante entra desde el hilo de UI (ConexionAmazon es síncrono).
                    _credencial = Task.Run(ObtenerDesdeApiAsync).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.WriteLine("[AmazonDiag] Credencial rotada no disponible, se usa la del config: " + ex.Message);
                    _credencial = null;
                }
                _consultada = true;
            }
            return _credencial;
        }

        private static async Task<CredencialAmazonRotada> ObtenerDesdeApiAsync()
        {
            IConfiguracion configuracion = ContainerLocator.Container.Resolve<IConfiguracion>();
            IServicioAutenticacion servicioAutenticacion = ContainerLocator.Container.Resolve<IServicioAutenticacion>();

            using HttpClient client = new()
            {
                BaseAddress = new Uri(configuracion.servidorAPI)
            };
            await servicioAutenticacion.ConfigurarAutorizacion(client);

            HttpResponseMessage response = await client.GetAsync("CredencialesAmazon");
            if (!response.IsSuccessStatusCode)
            {
                System.Diagnostics.Trace.WriteLine($"[AmazonDiag] GET CredencialesAmazon devolvió {(int)response.StatusCode}; se usa la credencial del config.");
                return null;
            }

            string contenido = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<CredencialAmazonRotada>(contenido);
        }

        /// <summary>Resetea la caché (solo para tests).</summary>
        internal static void ReiniciarParaTests()
        {
            lock (_candado)
            {
                _credencial = null;
                _consultada = false;
            }
        }
    }
}
