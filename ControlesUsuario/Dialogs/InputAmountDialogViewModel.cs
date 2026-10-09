using CommunityToolkit.Mvvm.Input;
using Nesto.Infrastructure.Contracts;

namespace ControlesUsuario.Dialogs
{
    public class InputAmountDialogViewModel : DialogoNestoBase
    {
        private string _message;
        private string _amount;

        public string Message
        {
            get => _message;
            set => SetProperty(ref _message, value);
        }

        public string Amount
        {
            get => _amount;
            set => SetProperty(ref _amount, value);
        }

        public RelayCommand AcceptCommand { get; }
        public RelayCommand CancelCommand { get; }

        public InputAmountDialogViewModel()
        {
            Title = "Introducir Importe";
            AcceptCommand = new RelayCommand(Accept);
            CancelCommand = new RelayCommand(Cancel);
        }

        private void Accept()
        {
            decimal amount;
            if (decimal.TryParse(Amount, out amount))
            {
                var parameters = new ParametrosDialogo
                {
                    { "amount", amount }
                };
                RaiseRequestClose(new ResultadoDialogo(ResultadoBoton.OK, parameters));
            }
            else
            {
                // Opcionalmente, mostrar un mensaje de error si el formato no es válido
            }
        }

        private void Cancel()
        {
            RaiseRequestClose(ResultadoBoton.Cancel);
        }

        public override void OnDialogOpened(ParametrosDialogo parameters)
        {
            if (parameters.ContainsKey("title"))
                Title = parameters.GetValue<string>("title");

            if (parameters.ContainsKey("message"))
                Message = parameters.GetValue<string>("message");

            if (parameters.ContainsKey("defaultAmount"))
                Amount = parameters.GetValue<string>("defaultAmount");
        }
    }
}
