using CommunityToolkit.Mvvm.Input;
using Nesto.Infrastructure.Contracts;

namespace ControlesUsuario.Dialogs
{
    /// <summary>
    /// NestoAPI#699 (corte 2): «El cliente lo sigue queriendo: volver a enviar». Enseña la dirección y los teléfonos del envío
    /// devuelto para corregirlos y la casilla «Cobrar los portes del reenvío» (desmarcada: los portes nos los comemos, Carlos
    /// 23/10/26). «Volver a enviar» devuelve OK con todo en los parámetros; «Cancelar» (o cerrar), Cancel.
    /// </summary>
    public class ReenviarEnvioDialogViewModel : DialogoNestoBase
    {
        public const string NOMBRE = "ReenviarEnvioDialog";
        public const string PARAMETRO_ENVIO = "envio";
        public const string PARAMETRO_PEDIDO = "pedido";
        public const string PARAMETRO_NOMBRE = "nombre";
        public const string PARAMETRO_DIRECCION = "direccion";
        public const string PARAMETRO_COD_POSTAL = "codPostal";
        public const string PARAMETRO_POBLACION = "poblacion";
        public const string PARAMETRO_PROVINCIA = "provincia";
        public const string PARAMETRO_TELEFONO = "telefono";
        public const string PARAMETRO_MOVIL = "movil";
        public const string PARAMETRO_OBSERVACIONES = "observaciones";
        public const string PARAMETRO_INCLUIR_PORTES = "incluirPortes";

        private string _cabecera;
        private string _direccion;
        private string _codPostal;
        private string _poblacion;
        private string _provincia;
        private string _telefono;
        private string _movil;
        private string _observaciones;
        private bool _incluirPortes;

        public ReenviarEnvioDialogViewModel()
        {
            Title = "Volver a enviar";
            AceptarCommand = new RelayCommand(Aceptar, PuedeAceptar);
            CancelarCommand = new RelayCommand(() => RaiseRequestClose(ResultadoBoton.Cancel));
        }

        public string Cabecera { get => _cabecera; set => SetProperty(ref _cabecera, value); }

        public string Direccion
        {
            get => _direccion;
            set
            {
                if (SetProperty(ref _direccion, value))
                {
                    AceptarCommand.NotifyCanExecuteChanged();
                }
            }
        }

        public string CodPostal
        {
            get => _codPostal;
            set
            {
                if (SetProperty(ref _codPostal, value))
                {
                    AceptarCommand.NotifyCanExecuteChanged();
                }
            }
        }

        public string Poblacion { get => _poblacion; set => SetProperty(ref _poblacion, value); }
        public string Provincia { get => _provincia; set => SetProperty(ref _provincia, value); }
        public string Telefono { get => _telefono; set => SetProperty(ref _telefono, value); }
        public string Movil { get => _movil; set => SetProperty(ref _movil, value); }
        public string Observaciones { get => _observaciones; set => SetProperty(ref _observaciones, value); }

        /// <summary>Desmarcada por defecto: en el 90 % de los casos los portes del reenvío no se cobran.</summary>
        public bool IncluirPortes { get => _incluirPortes; set => SetProperty(ref _incluirPortes, value); }

        public RelayCommand AceptarCommand { get; }
        public RelayCommand CancelarCommand { get; }

        /// <summary>Sin dirección o sin código postal no se puede enviar.</summary>
        public bool PuedeAceptar() => !string.IsNullOrWhiteSpace(Direccion) && !string.IsNullOrWhiteSpace(CodPostal);

        private void Aceptar()
        {
            if (!PuedeAceptar())
            {
                return;
            }
            RaiseRequestClose(new ResultadoDialogo(ResultadoBoton.OK, new ParametrosDialogo
            {
                { PARAMETRO_DIRECCION, Limpio(Direccion) },
                { PARAMETRO_COD_POSTAL, Limpio(CodPostal) },
                { PARAMETRO_POBLACION, Limpio(Poblacion) },
                { PARAMETRO_PROVINCIA, Limpio(Provincia) },
                { PARAMETRO_TELEFONO, Limpio(Telefono) },
                { PARAMETRO_MOVIL, Limpio(Movil) },
                { PARAMETRO_OBSERVACIONES, Limpio(Observaciones) },
                { PARAMETRO_INCLUIR_PORTES, IncluirPortes }
            }));
        }

        public override void OnDialogOpened(ParametrosDialogo parameters)
        {
            int envio = parameters.GetValue<int>(PARAMETRO_ENVIO);
            string pedido = parameters.GetValue<string>(PARAMETRO_PEDIDO);
            string nombre = parameters.GetValue<string>(PARAMETRO_NOMBRE);
            string documento = string.IsNullOrWhiteSpace(pedido) ? $"Envío {envio}" : $"Pedido {pedido.Trim()} (envío {envio})";
            Cabecera = string.IsNullOrWhiteSpace(nombre) ? documento : $"{documento}: {nombre.Trim()}";
            Title = $"Volver a enviar el envío {envio}";
            Direccion = Limpio(parameters.GetValue<string>(PARAMETRO_DIRECCION));
            CodPostal = Limpio(parameters.GetValue<string>(PARAMETRO_COD_POSTAL));
            Poblacion = Limpio(parameters.GetValue<string>(PARAMETRO_POBLACION));
            Provincia = Limpio(parameters.GetValue<string>(PARAMETRO_PROVINCIA));
            Telefono = Limpio(parameters.GetValue<string>(PARAMETRO_TELEFONO));
            Movil = Limpio(parameters.GetValue<string>(PARAMETRO_MOVIL));
            Observaciones = Limpio(parameters.GetValue<string>(PARAMETRO_OBSERVACIONES));
            IncluirPortes = false;
        }

        private static string Limpio(string texto) => texto?.Trim() ?? string.Empty;
    }
}
