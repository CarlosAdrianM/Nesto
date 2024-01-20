using Nesto.Modulos.Cajas.Models;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Shapes;
using Nesto.Infrastructure.Contracts;
using static Nesto.Infrastructure.Shared.Constantes;

namespace Nesto.Modulos.Cajas
{
    public class BancosService : IBancosService
    {
        private readonly IConfiguracion _configuracion;

        public BancosService(IConfiguracion configuracion)
        {
            _configuracion = configuracion;
        }

        public async Task<List<ApunteBancarioDTO>> CargarFicheroCuaderno43(string contenidoFichero)
        {
            using (HttpClient client = new HttpClient())
            {
                client.BaseAddress = new Uri(_configuracion.servidorAPI);

                try
                {
                    string urlConsulta = "Bancos/CargarFichero";

                    HttpResponseMessage response;
                    HttpContent content = new StringContent(JsonConvert.SerializeObject(contenidoFichero), Encoding.UTF8, "application/json");


                    response = await client.PostAsync(urlConsulta, content);

                    if (response.IsSuccessStatusCode)
                    {
                        string contenido = await response.Content.ReadAsStringAsync();
                        var resultado = JsonConvert.DeserializeObject<List<ApunteBancarioDTO>>(contenido);
                        return resultado;
                    }
                    else
                    {
                        string textoError = await response.Content.ReadAsStringAsync();
                        JObject requestException = JsonConvert.DeserializeObject<JObject>(textoError);

                        string errorMostrar = $"No se ha podido contabilizar\n";
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
    }
}
