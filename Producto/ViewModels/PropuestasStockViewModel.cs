using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nesto.Infrastructure.Contracts;
using Nesto.Modules.Producto.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace Nesto.Modules.Producto.ViewModels
{
    /// <summary>Una opción de un desplegable de filtro: el valor que va a la API (null = sin filtro) y lo que se ve.</summary>
    public class OpcionFiltroPropuestas
    {
        public OpcionFiltroPropuestas(string valor, string texto)
        {
            Valor = valor;
            Texto = texto;
        }

        public string Valor { get; }
        public string Texto { get; }
        public override string ToString() => Texto;
    }

    /// <summary>
    /// Nesto#529 (gemela de NestoAPI#631, corte 3 de la épica NestoAPI#628 de stock obsoleto): Compras decide cada propuesta
    /// mensual del motor de tendencias. Aplicar (el máximo y el mínimo se pueden cambiar; si cambian, motivo obligatorio),
    /// Rechazar (con motivo), Posponer, Silenciar (motivo de la lista de la API y fecha, como mucho 24 meses) o Liquidar
    /// (Outlet o Ganavisiones; la API pone el máximo a 0). Nada se aplica solo: las reglas son de la API y su mensaje se
    /// enseña tal cual. Solo la ven Compras y Dirección (la API contesta 403 al resto).
    /// <para>Sugerencia 644 (Manuel): columnas Familia y Grupo; filtros por familia, grupo, acción y texto (código o nombre),
    /// que filtra la API, y el contador de arriba cuenta solo lo filtrado (GET Resumen con los mismos filtros); y la
    /// «Consecuencia» de cada propuesta (qué pasa exactamente al aplicarla), que calcula la API.</para>
    /// </summary>
    public class PropuestasStockViewModel : ObservableObject, IReceptorNavegacion
    {
        private static readonly CultureInfo _es = CultureInfo.GetCultureInfo("es-ES");

        private readonly IServicioPropuestasStock _servicio;
        private readonly Func<DateTime> _hoy;
        private bool _motivosCargados;
        /// <summary>El mes del que están cargadas las opciones de familia, grupo y acción («» = el último guardado; null = sin cargar).</summary>
        private string _mesOpciones;
        /// <summary>Mientras se rellenan o se limpian los filtros, cambiar la selección no vuelve a leer.</summary>
        private bool _sinRecargar;

        internal const string TEXTO_TODAS = "(todas)";

        public PropuestasStockViewModel(IServicioPropuestasStock servicio) : this(servicio, null)
        {
        }

        internal PropuestasStockViewModel(IServicioPropuestasStock servicio, Func<DateTime> hoy)
        {
            _servicio = servicio ?? throw new ArgumentNullException(nameof(servicio));
            _hoy = hoy ?? (() => DateTime.Today);

            Meses = CrearMeses(_hoy());
            Estados = new List<OpcionFiltroPropuestas> { new OpcionFiltroPropuestas(null, "Todas") }
                .Concat(PropuestasStockConstantes.ESTADOS.Select(e => new OpcionFiltroPropuestas(e, NombreEstado(e))))
                .ToList();
            _mesSeleccionado = Meses[0];
            _estadoSeleccionado = Estados.First(e => e.Valor == PropuestasStockConstantes.ESTADO_PENDIENTE);

            Familias = new ObservableCollection<OpcionFiltroPropuestas> { Todas() };
            Grupos = new ObservableCollection<OpcionFiltroPropuestas> { Todas() };
            Acciones = new ObservableCollection<OpcionFiltroPropuestas> { Todas() };
            _familiaSeleccionada = Familias[0];
            _grupoSeleccionado = Grupos[0];
            _accionSeleccionada = Acciones[0];

            CargarCommand = new AsyncRelayCommand(CargarAsync, () => !EstaOcupado);
            LimpiarFiltrosCommand = new AsyncRelayCommand(LimpiarFiltrosAsync, () => !EstaOcupado);
            AplicarCommand = new AsyncRelayCommand(AplicarAsync, () => PuedeDecidir && !PropuestaSeleccionada.EsLiquidar);
            RechazarCommand = new AsyncRelayCommand(RechazarAsync, () => PuedeDecidir);
            PosponerCommand = new AsyncRelayCommand(PosponerAsync, () => PuedeDecidir);
            SilenciarCommand = new AsyncRelayCommand(SilenciarAsync, () => PuedeDecidir);
            LiquidarCommand = new AsyncRelayCommand(LiquidarAsync, () => PuedeDecidir && PropuestaSeleccionada.EsLiquidar);
        }

        public string Titulo => "Propuestas de stock";

        public IReadOnlyList<OpcionFiltroPropuestas> Meses { get; }
        public IReadOnlyList<OpcionFiltroPropuestas> Estados { get; }
        public IReadOnlyList<string> CanalesLiquidacion => PropuestasStockConstantes.CANALES_LIQUIDACION;

        private List<string> _motivosSilencio = PropuestasStockConstantes.MOTIVOS_SILENCIO_RESERVA.ToList();
        /// <summary>Los de la API (GET api/PropuestasStock/MotivosSilencio); mientras no los dé, los de reserva.</summary>
        public List<string> MotivosSilencio { get => _motivosSilencio; private set => SetProperty(ref _motivosSilencio, value); }

        private OpcionFiltroPropuestas _mesSeleccionado;
        public OpcionFiltroPropuestas MesSeleccionado
        {
            get => _mesSeleccionado;
            set
            {
                if (SetProperty(ref _mesSeleccionado, value) && !EstaOcupado)
                {
                    _ = CargarAsync();
                }
            }
        }

        private OpcionFiltroPropuestas _estadoSeleccionado;
        public OpcionFiltroPropuestas EstadoSeleccionado
        {
            get => _estadoSeleccionado;
            set
            {
                if (SetProperty(ref _estadoSeleccionado, value) && !EstaOcupado)
                {
                    _ = CargarAsync();
                }
            }
        }

        /// <summary>Sugerencia 644: las familias, grupos y acciones del mes (GET api/PropuestasStock/Filtros), con «(todas)» delante.</summary>
        public ObservableCollection<OpcionFiltroPropuestas> Familias { get; }
        public ObservableCollection<OpcionFiltroPropuestas> Grupos { get; }
        public ObservableCollection<OpcionFiltroPropuestas> Acciones { get; }

        private OpcionFiltroPropuestas _familiaSeleccionada;
        public OpcionFiltroPropuestas FamiliaSeleccionada
        {
            get => _familiaSeleccionada;
            set => CambiarFiltro(ref _familiaSeleccionada, value);
        }

        private OpcionFiltroPropuestas _grupoSeleccionado;
        public OpcionFiltroPropuestas GrupoSeleccionado
        {
            get => _grupoSeleccionado;
            set => CambiarFiltro(ref _grupoSeleccionado, value);
        }

        private OpcionFiltroPropuestas _accionSeleccionada;
        public OpcionFiltroPropuestas AccionSeleccionada
        {
            get => _accionSeleccionada;
            set => CambiarFiltro(ref _accionSeleccionada, value);
        }

        private string _textoBusqueda;
        /// <summary>Parte del código o del nombre del producto. Se busca con Intro o «Actualizar» (no a cada tecla: cada lectura carga la BD).</summary>
        public string TextoBusqueda { get => _textoBusqueda; set => SetProperty(ref _textoBusqueda, value); }

        private void CambiarFiltro(ref OpcionFiltroPropuestas campo, OpcionFiltroPropuestas valor, [System.Runtime.CompilerServices.CallerMemberName] string propiedad = null)
        {
            if (SetProperty(ref campo, valor, propiedad) && !EstaOcupado && !_sinRecargar)
            {
                _ = CargarAsync();
            }
        }

        /// <summary>Los filtros que van a la API (además del mes y el estado).</summary>
        public FiltroPropuestasStock FiltroActual() => new FiltroPropuestasStock
        {
            Familia = FamiliaSeleccionada?.Valor,
            Grupo = GrupoSeleccionado?.Valor,
            Accion = AccionSeleccionada?.Valor,
            Texto = string.IsNullOrWhiteSpace(TextoBusqueda) ? null : TextoBusqueda.Trim()
        };

        public ObservableCollection<PropuestaStockModel> Propuestas { get; } = new ObservableCollection<PropuestaStockModel>();
        public ObservableCollection<ResumenEstadoPropuestasModel> Resumen { get; } = new ObservableCollection<ResumenEstadoPropuestasModel>();
        /// <summary>Los cambios que ha hecho la última decisión («ALG: máximo 555 → 364, mínimo 295 → 220»).</summary>
        public ObservableCollection<string> UltimosCambios { get; } = new ObservableCollection<string>();

        private string _textoResumen;
        /// <summary>La cabecera: el mes y cuántas hay en cada estado con su exceso valorado.</summary>
        public string TextoResumen { get => _textoResumen; private set => SetProperty(ref _textoResumen, value); }

        private PropuestaStockModel _propuestaSeleccionada;
        public PropuestaStockModel PropuestaSeleccionada
        {
            get => _propuestaSeleccionada;
            set
            {
                if (SetProperty(ref _propuestaSeleccionada, value))
                {
                    PrepararDecision(value);
                    OnPropertyChanged(nameof(HayPropuestaSeleccionada));
                    OnPropertyChanged(nameof(PuedeDecidir));
                    NotificarComandos();
                }
            }
        }

        public bool HayPropuestaSeleccionada => PropuestaSeleccionada != null;

        /// <summary>Hay una fila que se puede decidir y no se está esperando a la API.</summary>
        public bool PuedeDecidir => PropuestaSeleccionada != null && PropuestaSeleccionada.SePuedeDecidir && !EstaOcupado && !SinPermiso;

        private int? _stockMaximo;
        /// <summary>El máximo que se aplica (de entrada, el propuesto).</summary>
        public int? StockMaximo { get => _stockMaximo; set => SetProperty(ref _stockMaximo, value); }

        private int? _stockMinimo;
        public int? StockMinimo { get => _stockMinimo; set => SetProperty(ref _stockMinimo, value); }

        private string _motivo;
        /// <summary>Obligatorio al rechazar y al aplicar otro máximo o mínimo que el propuesto.</summary>
        public string Motivo { get => _motivo; set => SetProperty(ref _motivo, value); }

        private string _motivoSilencio;
        public string MotivoSilencio { get => _motivoSilencio; set => SetProperty(ref _motivoSilencio, value); }

        private DateTime? _silenciarHasta;
        public DateTime? SilenciarHasta { get => _silenciarHasta; set => SetProperty(ref _silenciarHasta, value); }

        private string _canalLiquidacion;
        public string CanalLiquidacion { get => _canalLiquidacion; set => SetProperty(ref _canalLiquidacion, value); }

        private bool _estaOcupado;
        public bool EstaOcupado
        {
            get => _estaOcupado;
            private set
            {
                if (SetProperty(ref _estaOcupado, value))
                {
                    OnPropertyChanged(nameof(PuedeDecidir));
                    CargarCommand.NotifyCanExecuteChanged();
                    LimpiarFiltrosCommand.NotifyCanExecuteChanged();
                    NotificarComandos();
                }
            }
        }

        private bool _sinPermiso;
        /// <summary>La API ha contestado 403: solo Compras y Dirección.</summary>
        public bool SinPermiso
        {
            get => _sinPermiso;
            private set
            {
                if (SetProperty(ref _sinPermiso, value))
                {
                    OnPropertyChanged(nameof(PuedeDecidir));
                    NotificarComandos();
                }
            }
        }

        private string _mensaje;
        public string Mensaje { get => _mensaje; set => SetProperty(ref _mensaje, value); }

        public IAsyncRelayCommand CargarCommand { get; }
        /// <summary>Sugerencia 644: quita la familia, el grupo, la acción y el texto (no el mes ni el estado) y vuelve a leer.</summary>
        public IAsyncRelayCommand LimpiarFiltrosCommand { get; }
        public IAsyncRelayCommand AplicarCommand { get; }
        public IAsyncRelayCommand RechazarCommand { get; }
        public IAsyncRelayCommand PosponerCommand { get; }
        public IAsyncRelayCommand SilenciarCommand { get; }
        public IAsyncRelayCommand LiquidarCommand { get; }

        /// <summary>Al llegar (también desde la campana con la pestaña ya abierta) se vuelve a leer.</summary>
        public void AlLlegar(ParametrosNavegacion parametros)
        {
            _ = CargarAsync();
        }

        public async Task CargarAsync()
        {
            if (EstaOcupado)
            {
                return;
            }
            Mensaje = null;
            UltimosCambios.Clear();
            await LeerAsync(null).ConfigureAwait(true);
        }

        private async Task LimpiarFiltrosAsync()
        {
            _sinRecargar = true;
            try
            {
                FamiliaSeleccionada = Familias[0];
                GrupoSeleccionado = Grupos[0];
                AccionSeleccionada = Acciones[0];
                TextoBusqueda = null;
            }
            finally
            {
                _sinRecargar = false;
            }
            await CargarAsync().ConfigureAwait(true);
        }

        /// <summary>
        /// Las opciones de familia, grupo y acción del mes (solo si ha cambiado de mes). Lo elegido se mantiene si sigue en la
        /// lista; si no, vuelve a «(todas)».
        /// </summary>
        private async Task CargarOpcionesFiltroAsync(string mes)
        {
            string clave = mes ?? string.Empty;
            if (_mesOpciones == clave)
            {
                return;
            }
            OpcionesFiltroPropuestasModel opciones = await _servicio.LeerOpcionesFiltro(mes).ConfigureAwait(true) ?? new OpcionesFiltroPropuestasModel();
            _sinRecargar = true;
            try
            {
                FamiliaSeleccionada = Rellenar(Familias, opciones.Familias, FamiliaSeleccionada);
                GrupoSeleccionado = Rellenar(Grupos, opciones.Grupos, GrupoSeleccionado);
                AccionSeleccionada = Rellenar(Acciones, opciones.Acciones, AccionSeleccionada);
            }
            finally
            {
                _sinRecargar = false;
            }
            _mesOpciones = clave;
        }

        private static OpcionFiltroPropuestas Todas() => new OpcionFiltroPropuestas(null, TEXTO_TODAS);

        private static OpcionFiltroPropuestas Rellenar(ObservableCollection<OpcionFiltroPropuestas> lista, IEnumerable<string> valores, OpcionFiltroPropuestas seleccionada)
        {
            string anterior = seleccionada?.Valor;
            lista.Clear();
            lista.Add(Todas());
            foreach (string valor in (valores ?? Enumerable.Empty<string>()).Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim()).Distinct())
            {
                lista.Add(new OpcionFiltroPropuestas(valor, valor));
            }
            return lista.FirstOrDefault(o => o.Valor == anterior) ?? lista[0];
        }

        /// <summary>Lee el resumen y la lista con los filtros; si se le pasa una fila, intenta dejarla seleccionada (o la siguiente).</summary>
        private async Task LeerAsync(int? seleccionarCercaDe)
        {
            EstaOcupado = true;
            try
            {
                if (!_motivosCargados)
                {
                    List<string> motivos = await _servicio.LeerMotivosSilencio().ConfigureAwait(true);
                    if (motivos != null && motivos.Count > 0)
                    {
                        MotivosSilencio = motivos;
                    }
                    _motivosCargados = true;
                }
                string mes = MesSeleccionado?.Valor;
                await CargarOpcionesFiltroAsync(mes).ConfigureAwait(true);
                FiltroPropuestasStock filtro = FiltroActual();
                List<ResumenEstadoPropuestasModel> resumen = await _servicio.LeerResumen(mes, filtro).ConfigureAwait(true);
                List<PropuestaStockModel> filas = await _servicio.LeerPropuestas(mes, EstadoSeleccionado?.Valor, PropuestasStockConstantes.TOP, filtro).ConfigureAwait(true);
                SinPermiso = false;

                Resumen.Clear();
                foreach (ResumenEstadoPropuestasModel r in resumen.OrderBy(r => OrdenEstado(r.Estado)))
                {
                    Resumen.Add(r);
                }
                TextoResumen = ComponerResumen(resumen, filtro.Descripcion());

                int indiceAnterior = seleccionarCercaDe.HasValue ? Propuestas.ToList().FindIndex(p => p.Id == seleccionarCercaDe.Value) : -1;
                Propuestas.Clear();
                foreach (PropuestaStockModel fila in filas)
                {
                    Propuestas.Add(fila);
                }
                PropuestaSeleccionada = Seleccion(seleccionarCercaDe, indiceAnterior);
                if (Propuestas.Count == 0 && string.IsNullOrEmpty(Mensaje))
                {
                    Mensaje = "No hay propuestas con estos filtros.";
                }
            }
            catch (PropuestasStockException ex)
            {
                SinPermiso = ex.SinPermiso;
                if (ex.SinPermiso)
                {
                    Propuestas.Clear();
                    Resumen.Clear();
                    TextoResumen = null;
                    PropuestaSeleccionada = null;
                }
                Mensaje = ex.Message;
            }
            catch (Exception ex)
            {
                Mensaje = "No se han podido leer las propuestas de stock: " + ex.Message;
            }
            finally
            {
                EstaOcupado = false;
            }
        }

        private PropuestaStockModel Seleccion(int? id, int indiceAnterior)
        {
            if (id.HasValue)
            {
                PropuestaStockModel misma = Propuestas.FirstOrDefault(p => p.Id == id.Value);
                if (misma != null)
                {
                    return misma;
                }
                if (indiceAnterior >= 0 && Propuestas.Count > 0)
                {
                    return Propuestas[Math.Min(indiceAnterior, Propuestas.Count - 1)];
                }
            }
            return Propuestas.FirstOrDefault();
        }

        private void PrepararDecision(PropuestaStockModel propuesta)
        {
            StockMaximo = propuesta?.StockMaximoPropuesto;
            StockMinimo = propuesta?.StockMinimoPropuesto;
            Motivo = null;
            MotivoSilencio = null;
            SilenciarHasta = _hoy().Date.AddMonths(PropuestasStockConstantes.MESES_SILENCIO_POR_DEFECTO);
            CanalLiquidacion = null;
        }

        /// <summary>¿Ha cambiado el máximo o el mínimo propuesto? Entonces hay que decir por qué.</summary>
        public bool ValoresCambiados => PropuestaSeleccionada != null && PropuestaSeleccionada.EsAjuste
            && (StockMaximo != PropuestaSeleccionada.StockMaximoPropuesto || StockMinimo != PropuestaSeleccionada.StockMinimoPropuesto);

        private Task AplicarAsync()
        {
            PropuestaStockModel propuesta = PropuestaSeleccionada;
            var peticion = new DecisionPropuestaStockPeticion { Decision = PropuestasStockConstantes.DECISION_APLICAR, Motivo = MotivoLimpio() };
            if (propuesta.EsAjuste)
            {
                if (!StockMaximo.HasValue || !StockMinimo.HasValue)
                {
                    Mensaje = "Indique el máximo y el mínimo que quiere aplicar.";
                    return Task.CompletedTask;
                }
                if (StockMaximo.Value < 0 || StockMinimo.Value < 0)
                {
                    Mensaje = "El máximo y el mínimo no pueden ser negativos.";
                    return Task.CompletedTask;
                }
                if (StockMinimo.Value > StockMaximo.Value)
                {
                    Mensaje = "El mínimo no puede ser mayor que el máximo.";
                    return Task.CompletedTask;
                }
                if (ValoresCambiados)
                {
                    if (peticion.Motivo == null)
                    {
                        Mensaje = $"Si no aplica el máximo y el mínimo propuestos ({propuesta.MaxMinPropuesto}), indique el motivo.";
                        return Task.CompletedTask;
                    }
                    peticion.StockMaximo = StockMaximo;
                    peticion.StockMinimo = StockMinimo;
                }
            }
            return DecidirAsync(propuesta, peticion);
        }

        private Task RechazarAsync()
        {
            string motivo = MotivoLimpio();
            if (motivo == null)
            {
                Mensaje = "Para rechazar una propuesta indique el motivo: sirve para ajustar las fórmulas.";
                return Task.CompletedTask;
            }
            return DecidirAsync(PropuestaSeleccionada, new DecisionPropuestaStockPeticion { Decision = PropuestasStockConstantes.DECISION_RECHAZAR, Motivo = motivo });
        }

        private Task PosponerAsync()
        {
            return DecidirAsync(PropuestaSeleccionada, new DecisionPropuestaStockPeticion { Decision = PropuestasStockConstantes.DECISION_POSPONER, Motivo = MotivoLimpio() });
        }

        private Task SilenciarAsync()
        {
            if (string.IsNullOrWhiteSpace(MotivoSilencio))
            {
                Mensaje = $"Para silenciar elija el motivo del silencio: {string.Join(", ", MotivosSilencio)}.";
                return Task.CompletedTask;
            }
            if (!SilenciarHasta.HasValue)
            {
                Mensaje = "Para silenciar indique hasta cuándo (la fecha de caducidad).";
                return Task.CompletedTask;
            }
            DateTime hoy = _hoy().Date;
            DateTime hasta = SilenciarHasta.Value.Date;
            if (hasta <= hoy)
            {
                Mensaje = "La fecha de caducidad del silencio tiene que ser posterior a hoy.";
                return Task.CompletedTask;
            }
            if (hasta > hoy.AddMonths(PropuestasStockConstantes.MESES_MAXIMOS_SILENCIO))
            {
                Mensaje = $"Un silencio no puede durar más de {PropuestasStockConstantes.MESES_MAXIMOS_SILENCIO} meses.";
                return Task.CompletedTask;
            }
            return DecidirAsync(PropuestaSeleccionada, new DecisionPropuestaStockPeticion
            {
                Decision = PropuestasStockConstantes.DECISION_SILENCIAR,
                Motivo = MotivoLimpio(),
                MotivoSilencio = MotivoSilencio.Trim(),
                SilenciarHasta = hasta
            });
        }

        private Task LiquidarAsync()
        {
            if (string.IsNullOrWhiteSpace(CanalLiquidacion))
            {
                Mensaje = $"Elija dónde se liquida: {string.Join(" o ", PropuestasStockConstantes.CANALES_LIQUIDACION)}.";
                return Task.CompletedTask;
            }
            return DecidirAsync(PropuestaSeleccionada, new DecisionPropuestaStockPeticion
            {
                Decision = PropuestasStockConstantes.DECISION_APLICAR,
                Motivo = MotivoLimpio(),
                CanalLiquidacion = CanalLiquidacion.Trim()
            });
        }

        private async Task DecidirAsync(PropuestaStockModel propuesta, DecisionPropuestaStockPeticion peticion)
        {
            Mensaje = null;
            UltimosCambios.Clear();
            ResultadoDecisionPropuestaStock resultado;
            EstaOcupado = true;
            try
            {
                resultado = await _servicio.Decidir(propuesta.Id, peticion).ConfigureAwait(true);
            }
            catch (PropuestasStockException ex)
            {
                Mensaje = ex.Message;
                return;
            }
            catch (Exception ex)
            {
                Mensaje = $"No se ha podido guardar la decisión sobre el producto {propuesta.Producto?.Trim()}: {ex.Message}";
                return;
            }
            finally
            {
                EstaOcupado = false;
            }

            await LeerAsync(propuesta.Id).ConfigureAwait(true);
            // Después de leer: lo que se enseña es lo que ha hecho la decisión
            Mensaje = string.IsNullOrWhiteSpace(resultado?.Mensaje) ? $"Decisión guardada ({peticion.Decision})." : resultado.Mensaje;
            foreach (string cambio in resultado?.Cambios ?? Enumerable.Empty<string>())
            {
                UltimosCambios.Add(cambio);
            }
        }

        private string MotivoLimpio() => string.IsNullOrWhiteSpace(Motivo) ? null : Motivo.Trim();

        private void NotificarComandos()
        {
            AplicarCommand?.NotifyCanExecuteChanged();
            RechazarCommand?.NotifyCanExecuteChanged();
            PosponerCommand?.NotifyCanExecuteChanged();
            SilenciarCommand?.NotifyCanExecuteChanged();
            LiquidarCommand?.NotifyCanExecuteChanged();
        }

        /// <summary>El último mes calculado (por defecto) y los 24 anteriores al actual.</summary>
        internal static List<OpcionFiltroPropuestas> CrearMeses(DateTime hoy)
        {
            var meses = new List<OpcionFiltroPropuestas> { new OpcionFiltroPropuestas(null, "Último mes calculado") };
            DateTime mes = new DateTime(hoy.Year, hoy.Month, 1);
            for (int i = 0; i < 24; i++)
            {
                DateTime m = mes.AddMonths(-i);
                meses.Add(new OpcionFiltroPropuestas(m.ToString("yyyy-MM", CultureInfo.InvariantCulture), m.ToString("MMMM yyyy", _es)));
            }
            return meses;
        }

        internal static string NombreEstado(string estado)
            => estado == PropuestasStockConstantes.ESTADO_SIN_ACCION ? "Sin acción"
            : estado == PropuestasStockConstantes.ESTADO_EN_POTENCIACION ? "En potenciación"
            : estado;

        private static int OrdenEstado(string estado)
        {
            int indice = PropuestasStockConstantes.ESTADOS.ToList().IndexOf(estado);
            return indice < 0 ? int.MaxValue : indice;
        }

        /// <summary>La cabecera. Sugerencia 644: con <paramref name="filtros"/> (la descripción de los filtros), cuenta solo lo filtrado.</summary>
        internal static string ComponerResumen(IEnumerable<ResumenEstadoPropuestasModel> resumen, string filtros = null)
        {
            List<ResumenEstadoPropuestasModel> filas = resumen?.ToList() ?? new List<ResumenEstadoPropuestasModel>();
            if (filas.Count == 0)
            {
                return string.IsNullOrWhiteSpace(filtros)
                    ? "No hay propuestas guardadas de ese mes."
                    : $"No hay propuestas de ese mes con estos filtros ({filtros}).";
            }
            DateTime mes = filas.Max(f => f.Mes);
            string conFiltros = string.IsNullOrWhiteSpace(filtros) ? string.Empty : $" ({filtros})";
            string partes = string.Join(" · ", filas.OrderBy(f => OrdenEstado(f.Estado))
                .Select(f => $"{NombreEstado(f.Estado)}: {f.Propuestas} ({f.ExcesoValorado.ToString("C0", _es)})"));
            return $"Propuestas de {mes.ToString("MMMM yyyy", _es)}{conFiltros}: {filas.Sum(f => f.Propuestas)}. {partes}";
        }
    }
}
