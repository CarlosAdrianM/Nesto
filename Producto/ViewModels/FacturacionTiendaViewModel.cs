using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Models;
using Nesto.Infrastructure.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Nesto.Modules.Producto.ViewModels
{
    /// <summary>Una tienda que se puede elegir: el almacén que va a la API (null: la del usuario) y el texto que se enseña.</summary>
    public class OpcionTiendaFacturacion
    {
        public OpcionTiendaFacturacion(string almacen, string texto)
        {
            Almacen = almacen;
            Texto = texto;
        }

        public string Almacen { get; }
        public string Texto { get; }
    }

    /// <summary>
    /// Sugerencia 648 (Paloma, NestoAPI#658): el Excel con lo facturado en la tienda (cursos incluidos) para el «informe de cifras»
    /// de cada semana: por factura, cliente, dirección, comercial, forma de venta e importe, con los totales. Por defecto, la semana
    /// anterior de lunes a domingo. Dirección, Administración e Informática eligen la tienda; los demás sacan la suya (lo decide la
    /// API con su AlmacénPedidoVta, y su mensaje se enseña tal cual).
    /// El Excel NO se abre aquí: Nesto corre en el escritorio remoto, donde Office no tiene licencia (y no se quiere Excel consumiendo
    /// memoria en el servidor). La API lo manda por correo al usuario (a su CorreoDefecto, o al que escriba), que lo abre en su
    /// ordenador o en el móvil.
    /// </summary>
    public class FacturacionTiendaViewModel : ObservableObject, IReceptorNavegacion
    {
        public const string OPCION_SU_TIENDA = "Su tienda";
        public const string MENSAJE_SIN_CORREO = "No tiene puesto su correo en Nesto: escriba a qué correo se lo mandamos.";

        private readonly IServicioFacturacionTienda _servicio;
        private readonly IConfiguracion _configuracion;
        private readonly IServicioDialogos _dialogos;
        private readonly Func<DateTime> _ahora;
        private bool _tiendaLeida;
        private bool _correoLeido;

        public FacturacionTiendaViewModel(IServicioFacturacionTienda servicio, IConfiguracion configuracion, IServicioDialogos dialogos)
            : this(servicio, configuracion, dialogos, null)
        {
        }

        /// <param name="ahora">El reloj (los tests lo fijan); por defecto, DateTime.Now.</param>
        internal FacturacionTiendaViewModel(IServicioFacturacionTienda servicio, IConfiguracion configuracion, IServicioDialogos dialogos,
            Func<DateTime> ahora)
        {
            _servicio = servicio;
            _configuracion = configuracion;
            _dialogos = dialogos;
            _ahora = ahora ?? (() => DateTime.Now);
            PuedeElegirTienda = configuracion != null
                && (configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.DIRECCION)
                    || configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ADMINISTRACION)
                    || configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.INFORMATICA));
            Tiendas = PuedeElegirTienda
                ? new List<OpcionTiendaFacturacion>
                {
                    new OpcionTiendaFacturacion(Constantes.Almacenes.ALMACEN_REINA, "Reina (REI)"),
                    new OpcionTiendaFacturacion(Constantes.Almacenes.ALMACEN_ALCOBENDAS, "Alcobendas (ALC)")
                }
                : new List<OpcionTiendaFacturacion> { new OpcionTiendaFacturacion(null, OPCION_SU_TIENDA) };
            _tienda = Tiendas[0];
            MandarCommand = new AsyncRelayCommand(MandarAsync, PuedeMandar);
            SemanaAnteriorCommand = new RelayCommand(PonerSemanaAnterior);
            PonerSemanaAnterior();
        }

        public string Titulo => "Facturado en la tienda";
        public string Ayuda => "Todo lo facturado con el almacén de la tienda en esas fechas (cursos incluidos), factura a factura y por " +
            "cliente, con su dirección, su comercial, la forma de venta y el importe. Se lo mandamos en Excel a su correo, para que lo " +
            "abra en su ordenador o en el móvil.";

        /// <summary>Dirección, Administración e Informática eligen la tienda; los demás, la suya.</summary>
        public bool PuedeElegirTienda { get; }
        public IReadOnlyList<OpcionTiendaFacturacion> Tiendas { get; }

        private OpcionTiendaFacturacion _tienda;
        public OpcionTiendaFacturacion Tienda
        {
            get => _tienda;
            set => SetProperty(ref _tienda, value);
        }

        private DateTime _desde;
        public DateTime Desde
        {
            get => _desde;
            set
            {
                if (SetProperty(ref _desde, value.Date))
                {
                    MandarCommand.NotifyCanExecuteChanged();
                }
            }
        }

        private DateTime _hasta;
        public DateTime Hasta
        {
            get => _hasta;
            set
            {
                if (SetProperty(ref _hasta, value.Date))
                {
                    MandarCommand.NotifyCanExecuteChanged();
                }
            }
        }

        private string _correo;
        /// <summary>A qué correo se manda: empieza con el del usuario (su CorreoDefecto) y se puede cambiar. Vacío: el suyo.</summary>
        public string Correo
        {
            get => _correo;
            set => SetProperty(ref _correo, value);
        }

        private bool _estaOcupado;
        public bool EstaOcupado
        {
            get => _estaOcupado;
            private set
            {
                if (SetProperty(ref _estaOcupado, value))
                {
                    MandarCommand.NotifyCanExecuteChanged();
                }
            }
        }

        private string _mensaje;
        public string Mensaje { get => _mensaje; private set => SetProperty(ref _mensaje, value); }

        public IAsyncRelayCommand MandarCommand { get; }
        public IRelayCommand SemanaAnteriorCommand { get; }

        /// <summary>La última semana cerrada, de lunes a domingo, anterior a <paramref name="hoy"/> (un lunes da la semana pasada).</summary>
        public static (DateTime Desde, DateTime Hasta) SemanaAnterior(DateTime hoy)
        {
            int diasDesdeLunes = ((int)hoy.DayOfWeek + 6) % 7;
            DateTime lunesDeEstaSemana = hoy.Date.AddDays(-diasDesdeLunes);
            return (lunesDeEstaSemana.AddDays(-7), lunesDeEstaSemana.AddDays(-1));
        }

        private void PonerSemanaAnterior()
        {
            (DateTime desde, DateTime hasta) = SemanaAnterior(_ahora());
            Desde = desde;
            Hasta = hasta;
        }

        public void AlLlegar(ParametrosNavegacion parametros)
        {
            _ = LeerTiendaDelUsuarioAsync();
            _ = LeerCorreoDelUsuarioAsync();
        }

        /// <summary>Quien elige tienda empieza con la suya (su AlmacénPedidoVta) si es una de las tiendas.</summary>
        internal async Task LeerTiendaDelUsuarioAsync()
        {
            if (_tiendaLeida || !PuedeElegirTienda || _configuracion == null)
            {
                return;
            }
            _tiendaLeida = true;
            try
            {
                string almacen = (await _configuracion.leerParametro(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.AlmacenPedidoVta)
                    .ConfigureAwait(true))?.Trim();
                OpcionTiendaFacturacion suya = Tiendas.FirstOrDefault(t => string.Equals(t.Almacen, almacen, StringComparison.OrdinalIgnoreCase));
                if (suya != null)
                {
                    Tienda = suya;
                }
            }
            catch (Exception)
            {
                // Es solo la tienda que sale elegida: se queda la primera
            }
        }

        /// <summary>El correo al que va el Excel empieza siendo el del usuario (lo dice la API: su CorreoDefecto).</summary>
        internal async Task LeerCorreoDelUsuarioAsync()
        {
            if (_correoLeido || _servicio == null)
            {
                return;
            }
            _correoLeido = true;
            try
            {
                string correo = await _servicio.LeerCorreoUsuario().ConfigureAwait(true);
                if (string.IsNullOrWhiteSpace(Correo))
                {
                    Correo = correo;
                }
                if (string.IsNullOrWhiteSpace(Correo))
                {
                    Mensaje = MENSAJE_SIN_CORREO;
                }
            }
            catch (Exception)
            {
                // Es solo el correo que sale escrito: si se deja vacío, la API usa el suyo (o dice que no lo tiene)
            }
        }

        private bool PuedeMandar() => !EstaOcupado && Desde <= Hasta;

        internal async Task MandarAsync()
        {
            if (!PuedeMandar())
            {
                return;
            }
            Mensaje = null;
            EstaOcupado = true;
            try
            {
                string correo = string.IsNullOrWhiteSpace(Correo) ? null : Correo.Trim();
                ResultadoCorreoFacturacionTienda resultado = await _servicio
                    .MandarExcelPorCorreo(Tienda?.Almacen, Desde, Hasta, correo)
                    .ConfigureAwait(true);
                string destinatario = string.IsNullOrWhiteSpace(resultado?.Correo) ? correo : resultado.Correo.Trim();
                if (!string.IsNullOrWhiteSpace(destinatario))
                {
                    Correo = destinatario;
                }
                Mensaje = $"Se lo hemos mandado a {destinatario}: del {Desde:dd/MM/yyyy} al {Hasta:dd/MM/yyyy}" +
                    (resultado == null ? "." : $", {TextoFacturas(resultado.NumeroFacturas)} por {resultado.Total:N2} € (IVA incluido).");
                _dialogos?.ShowNotification("Facturado en la tienda", Mensaje);
            }
            catch (FacturacionTiendaException ex)
            {
                Mensaje = ex.Message;
                _dialogos?.ShowError(ex.Message);
            }
            catch (Exception ex)
            {
                Mensaje = "No se ha podido mandar el Excel: " + ex.Message;
                _dialogos?.ShowError(Mensaje);
            }
            finally
            {
                EstaOcupado = false;
            }
        }

        private static string TextoFacturas(int numero) => numero == 1 ? "1 factura" : $"{numero} facturas";
    }
}
