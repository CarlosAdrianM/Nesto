using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Prism.Services.Dialogs;
using System;

namespace ControlesUsuario.Dialogs
{
    /// <summary>
    /// NestoAPI#582: pide una fecha, con la opción «Todavía no se sabe» (cierra sin fecha). La fecha vuelve en el
    /// parámetro "date" (DateTime) del resultado. Sigue siendo IDialogAware de Prism como los demás diálogos
    /// genéricos hasta que se pasen todos a IDialogoNesto (Nesto#490).
    /// </summary>
    public class InputDateDialogViewModel : ObservableObject, IDialogAware
    {
        private string _title = "Fecha";
        private string _message;
        private DateTime? _fecha;

        public InputDateDialogViewModel()
        {
            AcceptCommand = new RelayCommand(Aceptar, () => Fecha.HasValue);
            NoSeSabeCommand = new RelayCommand(() => RequestClose?.Invoke(new DialogResult(ButtonResult.Cancel)));
        }

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

        public DateTime? Fecha
        {
            get => _fecha;
            set
            {
                if (SetProperty(ref _fecha, value))
                {
                    AcceptCommand?.NotifyCanExecuteChanged();
                }
            }
        }

        public event Action<IDialogResult> RequestClose;

        public RelayCommand AcceptCommand { get; }

        /// <summary>«Todavía no se sabe»: se cierra sin fecha.</summary>
        public RelayCommand NoSeSabeCommand { get; }

        private void Aceptar()
        {
            RequestClose?.Invoke(new DialogResult(ButtonResult.OK, new DialogParameters { { "date", Fecha.Value.Date } }));
        }

        public bool CanCloseDialog() => true;

        public void OnDialogClosed() { }

        public void OnDialogOpened(IDialogParameters parameters)
        {
            if (parameters.ContainsKey("title"))
            {
                Title = parameters.GetValue<string>("title");
            }
            if (parameters.ContainsKey("message"))
            {
                Message = parameters.GetValue<string>("message");
            }
            if (parameters.ContainsKey("defaultDate"))
            {
                Fecha = parameters.GetValue<DateTime?>("defaultDate");
            }
        }
    }
}
