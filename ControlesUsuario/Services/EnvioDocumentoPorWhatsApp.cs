using Nesto.Infrastructure.Contracts;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace ControlesUsuario.Services
{
    /// <summary>
    /// Nesto#570 (NestoAPI#737): lo que contesta POST api/Documentos/EnlaceDescarga: el enlace público del PDF (30 días), el texto
    /// de usted para el cliente (con el enlace dentro) y sus móviles en formato internacional (34… si es de España, sin «+»).
    /// </summary>
    public class EnlaceDescargaDocumento
    {
        public string Url { get; set; }
        public DateTime Caduca { get; set; }
        public string Tipo { get; set; }
        public string Numero { get; set; }
        public string Nombre { get; set; }
        public decimal Importe { get; set; }
        public List<string> Moviles { get; set; } = new List<string>();
        public string Texto { get; set; }
    }

    /// <summary>Nesto#570: pedir a la API el enlace de descarga de un pedido o una factura.</summary>
    public interface IServicioEnlacesDescarga
    {
        /// <summary>Lanza la excepción con el motivo de la API si no se puede (no es un empleado, no existe, serie que no se descarga…).</summary>
        Task<EnlaceDescargaDocumento> CrearEnlace(string tipo, string empresa, string numero);
    }

    public class ServicioEnlacesDescarga : IServicioEnlacesDescarga
    {
        private readonly IClienteApiFactory _clienteApiFactory;

        public ServicioEnlacesDescarga(IClienteApiFactory clienteApiFactory)
        {
            _clienteApiFactory = clienteApiFactory ?? throw new ArgumentNullException(nameof(clienteApiFactory));
        }

        public async Task<EnlaceDescargaDocumento> CrearEnlace(string tipo, string empresa, string numero)
        {
            var cuerpo = new { Tipo = tipo, Empresa = empresa?.Trim(), Numero = numero?.Trim() };
            using (HttpClient client = _clienteApiFactory.Crear())
            using (var contenido = new StringContent(JsonConvert.SerializeObject(cuerpo), Encoding.UTF8, "application/json"))
            using (HttpResponseMessage respuesta = await client.PostAsync("Documentos/EnlaceDescarga", contenido).ConfigureAwait(false))
            {
                string texto = await respuesta.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!respuesta.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException(ServicioEnvioCorreoDocumentos.MensajeDeError(texto));
                }
                return JsonConvert.DeserializeObject<EnlaceDescargaDocumento>(texto);
            }
        }
    }

    /// <summary>
    /// Nesto#570 (NestoAPI#737, sugerencias 866/867 y 831): «Enviar por WhatsApp…» junto a «Enviar por correo…». No es WhatsApp
    /// Business: pide a la API un enlace de descarga del PDF y abre WhatsApp Web con el mensaje de usted ya escrito y el móvil del
    /// cliente; el usuario solo pulsa «Enviar». Sin móvil, avisa (y deja abrirlo para elegir el contacto a mano); con varios, pregunta.
    /// </summary>
    public interface IEnvioDocumentoPorWhatsApp
    {
        /// <summary>True si se ha abierto WhatsApp Web; false si el usuario cancela o la API lo rechaza (ya se le ha dicho por qué).</summary>
        Task<bool> EnviarPedido(string empresa, int numero);
        Task<bool> EnviarFactura(string empresa, string factura);
    }

    public class EnvioDocumentoPorWhatsApp : IEnvioDocumentoPorWhatsApp
    {
        public const string TIPO_PEDIDO = "Pedido";
        public const string TIPO_FACTURA = "Factura";
        internal const string URL_WHATSAPP_WEB = "https://web.whatsapp.com/send";
        internal const string TITULO = "Enviar por WhatsApp";

        private readonly IServicioEnlacesDescarga _servicio;
        private readonly IServicioDialogos _dialogos;
        private readonly Action<string> _abrirNavegador;

        public EnvioDocumentoPorWhatsApp(IServicioEnlacesDescarga servicio, IServicioDialogos dialogos, Action<string> abrirNavegador = null)
        {
            _servicio = servicio ?? throw new ArgumentNullException(nameof(servicio));
            _dialogos = dialogos ?? throw new ArgumentNullException(nameof(dialogos));
            _abrirNavegador = abrirNavegador ?? (url => _ = Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }));
        }

        public Task<bool> EnviarPedido(string empresa, int numero)
            => Enviar(TIPO_PEDIDO, empresa, numero.ToString(System.Globalization.CultureInfo.InvariantCulture));

        public Task<bool> EnviarFactura(string empresa, string factura)
            => Enviar(TIPO_FACTURA, empresa, factura);

        private async Task<bool> Enviar(string tipo, string empresa, string numero)
        {
            EnlaceDescargaDocumento enlace;
            try
            {
                enlace = await _servicio.CrearEnlace(tipo, empresa?.Trim(), numero?.Trim());
            }
            catch (Exception ex)
            {
                _dialogos.ShowError("No se ha podido preparar el enlace de descarga: " + (ex.InnerException?.Message ?? ex.Message));
                return false;
            }
            if (enlace == null || string.IsNullOrWhiteSpace(enlace.Url))
            {
                _dialogos.ShowError("No se ha podido preparar el enlace de descarga.");
                return false;
            }

            string texto = string.IsNullOrWhiteSpace(enlace.Texto) ? enlace.Url : enlace.Texto;
            string movil = ElegirMovil(enlace.Moviles, out bool cancelado);
            if (cancelado)
            {
                return false;
            }
            _abrirNavegador(UrlWhatsApp(movil, texto));
            return true;
        }

        /// <summary>
        /// Uno: ese. Varios: se pregunta (con el primero propuesto; se puede escribir otro). Ninguno: se avisa y, si el usuario quiere,
        /// se abre WhatsApp Web sin número para que elija él el contacto (null). <paramref name="cancelado"/> si dice que no.
        /// </summary>
        private string ElegirMovil(List<string> moviles, out bool cancelado)
        {
            cancelado = false;
            List<string> lista = (moviles ?? new List<string>()).Where(m => !string.IsNullOrWhiteSpace(m)).Select(m => m.Trim()).Distinct().ToList();
            if (lista.Count == 1)
            {
                return lista[0];
            }
            if (lista.Count == 0)
            {
                bool abrir = _dialogos.ShowConfirmationAnswer(TITULO,
                    "El cliente no tiene ningún móvil en la ficha ni en las personas de contacto.\n\n" +
                    "¿Quiere abrir WhatsApp Web con el mensaje preparado para elegir usted el contacto?");
                cancelado = !abrir;
                return null;
            }
            string elegido = _dialogos.GetText(TITULO,
                "El cliente tiene varios móviles:\n" + string.Join("\n", lista.Select(m => "+" + m)) +
                "\n\nEscriba el móvil al que quiere enviarlo (con el prefijo del país):", lista[0]);
            if (string.IsNullOrWhiteSpace(elegido))
            {
                cancelado = true;
                return null;
            }
            return Internacional(elegido);
        }

        /// <summary>Lo que escribe el usuario: solo las cifras; 9 cifras = de España (34 delante); «00» delante = prefijo.</summary>
        internal static string Internacional(string movil)
        {
            string cifras = new string((movil ?? string.Empty).Where(char.IsDigit).ToArray());
            if (cifras.StartsWith("00"))
            {
                cifras = cifras.Substring(2);
            }
            return cifras.Length == 9 ? "34" + cifras : cifras;
        }

        /// <summary>https://web.whatsapp.com/send?phone=34600112233&amp;text=… (sin móvil, solo el texto: WhatsApp deja elegir el chat).</summary>
        public static string UrlWhatsApp(string movil, string texto)
        {
            string telefono = string.IsNullOrWhiteSpace(movil) ? null : Internacional(movil);
            string mensaje = "text=" + Uri.EscapeDataString(texto ?? string.Empty);
            return URL_WHATSAPP_WEB + "?" + (string.IsNullOrEmpty(telefono) ? mensaje : "phone=" + telefono + "&" + mensaje);
        }
    }
}
