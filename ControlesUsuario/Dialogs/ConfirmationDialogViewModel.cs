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
        }
    }
}
