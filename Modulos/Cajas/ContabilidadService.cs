using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.Cajas.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Nesto.Modulos.Cajas
{
    public class ContabilidadService : IContabilidadService
    {
        public IConfiguracion Configuracion { get; }
        public ContabilidadService(IConfiguracion configuracion) {
            Configuracion = configuracion;
        }
                

        public async Task<List<CuentaContableDTO>> LeerCuentas(string empresa, string grupo)
        {
            List<CuentaContableDTO> cuentas;
            using (HttpClient client = new HttpClient())
            {
                client.BaseAddress = new Uri(Configuracion.servidorAPI);
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
            using (HttpClient client = new HttpClient())
            {
                client.BaseAddress = new Uri(Configuracion.servidorAPI);
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
    }
}
