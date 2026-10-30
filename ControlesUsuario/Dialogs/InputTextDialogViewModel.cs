using CommunityToolkit.Mvvm.Input;
using Nesto.Infrastructure.Contracts;

namespace ControlesUsuario.Dialogs
{
    public class InputTextDialogViewModel : DialogoNestoBase
    {
        /// <summary>Nesto#589: el cuadro acepta lo leído con el escáner remoto (Ariadna).</summary>
        public const string PARAMETRO_ACEPTA_CODIGO_BARRAS = "aceptaCodigoBarras";

        private string _message;
        private string _text;
        private bool _aceptaCodigoBarras;

        /// <summary>Nesto#589: el cuadro lleva <c>LectorCodigoBarras.AceptaCodigoBarras</c> (lo pide GetCodigoBarras).</summary>
        public bool AceptaCodigoBarras
        {
            get => _aceptaCodigoBarras;
            set => SetProperty(ref _aceptaCodigoBarras, value);
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

        public RelayCommand AcceptCommand { get; }
        public RelayCommand CancelCommand { get; }

        public InputTextDialogViewModel()
        {
            Title = "Introducir texto";
            AcceptCommand = new RelayCommand(Accept);
            CancelCommand = new RelayCommand(Cancel);
        }

        private void Accept()
        {
            var parameters = new ParametrosDialogo
            {
                { "text", Text ?? string.Empty }
            };
            RaiseRequestClose(new ResultadoDialogo(ResultadoBoton.OK, parameters));
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

            if (parameters.ContainsKey("defaultText"))
                Text = parameters.GetValue<string>("defaultText");

            if (parameters.ContainsKey(PARAMETRO_ACEPTA_CODIGO_BARRAS))
                AceptaCodigoBarras = parameters.GetValue<bool>(PARAMETRO_ACEPTA_CODIGO_BARRAS);
        }
    }
}
