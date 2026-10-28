using CommunityToolkit.Mvvm.Input;
using Nesto.Infrastructure.Contracts;
using System;
using System.Collections.Generic;

namespace ControlesUsuario.Dialogs
{
    /// <summary>
    /// Nesto#564 (NestoAPI#725, Carlos 28/10/26, envío 249990): aviso EN ROJO antes de guardar un cambio de un envío ya
    /// registrado en la agencia (CTT, Innovatrans) que solo se hace en NUESTRO sistema. Se abre dos veces
    /// (<see cref="PeticionCambioSinAgencia"/>): la primera solo pregunta (Sí/No); la segunda
    /// (<see cref="PARAMETRO_PEDIR_TEXTO"/>) pide con quién se ha hablado en la agencia y cuándo, obligatorio
    /// (<see cref="LONGITUD_MINIMA_TEXTO"/> caracteres). OK devuelve el texto en <see cref="PARAMETRO_TEXTO"/>; «No» o
    /// cerrar, Cancel. El botón por defecto es «No».
    /// </summary>
    public class CambioSinAgenciaDialogViewModel : DialogoNestoBase
    {
        public const string NOMBRE = "CambioSinAgenciaDialog";
        public const string PARAMETRO_TITULO = "title";
        public const string PARAMETRO_MENSAJE = "message";
        public const string PARAMETRO_PEDIR_TEXTO = "pedirTexto";
        public const string PARAMETRO_TEXTO = "text";
        /// <summary>Igual que en la API (EnviosAgenciasController.LONGITUD_MINIMA_CONFIRMACION_AGENCIA).</summary>
        public const int LONGITUD_MINIMA_TEXTO = 5;

        private string _message;
        private string _texto;
        private bool _pedirTexto;

        public CambioSinAgenciaDialogViewModel()
        {
            Title = "⚠ Cambio SOLO en nuestro sistema ⚠";
            AceptarCommand = new RelayCommand(Aceptar, PuedeAceptar);
            CancelarCommand = new RelayCommand(() => RaiseRequestClose(ResultadoBoton.Cancel));
        }

        public string Message
        {
            get => _message;
            set => SetProperty(ref _message, value);
        }

        /// <summary>Segunda confirmación: con el cuadro «¿Con quién ha hablado y cuándo?».</summary>
        public bool PedirTexto
        {
            get => _pedirTexto;
            set
            {
                if (SetProperty(ref _pedirTexto, value))
                {
                    OnPropertyChanged(nameof(TextoBotonAceptar));
                    AceptarCommand.NotifyCanExecuteChanged();
                }
            }
        }

        public string Texto
        {
            get => _texto;
            set
            {
                if (SetProperty(ref _texto, value))
                {
                    AceptarCommand.NotifyCanExecuteChanged();
                }
            }
        }

        public string TextoBotonAceptar => PedirTexto ? "Guardar solo en Nesto" : "Sí, la agencia lo ha confirmado";

        public RelayCommand AceptarCommand { get; }
        public RelayCommand CancelarCommand { get; }

        internal bool PuedeAceptar() => !PedirTexto || (Texto?.Trim().Length ?? 0) >= LONGITUD_MINIMA_TEXTO;

        private void Aceptar()
        {
            if (!PuedeAceptar())
            {
                return;
            }
            RaiseRequestClose(new ResultadoDialogo(ResultadoBoton.OK, new ParametrosDialogo
            {
                { PARAMETRO_TEXTO, PedirTexto ? Texto.Trim() : null }
            }));
        }

        public override void OnDialogOpened(ParametrosDialogo parameters)
        {
            if (parameters.ContainsKey(PARAMETRO_TITULO))
            {
                Title = parameters.GetValue<string>(PARAMETRO_TITULO);
            }
            if (parameters.ContainsKey(PARAMETRO_MENSAJE))
            {
                Message = parameters.GetValue<string>(PARAMETRO_MENSAJE);
            }
            if (parameters.ContainsKey(PARAMETRO_PEDIR_TEXTO))
            {
                PedirTexto = parameters.GetValue<bool>(PARAMETRO_PEDIR_TEXTO);
            }
        }
    }

    /// <summary>
    /// Nesto#564: las dos confirmaciones del cambio que no llega a la agencia. Devuelve con quién se habló y cuándo, o null si
    /// la persona dice que no (en cualquiera de las dos), cierra, o el diálogo no está registrado: entonces no se guarda nada.
    /// </summary>
    public static class PeticionCambioSinAgencia
    {
        public const string PREGUNTA_TEXTO = "¿Con quién ha hablado en la agencia y cuándo? (obligatorio; quedará registrado en el envío)";

        /// <summary>El texto de alarma (de usted) con los datos que cambian.</summary>
        public static string Mensaje(IEnumerable<string> campos)
            => $"⚠ Este cambio (campos: {string.Join(", ", campos ?? Array.Empty<string>())}) se va a hacer en NUESTRO sistema pero NO en el de la agencia. ⚠" +
               Environment.NewLine + Environment.NewLine +
               "Solo debe hacerse si ha hablado con la agencia y le han confirmado que lo cambian en su sistema." +
               Environment.NewLine + Environment.NewLine +
               "Si quiere que la agencia reciba el cambio, use «Modificar datos y reenviar a la agencia».";

        public static string Pedir(IServicioDialogos dialogos, IEnumerable<string> campos)
        {
            if (dialogos == null)
            {
                return null;
            }
            string mensaje = Mensaje(campos);
            try
            {
                if (!Abrir(dialogos, mensaje, pedirTexto: false, out _))
                {
                    return null;
                }
                if (!Abrir(dialogos, mensaje + Environment.NewLine + Environment.NewLine + PREGUNTA_TEXTO, pedirTexto: true, out string texto))
                {
                    return null;
                }
                texto = texto?.Trim();
                return string.IsNullOrEmpty(texto) || texto.Length < CambioSinAgenciaDialogViewModel.LONGITUD_MINIMA_TEXTO ? null : texto;
            }
            catch (Exception)
            {
                // Sin diálogo registrado (o cualquier fallo al abrirlo) no se guarda nada: es lo seguro.
                return null;
            }
        }

        private static bool Abrir(IServicioDialogos dialogos, string mensaje, bool pedirTexto, out string texto)
        {
            bool aceptado = false;
            string contestado = null;
            var parametros = new ParametrosDialogo
            {
                { CambioSinAgenciaDialogViewModel.PARAMETRO_MENSAJE, mensaje },
                { CambioSinAgenciaDialogViewModel.PARAMETRO_PEDIR_TEXTO, pedirTexto }
            };
            dialogos.ShowDialog(CambioSinAgenciaDialogViewModel.NOMBRE, parametros, r =>
            {
                if (r == null || r.Result != ResultadoBoton.OK)
                {
                    return;
                }
                aceptado = true;
                contestado = r.Parameters?.GetValue<string>(CambioSinAgenciaDialogViewModel.PARAMETRO_TEXTO);
            });
            texto = contestado;
            return aceptado;
        }
    }
}
