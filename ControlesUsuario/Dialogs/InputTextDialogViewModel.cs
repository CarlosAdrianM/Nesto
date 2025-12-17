using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System;

namespace ControlesUsuario.Dialogs
{
    public class InputTextDialogViewModel : BindableBase, IDialogAware
    {
        private string _title = "Introducir texto";
        private string _message;
        private string _text;

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

        public string Text
        {
            get => _text;
            set => SetProperty(ref _text, value);
        }

        public event Action<IDialogResult> RequestClose;

        public DelegateCommand AcceptCommand { get; }
        public DelegateCommand CancelCommand { get; }

        public InputTextDialogViewModel()
        {
            AcceptCommand = new DelegateCommand(Accept);
            CancelCommand = new DelegateCommand(Cancel);
        }

        private void Accept()
        {
            var parameters = new DialogParameters
            {
                { "text", Text ?? string.Empty }
            };
            RequestClose?.Invoke(new DialogResult(ButtonResult.OK, parameters));
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

            if (parameters.ContainsKey("defaultText"))
                Text = parameters.GetValue<string>("defaultText");
        }
    }
}
