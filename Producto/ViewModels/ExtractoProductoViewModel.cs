using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nesto.Infrastructure.Contracts;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Nesto.Modules.Producto.ViewModels
{
    /// <summary>
    /// Sugerencia 1482 (Manuel, Compras; OK de Carlos 26/11/26): la ventana «Extracto del producto» (GET api/Productos/Extracto), aparte de
    /// la ficha como el extracto de cliente porque irá creciendo. Se abre desde el botón «Extracto» de la ficha (con el producto) o sola
    /// desde la cinta (se teclea la referencia). Filtro de almacén (vacío = todos) y fechas (por defecto los últimos 12 meses); del más nuevo
    /// al más antiguo; saldo inicial y final; «Cargar más» pide la página siguiente. Importe y Proveedor solo se ven si algún movimiento los
    /// trae (la API los manda a Compras, Dirección e Informática). Sin temporizadores. El 403 de la API, con su mensaje.
    /// </summary>
    public class ExtractoProductoViewModel : ObservableObject, IReceptorNavegacionPestanaNueva
    {
        public const string NOMBRE_VISTA = "ExtractoProductoView";
        public const string PARAMETRO_PRODUCTO = "producto";
        public const int TAMANO_PAGINA = 200;
        public const string EMPRESA = "1";

        private readonly IServicioExtractoProducto _servicio;
        private readonly Func<DateTime> _hoy;
        private int _paginaCargada;

        public ExtractoProductoViewModel(IServicioExtractoProducto servicio) : this(servicio, null)
        {
        }

        /// <summary>Para los tests: el día de hoy.</summary>
        internal ExtractoProductoViewModel(IServicioExtractoProducto servicio, Func<DateTime> hoy)
        {
            _servicio = servicio;
            _hoy = hoy ?? (() => DateTime.Today);
            Hasta = _hoy().Date;
            Desde = Hasta.Value.AddMonths(-12).AddDays(1);
            CargarCommand = new AsyncRelayCommand(CargarAsync, () => !EstaOcupado && !string.IsNullOrWhiteSpace(Producto));
            CargarMasCommand = new AsyncRelayCommand(CargarMasAsync, () => !EstaOcupado && HayMas);
        }

        public string Titulo => string.IsNullOrWhiteSpace(Nombre) ? "Extracto del producto" : $"Extracto de {Producto} {Nombre}";

        public IAsyncRelayCommand CargarCommand { get; }
        public IAsyncRelayCommand CargarMasCommand { get; }

        /// <summary>Vacío = todos.</summary>
        public IReadOnlyList<string> AlmacenesPropuestos { get; } = new[] { string.Empty, "ALG", "REI", "ALC" };

        private string _producto;
        public string Producto
        {
            get => _producto;
            set
            {
                if (SetProperty(ref _producto, string.IsNullOrWhiteSpace(value) ? null : value.Trim()))
                {
                    CargarCommand.NotifyCanExecuteChanged();
                }
            }
        }

        private string _almacen;
        public string Almacen { get => _almacen; set => SetProperty(ref _almacen, string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant()); }

        private bool _almacenFijadoPorLaApi;
        /// <summary>
        /// NestoAPI (Carlos 26/11/26): a quien solo es de Tiendas la API le devuelve SIEMPRE los movimientos de su almacén, pida el que pida.
        /// Cuando el almacén de la respuesta no es el que se pidió, el filtro se pone a ese y queda deshabilitado (no se puede cambiar).
        /// </summary>
        public bool AlmacenFijadoPorLaApi
        {
            get => _almacenFijadoPorLaApi;
            private set
            {
                if (SetProperty(ref _almacenFijadoPorLaApi, value))
                {
                    OnPropertyChanged(nameof(PuedeCambiarAlmacen));
                    OnPropertyChanged(nameof(TextoAlmacenFijado));
                }
            }
        }

        public bool PuedeCambiarAlmacen => !AlmacenFijadoPorLaApi;

        public string TextoAlmacenFijado => AlmacenFijadoPorLaApi ? $"Solo se muestran los movimientos de su almacén ({Almacen})." : null;

        private DateTime? _desde;
        public DateTime? Desde { get => _desde; set => SetProperty(ref _desde, value); }

        private DateTime? _hasta;
        public DateTime? Hasta { get => _hasta; set => SetProperty(ref _hasta, value); }

        public ObservableCollection<MovimientoExtractoProducto> Movimientos { get; } = new();

        private string _nombre;
        public string Nombre
        {
            get => _nombre;
            private set
            {
                if (SetProperty(ref _nombre, value))
                {
                    OnPropertyChanged(nameof(Titulo));
                }
            }
        }

        private decimal? _saldoInicial;
        public decimal? SaldoInicial { get => _saldoInicial; private set => SetProperty(ref _saldoInicial, value); }

        private decimal? _saldoFinal;
        public decimal? SaldoFinal { get => _saldoFinal; private set => SetProperty(ref _saldoFinal, value); }

        private int _total;
        public int Total
        {
            get => _total;
            private set
            {
                if (SetProperty(ref _total, value))
                {
                    NotificarPaginas();
                }
            }
        }

        public bool HayMas => Movimientos.Count < Total;

        /// <summary>«200 de 512 movimientos».</summary>
        public string TextoCuantos => Total == 0 ? null : $"{Movimientos.Count} de {Total} movimientos";

        /// <summary>Solo si algún movimiento trae el importe (coste).</summary>
        public bool MostrarImporte => Movimientos.Any(m => m.Importe.HasValue);
        /// <summary>Solo si algún movimiento trae el proveedor.</summary>
        public bool MostrarProveedor => Movimientos.Any(m => !string.IsNullOrWhiteSpace(m.Proveedor));

        private bool _estaOcupado;
        public bool EstaOcupado
        {
            get => _estaOcupado;
            private set
            {
                if (SetProperty(ref _estaOcupado, value))
                {
                    CargarCommand.NotifyCanExecuteChanged();
                    CargarMasCommand.NotifyCanExecuteChanged();
                }
            }
        }

        private string _mensaje;
        public string Mensaje { get => _mensaje; private set => SetProperty(ref _mensaje, value); }

        public void AlLlegar(ParametrosNavegacion parametros)
        {
            string producto = parametros?.GetValue<string>(PARAMETRO_PRODUCTO);
            if (!string.IsNullOrWhiteSpace(producto))
            {
                Producto = producto;
                _ = CargarAsync();
            }
        }

        /// <summary>Desde la primera página, con los filtros de ahora.</summary>
        internal async Task CargarAsync()
        {
            if (string.IsNullOrWhiteSpace(Producto))
            {
                return;
            }
            if (Desde.HasValue && Hasta.HasValue && Desde.Value.Date > Hasta.Value.Date)
            {
                Mensaje = "La fecha «desde» no puede ser posterior a «hasta».";
                return;
            }
            Movimientos.Clear();
            _paginaCargada = 0;
            SaldoInicial = null;
            SaldoFinal = null;
            Total = 0;
            Nombre = null;
            NotificarPaginas();
            await CargarPaginaAsync(1);
        }

        internal Task CargarMasAsync() => HayMas ? CargarPaginaAsync(_paginaCargada + 1) : Task.CompletedTask;

        private async Task CargarPaginaAsync(int pagina)
        {
            EstaOcupado = true;
            Mensaje = null;
            try
            {
                string almacenPedido = Almacen;
                ExtractoProducto extracto = await _servicio.LeerExtracto(EMPRESA, Producto, almacenPedido, Desde, Hasta, pagina, TAMANO_PAGINA);
                AplicarAlmacenDeLaRespuesta(almacenPedido, extracto?.Almacen);
                if (pagina == 1)
                {
                    Nombre = extracto?.Nombre?.Trim();
                    SaldoInicial = extracto?.SaldoInicial;
                    SaldoFinal = extracto?.SaldoFinal;
                }
                foreach (MovimientoExtractoProducto m in extracto?.Movimientos ?? new List<MovimientoExtractoProducto>())
                {
                    Movimientos.Add(m);
                }
                _paginaCargada = pagina;
                Total = extracto?.Total ?? Movimientos.Count;
                if (Total == 0)
                {
                    Mensaje = "No hay movimientos en esas fechas.";
                }
            }
            catch (Exception ex)
            {
                Mensaje = ex.Message; // el 403 de la API, de usted
            }
            finally
            {
                EstaOcupado = false;
                NotificarPaginas();
            }
        }

        /// <summary>Si la API devuelve un almacén concreto distinto del pedido (vacío u otro), manda el de la API y el filtro se bloquea.</summary>
        private void AplicarAlmacenDeLaRespuesta(string almacenPedido, string almacenRespuesta)
        {
            string devuelto = string.IsNullOrWhiteSpace(almacenRespuesta) ? null : almacenRespuesta.Trim().ToUpperInvariant();
            if (devuelto == null || string.Equals(devuelto, almacenPedido, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
            Almacen = devuelto;
            AlmacenFijadoPorLaApi = true;
            OnPropertyChanged(nameof(TextoAlmacenFijado));
        }

        private void NotificarPaginas()
        {
            OnPropertyChanged(nameof(HayMas));
            OnPropertyChanged(nameof(TextoCuantos));
            OnPropertyChanged(nameof(MostrarImporte));
            OnPropertyChanged(nameof(MostrarProveedor));
            CargarMasCommand?.NotifyCanExecuteChanged();
        }
    }
}
