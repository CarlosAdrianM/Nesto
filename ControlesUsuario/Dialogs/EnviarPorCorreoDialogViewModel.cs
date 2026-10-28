using CommunityToolkit.Mvvm.Input;
using Nesto.Infrastructure.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ControlesUsuario.Dialogs
{
    /// <summary>
    /// Nesto#563 (NestoAPI#724, sugerencia 831): el diálogo único para mandar un documento por correo (pedido, mandato
    /// SEPA o factura) sin descargarlo. Destinatarios precargados (las personas de contacto del cliente con correo),
    /// editables, varios separados por «;». Asunto y texto, opcionales: si se dejan en blanco, la API pone los suyos.
    /// «Enviar» devuelve OK con <see cref="PARAMETRO_DESTINATARIOS"/>, <see cref="PARAMETRO_ASUNTO"/> y
    /// <see cref="PARAMETRO_TEXTO"/>; «Cancelar» (o cerrar), Cancel.
    /// </summary>
    public class EnviarPorCorreoDialogViewModel : DialogoNestoBase
    {
        public const string NOMBRE = "EnviarPorCorreoDialog";
        public const string PARAMETRO_TITULO = "title";
        public const string PARAMETRO_MENSAJE = "message";
        public const string PARAMETRO_DESTINATARIOS = "destinatarios";
        public const string PARAMETRO_ASUNTO = "asunto";
        public const string PARAMETRO_TEXTO = "texto";
        /// <summary>False para las facturas: su envío usa siempre el asunto y el texto de la serie.</summary>
        public const string PARAMETRO_ADMITE_ASUNTO_Y_TEXTO = "admiteAsuntoYTexto";

        private string _message;
        private string _destinatarios;
        private string _asunto;
        private string _texto;
        private bool _admiteAsuntoYTexto = true;

        public EnviarPorCorreoDialogViewModel()
        {
            Title = "Enviar por correo";
            Message = "Se adjunta el documento en PDF.";
            EnviarCommand = new RelayCommand(Enviar, () => HayDestinatarios);
            CancelarCommand = new RelayCommand(() => RaiseRequestClose(ResultadoBoton.Cancel));
        }

        public string Message
        {
            get => _message;
            set => SetProperty(ref _message, value);
        }

        /// <summary>Uno o varios correos, separados por «;» (también vale «,»).</summary>
        public string Destinatarios
        {
            get => _destinatarios;
            set
            {
                if (SetProperty(ref _destinatarios, value))
                {
                    OnPropertyChanged(nameof(HayDestinatarios));
                    EnviarCommand.NotifyCanExecuteChanged();
                }
            }
        }

        public bool HayDestinatarios => SepararCorreos(Destinatarios).Any();

        public string Asunto
        {
            get => _asunto;
            set => SetProperty(ref _asunto, value);
        }

        public string Texto
        {
            get => _texto;
            set => SetProperty(ref _texto, value);
        }

        public bool AdmiteAsuntoYTexto
        {
            get => _admiteAsuntoYTexto;
            set => SetProperty(ref _admiteAsuntoYTexto, value);
        }

        public RelayCommand EnviarCommand { get; }
        public RelayCommand CancelarCommand { get; }

        private void Enviar()
        {
            if (!HayDestinatarios)
            {
                return;
            }
            RaiseRequestClose(new ResultadoDialogo(ResultadoBoton.OK, new ParametrosDialogo
            {
                { PARAMETRO_DESTINATARIOS, string.Join("; ", SepararCorreos(Destinatarios)) },
                { PARAMETRO_ASUNTO, !AdmiteAsuntoYTexto || string.IsNullOrWhiteSpace(Asunto) ? null : Asunto.Trim() },
                { PARAMETRO_TEXTO, !AdmiteAsuntoYTexto || string.IsNullOrWhiteSpace(Texto) ? null : Texto }
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
            if (parameters.ContainsKey(PARAMETRO_DESTINATARIOS))
            {
                Destinatarios = parameters.GetValue<string>(PARAMETRO_DESTINATARIOS);
            }
            if (parameters.ContainsKey(PARAMETRO_ASUNTO))
            {
                Asunto = parameters.GetValue<string>(PARAMETRO_ASUNTO);
            }
            if (parameters.ContainsKey(PARAMETRO_ADMITE_ASUNTO_Y_TEXTO))
            {
                AdmiteAsuntoYTexto = parameters.GetValue<bool>(PARAMETRO_ADMITE_ASUNTO_Y_TEXTO);
            }
        }

        /// <summary>Los correos escritos, sin espacios ni repetidos («;» o «,» para separarlos).</summary>
        public static List<string> SepararCorreos(string correos)
        {
            var resultado = new List<string>();
            foreach (string correo in (correos ?? string.Empty)
                .Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(c => c.Trim())
                .Where(c => c.Length > 0))
            {
                if (!resultado.Contains(correo, StringComparer.OrdinalIgnoreCase))
                {
                    resultado.Add(correo);
                }
            }
            return resultado;
        }
    }
}
