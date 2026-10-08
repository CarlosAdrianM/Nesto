using ControlesUsuario.Models;
using Nesto.Infrastructure.Contracts;
using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ControlesUsuario.Services
{
    /// <summary>NestoAPI#609: pide a la API la corrección del concepto de un enlace de pago.</summary>
    public interface IServicioRevisionConcepto
    {
        /// <summary>
        /// La revisión del concepto, o null si no se pudo (error, timeout, API sin el endpoint): la revisión
        /// es un paso previo opcional y nunca debe impedir crear el enlace.
        /// </summary>
        Task<RevisionConcepto> Revisar(string concepto, string empresa, string cliente);
    }

    public class ServicioRevisionConcepto : IServicioRevisionConcepto
    {
        internal const string URL = "Pagos/RevisarConcepto";
        /// <summary>La IA puede tardar: más de esto y se sigue con lo escrito (el usuario está esperando).</summary>
        internal static readonly TimeSpan TIEMPO_MAXIMO = TimeSpan.FromSeconds(4);

        private readonly IClienteApiFactory _clienteApiFactory;
        private readonly TimeSpan _tiempoMaximo;

        public ServicioRevisionConcepto(IClienteApiFactory clienteApiFactory) : this(clienteApiFactory, TIEMPO_MAXIMO) { }

        internal ServicioRevisionConcepto(IClienteApiFactory clienteApiFactory, TimeSpan tiempoMaximo)
        {
            _clienteApiFactory = clienteApiFactory ?? throw new ArgumentNullException(nameof(clienteApiFactory));
            _tiempoMaximo = tiempoMaximo;
        }

        public async Task<RevisionConcepto> Revisar(string concepto, string empresa, string cliente)
        {
            if (string.IsNullOrWhiteSpace(concepto))
            {
                return null;
            }
            try
            {
                using (var cancelacion = new CancellationTokenSource(_tiempoMaximo))
                using (HttpClient client = _clienteApiFactory.Crear())
                {
                    string json = JsonConvert.SerializeObject(new
                    {
                        Concepto = concepto,
                        Empresa = empresa?.Trim(),
                        Cliente = cliente?.Trim()
                    });
                    using (var contenido = new StringContent(json, Encoding.UTF8, "application/json"))
                    using (HttpResponseMessage respuesta = await client.PostAsync(URL, contenido, cancelacion.Token).ConfigureAwait(false))
                    {
                        // 404: API sin publicar todavía; cualquier otro error, igual: se sigue con lo escrito.
                        if (!respuesta.IsSuccessStatusCode)
                        {
                            return null;
                        }
                        string cuerpo = await respuesta.Content.ReadAsStringAsync(cancelacion.Token).ConfigureAwait(false);
                        return JsonConvert.DeserializeObject<RevisionConcepto>(cuerpo);
                    }
                }
            }
            catch (Exception)
            {
                // Timeout (OperationCanceledException), red, JSON raro...: sin revisión.
                return null;
            }
        }
    }
}
