using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace ControlesUsuario.Services
{
    /// <summary>Nesto#563: lo que se escribe en el diálogo de envío. Asunto y texto en blanco = los de la API.</summary>
    public class PeticionEnvioCorreo
    {
        public List<string> Destinatarios { get; set; } = new List<string>();
        public string Asunto { get; set; }
        public string Texto { get; set; }
    }

    /// <summary>Nesto#563: lo que contesta la API al mandar un documento por correo.</summary>
    public class ResultadoEnvioCorreo
    {
        public List<string> Destinatarios { get; set; } = new List<string>();
        public string Adjunto { get; set; }
        public string Mensaje { get; set; }
    }

    /// <summary>
    /// Nesto#563 (NestoAPI#724): mandar por correo, desde la API, el pedido, el mandato SEPA o una factura, sin descargarlos.
    /// </summary>
    public interface IServicioEnvioCorreoDocumentos
    {
        /// <summary>Los correos de las personas de contacto del cliente (vacío si no tiene o si falla: es solo una propuesta).</summary>
        Task<List<string>> LeerCorreosContacto(string empresa, string cliente, string contacto);
        /// <summary>El correo de facturas del cliente de esa factura (vacío si no tiene o si falla).</summary>
        Task<List<string>> LeerCorreosFacturas(string empresa, string factura);
        Task<ResultadoEnvioCorreo> EnviarPedido(string empresa, int numero, PeticionEnvioCorreo peticion);
        Task<ResultadoEnvioCorreo> EnviarMandato(string empresa, string cliente, string contacto, string ccc, string motor, PeticionEnvioCorreo peticion);
        /// <summary>Con el envío de facturas que ya había (POST Facturas/EnviarPorCorreo, Nesto#259).</summary>
        Task<ResultadoEnvioCorreo> EnviarFactura(string empresa, string factura, PeticionEnvioCorreo peticion);
    }

    public class ServicioEnvioCorreoDocumentos : IServicioEnvioCorreoDocumentos
    {
        private readonly IClienteApiFactory _clienteApiFactory;

        public ServicioEnvioCorreoDocumentos(IClienteApiFactory clienteApiFactory)
        {
            _clienteApiFactory = clienteApiFactory ?? throw new ArgumentNullException(nameof(clienteApiFactory));
        }

        private static string Url(string texto) => Uri.EscapeDataString(texto?.Trim() ?? string.Empty);

        public async Task<List<string>> LeerCorreosContacto(string empresa, string cliente, string contacto)
        {
            try
            {
                using (HttpClient client = _clienteApiFactory.Crear())
                using (HttpResponseMessage respuesta = await client.GetAsync($"Clientes/{Url(empresa)}/{Url(cliente)}/{Url(contacto)}/CorreosContacto").ConfigureAwait(false))
                {
                    if (!respuesta.IsSuccessStatusCode)
                    {
                        return new List<string>();
                    }
                    string cuerpo = await respuesta.Content.ReadAsStringAsync().ConfigureAwait(false);
                    return JsonConvert.DeserializeObject<List<string>>(cuerpo) ?? new List<string>();
                }
            }
            catch (Exception)
            {
                return new List<string>();
            }
        }

        public async Task<List<string>> LeerCorreosFacturas(string empresa, string factura)
        {
            try
            {
                using (HttpClient client = _clienteApiFactory.Crear())
                using (HttpResponseMessage respuesta = await client.GetAsync($"Facturas/CorreoFacturas?empresa={Url(empresa)}&numeroFactura={Url(factura)}").ConfigureAwait(false))
                {
                    if (!respuesta.IsSuccessStatusCode)
                    {
                        return new List<string>();
                    }
                    string correo = JObject.Parse(await respuesta.Content.ReadAsStringAsync().ConfigureAwait(false))["Correo"]?.ToString();
                    return Dialogs.EnviarPorCorreoDialogViewModel.SepararCorreos(correo);
                }
            }
            catch (Exception)
            {
                return new List<string>();
            }
        }

        public Task<ResultadoEnvioCorreo> EnviarPedido(string empresa, int numero, PeticionEnvioCorreo peticion)
            => Enviar($"PedidosVenta/{Url(empresa)}/{numero}/EnviarPorCorreo", new
            {
                Destinatarios = peticion?.Destinatarios,
                Asunto = peticion?.Asunto,
                Texto = peticion?.Texto
            });

        public Task<ResultadoEnvioCorreo> EnviarMandato(string empresa, string cliente, string contacto, string ccc, string motor, PeticionEnvioCorreo peticion)
            => Enviar($"Clientes/{Url(empresa)}/{Url(cliente)}/{Url(contacto)}/Mandato/EnviarPorCorreo", new
            {
                Ccc = ccc?.Trim(),
                Motor = motor?.Trim(),
                Destinatarios = peticion?.Destinatarios,
                Asunto = peticion?.Asunto,
                Texto = peticion?.Texto
            });

        public async Task<ResultadoEnvioCorreo> EnviarFactura(string empresa, string factura, PeticionEnvioCorreo peticion)
        {
            // El envío de facturas que ya había no admite asunto ni texto: usa los de la serie de la factura
            var cuerpo = new
            {
                Empresa = empresa?.Trim(),
                Facturas = new List<string> { factura?.Trim() },
                Correos = peticion?.Destinatarios ?? new List<string>()
            };
            using (HttpClient client = _clienteApiFactory.Crear())
            using (var contenido = new StringContent(JsonConvert.SerializeObject(cuerpo), Encoding.UTF8, "application/json"))
            using (HttpResponseMessage respuesta = await client.PostAsync("Facturas/EnviarPorCorreo", contenido).ConfigureAwait(false))
            {
                string texto = await respuesta.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (respuesta.IsSuccessStatusCode || respuesta.StatusCode == HttpStatusCode.BadGateway)
                {
                    JObject resultado = JObject.Parse(texto);
                    if (resultado.Value<bool?>("Enviado") != true)
                    {
                        throw new InvalidOperationException(resultado.Value<string>("Mensaje") ?? "El correo no se ha podido enviar.");
                    }
                    return new ResultadoEnvioCorreo
                    {
                        Destinatarios = resultado["Correos"]?.ToObject<List<string>>() ?? new List<string>(),
                        Adjunto = resultado["Facturas"]?.ToObject<List<string>>()?.FirstOrDefault(),
                        Mensaje = resultado.Value<string>("Mensaje")
                    };
                }
                throw new InvalidOperationException(MensajeDeError(texto));
            }
        }

        private async Task<ResultadoEnvioCorreo> Enviar(string url, object cuerpo)
        {
            using (HttpClient client = _clienteApiFactory.Crear())
            using (var contenido = new StringContent(JsonConvert.SerializeObject(cuerpo), Encoding.UTF8, "application/json"))
            using (HttpResponseMessage respuesta = await client.PostAsync(url, contenido).ConfigureAwait(false))
            {
                string texto = await respuesta.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!respuesta.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException(MensajeDeError(texto));
                }
                return JsonConvert.DeserializeObject<ResultadoEnvioCorreo>(texto) ?? new ResultadoEnvioCorreo();
            }
        }

        /// <summary>El motivo que manda la API: un BadRequest es {"Message": …}; un Content(403/404/502, texto), una cadena JSON.</summary>
        internal static string MensajeDeError(string cuerpo)
        {
            string limpio = cuerpo?.Trim();
            if (!string.IsNullOrEmpty(limpio) && limpio.StartsWith("\""))
            {
                try
                {
                    return JsonConvert.DeserializeObject<string>(limpio);
                }
                catch (JsonException)
                {
                    // sigue abajo
                }
            }
            return HttpErrorHelper.ParsearErrorHttp(limpio);
        }
    }
}
