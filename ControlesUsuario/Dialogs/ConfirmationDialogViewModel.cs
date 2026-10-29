using CommunityToolkit.Mvvm.Input;
using Nesto.Infrastructure.Contracts;

namespace ControlesUsuario.Dialogs
{
    public class ConfirmationDialogViewModel : DialogoNestoBase
    {
        private RelayCommand<string> _closeDialogCommand;
        public RelayCommand<string> CloseDialogCommand =>
            _closeDialogCommand ?? (_closeDialogCommand = new RelayCommand<string>(CloseDialog));

        private string _message;
        public string Message
        {
            get { return _message; }
            set { SetProperty(ref _message, value); }
        }

        /// <summary>Parámetro bool: con true, Intro contesta «No» (el botón por defecto es «No»).</summary>
        public const string PARAMETRO_POR_DEFECTO_NO = "porDefectoNo";

        private bool _porDefectoNo;
        /// <summary>El botón por defecto (Intro) es «No» en vez de «Sí».</summary>
        public bool PorDefectoNo
        {
            get { return _porDefectoNo; }
            set
            {
                if (SetProperty(ref _porDefectoNo, value))
                {
                    OnPropertyChanged(nameof(PorDefectoSi));
                }
            }
        }

        public bool PorDefectoSi => !PorDefectoNo;

        public ConfirmationDialogViewModel()
        {
            Title = "Confirmar";
        }

        protected virtual void CloseDialog(string parameter)
        {
            ResultadoBoton result = ResultadoBoton.None;

            if (parameter?.ToLower() == "true")
                result = ResultadoBoton.OK;
            else if (parameter?.ToLower() == "false")
                result = ResultadoBoton.Cancel;

            RaiseRequestClose(result);
        }

        public override void OnDialogOpened(ParametrosDialogo parameters)
        {
            Message = parameters.GetValue<string>("message");
            Title = parameters.GetValue<string>("title");
            PorDefectoNo = parameters.TryGetValue(PARAMETRO_POR_DEFECTO_NO, out bool porDefectoNo) && porDefectoNo;
            if (parameters.TryGetValue(PARAMETRO_TEXTO_SI, out string textoSi) && !string.IsNullOrWhiteSpace(textoSi))
            {
                TextoSi = textoSi;
            }
            if (parameters.TryGetValue(PARAMETRO_TEXTO_NO, out string textoNo) && !string.IsNullOrWhiteSpace(textoNo))
            {
                TextoNo = textoNo;
            }
        }

        /// <summary>Nesto#568: texto del botón «Sí» (p. ej. «Retener y consultar a Administración»).</summary>
        public const string PARAMETRO_TEXTO_SI = "textoSi";
        /// <summary>Nesto#568: texto del botón «No».</summary>
        public const string PARAMETRO_TEXTO_NO = "textoNo";

        private string _textoSi = "Sí";
        public string TextoSi { get => _textoSi; set => SetProperty(ref _textoSi, value); }

        private string _textoNo = "No";
        public string TextoNo { get => _textoNo; set => SetProperty(ref _textoNo, value); }
    }

    /// <summary>
    /// Nesto#568: una confirmación con los textos de los botones a medida (el diálogo de confirmación de siempre). True solo
    /// si se pulsa el primero.
    /// </summary>
    public static class ConfirmacionConBotones
    {
        public static bool Preguntar(IServicioDialogos dialogos, string titulo, string mensaje, string textoSi, string textoNo, bool porDefectoNo = false)
        {
            if (dialogos == null)
            {
                return false;
            }
            bool confirmado = false;
            var parametros = new ParametrosDialogo
            {
                { "title", titulo },
                { "message", mensaje },
                { ConfirmationDialogViewModel.PARAMETRO_TEXTO_SI, textoSi },
                { ConfirmationDialogViewModel.PARAMETRO_TEXTO_NO, textoNo }
            };
            if (porDefectoNo)
            {
                parametros.Add(ConfirmationDialogViewModel.PARAMETRO_POR_DEFECTO_NO, true);
            }
            dialogos.ShowDialog(ServicioDialogosBase.ConfirmationDialog, parametros, r => confirmado = r?.Result == ResultadoBoton.OK);
            return confirmado;
        }
    }
}
