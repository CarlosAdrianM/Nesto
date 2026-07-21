using Nesto.Infrastructure.Contracts;
using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Nesto.Infrastructure.Shared
{
    public class ServicioRegistroErrores : IServicioRegistroErrores
    {
        private readonly string _baseUrl;
        private readonly IServicioAutenticacion _servicioAutenticacion;
        private readonly string _usuarioCliente;

        public ServicioRegistroErrores(string baseUrl, IServicioAutenticacion servicioAutenticacion, string usuarioCliente = null)
        {
            _baseUrl = baseUrl;
            _servicioAutenticacion = servicioAutenticacion;
            _usuarioCliente = usuarioCliente;
        }

        public async Task RegistrarErrorAsync(Exception excepcion, string contexto = null)
        {
            try
            {
                if (excepcion == null || string.IsNullOrWhiteSpace(_baseUrl))
                {
                    return;
                }

                var dto = new
                {
                    Aplicacion = "Nesto",
                    Version = ObtenerVersion(),
                    TipoExcepcion = excepcion.GetType().FullName,
                    Mensaje = excepcion.Message,
                    // ToString() incluye tipo, mensaje, stack y excepciones internas (más útil en ELMAH)
                    StackTrace = excepcion.ToString(),
                    Contexto = contexto,
                    UsuarioCliente = _usuarioCliente,
                    Plataforma = "Windows"
                };

                using (var client = new HttpClient { BaseAddress = new Uri(_baseUrl) })
                {
                    client.Timeout = TimeSpan.FromSeconds(15);
                    await _servicioAutenticacion.ConfigurarAutorizacion(client).ConfigureAwait(false);

                    string json = JsonConvert.SerializeObject(dto);
                    using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
                    {
                        await client.PostAsync("Errores", content).ConfigureAwait(false);
                    }
                }
            }
            catch
            {
                // El reporte de errores NUNCA debe lanzar (evitar bucles de crash).
            }
        }

        private static string ObtenerVersion()
        {
            try
            {
                // Nesto#423: la AssemblyVersion no se bumpea al publicar (solo se bumpea el pubxml
                // de ClickOnce), así que TODOS los clientes reportaban la misma versión vieja
                // (1.10.4.*) y no se podía triar por versión real. En .NET moderno no existe
                // ApplicationDeployment: el launcher de ClickOnce expone la versión del despliegue
                // en la variable de entorno ClickOnce_CurrentVersion. Fuera de ClickOnce (debug,
                // ejecución local) se cae a la AssemblyVersion con sufijo para distinguirla.
                string versionClickOnce = Environment.GetEnvironmentVariable("ClickOnce_CurrentVersion");
                if (!string.IsNullOrWhiteSpace(versionClickOnce))
                {
                    return versionClickOnce;
                }
                string versionEnsamblado = (Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly())
                    ?.GetName()?.Version?.ToString();
                return versionEnsamblado == null ? null : versionEnsamblado + " (sin ClickOnce)";
            }
            catch
            {
                return null;
            }
        }
    }
}
