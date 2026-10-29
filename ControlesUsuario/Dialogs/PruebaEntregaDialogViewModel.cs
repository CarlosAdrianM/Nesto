using CommunityToolkit.Mvvm.Input;
using Nesto.Infrastructure.Contracts;
using System;
using System.Globalization;

namespace ControlesUsuario.Dialogs
{
    /// <summary>
    /// Nesto#571 (NestoAPI#739): la prueba de entrega de un envío del reparto propio, tal como la registró Ariadna: quién
    /// lo recogió, su DNI, cuándo, cómo se cobró el reembolso, quién lo registró y la firma (PNG). Solo lectura. La firma
    /// llega en bytes (<see cref="Firma"/>) y la vista la pinta: el ViewModel no conoce tipos de WPF.
    /// </summary>
    public class PruebaEntregaDialogViewModel : DialogoNestoBase
    {
        public const string NOMBRE = "PruebaEntregaDialog";
        public const string PARAMETRO_ENVIO = "envio";
        public const string PARAMETRO_PEDIDO = "pedido";
        public const string PARAMETRO_FECHA_HORA = "fechaHora";
        public const string PARAMETRO_RECEPTOR = "receptor";
        public const string PARAMETRO_DNI = "dni";
        public const string PARAMETRO_COBRO_REEMBOLSO = "cobroReembolso";
        public const string PARAMETRO_IMPORTE_COBRADO = "importeCobrado";
        public const string PARAMETRO_USUARIO = "usuario";
        public const string PARAMETRO_TIENE_FIRMA = "tieneFirma";
        public const string PARAMETRO_FIRMA = "firma";

        public const string COBRO_EFECTIVO = "Efectivo";
        public const string COBRO_TARJETA = "Tarjeta";
        public const string COBRO_NO_COBRADO = "NoCobrado";

        private static readonly CultureInfo Espanol = CultureInfo.GetCultureInfo("es-ES");

        private string _cabecera;
        private string _receptor;
        private string _dni;
        private string _fechaHora;
        private string _cobro;
        private string _usuario;
        private byte[] _firma;
        private string _textoSinFirma;

        public PruebaEntregaDialogViewModel()
        {
            Title = "Prueba de entrega";
            CerrarCommand = new RelayCommand(() => RaiseRequestClose(ResultadoBoton.OK));
        }

        public string Cabecera { get => _cabecera; set => SetProperty(ref _cabecera, value); }
        public string Receptor { get => _receptor; set => SetProperty(ref _receptor, value); }
        public string Dni { get => _dni; set => SetProperty(ref _dni, value); }
        public string FechaHora { get => _fechaHora; set => SetProperty(ref _fechaHora, value); }
        public string Cobro { get => _cobro; set => SetProperty(ref _cobro, value); }
        public string Usuario { get => _usuario; set => SetProperty(ref _usuario, value); }

        /// <summary>La firma en PNG; null si no hay (o no se ha podido leer).</summary>
        public byte[] Firma
        {
            get => _firma;
            set
            {
                if (SetProperty(ref _firma, value))
                {
                    OnPropertyChanged(nameof(HayFirma));
                }
            }
        }

        public bool HayFirma => Firma != null && Firma.Length > 0;

        /// <summary>Lo que se ve en el hueco de la firma cuando no hay imagen.</summary>
        public string TextoSinFirma { get => _textoSinFirma; set => SetProperty(ref _textoSinFirma, value); }

        public RelayCommand CerrarCommand { get; }

        public override void OnDialogOpened(ParametrosDialogo parameters)
        {
            int envio = parameters.GetValue<int>(PARAMETRO_ENVIO);
            string pedido = parameters.GetValue<string>(PARAMETRO_PEDIDO);
            Cabecera = string.IsNullOrWhiteSpace(pedido) ? $"Envío {envio}" : $"Envío {envio} (pedido {pedido.Trim()})";
            Title = $"Prueba de entrega del envío {envio}";
            Receptor = TextoOGuion(parameters.GetValue<string>(PARAMETRO_RECEPTOR));
            Dni = TextoOGuion(parameters.GetValue<string>(PARAMETRO_DNI));
            DateTime fecha = parameters.GetValue<DateTime>(PARAMETRO_FECHA_HORA);
            FechaHora = fecha == default ? "-" : fecha.ToString("dd/MM/yyyy 'a las' HH:mm", Espanol);
            Cobro = TextoCobro(parameters.GetValue<string>(PARAMETRO_COBRO_REEMBOLSO), parameters.GetValue<decimal?>(PARAMETRO_IMPORTE_COBRADO));
            Usuario = TextoOGuion(parameters.GetValue<string>(PARAMETRO_USUARIO));
            Firma = parameters.GetValue<byte[]>(PARAMETRO_FIRMA);
            bool tieneFirma = parameters.GetValue<bool>(PARAMETRO_TIENE_FIRMA);
            TextoSinFirma = tieneFirma
                ? "No se ha podido cargar la firma. Inténtelo de nuevo más tarde."
                : "Sin firma (se entregó con DNI).";
        }

        /// <summary>Cómo se cobró el reembolso, con el importe.</summary>
        public static string TextoCobro(string cobroReembolso, decimal? importeCobrado)
        {
            string importe = importeCobrado.HasValue ? importeCobrado.Value.ToString("C", Espanol) : null;
            switch (cobroReembolso?.Trim())
            {
                case null:
                case "":
                    return "Sin reembolso";
                case COBRO_EFECTIVO:
                    return importe == null ? "Cobrado en efectivo" : $"Cobrado en efectivo: {importe}";
                case COBRO_TARJETA:
                    return importe == null ? "Cobrado con tarjeta" : $"Cobrado con tarjeta: {importe}";
                case COBRO_NO_COBRADO:
                    return "No se cobró el reembolso";
                default:
                    return importe == null ? cobroReembolso.Trim() : $"{cobroReembolso.Trim()}: {importe}";
            }
        }

        private static string TextoOGuion(string texto) => string.IsNullOrWhiteSpace(texto) ? "-" : texto.Trim();
    }
}
