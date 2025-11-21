using ControlesUsuario.Models;
using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.Cajas.Interfaces;
using Nesto.Modulos.Cajas.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Nesto.Modulos.Cajas.Services
{
    public class ContabilidadService : IContabilidadService
    {
        private readonly IConfiguracion _configuracion;
        private readonly IServicioAutenticacion _servicioAutenticacion;

        public ContabilidadService(IConfiguracion configuracion, IServicioAutenticacion servicioAutenticacion)
        {
            _configuracion = configuracion;
            _servicioAutenticacion = servicioAutenticacion;
        }
                

        public async Task<List<CuentaContableDTO>> LeerCuentas(string empresa, string grupo)
        {
            List<CuentaContableDTO> cuentas;
            using (HttpClient client = new HttpClient())
            {
                client.BaseAddress = new Uri(_configuracion.servidorAPI);

                // Carlos 21/11/24: Agregar autenticación
                if (!await _servicioAutenticacion.ConfigurarAutorizacion(client))
                {
                    throw new UnauthorizedAccessException("No se pudo configurar la autorización");
                }

                HttpResponseMessage response;

                try
                {
                    string urlConsulta = $"PlanCuentas?empresa={empresa}&grupo={grupo}";

                    response = await client.GetAsync(urlConsulta);

                    if (response.IsSuccessStatusCode)
                    {
                        string resultado = await response.Content.ReadAsStringAsync();
                        cuentas = JsonConvert.DeserializeObject<List<CuentaContableDTO>>(resultado);
                    }
                    else
                    {
                        throw new Exception("Las cuentas no se han podido cargar correctamente");
                    }
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }

            return cuentas;
        }

        public async Task<int> Contabilizar(List<PreContabilidadDTO> lineas)
        {
            if (lineas is null)
            {
                return -1;
            }
            foreach (var linea in lineas)
            {
                linea.Usuario = _configuracion.usuario;
            }
            using (HttpClient client = new HttpClient())
            {
                client.BaseAddress = new Uri(_configuracion.servidorAPI);

                // Carlos 21/11/24: Agregar autenticación
                if (!await _servicioAutenticacion.ConfigurarAutorizacion(client))
                {
                    throw new UnauthorizedAccessException("No se pudo configurar la autorización");
                }

                HttpResponseMessage response;

                try
                {
                    string urlConsulta = "PreContabilidades";

                    var parametros = new
                    {
                        lineas,
                        contabilizar = true
                    };

                    string jsonParametros = JsonConvert.SerializeObject(parametros);

                    HttpContent content = new StringContent(jsonParametros, Encoding.UTF8, "application/json");

                    response = await client.PostAsync(urlConsulta, content);

                    if (response.IsSuccessStatusCode)
                    {
                        string contenido = await response.Content.ReadAsStringAsync();
                        int asiento = JsonConvert.DeserializeObject<int>(contenido);
                        return asiento;
                    }
                    else
                    {
                        string textoError = await response.Content.ReadAsStringAsync();
                        JObject requestException = JsonConvert.DeserializeObject<JObject>(textoError);

                        string errorMostrar = $"No se ha podido contabilizar\n";
                        if (requestException != null && requestException["exceptionMessage"] != null)
                        {
                            errorMostrar += requestException["exceptionMessage"] + "\n";
                        }
                        if (requestException != null && requestException["ModelState"] != null)
                        {
                            var firstError = requestException["ModelState"];
                            var nodoError = firstError.LastOrDefault();
                            errorMostrar += nodoError.FirstOrDefault()[0];
                        }
                        var innerException = requestException != null ? requestException["InnerException"] : null;
                        while (innerException != null)
                        {
                            errorMostrar += "\n" + innerException["ExceptionMessage"];
                            innerException = innerException["InnerException"];
                        }
                        throw new Exception(errorMostrar);
                    }
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }
        }

        public async Task<int> Contabilizar(PreContabilidadDTO linea)
        {
            var lineas = new List<PreContabilidadDTO> { linea };
            return await Contabilizar(lineas);
        }

        public async Task<List<ContabilidadDTO>> LeerAsientoContable(string empresa, int asiento)
        {
            using (HttpClient client = new HttpClient())
            {
                client.BaseAddress = new Uri(_configuracion.servidorAPI);

                // Carlos 21/11/24: Agregar autenticación
                if (!await _servicioAutenticacion.ConfigurarAutorizacion(client))
                {
                    throw new UnauthorizedAccessException("No se pudo configurar la autorización");
                }

                HttpResponseMessage response;

                try
                {
                    string urlConsulta = $"Contabilidades?empresa={empresa.Trim()}&asiento={asiento}";

                    response = await client.GetAsync(urlConsulta).ConfigureAwait(false);

                    if (response.IsSuccessStatusCode)
                    {
                        string resultado = await response.Content.ReadAsStringAsync();
                        var apuntes = JsonConvert.DeserializeObject<List<ContabilidadDTO>>(resultado);
                        return apuntes;
                    }
                    else
                    {
                        throw new Exception("No se han podido cargar el asiento contable");
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception("No se han podido cargar el asiento contable", ex);
                }
            }
        }
        public async Task<List<ContabilidadDTO>> LeerApuntesContabilidad(string empresa, string cuenta, DateTime fechaDesde, DateTime fechaHasta)
        {
            using (HttpClient client = new HttpClient())
            {
                client.BaseAddress = new Uri(_configuracion.servidorAPI);

                // Carlos 21/11/24: Agregar autenticación
                if (!await _servicioAutenticacion.ConfigurarAutorizacion(client))
                {
                    throw new UnauthorizedAccessException("No se pudo configurar la autorización");
                }

                HttpResponseMessage response;

                try
                {
                    string fechaDesdeFormateada = fechaDesde.ToString("yyyy-MM-dd");
                    string fechaHastaFormateada = fechaHasta.ToString("yyyy-MM-dd");
                    string urlConsulta = $"Contabilidades?empresa={empresa.Trim()}&cuenta={cuenta.Trim()}&fechaDesde={fechaDesdeFormateada}&fechaHasta={fechaHastaFormateada}";

                    response = await client.GetAsync(urlConsulta).ConfigureAwait(false);

                    if (response.IsSuccessStatusCode)
                    {
                        string resultado = await response.Content.ReadAsStringAsync();
                        var apuntes = JsonConvert.DeserializeObject<List<ContabilidadDTO>>(resultado);
                        return apuntes;
                    }
                    else
                    {
                        throw new Exception("No se han podido cargar los apuntes de la contabilidad");
                    }
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }
        }

        public async Task<decimal> SaldoCuenta(string empresa, string cuenta, DateTime fecha)
        {
            using (HttpClient client = new HttpClient())
            {
                client.BaseAddress = new Uri(_configuracion.servidorAPI);

                // Carlos 21/11/24: Agregar autenticación
                if (!await _servicioAutenticacion.ConfigurarAutorizacion(client))
                {
                    throw new UnauthorizedAccessException("No se pudo configurar la autorización");
                }

                HttpResponseMessage response;

                try
                {
                    string fechaFormateada = fecha.ToString("yyyy-MM-dd");
                    string urlConsulta = $"Contabilidades?empresa={empresa}&cuenta={cuenta}&fecha={fechaFormateada}";

                    response = await client.GetAsync(urlConsulta);

                    if (response.IsSuccessStatusCode)
                    {
                        string resultado = await response.Content.ReadAsStringAsync();
                        var saldo = JsonConvert.DeserializeObject<decimal>(resultado);
                        return saldo;
                    }
                    else
                    {
                        throw new Exception("No se ha podido calcular el saldo de la cuenta");
                    }
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }
        }

        public async Task<List<ContabilidadDTO>> LeerCuentasPorConcepto(string empresa, string concepto, DateTime fechaDesde, DateTime fechaHasta)
        {
            List<ContabilidadDTO> cuentas;
            using (HttpClient client = new HttpClient())
            {
                client.BaseAddress = new Uri(_configuracion.servidorAPI);

                // Carlos 21/11/24: Agregar autenticación
                if (!await _servicioAutenticacion.ConfigurarAutorizacion(client))
                {
                    throw new UnauthorizedAccessException("No se pudo configurar la autorización");
                }

                HttpResponseMessage response;

                try
                {
                    string fechaDesdeFormateada = fechaDesde.ToString("yyyy-MM-dd");
                    string fechaHastaFormateada = fechaHasta.ToString("yyyy-MM-dd");
                    string urlConsulta = $"Contabilidades/LeerCuentasPorConcepto?empresa={empresa}&concepto={concepto}&fechaDesde={fechaDesdeFormateada}&fechaHasta={fechaHastaFormateada}";

                    response = await client.GetAsync(urlConsulta);

                    if (response.IsSuccessStatusCode)
                    {
                        string resultado = await response.Content.ReadAsStringAsync();
                        cuentas = JsonConvert.DeserializeObject<List<ContabilidadDTO>>(resultado);
                    }
                    else
                    {
                        throw new Exception("No se han podido cargar correctamente las cuentas filtradas por concepto");
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception("No se han podido cargar correctamente las cuentas filtradas por concepto", ex);
                }
            }

            return cuentas;
        }

        public async Task<bool> PuntearPorImporte(string empresa, string cuenta, decimal importe)
        {
            using (HttpClient client = new HttpClient())
            {
                client.BaseAddress = new Uri(_configuracion.servidorAPI);

                // Carlos 21/11/24: Agregar autenticación
                if (!await _servicioAutenticacion.ConfigurarAutorizacion(client))
                {
                    throw new UnauthorizedAccessException("No se pudo configurar la autorización");
                }

                try
                {
                    string urlConsulta = "Contabilidades/PuntearPorImporte";

                    // Crear el objeto anónimo con los datos del contenido y el usuario
                    var datosPunteo = new
                    {
                        Empresa = empresa,
                        Cuenta = cuenta,
                        Importe = importe
                    };

                    // Serializar el objeto anónimo a JSON
                    var jsonContent = JsonConvert.SerializeObject(datosPunteo);

                    // Crear el contenido de la solicitud
                    HttpContent content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                    HttpResponseMessage response = await client.PostAsync(urlConsulta, content);

                    if (response.IsSuccessStatusCode)
                    {
                        string contenido = await response.Content.ReadAsStringAsync();
                        var resultado = JsonConvert.DeserializeObject<bool>(contenido);
                        return resultado;
                    }
                    else
                    {
                        string textoError = await response.Content.ReadAsStringAsync();
                        JObject requestException = JsonConvert.DeserializeObject<JObject>(textoError);

                        string errorMostrar = $"No se ha podido crear el punteo de contabilidad\n";
                        if (requestException != null && requestException["Message"] != null)
                        {
                            errorMostrar += requestException["Message"] + "\n";
                        }
                        if (requestException != null && requestException["message"] != null)
                        {
                            errorMostrar += requestException["message"] + "\n";
                        }
                        if (requestException != null && requestException["exceptionMessage"] != null)
                        {
                            errorMostrar += requestException["exceptionMessage"] + "\n";
                        }
                        if (requestException != null && requestException["ModelState"] != null)
                        {
                            var firstError = requestException["ModelState"];
                            var nodoError = firstError.LastOrDefault();
                            errorMostrar += nodoError.FirstOrDefault()[0];
                        }
                        var innerException = requestException != null ? requestException["InnerException"] : null;
                        while (innerException != null)
                        {
                            errorMostrar += "\n" + innerException["ExceptionMessage"];
                            innerException = innerException["InnerException"];
                        }
                        throw new Exception(errorMostrar);
                    }
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }
        }

        public async Task<List<ContabilidadDTO>> LeerApuntesContabilidad(string cuenta, bool punteado)
        {
            using (HttpClient client = new HttpClient())
            {
                client.BaseAddress = new Uri(_configuracion.servidorAPI);

                // Carlos 21/11/24: Agregar autenticación
                if (!await _servicioAutenticacion.ConfigurarAutorizacion(client))
                {
                    throw new UnauthorizedAccessException("No se pudo configurar la autorización");
                }

                HttpResponseMessage response;

                try
                {
                    string urlConsulta = $"Contabilidades?cuenta={cuenta.Trim()}&punteado={punteado}";

                    response = await client.GetAsync(urlConsulta).ConfigureAwait(false);

                    if (response.IsSuccessStatusCode)
                    {
                        string resultado = await response.Content.ReadAsStringAsync();
                        var apuntes = JsonConvert.DeserializeObject<List<ContabilidadDTO>>(resultado);
                        return apuntes;
                    }
                    else
                    {
                        throw new Exception("No se han podido cargar los apuntes de la contabilidad");
                    }
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }
        }
    }
}
