using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Models;
using Nesto.Infrastructure.Services;
using Nesto.Infrastructure.Shared;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
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
                if (SetProperty(ref _leido, Math.Max(0, value)))
                {
                    OnPropertyChanged(nameof(Diferencia));
                    OnPropertyChanged(nameof(Estado));
                    OnPropertyChanged(nameof(TipoEstado));
                }
            }
        }

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
    /// </summary>
    public class RecibirReposicionViewModel : ObservableObject
    {
        private readonly IServicioRecepcionReposiciones _servicio;
        private readonly IServicioDialogos _dialogos;
        private readonly IConfiguracion _configuracion;
        private readonly ISonidosLector _sonidos;
        private readonly Action<string> _abrirFichero;
        private readonly IServicioNavegacion _navegacion;
        private readonly IServicioEtiquetasProducto _etiquetas;

        /// <summary>Uno por reposición abierta: si la respuesta se pierde y se reintenta, no se recibe dos veces.</summary>
        private Guid _idRecepcion;

        public RecibirReposicionViewModel(IServicioRecepcionReposiciones servicio, IServicioDialogos dialogos, IConfiguracion configuracion,
            ISonidosLector sonidos, IServicioNavegacion navegacion, IServicioEtiquetasProducto etiquetas)
            : this(servicio, dialogos, configuracion, sonidos, navegacion, null, etiquetas)
        {
        }

        /// <param name="abrirFichero">Abre el PDF descargado (los tests lo cambian). Null: con el visor del sistema.</param>
        /// <param name="etiquetas">Nesto#534: la etiqueta de código de barras de cada línea. Sin él, el botón no hace nada.</param>
        internal RecibirReposicionViewModel(IServicioRecepcionReposiciones servicio, IServicioDialogos dialogos, IConfiguracion configuracion,
            ISonidosLector sonidos, IServicioNavegacion navegacion, Action<string> abrirFichero, IServicioEtiquetasProducto etiquetas = null)
        {
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
            Mensaje = null;
            await Ocupado(async () =>
            {
                RecepcionReposicion recepcion = await _servicio.LeerRecepcion(Empresa, Almacen, pendiente.Documento).ConfigureAwait(true);
                if (recepcion == null)
                {
                    Mensaje = $"La reposición {pendiente.Documento} ya no está pendiente.";
                    return;
                }
                _recepcion = recepcion;
                _idRecepcion = Guid.NewGuid();
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
                PuedeTerminar = recepcion.PuedeTerminar && recepcion.SeTerminaDesdeAqui;
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

        /// <summary>«R81010-2»: la etiqueta de un bulto (R, el traspaso, guion y el número de bulto).</summary>
        private static readonly Regex _patronEtiqueta = new Regex(@"^R\d+-\d+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        internal static bool EsEtiquetaBulto(string codigo) => !string.IsNullOrWhiteSpace(codigo) && _patronEtiqueta.IsMatch(codigo.Trim());

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
            codigo = codigo.Trim().ToUpperInvariant();
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
        }

        /// <summary>Lo que manda el lector (o se teclea): un código de barras o la referencia suma una unidad.</summary>
        private void Leer(string lectura)
        {
            string codigo = lectura?.Trim();
            if (string.IsNullOrEmpty(codigo))
            {
                return;
            }
            if (EsEtiquetaBulto(codigo))
            {
                // Nesto#530: la etiqueta de un bulto (también sin ninguna abierta: abre su reposición)
                LecturaEtiquetaEnCurso = LeerEtiquetaAsync(codigo);
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
            await Ocupado(async () =>
            {
                ResultadoRecepcionReposicion resultado = await _servicio.Terminar(Empresa, Almacen, Seleccionada.Documento, terminar).ConfigureAwait(true);
                Mensaje = TextoResultado(Seleccionada.Documento, resultado);
                Pendientes.Remove(Seleccionada);
                Seleccionada = null;
                _recepcion = null;
                Lineas.Clear();
                CodigosBulto.Clear();
                RefrescarBultos();
                PuedeTerminar = false;
            }).ConfigureAwait(true);
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
                Mensaje = "No se puede conectar con el servidor. Vuelve a intentarlo; lo leído no se pierde.";
            }
            finally
            {
                EstaOcupado = false;
            }
        }
    }
}
