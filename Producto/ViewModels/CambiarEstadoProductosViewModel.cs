using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Nesto.Modules.Producto.ViewModels
{
    /// <summary>Una fila de la vista previa: lo que dice la API, con los textos de la rejilla.</summary>
    public class FilaEstadoProducto
    {
        public FilaEstadoProducto(ProductoEstadoVistaPreviaDTO dto, Func<short?, string> textoEstado)
        {
            Dto = dto ?? new ProductoEstadoVistaPreviaDTO();
            textoEstado ??= e => e?.ToString() ?? "—";
            EstadoActualTexto = textoEstado(Dto.EstadoActual);
            EstadoNuevoTexto = textoEstado(Dto.EstadoNuevo);
        }

        public ProductoEstadoVistaPreviaDTO Dto { get; }
        public string Producto => Dto.Producto?.Trim();
        public string Nombre => Dto.Nombre?.Trim();
        public string EstadoActualTexto { get; }
        public string EstadoNuevoTexto { get; }
        public string CambioEstado => $"{EstadoActualTexto} → {EstadoNuevoTexto}";
        public int StockALG => Dto.StockALG;
        public int StockREI => Dto.StockREI;
        public int StockALC => Dto.StockALC;
        public int PendienteRecibir => Dto.PendienteRecibirProveedor;

        public int NumeroPedidosPendientes => Dto.PedidosPendientes?.Count ?? 0;
        public string DetallePedidosPendientes => NumeroPedidosPendientes == 0
            ? null
            : string.Join("\n", Dto.PedidosPendientes.Select(p => $"Pedido {p.Pedido} · cliente {p.Cliente?.Trim()} · {p.Cantidad} uds."));

        /// <summary>«ALG, REI»: los almacenes donde tiene controles de stock.</summary>
        public string Controles
        {
            get
            {
                var almacenes = new List<string>();
                if (Dto.TieneControlesStock?.ALG == true) almacenes.Add(Constantes.Almacenes.ALMACEN_ALGETE);
                if (Dto.TieneControlesStock?.REI == true) almacenes.Add(Constantes.Almacenes.ALMACEN_REINA);
                if (Dto.TieneControlesStock?.ALC == true) almacenes.Add(Constantes.Almacenes.ALMACEN_ALCOBENDAS);
                return string.Join(", ", almacenes);
            }
        }

        /// <summary>«ALG 4/12»: los controles que se crearán al volver a Disponible (mínimo/máximo).</summary>
        public string Sugeridos => Dto.ControlesSugeridos == null || Dto.ControlesSugeridos.Count == 0
            ? null
            : string.Join(", ", Dto.ControlesSugeridos.Select(c => $"{c.Almacen?.Trim()} {c.StockMinimo}/{c.StockMaximo}"));
        public string ExplicacionSugeridos => Dto.ControlesSugeridos == null || Dto.ControlesSugeridos.Count == 0
            ? null
            : string.Join("\n", Dto.ControlesSugeridos.Select(c => $"{c.Almacen?.Trim()}: mínimo {c.StockMinimo}, máximo {c.StockMaximo}. {c.Explicacion?.Trim()}"));

        public string Avisos => Dto.Avisos == null || Dto.Avisos.Count == 0 ? null : string.Join(" ", Dto.Avisos.Select(a => a?.Trim()));
        public string AvisosDetalle => Dto.Avisos == null || Dto.Avisos.Count == 0 ? null : string.Join("\n", Dto.Avisos.Select(a => "• " + a?.Trim()));

        public string MotivoNoSeCambia => string.IsNullOrWhiteSpace(Dto.MotivoNoSeCambia) ? null : Dto.MotivoNoSeCambia.Trim();
        /// <summary>Las que no se cambian salen en gris.</summary>
        public bool SeCambia => MotivoNoSeCambia == null;
    }

    /// <summary>
    /// Nesto#624 (NestoAPI#738, sugerencia 793 de Manuel): cambiar el estado de muchos productos a la vez. Filtro (proveedor,
    /// familia, grupo, subgrupo y/o lista de referencias) + estado nuevo → «Vista previa» (no cambia nada) → «Aplicar». Las
    /// reglas (permisos, controles de stock, avisos, controles sugeridos al volver a Disponible) las aplica la API; aquí solo se
    /// pinta, se pregunta UNA vez lo de los controles y se llama.
    /// </summary>
    public class CambiarEstadoProductosViewModel : ObservableObject, IReceptorNavegacion
    {
        public const string MENSAJE_SIN_FILTRO = "Indique un proveedor, una familia, un grupo, un subgrupo o una lista de productos.";
        public const string MENSAJE_SIN_ESTADO = "Indique el estado nuevo.";
        public const string MOTIVO_SIN_VISTA_PREVIA = "Haga primero la vista previa.";
        public const string MOTIVO_NINGUNO_CAMBIA = "Ninguno de los productos de la vista previa cambiaría de estado.";

        private static readonly char[] SEPARADORES_REFERENCIAS = { ' ', '\t', '\r', '\n', ',', ';' };

        private readonly IServicioEstadoProductos _servicio;
        private readonly IServicioDialogos _dialogos;
        private bool _estadosLeidos;
        private short _estadoDeLaVistaPrevia;

        public CambiarEstadoProductosViewModel(IServicioEstadoProductos servicio, IServicioDialogos dialogos)
        {
            _servicio = servicio;
            _dialogos = dialogos;
            AnnadirReferenciasCommand = new RelayCommand(() => AnnadirReferencias(ReferenciaNueva));
            QuitarReferenciaCommand = new RelayCommand(QuitarReferencia, () => ReferenciaSeleccionada != null);
            VaciarReferenciasCommand = new RelayCommand(VaciarReferencias, () => Referencias.Count > 0);
            VistaPreviaCommand = new AsyncRelayCommand(() => VistaPreviaAsync(), () => !EstaOcupado);
            AplicarCommand = new AsyncRelayCommand(AplicarAsync, () => PuedeAplicar);
        }

        public string Titulo => "Cambiar estado de productos";

        public ObservableCollection<EstadoProductoDTO> Estados { get; } = new ObservableCollection<EstadoProductoDTO>();

        private EstadoProductoDTO _estadoNuevo;
        public EstadoProductoDTO EstadoNuevo
        {
            get => _estadoNuevo;
            set
            {
                if (SetProperty(ref _estadoNuevo, value))
                {
                    InvalidarVistaPrevia();
                }
            }
        }

        private string _proveedor;
        public string Proveedor { get => _proveedor; set { if (SetProperty(ref _proveedor, value)) InvalidarVistaPrevia(); } }

        private string _familia;
        public string Familia { get => _familia; set { if (SetProperty(ref _familia, value)) InvalidarVistaPrevia(); } }

        private string _grupo;
        public string Grupo { get => _grupo; set { if (SetProperty(ref _grupo, value)) InvalidarVistaPrevia(); } }

        private string _subgrupo;
        public string Subgrupo { get => _subgrupo; set { if (SetProperty(ref _subgrupo, value)) InvalidarVistaPrevia(); } }

        // ------------------------------------------------------------------ la lista de referencias

        public ObservableCollection<string> Referencias { get; } = new ObservableCollection<string>();

        private string _referenciaNueva;
        /// <summary>Lo que se teclea o se pega (una o muchas, separadas por saltos de línea, espacios, comas o punto y coma).</summary>
        public string ReferenciaNueva { get => _referenciaNueva; set => SetProperty(ref _referenciaNueva, value); }

        private string _referenciaSeleccionada;
        public string ReferenciaSeleccionada
        {
            get => _referenciaSeleccionada;
            set
            {
                if (SetProperty(ref _referenciaSeleccionada, value))
                {
                    QuitarReferenciaCommand.NotifyCanExecuteChanged();
                }
            }
        }

        public IRelayCommand AnnadirReferenciasCommand { get; }
        public IRelayCommand QuitarReferenciaCommand { get; }
        public IRelayCommand VaciarReferenciasCommand { get; }

        public string TextoReferencias => Referencias.Count switch
        {
            0 => "Ninguna referencia",
            1 => "1 referencia",
            _ => $"{Referencias.Count} referencias"
        };

        /// <summary>Añade las referencias de <paramref name="texto"/> (las repetidas no) y deja la casilla vacía.</summary>
        public void AnnadirReferencias(string texto)
        {
            if (!string.IsNullOrWhiteSpace(texto))
            {
                bool annadida = false;
                foreach (string referencia in texto.Split(SEPARADORES_REFERENCIAS, StringSplitOptions.RemoveEmptyEntries).Select(r => r.Trim()))
                {
                    if (referencia.Length > 0 && !Referencias.Any(r => string.Equals(r, referencia, StringComparison.OrdinalIgnoreCase)))
                    {
                        Referencias.Add(referencia);
                        annadida = true;
                    }
                }
                if (annadida)
                {
                    ReferenciasCambiadas();
                }
            }
            ReferenciaNueva = null;
        }

        private void QuitarReferencia()
        {
            if (ReferenciaSeleccionada != null && Referencias.Remove(ReferenciaSeleccionada))
            {
                ReferenciaSeleccionada = null;
                ReferenciasCambiadas();
            }
        }

        private void VaciarReferencias()
        {
            Referencias.Clear();
            ReferenciasCambiadas();
        }

        private void ReferenciasCambiadas()
        {
            OnPropertyChanged(nameof(TextoReferencias));
            VaciarReferenciasCommand.NotifyCanExecuteChanged();
            InvalidarVistaPrevia();
        }

        // ------------------------------------------------------------------ vista previa y resultado

        public ObservableCollection<FilaEstadoProducto> Filas { get; } = new ObservableCollection<FilaEstadoProducto>();

        private VistaPreviaEstadoProductosDTO _vistaPrevia;
        public VistaPreviaEstadoProductosDTO VistaPrevia
        {
            get => _vistaPrevia;
            private set
            {
                if (SetProperty(ref _vistaPrevia, value))
                {
                    OnPropertyChanged(nameof(AvisosLote));
                    OnPropertyChanged(nameof(HayAvisosLote));
                    OnPropertyChanged(nameof(ResumenVistaPrevia));
                    ActualizarAplicar();
                }
            }
        }

        public string AvisosLote => VistaPrevia?.Avisos == null || VistaPrevia.Avisos.Count == 0
            ? null
            : string.Join("\n", VistaPrevia.Avisos.Where(a => !string.IsNullOrWhiteSpace(a)).Select(a => "• " + a.Trim()));
        public bool HayAvisosLote => AvisosLote != null;

        /// <summary>«12 productos: 10 cambiarán de estado y 2 no.»</summary>
        public string ResumenVistaPrevia
        {
            get
            {
                if (VistaPrevia == null)
                {
                    return null;
                }
                int total = Filas.Count;
                int cambian = Filas.Count(f => f.SeCambia);
                string productos = total == 1 ? "1 producto" : $"{total} productos";
                return total == 0
                    ? "Ningún producto cumple el filtro."
                    : cambian == total
                        ? $"{productos}: {(total == 1 ? "cambiará" : "cambiarán todos")} de estado."
                        : $"{productos}: {cambian} {(cambian == 1 ? "cambiará" : "cambiarán")} de estado y {total - cambian} no.";
            }
        }

        /// <summary>Si «Aplicar» se puede pulsar: hay vista previa, la API dice que el usuario puede y alguna fila cambia.</summary>
        public bool PuedeAplicar => !EstaOcupado && MotivoNoSePuedeAplicar == null;

        /// <summary>Por qué «Aplicar» está deshabilitado (null si no lo está).</summary>
        public string MotivoNoSePuedeAplicar
        {
            get
            {
                if (VistaPrevia == null)
                {
                    return MOTIVO_SIN_VISTA_PREVIA;
                }
                if (!VistaPrevia.PuedeCambiar)
                {
                    return ServicioEstadoProductos.MENSAJE_SIN_PERMISO;
                }
                return Filas.Any(f => f.SeCambia) ? null : MOTIVO_NINGUNO_CAMBIA;
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
                    VistaPreviaCommand.NotifyCanExecuteChanged();
                    ActualizarAplicar();
                }
            }
        }

        private string _mensaje;
        /// <summary>Lo que falta para la vista previa o el error de la API.</summary>
        public string Mensaje { get => _mensaje; private set => SetProperty(ref _mensaje, value); }

        private string _resultado;
        /// <summary>Lo que ha pasado al aplicar (cambiados, omitidos con su motivo, controles y avisos).</summary>
        public string Resultado { get => _resultado; private set => SetProperty(ref _resultado, value); }

        public IAsyncRelayCommand VistaPreviaCommand { get; }
        public IAsyncRelayCommand AplicarCommand { get; }

        private void ActualizarAplicar()
        {
            OnPropertyChanged(nameof(PuedeAplicar));
            OnPropertyChanged(nameof(MotivoNoSePuedeAplicar));
            AplicarCommand?.NotifyCanExecuteChanged();
        }

        /// <summary>Cambiar el filtro o el estado deja la vista previa vieja sin valor: se quita para no aplicar lo que no se ve.</summary>
        private void InvalidarVistaPrevia()
        {
            if (VistaPrevia == null && Filas.Count == 0)
            {
                return;
            }
            Filas.Clear();
            VistaPrevia = null;
        }

        public void AlLlegar(ParametrosNavegacion parametros)
        {
            _ = CargarEstadosAsync();
        }

        internal async Task CargarEstadosAsync()
        {
            if (_estadosLeidos || _servicio == null)
            {
                return;
            }
            _estadosLeidos = true;
            try
            {
                List<EstadoProductoDTO> estados = await _servicio.LeerEstados(Constantes.Empresas.EMPRESA_DEFECTO).ConfigureAwait(true);
                Estados.Clear();
                foreach (EstadoProductoDTO estado in (estados ?? new List<EstadoProductoDTO>()).OrderBy(e => e.Estado))
                {
                    Estados.Add(estado);
                }
            }
            catch (Exception ex)
            {
                _estadosLeidos = false;
                Mensaje = "No se han podido leer los estados de los productos: " + ex.Message;
            }
        }

        private string TextoEstado(short? estado)
        {
            if (estado == null)
            {
                return "—";
            }
            EstadoProductoDTO conocido = Estados.FirstOrDefault(e => e.Estado == estado.Value);
            return conocido?.Texto ?? estado.Value.ToString();
        }

        internal FiltroEstadoProductos Filtro() => new FiltroEstadoProductos
        {
            Proveedor = string.IsNullOrWhiteSpace(Proveedor) ? null : Proveedor.Trim(),
            Familia = string.IsNullOrWhiteSpace(Familia) ? null : Familia.Trim(),
            Grupo = string.IsNullOrWhiteSpace(Grupo) ? null : Grupo.Trim(),
            Subgrupo = string.IsNullOrWhiteSpace(Subgrupo) ? null : Subgrupo.Trim(),
            Productos = Referencias.Count == 0 ? null : Referencias.ToList()
        };

        internal async Task VistaPreviaAsync(bool conservarResultado = false)
        {
            if (EstaOcupado)
            {
                return;
            }
            // Lo tecleado en la casilla de referencias y sin Intro también cuenta
            if (!string.IsNullOrWhiteSpace(ReferenciaNueva))
            {
                AnnadirReferencias(ReferenciaNueva);
            }
            Mensaje = null;
            if (!conservarResultado)
            {
                Resultado = null;
            }
            FiltroEstadoProductos filtro = Filtro();
            if (filtro.Proveedor == null && filtro.Familia == null && filtro.Grupo == null && filtro.Subgrupo == null && filtro.Productos == null)
            {
                Mensaje = MENSAJE_SIN_FILTRO;
                return;
            }
            if (EstadoNuevo == null)
            {
                Mensaje = MENSAJE_SIN_ESTADO;
                return;
            }
            EstaOcupado = true;
            try
            {
                short estado = EstadoNuevo.Estado;
                VistaPreviaEstadoProductosDTO vista = await _servicio
                    .VistaPrevia(Constantes.Empresas.EMPRESA_DEFECTO, filtro, estado).ConfigureAwait(true);
                vista ??= new VistaPreviaEstadoProductosDTO();
                Filas.Clear();
                foreach (ProductoEstadoVistaPreviaDTO producto in vista.Productos ?? new List<ProductoEstadoVistaPreviaDTO>())
                {
                    Filas.Add(new FilaEstadoProducto(producto, TextoEstado));
                }
                _estadoDeLaVistaPrevia = estado;
                _vistaPrevia = null; // para que avise aunque llegue el mismo objeto
                VistaPrevia = vista;
            }
            catch (EstadoProductosException ex)
            {
                Mensaje = ex.Message;
            }
            catch (Exception ex)
            {
                Mensaje = "No se ha podido hacer la vista previa: " + ex.Message;
            }
            finally
            {
                EstaOcupado = false;
            }
        }

        internal async Task AplicarAsync()
        {
            if (!PuedeAplicar)
            {
                return;
            }
            List<string> productos = Filas.Where(f => f.SeCambia).Select(f => f.Producto).ToList();
            string estado = TextoEstado(_estadoDeLaVistaPrevia);
            string resumen = productos.Count == 1
                ? $"¿Cambia el estado de {productos[0]} a «{estado}»?"
                : $"¿Cambia el estado de {productos.Count} productos a «{estado}»?";
            DecisionCambioEstado decision = FlujoCambioEstadoProductos.Preguntar(_dialogos, VistaPrevia, VistaPrevia.Avisos, resumen, confirmarSiempre: false);
            if (decision == DecisionCambioEstado.Cancelar)
            {
                return;
            }

            Mensaje = null;
            EstaOcupado = true;
            bool aplicado = false;
            try
            {
                ResultadoCambioEstadoProductosDTO resultado = await _servicio.Cambiar(Constantes.Empresas.EMPRESA_DEFECTO, productos,
                    _estadoDeLaVistaPrevia, decision == DecisionCambioEstado.AplicarQuitandoControles).ConfigureAwait(true);
                Resultado = FlujoCambioEstadoProductos.TextoResultado(resultado);
                _dialogos?.ShowNotification(FlujoCambioEstadoProductos.TITULO, Resultado);
                aplicado = true;
            }
            catch (EstadoProductosException ex)
            {
                Mensaje = ex.Message;
                _dialogos?.ShowError(ex.Message);
            }
            catch (Exception ex)
            {
                Mensaje = "No se ha podido cambiar el estado: " + ex.Message;
                _dialogos?.ShowError(Mensaje);
            }
            finally
            {
                EstaOcupado = false;
            }
            if (aplicado)
            {
                // Se vuelve a pedir: así se ve cómo han quedado
                await VistaPreviaAsync(conservarResultado: true).ConfigureAwait(true);
            }
        }
    }
}
