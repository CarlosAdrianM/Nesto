using ControlesUsuario.Behaviors;
using ControlesUsuario.Models;
using Nesto.Infrastructure.Contracts;
using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;

namespace ControlesUsuario.Services
{
    /// <summary>
    /// Implementación del servicio de cuentas contables.
    /// Issue #258 - Carlos 11/12/25
    /// </summary>
    public class ServicioCuentaContable : IServicioCuentaContable
    {
        private readonly IConfiguracion _configuracion;
        private readonly IClienteApiFactory _clienteApiFactory;

        public ServicioCuentaContable(IConfiguracion configuracion) : this(configuracion, null)
        {
        }

        // Nesto#369: Unity resuelve este constructor (el más largo); el de un parámetro se
        // mantiene por compatibilidad con el fallback de CuentaContableBehavior.
        public ServicioCuentaContable(IConfiguracion configuracion, IClienteApiFactory clienteApiFactory)
        {
            _configuracion = configuracion ?? throw new ArgumentNullException(nameof(configuracion));
            _clienteApiFactory = clienteApiFactory;
        }

        // Nesto#369: con factoría el HttpClient adjunta el JWT (usuario en ELMAH)
        private HttpClient CrearClient()
        {
            return _clienteApiFactory != null
                ? _clienteApiFactory.Crear()
                : new HttpClient { BaseAddress = new Uri(_configuracion.servidorAPI) };
        }

        /// <summary>
        /// Busca una cuenta contable por empresa y número de cuenta.
        /// Soporta formato abreviado (ej: "572.13" se expande a "57200013").
        /// </summary>
        public async Task<CuentaContableDTO> BuscarCuenta(string empresa, string cuenta)
        {
            if (string.IsNullOrWhiteSpace(empresa) || string.IsNullOrWhiteSpace(cuenta))
            {
                return null;
            }

            // Expandir la cuenta si está en formato abreviado
            string cuentaExpandida;
            if (!CuentaContableHelper.TryExpandirCuenta(cuenta, out cuentaExpandida))
            {
                Debug.WriteLine($"[ServicioCuentaContable] Formato de cuenta inválido: {cuenta}");
                return null;
            }

            using (var client = CrearClient())
            {
                try
                {
                    var url = $"PlanCuentas/Buscar?empresa={empresa}&cuenta={cuentaExpandida}";
                    var response = await client.GetAsync(url);

                    if (!response.IsSuccessStatusCode)
                    {
                        Debug.WriteLine($"[ServicioCuentaContable] Cuenta no encontrada: {cuentaExpandida}");
                        return null;
                    }

                    var json = await response.Content.ReadAsStringAsync();
                    var cuentaContable = JsonConvert.DeserializeObject<CuentaContableDTO>(json);

                    return cuentaContable;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ServicioCuentaContable] Error: {ex.Message}");
                    return null;
                }
            }
        }
    }
}
