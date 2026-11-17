using CommunityToolkit.Mvvm.Input;
using ControlesUsuario.Dialogs;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using System;
using System.Threading.Tasks;

namespace Nesto.Modulos.Cajas.ViewModels
{
    /// <summary>
    /// Nesto#645 (Novedades 1317 y 1296): el cobro en caja de la factura recién creada en la tienda. Es la pestaña «Cobros» de
    /// Cajas (el mismo <see cref="CajasViewModel"/> y el mismo control, <c>CobrosCajaView</c>) en un diálogo, abierta con el cliente y
    /// el efecto de esa factura ya marcado. Al contabilizar el cobro se cierra con OK (el detalle del pedido imprime entonces la
    /// factura, que ya sale cobrada); «Cerrar sin cobrar» la cierra con Cancel y la factura se imprime igual.
    /// </summary>
    public class CobroCajaDialogViewModel : DialogoNestoBase
    {
        public const string NOMBRE = CobroEnCajaFactura.DIALOGO;

        private string _empresa;
        private string _cliente;
        private string _contacto;
        private string _documento;
        private bool _preparado;

        public CobroCajaDialogViewModel(CajasViewModel caja)
        {
            Caja = caja ?? throw new ArgumentNullException(nameof(caja));
            Title = "Cobro en caja";
            Caja.CobroContabilizado += Caja_CobroContabilizado;
            LoadedCommand = new AsyncRelayCommand(PrepararAsync);
            CerrarCommand = new RelayCommand(() => RaiseRequestClose(ResultadoBoton.Cancel));
        }

        public CajasViewModel Caja { get; }

        /// <summary>Lo lanza la vista al cargarse (como Cajas: la caja necesita la lista de formas de pago del control).</summary>
        public IAsyncRelayCommand LoadedCommand { get; }

        public IRelayCommand CerrarCommand { get; }

        public string Documento => _documento;

        public override void OnDialogOpened(ParametrosDialogo parameters)
        {
            _empresa = parameters.GetValue<string>(CobroEnCajaFactura.PARAMETRO_EMPRESA);
            _cliente = parameters.GetValue<string>(CobroEnCajaFactura.PARAMETRO_CLIENTE);
            _contacto = parameters.GetValue<string>(CobroEnCajaFactura.PARAMETRO_CONTACTO);
            _documento = parameters.GetValue<string>(CobroEnCajaFactura.PARAMETRO_DOCUMENTO);
            Title = string.IsNullOrWhiteSpace(_documento) ? "Cobro en caja" : $"Cobro en caja de la factura {_documento.Trim()}";
            OnPropertyChanged(nameof(Documento));
        }

        /// <summary>Carga la caja y el cliente y marca el efecto de la factura (una sola vez).</summary>
        public async Task PrepararAsync()
        {
            if (_preparado)
            {
                return;
            }
            _preparado = true;
            await Caja.PrepararCobroAsync(_empresa, _cliente, _contacto, _documento);
        }

        private void Caja_CobroContabilizado(object sender, EventArgs e)
        {
            RaiseRequestClose(new ResultadoDialogo(ResultadoBoton.OK, new ParametrosDialogo { { CobroEnCajaFactura.PARAMETRO_DOCUMENTO, _documento } }));
        }

        public override void OnDialogClosed()
        {
            Caja.CobroContabilizado -= Caja_CobroContabilizado;
        }
    }
}
