using CommunityToolkit.Mvvm.Input;
using Nesto.Infrastructure.Contracts;
using System;

namespace ControlesUsuario.Dialogs
{
    /// <summary>
    /// NestoAPI#582: pide una fecha, con la opción «Todavía no se sabe» (cierra sin fecha). La fecha vuelve en el
    /// parámetro "date" (DateTime) del resultado. Nesto#490: es <see cref="IDialogoNesto"/> (por
    /// <see cref="DialogoNestoBase"/>), sin tipos de Prism.
    /// </summary>
    public class InputDateDialogViewModel : DialogoNestoBase
    {
        private string _message;
        private DateTime? _fecha;

        public InputDateDialogViewModel()
        {
            Title = "Fecha";
            AcceptCommand = new RelayCommand(Aceptar, () => Fecha.HasValue);
            NoSeSabeCommand = new RelayCommand(() => RaiseRequestClose(ResultadoBoton.Cancel));
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

        public RelayCommand AcceptCommand { get; }

        /// <summary>«Todavía no se sabe»: se cierra sin fecha.</summary>
        public RelayCommand NoSeSabeCommand { get; }

        private void Aceptar()
        {
            RaiseRequestClose(new ResultadoDialogo(ResultadoBoton.OK, new ParametrosDialogo { { "date", Fecha.Value.Date } }));
        }

        public override void OnDialogOpened(ParametrosDialogo parameters)
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
