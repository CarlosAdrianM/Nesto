using CommunityToolkit.Mvvm.Input;
using ControlesUsuario.Dialogs;
using Nesto.Infrastructure.Contracts;
using System.Collections.Generic;

namespace Nesto.Modulos.Cliente.ViewModels
{
    public class NotificacionTelefonoViewModel : DialogoNestoBase
    {
        public NotificacionTelefonoViewModel()
        {
            Title = "Clientes con el mismo teléfono:";
        }

        private RelayCommand<string> _closeDialogCommand;
        public RelayCommand<string> CloseDialogCommand =>
            _closeDialogCommand ?? (_closeDialogCommand = new RelayCommand<string>(CloseDialog));

        private List<ClienteTelefonoLookup> _clientesMismoTelefono;
        public List<ClienteTelefonoLookup> ClientesMismoTelefono
        {
            get { return _clientesMismoTelefono; }
            set { SetProperty(ref _clientesMismoTelefono, value); }
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
            ClientesMismoTelefono = parameters.GetValue<List<ClienteTelefonoLookup>>("clientesMismoTelefono");
        }
    }
}
