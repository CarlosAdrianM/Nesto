using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Models;
using Nesto.Infrastructure.Services;
using Nesto.Infrastructure.Shared;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;

namespace Nesto.Modules.Producto.ViewModels
{
    /// <summary>Cómo va una línea al recibirla: lo leído frente a lo enviado.</summary>
    public enum TipoEstadoLineaRecibir
    {
        Bien,
        Faltan,
        Sobran,
        /// <summary>Se ha leído y no venía en la reposición.</summary>
        NoVenia
    }

    /// <summary>Una línea de la reposición: lo enviado y lo que se ha leído al recibirla.</summary>
    public class LineaRecibirReposicion : ObservableObject
    {
        public string Producto { get; set; }
        public string Descripcion { get; set; }
        /// <summary>El código de barras principal (el de la ficha).</summary>
        public string CodigoBarras { get; set; }
        /// <summary>NestoAPI#605: todos los códigos activos del producto. Vacía con una API anterior: solo el principal.</summary>
        public List<string> CodigosBarras { get; set; } = new List<string>();
        public bool SinCodigo { get; set; }
        public int Enviado { get; set; }
        /// <summary>Se ha leído y no venía en la reposición.</summary>
        public bool NoVenia { get; set; }

        private int _leido;
        /// <summary>Lo leído (o tecleado, en los productos sin código).</summary>
        public int Leido
        {
            get => _leido;
            set
            {
                int anterior = _leido;
                if (SetProperty(ref _leido, Math.Max(0, value)))
                {
                    OnPropertyChanged(nameof(Diferencia));
                    OnPropertyChanged(nameof(Estado));
                    OnPropertyChanged(nameof(TipoEstado));
                    LeidoCambiado?.Invoke(this, _leido - anterior);
                }
            }
        }

        /// <summary>
        /// Ariadna#51: lo leído ha cambiado (por el lector o tecleado en «Leído»), con cuánto ha cambiado: es el apunte que se
        /// guarda en el servidor.
        /// </summary>
        internal event Action<LineaRecibirReposicion, int> LeidoCambiado;

        public int Diferencia => Leido - Enviado;

        public string Estado => TipoEstado switch
        {
            TipoEstadoLineaRecibir.NoVenia => "No venía",
            TipoEstadoLineaRecibir.Bien => "Bien",
            TipoEstadoLineaRecibir.Faltan => $"Faltan {-Diferencia}",
            _ => $"Sobran {Diferencia}"
        };

        /// <summary>Sugerencia 543: la vista colorea la columna de estado por esto, no por el texto.</summary>
        public TipoEstadoLineaRecibir TipoEstado => NoVenia ? TipoEstadoLineaRecibir.NoVenia
            : Diferencia == 0 ? TipoEstadoLineaRecibir.Bien
            : Diferencia < 0 ? TipoEstadoLineaRecibir.Faltan
            : TipoEstadoLineaRecibir.Sobran;

        /// <summary>
        /// Sugerencia 545: la línea casa con lo buscado si su referencia o su descripción lo contienen, sin distinguir
        /// mayúsculas ni tildes. Con el cuadro vacío casan todas.
        /// </summary>
        internal bool CasaConFiltro(string filtro)
        {
            string buscado = Normalizar(filtro);
            return buscado.Length == 0
                || Normalizar(Producto).Contains(buscado)
                || Normalizar(Descripcion).Contains(buscado);
        }

