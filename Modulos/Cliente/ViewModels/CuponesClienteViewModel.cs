using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Models;
using Nesto.Infrastructure.Services;
using Nesto.Infrastructure.Shared;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Nesto.Modulos.Cliente
{
    /// <summary>Nesto#518: una fila de la pestaña «Cupones» de la ficha del cliente.</summary>
    public class CuponClienteFila
    {
        public CuponClienteFila(CuponDTO cupon)
        {
            Cupon = cupon ?? throw new ArgumentNullException(nameof(cupon));
        }

        public CuponDTO Cupon { get; }
        public string Codigo => Cupon.Codigo?.Trim();
        /// <summary>IVA incluido.</summary>
        public decimal Importe => Cupon.Importe;
        public decimal PedidoMinimo => Cupon.PedidoMinimo;
        public string Estado => Cupon.Estado?.Trim();
        /// <summary>«Evento: Masterclass Cloasma» o «Manual: motivo».</summary>
        public string Origen
        {
            get
            {
                if (string.Equals(Cupon.Origen?.Trim(), ReglasCupon.ORIGEN_MANUAL, StringComparison.OrdinalIgnoreCase))
                {
                    return string.IsNullOrWhiteSpace(Cupon.Motivo) ? "Manual" : $"Manual: {Cupon.Motivo.Trim()}";
                }
                return string.IsNullOrWhiteSpace(Cupon.Evento) ? "Evento" : $"Evento: {Cupon.Evento.Trim()}";
            }
        }
        public DateTime ValidoDesde => Cupon.ValidoDesde;
        public DateTime Caducidad => Cupon.Caducidad;
        /// <summary>Dónde se usó, desde cuándo vale, hasta cuándo...</summary>
        public string Situacion => ReglasCupon.Situacion(Cupon);
        public string FacturaOrigen => Cupon.FacturaOrigen?.Trim();
        public bool SePuedeAnular => ReglasCupon.SePuedeAnular(Cupon);
    }

    /// <summary>
    /// Nesto#518 (NestoAPI#613): los cupones del cliente en su ficha (todos: también los usados, anulados y caducados), con su
    /// estado, importe, validez y dónde se usaron. Dirección puede emitir un cupón manual (con motivo obligatorio) y
    /// Administración o Dirección anular uno que no se ha usado (devolución del curso). Las reglas y los permisos los
    /// comprueba la API; aquí solo se esconden los botones a quien no puede.
    /// </summary>
    public class CuponesClienteViewModel : ObservableObject
    {
        private readonly IServicioCupones _servicio;
        private readonly IServicioDialogos _dialogos;
        private readonly IConfiguracion _configuracion;
        private int _version;

        public CuponesClienteViewModel(IServicioCupones servicio, IServicioDialogos dialogos, IConfiguracion configuracion)
        {
            _servicio = servicio;
            _dialogos = dialogos;
            _configuracion = configuracion;
            RecargarCommand = new AsyncRelayCommand(RecargarAsync, () => HayCliente);
            EmitirManualCommand = new AsyncRelayCommand(EmitirManualAsync, () => PuedeEmitirManual && HayCliente);
            AnularCommand = new AsyncRelayCommand(AnularAsync, () => PuedeAnular && CuponSeleccionado?.SePuedeAnular == true);
        }

        public AsyncRelayCommand RecargarCommand { get; }
        public AsyncRelayCommand EmitirManualCommand { get; }
        public AsyncRelayCommand AnularCommand { get; }

        /// <summary>Solo Dirección emite cupones sin evento (la API devuelve 403 a los demás).</summary>
        public bool PuedeEmitirManual => _configuracion?.UsuarioEnGrupo(Constantes.GruposSeguridad.DIRECCION) == true;

        /// <summary>Anulan Administración y Dirección (la API devuelve 403 a los demás).</summary>
        public bool PuedeAnular => _configuracion != null && (_configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ADMINISTRACION)
            || _configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.DIRECCION));

        public string Empresa { get; private set; }
        public string Cliente { get; private set; }
        public string Contacto { get; private set; }
        public bool HayCliente => !string.IsNullOrWhiteSpace(Cliente);

        private ObservableCollection<CuponClienteFila> _cupones = new();
        public ObservableCollection<CuponClienteFila> Cupones
        {
            get => _cupones;
            private set => SetProperty(ref _cupones, value);
        }

        private CuponClienteFila _cuponSeleccionado;
        public CuponClienteFila CuponSeleccionado
        {
            get => _cuponSeleccionado;
            set
            {
                if (SetProperty(ref _cuponSeleccionado, value))
                {
                    AnularCommand.NotifyCanExecuteChanged();
                }
            }
        }

        private bool _estaOcupado;
        public bool EstaOcupado
        {
            get => _estaOcupado;
            private set => SetProperty(ref _estaOcupado, value);
        }

        private string _mensaje;
        /// <summary>«El cliente no tiene cupones.», «No se han podido leer…» o nada.</summary>
        public string Mensaje
        {
            get => _mensaje;
            private set
            {
                if (SetProperty(ref _mensaje, value))
                {
                    OnPropertyChanged(nameof(HayMensaje));
                }
            }
        }

        public bool HayMensaje => !string.IsNullOrWhiteSpace(Mensaje);

        /// <summary>
        /// Cambia el cliente de la ficha y carga sus cupones. Si mientras tanto se cambia otra vez de cliente, la respuesta
        /// del anterior se descarta.
        /// </summary>
        public async Task CargarAsync(string empresa, string cliente, string contacto)
        {
            Empresa = string.IsNullOrWhiteSpace(empresa) ? Constantes.Empresas.EMPRESA_DEFECTO : empresa.Trim();
            Cliente = cliente?.Trim();
            Contacto = contacto?.Trim();
            OnPropertyChanged(nameof(HayCliente));
            RecargarCommand.NotifyCanExecuteChanged();
            EmitirManualCommand.NotifyCanExecuteChanged();
            await RecargarAsync();
        }

        public async Task RecargarAsync()
        {
            int version = ++_version;
            CuponSeleccionado = null;
            if (!HayCliente || _servicio == null)
            {
                Cupones = new ObservableCollection<CuponClienteFila>();
                Mensaje = null;
                return;
            }
            EstaOcupado = true;
            try
            {
                List<CuponDTO> cupones = await _servicio.LeerDelCliente(Empresa, Cliente, true);
                if (version != _version)
                {
                    return;
                }
                Cupones = new ObservableCollection<CuponClienteFila>((cupones ?? new List<CuponDTO>())
                    .Where(c => c != null)
                    .OrderBy(c => OrdenEstado(c.Estado))
                    .ThenByDescending(c => c.FechaEmision)
                    .Select(c => new CuponClienteFila(c)));
                Mensaje = cupones == null
                    ? "No se han podido leer los cupones del cliente."
                    : Cupones.Count == 0 ? "El cliente no tiene cupones." : null;
            }
            finally
            {
                if (version == _version)
                {
                    EstaOcupado = false;
                }
            }
        }

        /// <summary>Primero los que se pueden usar, luego los que valdrán, y después el histórico.</summary>
        internal static int OrdenEstado(string estado)
        {
            switch (estado?.Trim())
            {
                case ReglasCupon.ESTADO_ACTIVO: return 0;
                case ReglasCupon.ESTADO_EMITIDO: return 1;
                case ReglasCupon.ESTADO_USADO: return 2;
                case ReglasCupon.ESTADO_CADUCADO: return 3;
                default: return 4;
            }
        }

        /// <summary>
        /// Cupón manual (solo Dirección): importe IVA incluido, motivo obligatorio y desde cuándo vale (hoy si no se dice). Las
        /// demás reglas son las de siempre: pedido mínimo el doble y caduca a los 6 meses.
        /// </summary>
        public async Task EmitirManualAsync()
        {
            if (!PuedeEmitirManual || !HayCliente)
            {
                return;
            }
            decimal? importe = _dialogos.GetAmount("Cupón manual", $"Importe del cupón para el cliente {Cliente}, con el IVA incluido:");
            if (importe == null)
            {
                return;
            }
            if (importe.Value <= 0)
            {
                _dialogos.ShowError("El importe del cupón tiene que ser mayor que cero.");
                return;
            }
            string motivo = _dialogos.GetText("Cupón manual", "Motivo del cupón (obligatorio):");
            if (motivo == null)
            {
                return;
            }
            if (string.IsNullOrWhiteSpace(motivo))
            {
                _dialogos.ShowError("El cupón manual necesita un motivo.");
                return;
            }
            DateTime? validoDesde = _dialogos.GetDate("Cupón manual", "¿Desde qué día vale el cupón? Si no lo sabe, vale desde hoy.", DateTime.Today);
            DateTime desde = (validoDesde ?? DateTime.Today).Date;
            string confirmacion = $"Se va a emitir un cupón de {ReglasCupon.Euros(importe.Value)} (IVA incluido) para el cliente {Cliente}, " +
                $"válido desde el {ReglasCupon.Fecha(desde)} y durante 6 meses, para un pedido de al menos {ReglasCupon.Euros(importe.Value * 2)} de productos.\n\n" +
                $"Motivo: {motivo.Trim()}\n\n¿Lo emite?";
            if (!_dialogos.ShowConfirmationAnswer("Cupón manual", confirmacion))
            {
                return;
            }
            EstaOcupado = true;
            try
            {
                CuponDTO cupon = await _servicio.CrearManual(new CrearCuponManualDTO
                {
                    Empresa = Empresa,
                    Cliente = Cliente,
                    Contacto = Contacto,
                    Importe = importe.Value,
                    Motivo = motivo.Trim(),
                    ValidoDesde = desde
                });
                _dialogos.ShowNotification("Cupón manual", $"Se ha emitido el cupón {cupon?.Codigo?.Trim()} de {ReglasCupon.Euros(importe.Value)}.");
            }
            catch (Exception ex)
            {
                _dialogos.ShowError(ex.Message);
            }
            finally
            {
                EstaOcupado = false;
            }
            await RecargarAsync();
        }

        /// <summary>Anula el cupón seleccionado si no se ha usado (devolución del curso). Si ya se usó, la API lo dice (409).</summary>
        public async Task AnularAsync()
        {
            CuponClienteFila fila = CuponSeleccionado;
            if (!PuedeAnular || fila == null || !fila.SePuedeAnular)
            {
                return;
            }
            string motivo = _dialogos.GetText("Anular cupón", $"Motivo para anular el cupón {fila.Codigo} de {ReglasCupon.Euros(fila.Importe)}:", "Devolución del curso.");
            if (motivo == null)
            {
                return;
            }
            if (!_dialogos.ShowConfirmationAnswer("Anular cupón", $"El cupón {fila.Codigo} ya no se podrá usar. ¿Lo anula?"))
            {
                return;
            }
            EstaOcupado = true;
            try
            {
                _ = await _servicio.Anular(fila.Codigo, motivo);
                _dialogos.ShowNotification("Anular cupón", $"El cupón {fila.Codigo} está anulado.");
            }
            catch (Exception ex)
            {
                _dialogos.ShowError(ex.Message);
            }
            finally
            {
                EstaOcupado = false;
            }
            await RecargarAsync();
        }
    }
}
