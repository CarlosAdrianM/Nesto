using CommunityToolkit.Mvvm.Input;
using Nesto.Infrastructure.Contracts;

namespace ControlesUsuario.Dialogs
{
    public class NotificationDialogViewModel : DialogoNestoBase
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

        public NotificationDialogViewModel()
        {
            Title = "Información";
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
            if (parameters.ContainsKey("message"))
            {
                Message = parameters.GetValue<string>("message");
            }

            if (parameters.ContainsKey("title"))
            {
                Title = parameters.GetValue<string>("title");
            }
        }
    }
}