        private static string Normalizar(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto))
            {
                return string.Empty;
            }
            var sinTildes = new StringBuilder();
            foreach (char c in texto.Trim().Normalize(NormalizationForm.FormD))
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                {
                    sinTildes.Append(c);
                }
            }
            return sinTildes.ToString().Normalize(NormalizationForm.FormC).ToUpperInvariant();
        }

        /// <summary>
        /// Lo leído es de esta línea si es su referencia, su código principal o (NestoAPI#605) cualquiera de sus códigos
        /// activos; los alternativos cuentan aunque no tenga principal, como en el servidor (CasadorEscaneos).
        /// </summary>
        internal bool TieneCodigo(string codigo)
            => (!SinCodigo && string.Equals(CodigoBarras?.Trim(), codigo, StringComparison.OrdinalIgnoreCase))
               || string.Equals(Producto?.Trim(), codigo, StringComparison.OrdinalIgnoreCase)
               || (CodigosBarras ?? new List<string>()).Any(c => string.Equals(c?.Trim(), codigo, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// NestoAPI#553: la tienda recibe la reposición que sale de Algete con la misma API que Ariadna. Se elige la
    /// reposición, se lee lo que llega (el lector suma una unidad; los productos sin código se teclean en la columna
    /// «Leído») y al terminar entra LO LEÍDO; el servidor informa de las diferencias a quien creó el traspaso.
    /// Ariadna#51 (corte 2, reposición 81119): lo leído ya no vive solo en esta pantalla. Cada lectura se guarda en el servidor
    /// (…/Lecturas), compartida con Ariadna y con otros Nesto: si se corta la API al terminar o se cierra la pantalla, no se
    /// pierde nada y se puede seguir o terminar desde cualquier equipo. Con una API anterior (404), como antes.
    /// </summary>
    public class RecibirReposicionViewModel : ObservableObject, ITabCloseConfirmation
    {
        private readonly IServicioRecepcionReposiciones _servicio;
        private readonly IServicioDialogos _dialogos;
        private readonly IConfiguracion _configuracion;
        private readonly ISonidosLector _sonidos;
        private readonly Action<string> _abrirFichero;
        private readonly IServicioNavegacion _navegacion;
        private readonly IServicioEtiquetasProducto _etiquetas;

        /// <summary>
        /// Uno por recepción: si la respuesta se pierde y se reintenta, no se recibe dos veces. Ariadna#51: el del servidor,
        /// compartido por todos los equipos; con una API anterior, uno nuevo al abrirla.
        /// </summary>
        private Guid _idRecepcion;

        // ---------------------------------------------------------------- Ariadna#51 (corte 2): lo leído, en el servidor

        public const string TEXTO_LECTURA_NO_GUARDADA = "No se ha podido guardar la lectura: compruebe la conexión.";

        /// <summary>Los avisos de SignalR del grupo de la recepción. Null: sin avisos (se recarga al abrirla y al volver).</summary>
        private readonly IGruposAlmacenTiempoReal _grupos;
        /// <summary>Los avisos de SignalR llegan en otro hilo: se recarga en el de la pantalla.</summary>
        private readonly SynchronizationContext _contextoUI;
        /// <summary>La reposición abierta guarda lo leído en el servidor (false con una API anterior: solo en la pantalla).</summary>
        private bool _lecturasEnServidor;
        /// <summary>El grupo de SignalR en el que se está (el de la reposición abierta). Null si ninguno.</summary>
        private string _grupoActual;
        /// <summary>Se está pintando lo que dice el servidor: esos cambios de «Leído» no son lecturas.</summary>
        private bool _pintando;
        /// <summary>Lo leído que falta por mandar; se manda en orden, de una vez lo que se haya juntado.</summary>
        private readonly List<(LecturaEnCursoRecepcion Lectura, Action Deshacer)> _cola = new List<(LecturaEnCursoRecepcion, Action)>();
        private bool _enviando;
        /// <summary>Ha llegado un aviso mientras se mandaba: al acabar, se recarga.</summary>
        private bool _recargarTrasEnviar;

        /// <summary>Lo que se está mandando al servidor (los tests lo esperan).</summary>
        internal Task EnvioEnCurso { get; private set; } = Task.CompletedTask;
        /// <summary>La recarga tras un aviso de SignalR (los tests la esperan).</summary>
        internal Task RecargaEnCurso { get; private set; } = Task.CompletedTask;

        /// <summary>La reposición abierta guarda lo leído en el servidor, compartido con los demás equipos.</summary>
        public bool LecturasCompartidas => _lecturasEnServidor;

        private string _textoOtrosEquipos;
        /// <summary>«También está recibiendo: Andre (Ariadna)». Null si no hay nadie más.</summary>
        public string TextoOtrosEquipos { get => _textoOtrosEquipos; private set => SetProperty(ref _textoOtrosEquipos, value); }

        public RecibirReposicionViewModel(IServicioRecepcionReposiciones servicio, IServicioDialogos dialogos, IConfiguracion configuracion,
            ISonidosLector sonidos, IServicioNavegacion navegacion, IServicioEtiquetasProducto etiquetas, IAvisosEnTiempoReal avisos)
            : this(servicio, dialogos, configuracion, sonidos, navegacion, null, etiquetas, avisos as IGruposAlmacenTiempoReal)
        {
        }

        /// <param name="abrirFichero">Abre el PDF descargado (los tests lo cambian). Null: con el visor del sistema.</param>
        /// <param name="etiquetas">Nesto#534: la etiqueta de código de barras de cada línea. Sin él, el botón no hace nada.</param>
        /// <param name="grupos">Ariadna#51: los avisos de SignalR de la recepción. Sin él, no llegan (se recarga al abrir y al volver).</param>
        internal RecibirReposicionViewModel(IServicioRecepcionReposiciones servicio, IServicioDialogos dialogos, IConfiguracion configuracion,
            ISonidosLector sonidos, IServicioNavegacion navegacion, Action<string> abrirFichero, IServicioEtiquetasProducto etiquetas = null,
            IGruposAlmacenTiempoReal grupos = null)
        {
            _grupos = grupos;
            _contextoUI = SynchronizationContext.Current;
            Lineas.CollectionChanged += AlCambiarLineas;
            _etiquetas = etiquetas;
            _servicio = servicio;
            _dialogos = dialogos;
            _configuracion = configuracion;
            _sonidos = sonidos;
            _abrirFichero = abrirFichero ?? AbridorFicheros.AbrirConElVisorDelSistema;
            _navegacion = navegacion;
            // Sugerencia 599 (Paloma): doble clic en la referencia abre la ficha del producto
            AbrirFichaProductoCommand = new RelayCommand<string>(AbrirFichaProducto);
            CargarCommand = new AsyncRelayCommand(CargarAsync);
            ElegirCommand = new AsyncRelayCommand<RecepcionPendiente>(ElegirAsync);
            LeerCommand = new RelayCommand<string>(Leer);
            LeerLecturaCommand = new RelayCommand(() =>
            {
                Leer(Lectura);
                Lectura = string.Empty;
            });
            TerminarCommand = new AsyncRelayCommand(TerminarAsync, () => PuedeTerminar && !EstaOcupado);
            RecibirLoLeidoCommand = new AsyncRelayCommand(RecibirLoLeidoAsync, () => PuedeRecibirEnParte && !EstaOcupado);
            LimpiarFiltroCommand = new RelayCommand(() => Filtro = null);
            ImprimirCommand = new AsyncRelayCommand(ImprimirAsync, () => Seleccionada != null && !EstaOcupado);
            ImprimirEtiquetaCommand = new AsyncRelayCommand<LineaRecibirReposicion>(ImprimirEtiquetaAsync,
                l => l != null && !string.IsNullOrWhiteSpace(l.Producto) && !EstaOcupado);
            // Sugerencia 545: la grid enseña las líneas filtradas; lo leído está en las líneas, así que filtrar no lo pierde
            var vista = new ListCollectionView(Lineas);
            vista.Filter = l => ((LineaRecibirReposicion)l).CasaConFiltro(Filtro);
            LineasVisibles = vista;
        }

        public string Titulo => "Recibir reposición";

        public string Empresa { get; set; } = Constantes.Empresas.EMPRESA_DEFECTO;

        private string _almacen;
        /// <summary>El almacén del usuario (AlmacénPedidoVta): el de destino de las reposiciones que puede recibir.</summary>
        public string Almacen { get => _almacen; private set => SetProperty(ref _almacen, value); }

        public ObservableCollection<RecepcionPendiente> Pendientes { get; } = new ObservableCollection<RecepcionPendiente>();
        public ObservableCollection<LineaRecibirReposicion> Lineas { get; } = new ObservableCollection<LineaRecibirReposicion>();

        /// <summary>Sugerencia 545: <see cref="Lineas"/> con el filtro del cuadro «Buscar». Es lo que enseña la grid.</summary>
        public ICollectionView LineasVisibles { get; }

        private string _filtro;
        /// <summary>
        /// Sugerencia 545: buscar en la lista por referencia o descripción, para encontrar lo que no entra por el lector y
        /// teclear la cantidad en «Leído». Vacío, la lista entera. El lector sigue leyendo en todas las líneas.
        /// </summary>
        public string Filtro
        {
            get => _filtro;
            set
            {
                if (SetProperty(ref _filtro, value))
                {
                    RefrescarFiltro();
                }
            }
        }

        private void RefrescarFiltro()
        {
            // Si se estaba tecleando en «Leído», lo tecleado ya está en la línea (PropertyChanged): se cierra la edición,
            // porque la vista no se deja refrescar a mitad
            if (LineasVisibles is IEditableCollectionView editable)
            {
                if (editable.IsEditingItem)
                {
                    editable.CommitEdit();
                }
                if (editable.IsAddingNew)
                {
                    editable.CancelNew();
                }
            }
            LineasVisibles.Refresh();
        }

        private RecepcionPendiente _seleccionada;
        public RecepcionPendiente Seleccionada
        {
            get => _seleccionada;
            private set
            {
                if (SetProperty(ref _seleccionada, value))
                {
                    ImprimirCommand.NotifyCanExecuteChanged();
                    RefrescarBultos();
                }
            }
        }

        /// <summary>La vista vuelve a poner el foco en el cuadro del lector tras cada lectura.</summary>
        public event Action PedirFocoEnLector;

        private RecepcionReposicion _recepcion;

        private bool _puedeTerminar;
        public bool PuedeTerminar
        {
            get => _puedeTerminar;
            private set
            {
                if (SetProperty(ref _puedeTerminar, value))
                {
                    TerminarCommand.NotifyCanExecuteChanged();
                    RefrescarRecibirEnParte();
                }
            }
        }

        private string _mensaje;
        public string Mensaje { get => _mensaje; set => SetProperty(ref _mensaje, value); }

        private bool _estaOcupado;
        public bool EstaOcupado
        {
            get => _estaOcupado;
            private set
            {
                if (SetProperty(ref _estaOcupado, value))
                {
                    TerminarCommand.NotifyCanExecuteChanged();
                    RecibirLoLeidoCommand.NotifyCanExecuteChanged();
                    ImprimirCommand.NotifyCanExecuteChanged();
                    ImprimirEtiquetaCommand.NotifyCanExecuteChanged();
                }
            }
        }

        /// <summary>
        /// Sugerencia 599 (Paloma): doble clic en la referencia de una línea abre la ficha del producto (en otra pestaña:
        /// esta se queda como estaba). Recibe la referencia; vacía, no hace nada.
        /// </summary>
        public IRelayCommand<string> AbrirFichaProductoCommand { get; }

        private void AbrirFichaProducto(string producto)
        {
            if (string.IsNullOrWhiteSpace(producto) || _navegacion == null)
            {
                return;
            }
            var parameters = new ParametrosNavegacion
            {
                { "numeroProductoParameter", producto.Trim() }
            };
            _navegacion.RequestNavigate("MainRegion", "ProductoView", parameters);
        }

        public IAsyncRelayCommand CargarCommand { get; }
        public IAsyncRelayCommand<RecepcionPendiente> ElegirCommand { get; }
        public IRelayCommand<string> LeerCommand { get; }

        private string _lectura;
        /// <summary>El cuadro del lector: Intro lo lee (<see cref="LeerLecturaCommand"/>) y lo vacía.</summary>
        public string Lectura { get => _lectura; set => SetProperty(ref _lectura, value); }

        public IRelayCommand LeerLecturaCommand { get; }
        /// <summary>Sugerencia 545: Esc en el cuadro «Buscar» vuelve a la lista entera.</summary>
        public IRelayCommand LimpiarFiltroCommand { get; }
        public IAsyncRelayCommand TerminarCommand { get; }
        /// <summary>Sugerencia 564: lo que llega en papel, para comprobarlo a mano.</summary>
        public IAsyncRelayCommand ImprimirCommand { get; }

        public async Task CargarAsync()
        {
            Mensaje = null;
            await Ocupado(async () =>
            {
                string almacen = (await _configuracion.leerParametro(Empresa, Parametros.Claves.AlmacenPedidoVta).ConfigureAwait(true))?.Trim();
                if (string.IsNullOrWhiteSpace(almacen))
                {
                    Mensaje = "Tu usuario no tiene almacén (parámetro AlmacénPedidoVta): pide a la oficina que te lo pongan.";
                    return;
                }
                Almacen = almacen.ToUpperInvariant();
                List<RecepcionPendiente> pendientes = await _servicio.LeerPendientes(Empresa, Almacen).ConfigureAwait(true);
                Pendientes.Clear();
                foreach (RecepcionPendiente pendiente in pendientes ?? new List<RecepcionPendiente>())
                {
                    Pendientes.Add(pendiente);
                }
                if (Pendientes.Count == 0)
                {
                    Mensaje = $"No hay ninguna reposición pendiente de recibir en {Almacen}.";
                }
                else
                {
                    // NestoAPI#673: la que lleva más de 2 días recibida solo en parte se señala nada más entrar
                    List<string> avisos = Pendientes.Where(p => p.AvisarRecibidaEnParte)
                        .Select(p => string.IsNullOrWhiteSpace(p.AvisoRecibidaEnParte)
                            ? $"La reposición {p.Documento?.Trim()} lleva más de 2 días recibida solo en parte: termine de recibirla."
                            : p.AvisoRecibidaEnParte.Trim())
                        .ToList();
                    Mensaje = avisos.Count == 0 ? null : string.Join(Environment.NewLine, avisos);
                }
            }).ConfigureAwait(true);
            // Incidencia 505: si solo hay una, se abre sola (antes había que hacer doble clic y no era evidente)
            if (Pendientes.Count == 1 && Seleccionada == null)
            {
                await ElegirAsync(Pendientes[0]).ConfigureAwait(true);
            }
        }

        private async Task ElegirAsync(RecepcionPendiente pendiente)
        {
            if (pendiente == null)
            {
                return;
            }
            // NestoAPI#673: el aviso de las recibidas en parte hace más de 2 días se queda a la vista al abrir una
            Mensaje = Pendientes.Any(p => p.AvisarRecibidaEnParte) ? Mensaje : null;
            // Ariadna#51: lo leído de la que estaba abierta tiene que llegar al servidor antes de cambiar
            await EsperarEnviosAsync().ConfigureAwait(true);
            await Ocupado(async () =>
            {
                RecepcionReposicion recepcion = await _servicio.LeerRecepcion(Empresa, Almacen, pendiente.Documento).ConfigureAwait(true);
                if (recepcion == null)
                {
                    Mensaje = $"La reposición {pendiente.Documento} ya no está pendiente.";
                    return;
                }
                // Ariadna#51: lo ya leído (en este u otro equipo) y el IdRecepcion compartido. Null con una API anterior (404)
                LecturasRecepcionReposicion enCurso = await _servicio.LeerLecturas(Empresa, Almacen, pendiente.Documento).ConfigureAwait(true);
                SalirDelGrupo();
                _recepcion = recepcion;
                _lecturasEnServidor = enCurso != null && enCurso.IdRecepcion != Guid.Empty;
                OnPropertyChanged(nameof(LecturasCompartidas));
                _idRecepcion = _lecturasEnServidor ? enCurso.IdRecepcion : Guid.NewGuid();
                TextoOtrosEquipos = null;
                Seleccionada = pendiente;
                Filtro = null;
                Lineas.Clear();
                CodigosBulto.Clear();
                foreach (LineaRecepcionReposicion linea in recepcion.Lineas ?? new List<LineaRecepcionReposicion>())
                {
                    Lineas.Add(new LineaRecibirReposicion
                    {
                        Producto = linea.Producto?.Trim(),
                        Descripcion = linea.Descripcion?.Trim(),
                        CodigoBarras = linea.CodigoBarras?.Trim(),
                        CodigosBarras = (linea.CodigosBarras ?? new List<string>())
                            .Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim()).ToList(),
                        SinCodigo = linea.SinCodigo,
                        Enviado = linea.Cantidad
                    });
                }
                if (_lecturasEnServidor)
                {
                    Pintar(enCurso);
                    EntrarEnGrupo(pendiente.Documento);
                }
                PuedeTerminar = recepcion.PuedeTerminar && recepcion.SeTerminaDesdeAqui;
                RefrescarRecibirEnParte();
                RefrescarBultos();
                if (!PuedeTerminar)
                {
                    Mensaje = $"Esta reposición se recibe en {Almacen}: con tu usuario puedes leerla, pero no puedes terminarla.";
                }
                else if (EsperandoEtiqueta)
                {
                    Mensaje = TextoLeaEtiqueta(DocumentoAbierto);
                }
            }).ConfigureAwait(true);
            if (EsperandoEtiqueta)
            {
                PedirFocoEnLector?.Invoke();
            }
        }

        // ---------------------------------------------------------------- Nesto#530 (NestoAPI#645): etiquetas de los bultos

        /// <summary>
        /// «R81010-2»: la etiqueta de un bulto (R, el traspaso, un separador y el número de bulto). NestoAPI#675: el separador
        /// puede ser cualquiera que no sea cifra ni letra; la pistola con teclado americano en un Windows español manda
        /// «R81029'1» (o «?», «_», «/»…) en vez de «R81029-1».
        /// </summary>
        private static readonly Regex _patronEtiqueta = new Regex(@"^R([0-9]+)[^\p{L}\p{N}]+([0-9]+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        internal static bool EsEtiquetaBulto(string codigo) => NormalizarEtiquetaBulto(codigo) != null;

        /// <summary>
        /// NestoAPI#675: la etiqueta leída, siempre como «R{traspaso}-{bulto}» («r81029'1 » → «R81029-1»), que es como la
        /// guarda y la busca la API. Null si lo leído no es una etiqueta de bulto (un código de producto, una referencia…).
        /// </summary>
        internal static string NormalizarEtiquetaBulto(string codigo)
        {
            if (string.IsNullOrWhiteSpace(codigo))
            {
                return null;
            }
            Match encontrado = _patronEtiqueta.Match(codigo.Trim());
            return encontrado.Success
                && int.TryParse(encontrado.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int traspaso)
                && int.TryParse(encontrado.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int bulto)
                && traspaso > 0 && bulto > 0
                ? string.Format(CultureInfo.InvariantCulture, "R{0}-{1}", traspaso, bulto)
                : null;
        }

        internal static string TextoLeaEtiqueta(string documento)
            => $"Lea la etiqueta del bulto (R{documento}-n) antes de leer los productos.";

        /// <summary>Las etiquetas de los bultos leídas de la reposición abierta: van todas al terminar.</summary>
        public ObservableCollection<string> CodigosBulto { get; } = new ObservableCollection<string>();

        private string DocumentoAbierto => Seleccionada?.Documento?.Trim();

        /// <summary>
        /// La reposición abierta exige leer la etiqueta de un bulto (las enviadas con bultos) y todavía no se ha leído ninguna:
        /// hasta entonces no se pueden leer productos.
        /// </summary>
        public bool EsperandoEtiqueta => Seleccionada != null && _recepcion?.ExigeEtiqueta == true && CodigosBulto.Count == 0;

        /// <summary>Se pueden leer (o teclear) productos: hay una reposición abierta y, si la exige, ya se ha leído la etiqueta.</summary>
        public bool PuedeLeerProductos => Seleccionada != null && !EsperandoEtiqueta;

        /// <summary>«Bultos leídos: 2 de 3 (R81010-1, R81010-3)». Null si no se ha leído ninguno.</summary>
        public string TextoBultos
        {
            get
            {
                if (CodigosBulto.Count == 0)
                {
                    return null;
                }
                int total = _recepcion?.Bultos ?? 0;
                string cuantos = total > 0 ? $"{CodigosBulto.Count} de {total}" : CodigosBulto.Count.ToString(CultureInfo.InvariantCulture);
                return $"Bultos leídos: {cuantos} ({string.Join(", ", CodigosBulto)})";
            }
        }

        /// <summary>La lectura de etiqueta en curso (los tests la esperan).</summary>
        internal Task LecturaEtiquetaEnCurso { get; private set; } = Task.CompletedTask;

        private void RefrescarBultos()
        {
            OnPropertyChanged(nameof(EsperandoEtiqueta));
            OnPropertyChanged(nameof(PuedeLeerProductos));
            OnPropertyChanged(nameof(TextoBultos));
        }

        /// <summary>
        /// Nesto#530: lo leído es la etiqueta de un bulto. Se comprueba en la API de qué reposición es: si no hay ninguna abierta
        /// se abre la suya; si hay otra abierta, no vale (cada bulto se recibe en su reposición).
        /// </summary>
        internal async Task LeerEtiquetaAsync(string codigo)
        {
            codigo = NormalizarEtiquetaBulto(codigo) ?? codigo.Trim().ToUpperInvariant();
            EtiquetaBultoReposicion etiqueta = null;
            Mensaje = null;
            await Ocupado(async () =>
            {
                etiqueta = await _servicio.LeerBulto(Empresa, codigo).ConfigureAwait(true);
            }).ConfigureAwait(true);
            if (etiqueta == null)
            {
                // No existe (404, con el texto de la API) o no ha contestado: Ocupado ya ha puesto el motivo
                LecturaFallida(Mensaje ?? $"No hay ningún bulto con la etiqueta {codigo}.");
                return;
            }
            string traspaso = etiqueta.Traspaso?.Trim();
            if (Seleccionada == null)
            {
                RecepcionPendiente suya = Pendientes.FirstOrDefault(p => string.Equals(p.Documento?.Trim(), traspaso, StringComparison.OrdinalIgnoreCase));
                if (suya == null)
                {
                    LecturaFallida($"La etiqueta {codigo} es de la reposición {traspaso}, que no está pendiente de recibir en {Almacen}.");
                    return;
                }
                await ElegirAsync(suya).ConfigureAwait(true);
                if (Seleccionada != suya)
                {
                    // No se ha podido abrir: ElegirAsync ya ha dicho por qué
                    _sonidos?.Error();
                    PedirFocoEnLector?.Invoke();
                    return;
                }
            }
            if (!string.Equals(traspaso, DocumentoAbierto, StringComparison.OrdinalIgnoreCase))
            {
                LecturaFallida($"Esa etiqueta es de la reposición {traspaso}, no de la {DocumentoAbierto}.");
                return;
            }
            string leida = string.IsNullOrWhiteSpace(etiqueta.Codigo) ? codigo : etiqueta.Codigo.Trim().ToUpperInvariant();
            string bulto = etiqueta.Total > 0 ? $"{etiqueta.Bulto} de {etiqueta.Total}" : etiqueta.Bulto.ToString(CultureInfo.InvariantCulture);
            if (CodigosBulto.Contains(leida))
            {
                LecturaFallida($"El bulto {bulto} ({leida}) ya estaba leído.");
                return;
            }
            CodigosBulto.Add(leida);
            RefrescarBultos();
            Mensaje = $"Bulto {bulto} de la reposición {DocumentoAbierto} leído. Lea los productos.";
            _sonidos?.Correcto();
            PedirFocoEnLector?.Invoke();
            if (_lecturasEnServidor)
            {
                // Ariadna#51: la etiqueta leída también se guarda en el servidor (la ven los demás equipos)
                Encolar(new LecturaEnCursoRecepcion { CodigoBulto = leida, Cantidad = 1 }, () =>
                {
                    _ = CodigosBulto.Remove(leida);
                    RefrescarBultos();
                });
            }
        }

        /// <summary>Lo que manda el lector (o se teclea): un código de barras o la referencia suma una unidad.</summary>
        private void Leer(string lectura)
        {
            string codigo = lectura?.Trim();
            if (string.IsNullOrEmpty(codigo))
            {
                return;
            }
            string etiqueta = NormalizarEtiquetaBulto(codigo);
            if (etiqueta != null)
            {
                // Nesto#530: la etiqueta de un bulto (también sin ninguna abierta: abre su reposición); NestoAPI#675: normalizada
                LecturaEtiquetaEnCurso = LeerEtiquetaAsync(etiqueta);
                return;
            }
            if (Seleccionada == null)
            {
                // Incidencia 505: antes lo leído desaparecía sin decir nada
                LecturaFallida("Primero abre una reposición de la lista (doble clic) y después lee los códigos.");
                return;
            }
            if (EsperandoEtiqueta)
            {
                LecturaFallida(TextoLeaEtiqueta(DocumentoAbierto));
                return;
            }
            List<LineaRecibirReposicion> candidatas = Lineas.Where(l => !l.NoVenia && l.TieneCodigo(codigo)).ToList();
            if (candidatas.Count > 1)
            {
                LecturaFallida(TextoCodigoCompartido(codigo));
                return;
            }
            // Nesto#538: lo que no venía y ya está apuntado (por su referencia o por un código ya leído) suma sin preguntar
            LineaRecibirReposicion linea = candidatas.FirstOrDefault() ?? Lineas.FirstOrDefault(l => l.NoVenia && l.TieneCodigo(codigo));
            if (linea != null)
            {
                SumarUnaUnidad(linea, codigo, null);
                return;
            }
            LecturaProductoEnCurso = LeerCodigoQueNoEstaAsync(codigo);
        }

        private static string TextoCodigoCompartido(string codigo)
            => $"El código {codigo} lo comparten varios productos: escriba la cantidad en la columna «Leído» del que es.";

        /// <summary>La búsqueda en la API del último código que no estaba en la reposición (los tests la esperan).</summary>
        internal Task LecturaProductoEnCurso { get; private set; } = Task.CompletedTask;

        /// <summary>
        /// Nesto#538: lo leído no es de ninguna línea. Antes se apuntaba «no venía» con el código leído como producto (un EAN) y
        /// la API lo rechazaba al terminar. Ahora se pregunta a la API de qué producto es: si es de uno de la reposición (un
        /// código que aquí no constaba), suma en su línea; si es de otro, se apunta aparte con su referencia y su nombre; si no
        /// es de ninguno, o de varios, o la API no contesta, se avisa y no se apunta nada.
        /// </summary>
        private async Task LeerCodigoQueNoEstaAsync(string codigo)
        {
            RecepcionPendiente abierta = Seleccionada;
            List<ProductoCodigoRecepcion> productos;
            try
            {
                productos = await _servicio.BuscarProductosPorCodigo(Empresa, codigo).ConfigureAwait(true);
            }
            catch (Exception)
            {
                // Sin red o la API falla: no se apunta nada que luego no pueda entrar
                if (Seleccionada == abierta)
                {
                    LecturaFallida($"No se ha podido comprobar de qué producto es el código {codigo}: no se ha apuntado. Vuelva a leerlo.");
                }
                return;
            }
            if (Seleccionada != abierta)
            {
                // Mientras se preguntaba, se ha terminado o cambiado de reposición: lo leído era de la otra
                return;
            }
            List<ProductoCodigoRecepcion> distintos = (productos ?? new List<ProductoCodigoRecepcion>())
                .Where(p => !string.IsNullOrWhiteSpace(p?.Producto))
                .GroupBy(p => p.Producto.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();
            if (distintos.Count == 0)
            {
                LecturaFallida($"El código {codigo} no es de ningún producto.");
                return;
            }
            if (distintos.Count > 1)
            {
                LecturaFallida(TextoCodigoCompartido(codigo));
                return;
            }
            string referencia = distintos[0].Producto.Trim();
            // Se busca otra vez: mientras se preguntaba, otra lectura del mismo producto ha podido apuntarlo
            LineaRecibirReposicion linea = Lineas.FirstOrDefault(l => !l.NoVenia && string.Equals(l.Producto?.Trim(), referencia, StringComparison.OrdinalIgnoreCase))
                ?? Lineas.FirstOrDefault(l => l.NoVenia && string.Equals(l.Producto?.Trim(), referencia, StringComparison.OrdinalIgnoreCase));
            if (linea != null && !linea.TieneCodigo(codigo))
            {
                // Las siguientes lecturas de este código ya no preguntan
                linea.CodigosBarras ??= new List<string>();
                linea.CodigosBarras.Add(codigo);
            }
            SumarUnaUnidad(linea, referencia, distintos[0].Nombre, codigo);
        }

        /// <summary>
        /// Sugerencia 545: el producto elegido en las sugerencias del cuadro «Lector» (se escribe el nombre y se elige)
        /// cuenta como una lectura de su referencia. Va por la referencia exacta, no por los códigos de barras.
        /// </summary>
        public void LeerProductoElegido(string producto, string nombre)
        {
            string referencia = producto?.Trim();
            Lectura = string.Empty;
            if (string.IsNullOrEmpty(referencia))
            {
                return;
            }
            if (Seleccionada == null)
            {
                LecturaFallida("Primero abre una reposición de la lista (doble clic) y después lee los códigos.");
                return;
            }
            if (EsperandoEtiqueta)
            {
                LecturaFallida(TextoLeaEtiqueta(DocumentoAbierto));
                return;
            }
            SumarUnaUnidad(Lineas.FirstOrDefault(l => !l.NoVenia && string.Equals(l.Producto?.Trim(), referencia, StringComparison.OrdinalIgnoreCase)),
                referencia, nombre);
        }

        /// <summary>
        /// Suma una unidad a la línea; sin línea, a la de lo que no venía de esa referencia (o a una nueva).
        /// </summary>
        /// <param name="producto">La referencia del producto (siempre una que existe: la API no admite otra cosa al terminar).</param>
        /// <param name="codigoLeido">Nesto#538: el código de barras leído, si no era la referencia; la línea nueva lo guarda.</param>
        private void SumarUnaUnidad(LineaRecibirReposicion linea, string producto, string nombre, string codigoLeido = null)
        {
            // Ariadna#51: lo leído se manda al servidor después de decir cómo ha ido (si no llega, se dice después)
            _juntando = true;
            try
            {
                SumarUnaUnidadAqui(linea, producto, nombre, codigoLeido);
            }
            finally
            {
                _juntando = false;
            }
            IniciarEnvio();
        }

        private void SumarUnaUnidadAqui(LineaRecibirReposicion linea, string producto, string nombre, string codigoLeido)
        {
            linea ??= Lineas.FirstOrDefault(l => l.NoVenia && string.Equals(l.Producto, producto, StringComparison.OrdinalIgnoreCase));
            if (linea == null)
            {
                // Entra lo leído, también lo que no venía (04/10): se apunta aparte y el servidor lo informa
                string descripcion = string.IsNullOrWhiteSpace(nombre)
                    ? "(no venía en la reposición)"
                    : $"{nombre.Trim()} (no venía en la reposición)";
                linea = new LineaRecibirReposicion { Producto = producto, Descripcion = descripcion, NoVenia = true };
                if (!string.IsNullOrWhiteSpace(codigoLeido) && !linea.TieneCodigo(codigoLeido))
                {
                    linea.CodigosBarras.Add(codigoLeido);
                }
                Lineas.Add(linea);
            }
            bool yaEstabaCompleta = !linea.NoVenia && linea.Leido >= linea.Enviado;
            linea.Leido++;
            if (linea.NoVenia)
            {
                LecturaFallida(string.IsNullOrWhiteSpace(codigoLeido) || string.Equals(codigoLeido, linea.Producto, StringComparison.OrdinalIgnoreCase)
                    ? $"El código {producto} no venía en esta reposición: se apunta aparte."
                    : $"El código {codigoLeido} es de {linea.Producto}, que no venía en esta reposición: se apunta aparte.");
            }
            else if (yaEstabaCompleta)
            {
                // Se suma igual (entra lo leído), pero suena: de ese producto ya estaba todo lo enviado
                LecturaFallida($"De {linea.Producto} ya estaba leído todo lo enviado ({linea.Enviado}): ahora sobran {linea.Diferencia}.");
            }
            else
            {
                Mensaje = null;
                _sonidos?.Correcto();
                PedirFocoEnLector?.Invoke();
            }
        }

        /// <summary>Lo leído no entra como se esperaba: se dice, suena el error y el cursor vuelve al lector.</summary>
        private void LecturaFallida(string mensaje)
        {
            Mensaje = mensaje;
            _sonidos?.Error();
            PedirFocoEnLector?.Invoke();
        }

        // ---------------------------------------------------------------- Ariadna#51 (corte 2): lo leído, en el servidor

        private void AlCambiarLineas(object sender, NotifyCollectionChangedEventArgs e)
        {
            foreach (LineaRecibirReposicion linea in e.OldItems?.OfType<LineaRecibirReposicion>() ?? Enumerable.Empty<LineaRecibirReposicion>())
            {
                linea.LeidoCambiado -= AlCambiarLeido;
            }
            foreach (LineaRecibirReposicion linea in e.NewItems?.OfType<LineaRecibirReposicion>() ?? Enumerable.Empty<LineaRecibirReposicion>())
            {
                linea.LeidoCambiado -= AlCambiarLeido;
                linea.LeidoCambiado += AlCambiarLeido;
            }
        }

        /// <summary>
        /// Lo leído de una línea ha cambiado en esta pantalla (el lector suma uno; en «Leído» se teclea lo que sea, también
        /// para quitar): ese cambio es el apunte que se guarda en el servidor. Lo que se pinta del servidor no es una lectura.
        /// </summary>
        private void AlCambiarLeido(LineaRecibirReposicion linea, int diferencia)
        {
            if (!_lecturasEnServidor || _pintando || diferencia == 0 || Seleccionada == null || string.IsNullOrWhiteSpace(linea?.Producto))
            {
                return;
            }
            Encolar(new LecturaEnCursoRecepcion { Producto = linea.Producto.Trim(), Cantidad = diferencia }, () =>
            {
                // No ha llegado al servidor: no cuenta
                ComoDelServidor(() =>
                {
                    linea.Leido -= diferencia;
                    if (linea.NoVenia && linea.Leido == 0)
                    {
                        _ = Lineas.Remove(linea);
                    }
                });
            });
        }

        /// <summary>
        /// Apunta lo leído para mandarlo al servidor. Se manda después de la lectura (lo que se lea mientras tanto va en el
        /// mismo envío) y en orden; si no llega, se deshace y se avisa.
        /// </summary>
        private void Encolar(LecturaEnCursoRecepcion lectura, Action deshacer)
        {
            lectura.IdCliente = Guid.NewGuid();
            lectura.Dispositivo = Environment.MachineName;
            lectura.Fecha = DateTime.Now;
            _cola.Add((lectura, deshacer));
            if (!_juntando)
            {
                IniciarEnvio();
            }
        }

        /// <summary>Se está apuntando una lectura (mensaje y sonido): lo leído se manda al acabar.</summary>
        private bool _juntando;

        private void IniciarEnvio()
        {
            if (!_enviando && _cola.Count > 0)
            {
                _enviando = true;
                EnvioEnCurso = EnviarColaAsync();
            }
        }

        private async Task EnviarColaAsync()
        {
            try
            {
                while (_cola.Count > 0)
                {
                    List<(LecturaEnCursoRecepcion Lectura, Action Deshacer)> lote = _cola.ToList();
                    _cola.Clear();
                    RecepcionPendiente abierta = Seleccionada;
                    LecturasRecepcionReposicion respuesta = null;
                    string fallo = null;
                    try
                    {
                        respuesta = await _servicio.GuardarLecturas(Empresa, Almacen, abierta?.Documento,
                            lote.Select(l => l.Lectura).ToList(), _grupos?.IdConexion).ConfigureAwait(true);
                    }
                    catch (RecepcionReposicionException ex)
                    {
                        fallo = $"No se ha podido guardar la lectura: {ex.Message}";
                    }
                    catch (Exception)
                    {
                        fallo = TEXTO_LECTURA_NO_GUARDADA;
                    }
                    if (Seleccionada != abierta || abierta == null)
                    {
                        // Se ha terminado o cerrado mientras tanto: lo de esta pantalla ya no está
                        continue;
                    }
                    if (respuesta == null || respuesta.IdRecepcion == Guid.Empty)
                    {
                        for (int i = lote.Count - 1; i >= 0; i--)
                        {
                            lote[i].Deshacer();
                        }
                        LecturaFallida(fallo ?? TEXTO_LECTURA_NO_GUARDADA);
                        continue;
                    }
                    if (_cola.Count == 0)
                    {
                        // Lo que hay en el servidor, con lo de los demás equipos (si queda algo por mandar, lo pinta su respuesta)
                        Pintar(respuesta);
                    }
                }
            }
            finally
            {
                _enviando = false;
            }
            if (_recargarTrasEnviar)
            {
                _recargarTrasEnviar = false;
                RecargaEnCurso = RecargarLecturasAsync();
            }
        }

        /// <summary>Espera a que llegue al servidor todo lo leído (antes de terminar o de cambiar de reposición).</summary>
        private async Task EsperarEnviosAsync()
        {
            while (_enviando)
            {
                await EnvioEnCurso.ConfigureAwait(true);
            }
        }

        private void ComoDelServidor(Action cambio)
        {
            bool antes = _pintando;
            _pintando = true;
            try
            {
                cambio();
            }
            finally
            {
                _pintando = antes;
            }
        }

        /// <summary>Pinta lo leído según el servidor (de todos los equipos): «Leído» de cada línea, bultos y quién más recibe.</summary>
        private void Pintar(LecturasRecepcionReposicion lecturas)
        {
            if (lecturas == null)
            {
                return;
            }
            ComoDelServidor(() =>
            {
                if (lecturas.IdRecepcion != Guid.Empty)
                {
                    _idRecepcion = lecturas.IdRecepcion;
                }
                Dictionary<string, int> leido = (lecturas.Lecturas ?? new List<LecturaRecepcionReposicion>())
                    .Where(l => !string.IsNullOrWhiteSpace(l?.Producto))
                    .GroupBy(l => l.Producto.Trim(), StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.Sum(l => l.Cantidad), StringComparer.OrdinalIgnoreCase);
                var pintados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                // Primero las que venían (un producto que venía nunca va a «no venía»)
                foreach (LineaRecibirReposicion linea in Lineas.Where(l => !l.NoVenia).Concat(Lineas.Where(l => l.NoVenia)).ToList())
                {
                    string producto = linea.Producto?.Trim() ?? string.Empty;
                    linea.Leido = pintados.Add(producto) && leido.TryGetValue(producto, out int cantidad) ? cantidad : 0;
                }
                foreach (KeyValuePair<string, int> otro in leido.Where(l => l.Value > 0 && !pintados.Contains(l.Key)))
                {
                    // Lo ha leído otro equipo y no venía en la reposición
                    Lineas.Add(new LineaRecibirReposicion
                    {
                        Producto = otro.Key,
                        Descripcion = "(no venía en la reposición)",
                        NoVenia = true,
                        Leido = otro.Value
                    });
                }
                List<string> bultos = (lecturas.CodigosBulto ?? new List<string>())
                    .Where(b => !string.IsNullOrWhiteSpace(b))
                    .Select(b => b.Trim().ToUpperInvariant())
                    .Distinct()
                    .ToList();
                if (!bultos.SequenceEqual(CodigosBulto))
                {
                    CodigosBulto.Clear();
                    foreach (string bulto in bultos)
                    {
                        CodigosBulto.Add(bulto);
                    }
                    RefrescarBultos();
                }
            });
            TextoOtrosEquipos = TextoEquipos(lecturas.Equipos);
        }

        /// <summary>«También está recibiendo: Andre (Ariadna), Pedro (PDA2)», sin este equipo. Null si no hay nadie más.</summary>
        internal string TextoEquipos(IEnumerable<EquipoRecepcionReposicion> equipos)
        {
            string yo = SinDominio(_configuracion?.usuario);
            List<string> otros = (equipos ?? Enumerable.Empty<EquipoRecepcionReposicion>())
                .Where(e => e != null && !string.IsNullOrWhiteSpace(e.Usuario))
                .Where(e => !(string.Equals(SinDominio(e.Usuario), yo, StringComparison.OrdinalIgnoreCase)
                              && string.Equals(e.Dispositivo?.Trim(), Environment.MachineName, StringComparison.OrdinalIgnoreCase)))
                .Select(e => string.IsNullOrWhiteSpace(e.Dispositivo) ? SinDominio(e.Usuario) : $"{SinDominio(e.Usuario)} ({e.Dispositivo.Trim()})")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            return otros.Count == 0 ? null : "También está recibiendo: " + string.Join(", ", otros);
        }

        private static string SinDominio(string usuario)
        {
            string limpio = usuario?.Trim() ?? string.Empty;
            int barra = limpio.LastIndexOf('\\');
            return barra >= 0 ? limpio.Substring(barra + 1) : limpio;
        }

        private void EntrarEnGrupo(string documento)
        {
            if (_grupos == null)
            {
                return;
            }
            _grupoActual = GruposAlmacen.Recepcion(ServicioRecepcionReposiciones.TIPO_REPOSICION, documento, Almacen);
            _grupos.GrupoHaCambiado -= AlCambiarGrupo;
            _grupos.GrupoHaCambiado += AlCambiarGrupo;
            _grupos.EntrarEnGrupo(_grupoActual);
        }

        private void SalirDelGrupo()
        {
            if (_grupos == null || _grupoActual == null)
            {
                return;
            }
            _grupos.GrupoHaCambiado -= AlCambiarGrupo;
            _grupos.SalirDeGrupo(_grupoActual);
            _grupoActual = null;
        }

        /// <summary>Otro equipo ha leído algo de la reposición abierta (llega en otro hilo): se recarga lo leído.</summary>
        private void AlCambiarGrupo(object sender, string grupo)
        {
            string esperado = _grupoActual;
            if (esperado == null || !string.Equals(grupo?.Trim(), esperado, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
            if (_contextoUI != null)
            {
                _contextoUI.Post(_ => RecargaEnCurso = RecargarLecturasAsync(), null);
            }
            else
            {
                RecargaEnCurso = RecargarLecturasAsync();
            }
        }

        /// <summary>
        /// Vuelve a pedir lo leído de la reposición abierta (tras un aviso de SignalR o al volver a la pantalla). Si se está
        /// mandando algo, se recarga al acabar; si no contesta, se deja como está (el aviso es un extra).
        /// </summary>
        public async Task RecargarLecturasAsync()
        {
            if (!_lecturasEnServidor || Seleccionada == null || EstaOcupado)
            {
                return;
            }
            if (_enviando)
            {
                _recargarTrasEnviar = true;
                return;
            }
            RecepcionPendiente abierta = Seleccionada;
            LecturasRecepcionReposicion lecturas;
            try
            {
                lecturas = await _servicio.LeerLecturas(Empresa, Almacen, abierta.Documento).ConfigureAwait(true);
            }
            catch (Exception)
            {
                return;
            }
            if (lecturas == null || lecturas.IdRecepcion == Guid.Empty || Seleccionada != abierta || EstaOcupado)
            {
                return;
            }
            if (_enviando)
            {
                _recargarTrasEnviar = true;
                return;
            }
            Pintar(lecturas);
        }

        /// <summary>
        /// Antes de terminar: lo leído aquí tiene que estar en el servidor y se pinta lo del servidor (también lo de los demás
        /// equipos), que es lo que se termina. False si no se ha podido comprobar (el motivo, en <see cref="Mensaje"/>).
        /// </summary>
        private async Task<bool> SincronizarAntesDeTerminarAsync(string boton)
        {
            if (!_lecturasEnServidor)
            {
                return true;
            }
            await EsperarEnviosAsync().ConfigureAwait(true);
            RecepcionPendiente abierta = Seleccionada;
            LecturasRecepcionReposicion lecturas = null;
            bool leidas = false;
            await Ocupado(async () =>
            {
                lecturas = await _servicio.LeerLecturas(Empresa, Almacen, abierta.Documento).ConfigureAwait(true);
                leidas = true;
            }).ConfigureAwait(true);
            if (!leidas)
            {
                Mensaje = $"{Mensaje} {TextoSigueEnElServidor(boton)}";
                return false;
            }
            if (Seleccionada != abierta)
            {
                return false;
            }
            Pintar(lecturas);
            return true;
        }

        internal static string TextoSigueEnElServidor(string boton)
            => $"Lo leído sigue guardado en el servidor: basta con volver a pulsar «{boton}» (también desde otro equipo).";

        /// <summary>Ariadna#51: al cerrar la pestaña se sale del grupo de SignalR (lo leído ya está en el servidor).</summary>
        public bool ConfirmTabClose()
        {
            SalirDelGrupo();
            return true;
        }

        /// <summary>
        /// Sugerencia 564 (Paloma): lo que llega con la reposición abierta en un PDF (lo genera la API) para comprobarlo a
        /// mano mientras la tienda no tiene Ariadna.
        /// </summary>
        internal async Task ImprimirAsync()
        {
            if (Seleccionada == null)
            {
                return;
            }
            string documento = Seleccionada.Documento;
            await Ocupado(async () =>
            {
                byte[] pdf = await _servicio.DescargarListadoPdf(Empresa, Almacen, documento).ConfigureAwait(true);
                string error = AbridorFicheros.GuardarYAbrir(pdf, $"Reposicion_{documento?.Trim()}", _abrirFichero);
                if (error != null)
                {
                    Mensaje = error;
                }
            }).ConfigureAwait(true);
        }

        // ---------------------------------------------------------------- Nesto#534 (NestoAPI#654): etiqueta de producto

        /// <summary>
        /// Sugerencia 546 (Paloma): el botón de cada línea imprime la etiqueta de código de barras del producto (30×20 mm, en
        /// la impresora de etiquetas del usuario), para pegarla en lo que llega sin un código que se pueda leer.
        /// </summary>
        public IAsyncRelayCommand<LineaRecibirReposicion> ImprimirEtiquetaCommand { get; }

        private int _copiasEtiqueta = 1;
        /// <summary>Cuántas etiquetas saca cada botón de línea (de 1 a 100).</summary>
        public int CopiasEtiqueta
        {
            get => _copiasEtiqueta;
            set => SetProperty(ref _copiasEtiqueta, Math.Max(1, Math.Min(100, value)));
        }

        internal async Task ImprimirEtiquetaAsync(LineaRecibirReposicion linea)
        {
            if (linea == null || string.IsNullOrWhiteSpace(linea.Producto) || _etiquetas == null)
            {
                return;
            }
            Mensaje = null;
            await Ocupado(async () =>
            {
                try
                {
                    ResultadoEtiquetasProducto resultado = await _etiquetas.Imprimir(Empresa, linea.Producto, CopiasEtiqueta).ConfigureAwait(true);
                    Mensaje = string.IsNullOrWhiteSpace(resultado?.Mensaje)
                        ? $"Etiqueta de {linea.Producto} mandada a la impresora."
                        : resultado.Mensaje.Trim();
                }
                catch (EtiquetasProductoException ex)
                {
                    Mensaje = ex.Message;
                }
            }).ConfigureAwait(true);
            PedirFocoEnLector?.Invoke();
        }

        private async Task TerminarAsync()
        {
            if (_recepcion == null || Seleccionada == null)
            {
                return;
            }
            // Ariadna#51: se termina con lo leído que hay en el servidor (también lo de los demás equipos)
            if (!await SincronizarAntesDeTerminarAsync("Terminar").ConfigureAwait(true))
            {
                return;
            }
            if (EsperandoEtiqueta)
            {
                // La API tampoco la deja terminar sin la etiqueta
                LecturaFallida(TextoLeaEtiqueta(DocumentoAbierto));
                return;
            }
            string tituloConfirmacion = string.IsNullOrWhiteSpace(_recepcion.TituloConfirmacion)
                ? $"¿Terminar la reposición {Seleccionada.Documento}?"
                : _recepcion.TituloConfirmacion.Trim();
            if (!await _dialogos.ShowConfirmationAsync(tituloConfirmacion, ResumenParaConfirmar()).ConfigureAwait(true))
            {
                return;
            }
            var terminar = new TerminarRecepcionReposicion
            {
                IdRecepcion = _idRecepcion,
                Dispositivo = Environment.MachineName,
                Lecturas = Lineas.Where(l => l.Leido > 0)
                    .Select(l => new LecturaRecepcionReposicion { Producto = l.Producto, Cantidad = l.Leido })
                    .ToList(),
                // Nesto#530: las etiquetas de los bultos leídas (la API las exige en las reposiciones enviadas con bultos)
                CodigosBulto = CodigosBulto.ToList()
            };
            bool terminada = false;
            await Ocupado(async () =>
            {
                ResultadoRecepcionReposicion resultado = await _servicio.Terminar(Empresa, Almacen, Seleccionada.Documento, terminar).ConfigureAwait(true);
                terminada = true;
                Mensaje = TextoResultado(Seleccionada.Documento, resultado);
                Pendientes.Remove(Seleccionada);
                CerrarLaAbierta();
            }).ConfigureAwait(true);
            if (!terminada && _lecturasEnServidor)
            {
                // Ariadna#51 (reposición 81119): ya no se pierde lo leído si se corta la API al terminar
                Mensaje = $"{Mensaje} {TextoSigueEnElServidor("Terminar")}";
            }
        }

        /// <summary>Tras terminarla (entera): ya no hay ninguna abierta.</summary>
        private void CerrarLaAbierta()
        {
            SalirDelGrupo();
            _lecturasEnServidor = false;
            OnPropertyChanged(nameof(LecturasCompartidas));
            TextoOtrosEquipos = null;
            Seleccionada = null;
            _recepcion = null;
            Lineas.Clear();
            CodigosBulto.Clear();
            RefrescarBultos();
            PuedeTerminar = false;
            RefrescarRecibirEnParte();
        }

        // ---------------------------------------------------------------- NestoAPI#673: recibir por partes

        /// <summary>
        /// NestoAPI#673 (Novedades 598, Reina): «Dar de alta solo lo leído». La clienta que viene a recoger lo que pidió el mismo
        /// día que llega la reposición: se lee solo eso, entra ya y el resto de la reposición queda pendiente para recibirlo
        /// después (sin contarlo como falta). Como en Nesto viejo, que dejaba contabilizar una sola línea.
        /// </summary>
        public IAsyncRelayCommand RecibirLoLeidoCommand { get; }

        /// <summary>Se puede dar de alta solo lo leído: se puede terminar y la API lo admite (una anterior la terminaría entera).</summary>
        public bool PuedeRecibirEnParte => PuedeTerminar && _recepcion?.AdmiteRecibirEnParte == true;

        /// <summary>«Recibido en parte: 1 línea, 2 ud.» de la reposición abierta. Null si no se ha recibido nada en parte.</summary>
        public string TextoRecibidaEnParte => string.IsNullOrWhiteSpace(_recepcion?.TextoRecibidaEnParte) ? null : _recepcion.TextoRecibidaEnParte.Trim();

        private void RefrescarRecibirEnParte()
        {
            OnPropertyChanged(nameof(PuedeRecibirEnParte));
            OnPropertyChanged(nameof(TextoRecibidaEnParte));
            RecibirLoLeidoCommand?.NotifyCanExecuteChanged();
        }

        /// <summary>Lo que entra en una parcial: lo leído de cada línea que venía (nunca más de lo enviado).</summary>
        internal int UnidadesParaRecibirEnParte => Lineas.Where(l => !l.NoVenia).Sum(l => Math.Min(l.Leido, l.Enviado));

        internal static string TextoConfirmarParcial(int unidades)
            => $"Se darán de alta {(unidades == 1 ? "la unidad leída" : $"las {unidades} unidades leídas")}. " +
               "El resto de la reposición queda pendiente para recibirlo después.";

        internal async Task RecibirLoLeidoAsync()
        {
            if (_recepcion == null || Seleccionada == null || !PuedeRecibirEnParte)
            {
                return;
            }
            // Ariadna#51: se da de alta lo leído que hay en el servidor (también lo de los demás equipos)
            if (!await SincronizarAntesDeTerminarAsync("Dar de alta solo lo leído").ConfigureAwait(true))
            {
                return;
            }
            if (EsperandoEtiqueta)
            {
                // La API tampoco la deja recibir sin la etiqueta
                LecturaFallida(TextoLeaEtiqueta(DocumentoAbierto));
                return;
            }
            // En una parcial no entra nada que no viniera ni más de lo enviado (eso se ve al terminarla): se dice antes de preguntar
            List<string> noVenian = Lineas.Where(l => l.NoVenia && l.Leido > 0).Select(l => l.Producto).ToList();
            if (noVenian.Count > 0)
            {
                LecturaFallida($"Para dar de alta solo lo leído, todo tiene que venir en la reposición: {string.Join(", ", noVenian)} no venía. " +
                    "Ponga su «Leído» a 0 o termine la reposición entera.");
                return;
            }
            List<LineaRecibirReposicion> sobran = Lineas.Where(l => !l.NoVenia && l.Leido > l.Enviado).ToList();
            if (sobran.Count > 0)
            {
                LecturaFallida("Para dar de alta solo lo leído, no puede haber más de lo enviado: " +
                    string.Join(", ", sobran.Select(l => $"{l.Producto} (enviado {l.Enviado}, leído {l.Leido})")) +
                    ". Corrija lo leído o termine la reposición entera.");
                return;
            }
            int unidades = UnidadesParaRecibirEnParte;
            if (unidades == 0)
            {
                LecturaFallida("No ha leído nada: lea lo que quiere dar de alta y vuelva a pulsar «Dar de alta solo lo leído».");
                return;
            }
            if (!await _dialogos.ShowConfirmationAsync("¿Dar de alta solo lo leído?", TextoConfirmarParcial(unidades)).ConfigureAwait(true))
            {
                return;
            }
            var terminar = new TerminarRecepcionReposicion
            {
                IdRecepcion = _idRecepcion,
                Dispositivo = Environment.MachineName,
                Lecturas = Lineas.Where(l => !l.NoVenia && l.Leido > 0)
                    .Select(l => new LecturaRecepcionReposicion { Producto = l.Producto, Cantidad = l.Leido })
                    .ToList(),
                CodigosBulto = CodigosBulto.ToList(),
                Parcial = true
            };
            RecepcionPendiente abierta = Seleccionada;
            ResultadoRecepcionReposicion resultado = null;
            await Ocupado(async () =>
            {
                resultado = await _servicio.Terminar(Empresa, Almacen, abierta.Documento, terminar).ConfigureAwait(true);
            }).ConfigureAwait(true);
            if (resultado == null)
            {
                // No ha entrado nada: Ocupado ya ha puesto el motivo y lo leído sigue ahí
                if (_lecturasEnServidor)
                {
                    Mensaje = $"{Mensaje} {TextoSigueEnElServidor("Dar de alta solo lo leído")}";
                }
                return;
            }
            string texto = TextoResultadoParcial(abierta.Documento, resultado);
            if (!resultado.Parcial && !resultado.YaEstabaTerminada)
            {
                // Lo leído era la reposición entera: queda recibida, como al terminar
                Mensaje = texto;
                Pendientes.Remove(abierta);
                CerrarLaAbierta();
                return;
            }
            // Sigue pendiente con lo que queda: se vuelve a abrir (IdRecepcion nuevo y lo leído a cero) sin volver a pedir
            // la etiqueta del bulto
            List<string> bultos = CodigosBulto.ToList();
            await RecargarTrasParcialAsync(abierta.Documento).ConfigureAwait(true);
            if (Seleccionada != null)
            {
                foreach (string bulto in bultos.Where(b => !CodigosBulto.Contains(b)))
                {
                    CodigosBulto.Add(bulto);
                    if (_lecturasEnServidor)
                    {
                        // Ariadna#51: la recepción en curso nueva también los tiene (los demás equipos no los vuelven a pedir)
                        string leido = bulto;
                        Encolar(new LecturaEnCursoRecepcion { CodigoBulto = leido, Cantidad = 1 }, () =>
                        {
                            _ = CodigosBulto.Remove(leido);
                            RefrescarBultos();
                        });
                    }
                }
                RefrescarBultos();
            }
            Mensaje = texto;
            PedirFocoEnLector?.Invoke();
        }

        private async Task RecargarTrasParcialAsync(string documento)
        {
            List<RecepcionPendiente> pendientes = null;
            await Ocupado(async () =>
            {
                pendientes = await _servicio.LeerPendientes(Empresa, Almacen).ConfigureAwait(true);
            }).ConfigureAwait(true);
            if (pendientes == null)
            {
                return;
            }
            Pendientes.Clear();
            foreach (RecepcionPendiente pendiente in pendientes)
            {
                Pendientes.Add(pendiente);
            }
            RecepcionPendiente misma = Pendientes.FirstOrDefault(p => string.Equals(p.Documento?.Trim(), documento?.Trim(), StringComparison.OrdinalIgnoreCase));
            CerrarLaAbierta();
            if (misma != null)
            {
                await ElegirAsync(misma).ConfigureAwait(true);
            }
        }

        /// <summary>Lo que dice el servidor tras una parcial (o, con una API sin <see cref="ResultadoRecepcionReposicion.Mensaje"/>, se monta aquí).</summary>
        internal static string TextoResultadoParcial(string documento, ResultadoRecepcionReposicion resultado)
        {
            if (resultado?.Parcial != true || !string.IsNullOrWhiteSpace(resultado.Mensaje) || resultado.YaEstabaTerminada)
            {
                return TextoResultado(documento, resultado);
            }
            var partes = new List<string> { $"Reposición {documento?.Trim()}: se ha dado de alta lo leído." };
            string lineas = resultado.LineasPendientes == 1 ? "1 línea" : $"{resultado.LineasPendientes} líneas";
            partes.Add($"Sigue pendiente el resto ({lineas}, {resultado.UnidadesPendientes} ud.).");
            return string.Join(Environment.NewLine, partes);
        }

        internal string ResumenParaConfirmar()
        {
            int faltan = Lineas.Where(l => !l.NoVenia && l.Diferencia < 0).Sum(l => -l.Diferencia);
            int sobran = Lineas.Where(l => !l.NoVenia && l.Diferencia > 0).Sum(l => l.Diferencia);
            int noVenian = Lineas.Where(l => l.NoVenia && l.Leido > 0).Sum(l => l.Leido);
            bool coincide = faltan == 0 && sobran == 0 && noVenian == 0;
            // Nesto#515 (NestoAPI#600): el desglose es de aquí (lo leído); lo que va a pasar, del servidor (según el tipo)
            bool textosDelServidor = !string.IsNullOrWhiteSpace(_recepcion?.AvisoCoincide) || !string.IsNullOrWhiteSpace(_recepcion?.AvisoNoCoincide);
            var partes = new List<string>();
            if (coincide)
            {
                partes.Add("Todo coincide con lo enviado.");
            }
            else
            {
                if (faltan > 0)
                {
                    partes.Add($"Faltan {faltan} ud. de lo enviado.");
                }
                if (sobran > 0)
                {
                    partes.Add($"Sobran {sobran} ud. de lo enviado.");
                }
                if (noVenian > 0)
                {
                    partes.Add($"Han llegado {noVenian} ud. que no venían en la reposición.");
                }
            }
            if (textosDelServidor)
            {
                partes.Add(coincide ? _recepcion.AvisoCoincide : _recepcion.AvisoNoCoincide);
                partes.Add(faltan > 0 ? _recepcion.AvisoConFaltas : null);
                partes.Add(sobran > 0 ? _recepcion.AvisoConSobras : null);
                partes.Add(noVenian > 0 ? _recepcion.AvisoConAjenos : null);
            }
            else
            {
                // Con una API anterior a NestoAPI#600, los textos de antes
                if (!coincide)
                {
                    partes.Add("Se informará de las diferencias a quien hizo la reposición.");
                }
                partes.Add("Ojo: entra lo leído, no lo enviado.");
            }
            return string.Join(Environment.NewLine, partes.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim()));
        }

        /// <summary>
        /// Nesto#515 (NestoAPI#600): el servidor manda el texto entero (diferencias, a quién se ha avisado, Ubicar) y se
        /// enseña tal cual. Con una API anterior se monta aquí; a quién se ha avisado ya viene en los avisos del servidor,
        /// así que no se repite con <see cref="ResultadoRecepcionReposicion.AvisadoA"/>.
        /// </summary>
        internal static string TextoResultado(string documento, ResultadoRecepcionReposicion resultado)
        {
            if (!string.IsNullOrWhiteSpace(resultado?.Mensaje))
            {
                return resultado.Mensaje.Trim();
            }
            var partes = new List<string>
            {
                resultado?.YaEstabaTerminada == true ? $"La reposición {documento} ya estaba recibida." : $"Reposición {documento} recibida."
            };
            List<DiferenciaRecepcionReposicion> diferencias = resultado?.Diferencias ?? new List<DiferenciaRecepcionReposicion>();
            if (diferencias.Count > 0)
            {
                partes.Add("No coincide con lo enviado (ha entrado lo leído):");
                partes.AddRange(diferencias.Select(d => d.Ajeno
                    ? $"  {d.Producto?.Trim()} {d.Descripcion?.Trim()}: no venía, han entrado {d.Leido}."
                    : $"  {d.Producto?.Trim()} {d.Descripcion?.Trim()}: enviadas {d.Esperado}, han entrado {d.Leido}."));
            }
            partes.AddRange((resultado?.Avisos ?? new List<string>()).Where(a => !string.IsNullOrWhiteSpace(a)));
            return string.Join(Environment.NewLine, partes);
        }

        private async Task Ocupado(Func<Task> accion)
        {
            EstaOcupado = true;
            try
            {
                await accion().ConfigureAwait(true);
            }
            catch (RecepcionReposicionException ex)
            {
                Mensaje = ex.Message;
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
            {
                // Ariadna#51: con lo leído en el servidor, quien llama añade que sigue allí
                Mensaje = _lecturasEnServidor
                    ? "No se puede conectar con el servidor."
                    : "No se puede conectar con el servidor. Vuelva a intentarlo; lo leído no se pierde.";
            }
            finally
            {
                EstaOcupado = false;
            }
        }
    }
}
