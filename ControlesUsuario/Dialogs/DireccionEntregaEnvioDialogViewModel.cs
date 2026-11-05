using CommunityToolkit.Mvvm.Input;
using Nesto.Infrastructure.Contracts;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace ControlesUsuario.Dialogs
{
    /// <summary>
    /// Nesto#625 (NestoAPI#726): «Cambiar dirección de entrega» de un envío que ya viaja (hoy solo CTT). Se abre con los datos
    /// del envío para corregirlos; el envío conserva su código y su reembolso. CTT exige teléfono y correo: sin ellos (o con un
    /// correo mal escrito) no se puede aceptar y <see cref="Aviso"/> dice qué falta. «Cambiar dirección» devuelve OK con todo
    /// en los parámetros; «Cancelar» (o cerrar), Cancel.
    /// </summary>
    public class DireccionEntregaEnvioDialogViewModel : DialogoNestoBase
    {
        public const string NOMBRE = "DireccionEntregaEnvioDialog";
        public const string PARAMETRO_ENVIO = "envio";
        public const string PARAMETRO_PEDIDO = "pedido";
        public const string PARAMETRO_NOMBRE = "nombre";
        public const string PARAMETRO_DIRECCION = "direccion";
        public const string PARAMETRO_COD_POSTAL = "codPostal";
        public const string PARAMETRO_POBLACION = "poblacion";
        public const string PARAMETRO_PAIS = "pais";
        public const string PARAMETRO_TELEFONO = "telefono";
        public const string PARAMETRO_CORREO = "correo";

        public const string PAIS_POR_DEFECTO = "ES";

        private static readonly Regex PATRON_CORREO = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

        private string _cabecera;
        private string _nombre;
        private string _direccion;
        private string _codPostal;
        private string _poblacion;
        private string _pais;
        private string _telefono;
        private string _correo;

        public DireccionEntregaEnvioDialogViewModel()
        {
            Title = "Cambiar dirección de entrega";
            AceptarCommand = new RelayCommand(Aceptar, PuedeAceptar);
            CancelarCommand = new RelayCommand(() => RaiseRequestClose(ResultadoBoton.Cancel));
        }

        public string Cabecera { get => _cabecera; set => SetProperty(ref _cabecera, value); }
        public string Destinatario { get => _nombre; set => Cambiar(ref _nombre, value); }
        public string Direccion { get => _direccion; set => Cambiar(ref _direccion, value); }
        public string CodPostal { get => _codPostal; set => Cambiar(ref _codPostal, value); }
        public string Poblacion { get => _poblacion; set => Cambiar(ref _poblacion, value); }
        public string Pais { get => _pais; set => Cambiar(ref _pais, value); }
        public string Telefono { get => _telefono; set => Cambiar(ref _telefono, value); }
        public string Correo { get => _correo; set => Cambiar(ref _correo, value); }

        public RelayCommand AceptarCommand { get; }
        public RelayCommand CancelarCommand { get; }

        /// <summary>Lo primero que falta para poder aceptar (de usted), o vacío si está todo.</summary>
        public string Aviso
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Destinatario)) return "Indique el nombre del destinatario.";
                if (string.IsNullOrWhiteSpace(Direccion)) return "Indique la dirección.";
                if (string.IsNullOrWhiteSpace(CodPostal)) return "Indique el código postal.";
                if (string.IsNullOrWhiteSpace(Poblacion)) return "Indique la población.";
                if (string.IsNullOrWhiteSpace(Pais)) return "Indique el país (código de dos letras, por ejemplo ES).";
                if (!TelefonoValido(Telefono)) return "Indique un teléfono de contacto (la agencia lo exige).";
                if (!CorreoValido(Correo)) return "Indique un correo electrónico válido (la agencia lo exige).";
                return string.Empty;
            }
        }

        public bool PuedeAceptar() => string.IsNullOrEmpty(Aviso);

        /// <summary>Al menos 9 cifras (se admiten espacios, guiones y el prefijo con +).</summary>
        public static bool TelefonoValido(string telefono)
            => !string.IsNullOrWhiteSpace(telefono) && telefono.Count(char.IsDigit) >= 9;

        public static bool CorreoValido(string correo)
            => !string.IsNullOrWhiteSpace(correo) && PATRON_CORREO.IsMatch(correo.Trim());

        private void Cambiar(ref string campo, string valor, [CallerMemberName] string propiedad = null)
        {
            if (SetProperty(ref campo, valor, propiedad))
            {
                OnPropertyChanged(nameof(Aviso));
                AceptarCommand?.NotifyCanExecuteChanged();
            }
        }

        private void Aceptar()
        {
            if (!PuedeAceptar())
            {
                return;
            }
            RaiseRequestClose(new ResultadoDialogo(ResultadoBoton.OK, new ParametrosDialogo
            {
                { PARAMETRO_NOMBRE, Limpio(Destinatario) },
                { PARAMETRO_DIRECCION, Limpio(Direccion) },
                { PARAMETRO_COD_POSTAL, Limpio(CodPostal) },
                { PARAMETRO_POBLACION, Limpio(Poblacion) },
                { PARAMETRO_PAIS, Limpio(Pais).ToUpperInvariant() },
                { PARAMETRO_TELEFONO, Limpio(Telefono) },
                { PARAMETRO_CORREO, Limpio(Correo) }
            }));
        }

        public override void OnDialogOpened(ParametrosDialogo parameters)
        {
            int envio = parameters.GetValue<int>(PARAMETRO_ENVIO);
            string pedido = parameters.GetValue<string>(PARAMETRO_PEDIDO);
            Cabecera = string.IsNullOrWhiteSpace(pedido) ? $"Envío {envio}" : $"Pedido {pedido.Trim()} (envío {envio})";
            Title = $"Cambiar la dirección de entrega del envío {envio}";
            Destinatario = Limpio(parameters.GetValue<string>(PARAMETRO_NOMBRE));
            Direccion = Limpio(parameters.GetValue<string>(PARAMETRO_DIRECCION));
            CodPostal = Limpio(parameters.GetValue<string>(PARAMETRO_COD_POSTAL));
            Poblacion = Limpio(parameters.GetValue<string>(PARAMETRO_POBLACION));
            string pais = Limpio(parameters.GetValue<string>(PARAMETRO_PAIS));
            Pais = string.IsNullOrEmpty(pais) ? PAIS_POR_DEFECTO : pais;
            Telefono = Limpio(parameters.GetValue<string>(PARAMETRO_TELEFONO));
            Correo = Limpio(parameters.GetValue<string>(PARAMETRO_CORREO));
        }

        private static string Limpio(string texto) => texto?.Trim() ?? string.Empty;
    }
}
