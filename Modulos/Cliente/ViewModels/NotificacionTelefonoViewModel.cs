using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Nesto.Modulos.Cliente.ViewModels
{
    public class NotificacionTelefonoViewModel : BindableBase, IDialogAware
    {
        public NotificacionTelefonoViewModel()
        {

        }

        private DelegateCommand<string> _closeDialogCommand;
        public DelegateCommand<string> CloseDialogCommand =>
            _closeDialogCommand ?? (_closeDialogCommand = new DelegateCommand<string>(CloseDialog));

        public string Title => "Clientes con el mismo teléfono:";

        private List<ClienteTelefonoLookup> _clientesMismoTelefono;
        public List<ClienteTelefonoLookup> ClientesMismoTelefono
        {
            get { return _clientesMismoTelefono; }
            set { SetProperty(ref _clientesMismoTelefono, value); }
        }

        public event Action<IDialogResult> RequestClose;

        protected virtual void CloseDialog(string parameter)
        {
            ButtonResult result = ButtonResult.None;

            if (parameter?.ToLower() == "true")
                result = ButtonResult.OK;
            else if (parameter?.ToLower() == "false")
                result = ButtonResult.Cancel;

            RaiseRequestClose(new DialogResult(result));
        }

        public virtual void RaiseRequestClose(IDialogResult dialogResult)
        {
            RequestClose?.Invoke(dialogResult);
        }

        public bool CanCloseDialog()
        {
            return true;
        }

        public void OnDialogClosed()
        {
            
        }

        public void OnDialogOpened(IDialogParameters parameters)
        {
            ClientesMismoTelefono = parameters.GetValue<List<ClienteTelefonoLookup>>("clientesMismoTelefono");
        }
    }
}
