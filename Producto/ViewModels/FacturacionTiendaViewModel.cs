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
    /// </summary>
    public class FacturacionTiendaViewModel : ObservableObject, IReceptorNavegacion
    {
        public const string OPCION_SU_TIENDA = "Su tienda";

        private readonly IServicioFacturacionTienda _servicio;
        private readonly IConfiguracion _configuracion;
        private readonly IServicioDialogos _dialogos;
        private readonly Func<DateTime> _ahora;
        private bool _tiendaLeida;

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
            GenerarCommand = new AsyncRelayCommand(GenerarAsync, PuedeGenerar);
            SemanaAnteriorCommand = new RelayCommand(PonerSemanaAnterior);
            PonerSemanaAnterior();
        }

        public string Titulo => "Facturado en la tienda";
        public string Ayuda => "Todo lo facturado con el almacén de la tienda en esas fechas (cursos incluidos), factura a factura y por " +
            "cliente, con su dirección, su comercial, la forma de venta y el importe. Se abre en Excel.";

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
                    GenerarCommand.NotifyCanExecuteChanged();
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
                    GenerarCommand.NotifyCanExecuteChanged();
                }
            }
        }

        private bool _estaOcupado;
        public bool EstaOcupado
        {
            get => _estaOcupado;
            private set
            {
                if (SetProperty(ref _estaOcupado, value))
                {
                    GenerarCommand.NotifyCanExecuteChanged();
                }
            }
        }

        private string _mensaje;
        public string Mensaje { get => _mensaje; private set => SetProperty(ref _mensaje, value); }

        /// <summary>Guarda el Excel en la carpeta temporal y lo abre (los tests lo sustituyen). Devuelve null o el error.</summary>
        internal Func<byte[], string, string> GuardarYAbrir { get; set; } = (excel, nombre) =>
            AbridorFicheros.GuardarYAbrir(excel, nombre, "xlsx", "el Excel", AbridorFicheros.AbrirConElVisorDelSistema);

        public IAsyncRelayCommand GenerarCommand { get; }
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

        private bool PuedeGenerar() => !EstaOcupado && Desde <= Hasta;

        internal async Task GenerarAsync()
        {
            if (!PuedeGenerar())
            {
                return;
            }
            Mensaje = null;
            EstaOcupado = true;
            try
            {
                string tienda = Tienda?.Almacen;
                byte[] excel = await _servicio.DescargarExcel(tienda, Desde, Hasta).ConfigureAwait(true);
                string nombre = $"FacturacionTienda_{tienda ?? "Tienda"}_{Desde:yyyyMMdd}_{Hasta:yyyyMMdd}";
                string error = GuardarYAbrir(excel, nombre);
                Mensaje = error ?? $"Excel abierto: del {Desde:dd/MM/yyyy} al {Hasta:dd/MM/yyyy}.";
                if (error != null)
                {
                    _dialogos?.ShowError(error);
                }
            }
            catch (FacturacionTiendaException ex)
            {
                Mensaje = ex.Message;
                _dialogos?.ShowError(ex.Message);
            }
            catch (Exception ex)
            {
                Mensaje = "No se ha podido sacar el Excel: " + ex.Message;
                _dialogos?.ShowError(Mensaje);
            }
            finally
            {
                EstaOcupado = false;
            }
        }
    }
}
