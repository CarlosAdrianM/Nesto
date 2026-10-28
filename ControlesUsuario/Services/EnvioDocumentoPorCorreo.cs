using ControlesUsuario.Dialogs;
using Nesto.Infrastructure.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ControlesUsuario.Services
{
    /// <summary>
    /// Nesto#563 (NestoAPI#724, sugerencia 831): «Enviar por correo…» junto a cada descarga. Nesto corre en el escritorio
    /// remoto y Outlook web no adjunta ficheros de allí: en vez de descargar, copiar al escritorio y adjuntar, el
    /// correo sale desde la API. Todas las pantallas lo hacen igual: este paso abre el diálogo único
    /// (<see cref="EnviarPorCorreoDialogViewModel"/>) con los destinatarios propuestos, manda lo escrito y avisa.
    /// </summary>
    public interface IEnvioDocumentoPorCorreo
    {
        /// <summary>True si el correo ha salido; false si el usuario cancela o la API lo rechaza (ya se le ha dicho por qué).</summary>
        Task<bool> EnviarPedido(string empresa, int numero, string cliente, string contacto);
        Task<bool> EnviarMandato(string empresa, string cliente, string contacto, string ccc, string motor);
        Task<bool> EnviarFactura(string empresa, string factura, string cliente, string contacto);
    }

    public class EnvioDocumentoPorCorreo : IEnvioDocumentoPorCorreo
    {
        internal const string TITULO_ENVIADO = "Correo enviado";

        private readonly IServicioEnvioCorreoDocumentos _servicio;
        private readonly IServicioDialogos _dialogos;

        public EnvioDocumentoPorCorreo(IServicioEnvioCorreoDocumentos servicio, IServicioDialogos dialogos)
        {
            _servicio = servicio ?? throw new ArgumentNullException(nameof(servicio));
            _dialogos = dialogos ?? throw new ArgumentNullException(nameof(dialogos));
        }

        public Task<bool> EnviarPedido(string empresa, int numero, string cliente, string contacto)
            => Enviar($"Enviar el pedido {numero} por correo",
                $"Se adjunta el pedido {numero} en PDF (el mismo de «Descargar PDF»).",
                () => _servicio.LeerCorreosContacto(empresa, cliente, contacto),
                peticion => _servicio.EnviarPedido(empresa, numero, peticion));

        public Task<bool> EnviarMandato(string empresa, string cliente, string contacto, string ccc, string motor)
            => Enviar("Enviar el mandato SEPA por correo",
                $"Se adjunta la orden de domiciliación SEPA del cliente {cliente?.Trim()}/{contacto?.Trim()} para que la firme.",
                () => _servicio.LeerCorreosContacto(empresa, cliente, contacto),
                peticion => _servicio.EnviarMandato(empresa, cliente, contacto, ccc, motor, peticion));

        public Task<bool> EnviarFactura(string empresa, string factura, string cliente, string contacto)
            => Enviar($"Enviar la factura {factura?.Trim()} por correo",
                $"Se adjunta la factura {factura?.Trim()} en PDF. Va con el asunto y el texto de siempre de las facturas.",
                async () =>
                {
                    // Primero el correo de facturas del cliente; si no tiene, sus personas de contacto
                    List<string> deFacturas = await _servicio.LeerCorreosFacturas(empresa, factura);
                    return deFacturas != null && deFacturas.Any()
                        ? deFacturas
                        : await _servicio.LeerCorreosContacto(empresa, cliente, contacto);
                },
                peticion => _servicio.EnviarFactura(empresa, factura, peticion),
                admiteAsuntoYTexto: false);

        private async Task<bool> Enviar(string titulo, string mensaje, Func<Task<List<string>>> propuestos,
            Func<PeticionEnvioCorreo, Task<ResultadoEnvioCorreo>> enviar, bool admiteAsuntoYTexto = true)
        {
            List<string> correos;
            try
            {
                correos = await propuestos() ?? new List<string>();
            }
            catch (Exception)
            {
                // Solo es una propuesta: sin ella, el usuario escribe el correo
                correos = new List<string>();
            }

            var parametros = new ParametrosDialogo
            {
                { EnviarPorCorreoDialogViewModel.PARAMETRO_TITULO, titulo },
                { EnviarPorCorreoDialogViewModel.PARAMETRO_MENSAJE, mensaje },
                { EnviarPorCorreoDialogViewModel.PARAMETRO_DESTINATARIOS, string.Join("; ", correos) },
                { EnviarPorCorreoDialogViewModel.PARAMETRO_ADMITE_ASUNTO_Y_TEXTO, admiteAsuntoYTexto }
            };
            ResultadoDialogo resultado = await _dialogos.ShowDialogAsync(EnviarPorCorreoDialogViewModel.NOMBRE, parametros);
            if (resultado?.Result != ResultadoBoton.OK)
            {
                return false;
            }

            var peticion = new PeticionEnvioCorreo
            {
                Destinatarios = EnviarPorCorreoDialogViewModel.SepararCorreos(
                    Leer(resultado, EnviarPorCorreoDialogViewModel.PARAMETRO_DESTINATARIOS)),
                Asunto = admiteAsuntoYTexto ? Leer(resultado, EnviarPorCorreoDialogViewModel.PARAMETRO_ASUNTO) : null,
                Texto = admiteAsuntoYTexto ? Leer(resultado, EnviarPorCorreoDialogViewModel.PARAMETRO_TEXTO) : null
            };
            if (!peticion.Destinatarios.Any())
            {
                _dialogos.ShowError("Escriba el correo al que quiere mandarlo.");
                return false;
            }

            try
            {
                ResultadoEnvioCorreo enviado = await enviar(peticion);
                List<string> a = enviado?.Destinatarios != null && enviado.Destinatarios.Any() ? enviado.Destinatarios : peticion.Destinatarios;
                _dialogos.ShowNotification(TITULO_ENVIADO, $"Enviado a {string.Join(", ", a)}.");
                return true;
            }
            catch (Exception ex)
            {
                _dialogos.ShowError("No se ha enviado el correo: " + (ex.InnerException?.Message ?? ex.Message));
                return false;
            }
        }

        private static string Leer(ResultadoDialogo resultado, string clave)
            => resultado.Parameters != null && resultado.Parameters.ContainsKey(clave) ? resultado.Parameters.GetValue<string>(clave) : null;
    }
}
