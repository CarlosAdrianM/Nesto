using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System;
using System.Windows.Input;

namespace ControlesUsuario.Dialogs
{
    public class InputAmountDialogViewModel : BindableBase, IDialogAware
    {
        private string _title = "Introducir Importe";
        private string _message;
        private string _amount;

        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

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

        public event Action<IDialogResult> RequestClose;

        public DelegateCommand AcceptCommand { get; }
        public DelegateCommand CancelCommand { get; }

        public InputAmountDialogViewModel()
        {
            AcceptCommand = new DelegateCommand(Accept);
            CancelCommand = new DelegateCommand(Cancel);
        }

        private void Accept()
        {
            decimal amount;
            if (decimal.TryParse(Amount, out amount))
            {
                var parameters = new DialogParameters
                {
                    { "amount", amount }
                };
                RequestClose?.Invoke(new DialogResult(ButtonResult.OK, parameters));
            }
            else
            {
                // Opcionalmente, mostrar un mensaje de error si el formato no es válido
            }
        }

        private void Cancel()
        {
            RequestClose?.Invoke(new DialogResult(ButtonResult.Cancel));
        }

        public bool CanCloseDialog() => true;
        public void OnDialogClosed() { }

        public void OnDialogOpened(IDialogParameters parameters)
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