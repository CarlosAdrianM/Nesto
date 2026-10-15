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
using System.Threading.Tasks;

namespace Nesto.Modules.Producto.ViewModels
{
    /// <summary>Una línea de la reposición en preparación: lo que tiene el servidor y lo que se manda de verdad.</summary>
    public class LineaEnviarReposicion : ObservableObject
    {
        public int NumeroOrden { get; set; }
        public string Producto { get; set; }
        public string Nombre { get; set; }
        public string CodigoBarras { get; set; }

        private int _stockOrigen;
        /// <summary>Stock en la tienda.</summary>
        public int StockOrigen { get => _stockOrigen; set => SetProperty(ref _stockOrigen, value); }

        /// <summary>La cantidad que tiene el servidor: la de la pantalla solo puede bajar de aquí.</summary>
        public int Preparada { get; set; }

        private int _cantidad;
        /// <summary>Lo que se manda (editable, solo a menos). A 0 se borra al terminar.</summary>
        public int Cantidad
        {
            get => _cantidad;
            set
            {
                if (SetProperty(ref _cantidad, Math.Max(0, value)))
                {
                    OnPropertyChanged(nameof(EstaACero));
                }
            }
        }

        public bool EstaACero => Cantidad == 0;

        internal bool TieneCodigo(string codigo)
            => string.Equals(CodigoBarras?.Trim(), codigo, StringComparison.OrdinalIgnoreCase)
               || string.Equals(Producto?.Trim(), codigo, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Sugerencia 581: una línea de la reposición puntual. Solo existe en la pantalla hasta que se manda (el servidor la crea
    /// y la termina de una vez).
    /// </summary>
    public class LineaPuntualReposicion : ObservableObject
    {
        public string Producto { get; set; }
        public string Nombre { get; set; }
        public string CodigoBarras { get; set; }
        /// <summary>Las unidades que suma cada lectura del código con el que se añadió (la caja de 6, 6).</summary>
        public int UnidadesPorLectura { get; set; } = 1;
        /// <summary>Stock en la tienda.</summary>
        public int StockOrigen { get; set; }
        /// <summary>Sugerencia 581 (Carlos, 13/10/26): lo que necesitan los pedidos pendientes de la tienda: no se manda.</summary>
        public int Comprometido { get; set; }

        private int? _disponible;
        /// <summary>Lo que se puede mandar (stock − comprometido, lo calcula la API). Sin dato (API anterior): el stock.</summary>
        public int Disponible
        {
            get => _disponible ?? StockOrigen;
            set => _disponible = value;
        }

        private int _cantidad;
        public int Cantidad
        {
            get => _cantidad;
            set
            {
                if (SetProperty(ref _cantidad, Math.Max(0, value)))
                {
                    OnPropertyChanged(nameof(FaltaStock));
                }
            }
        }

        /// <summary>
        /// Se piden más de las que se pueden mandar (las que hay en la tienda menos las que necesitan sus pedidos): el servidor
        /// no la dejaría mandar.
        /// </summary>
        public bool FaltaStock => Cantidad > Disponible;

        /// <summary>«41980 (pones 3, hay 2)» o, si parte es para sus pedidos, «41980 (pones 3, puedes mandar 1: el resto es para sus pedidos)».</summary>
        internal string TextoFalta()
            => Comprometido > 0
                ? $"{Producto} (pones {Cantidad}, puedes mandar {Math.Max(0, Disponible)}: el resto es para sus pedidos)"
                : $"{Producto} (pones {Cantidad}, hay {Math.Max(0, StockOrigen)})";

        internal bool TieneCodigo(string codigo)
            => string.Equals(CodigoBarras?.Trim(), codigo, StringComparison.OrdinalIgnoreCase)
               || string.Equals(Producto?.Trim(), codigo, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// NestoAPI#553: la tienda prepara la reposición que manda a Algete y la termina (api/Reposiciones), en lugar de
    /// hacerlo en Nesto viejo. El servidor propone lo que hay que mandar; aquí solo se baja lo que no se manda y se
    /// termina. Desde Algete no: esas reposiciones se hacen en Ariadna.
    /// NestoAPI#577: la reposición la rellena la API sola a la hora de corte del calendario; «Preparar reposición» (rellenarla
    /// a mano) solo lo ven las personas autorizadas (GET api/Reposiciones/PuedeRellenarManual).
    /// </summary>
    public class EnviarReposicionViewModel : ObservableObject
    {
        private readonly IServicioEnvioReposiciones _servicio;
        private readonly IServicioDialogos _dialogos;
        private readonly IConfiguracion _configuracion;
        private readonly ISonidosLector _sonidos;
        private readonly Action<string> _abrirFichero;
        private readonly IServicioNavegacion _navegacion;

        /// <summary>Mientras se ponen las cantidades que manda el servidor (o se deshace un cambio) no se le vuelven a mandar.</summary>
        private bool _aplicandoServidor;

        public EnviarReposicionViewModel(IServicioEnvioReposiciones servicio, IServicioDialogos dialogos, IConfiguracion configuracion,
            ISonidosLector sonidos, IServicioNavegacion navegacion)
            : this(servicio, dialogos, configuracion, sonidos, navegacion, null)
        {
        }

        /// <param name="abrirFichero">Abre el PDF descargado (los tests lo cambian). Null: con el visor del sistema.</param>
        internal EnviarReposicionViewModel(IServicioEnvioReposiciones servicio, IServicioDialogos dialogos, IConfiguracion configuracion,
            ISonidosLector sonidos, IServicioNavegacion navegacion, Action<string> abrirFichero)
        {
            _servicio = servicio;
            _dialogos = dialogos;
            _configuracion = configuracion;
            _sonidos = sonidos;
            _abrirFichero = abrirFichero ?? AbridorFicheros.AbrirConElVisorDelSistema;
            _navegacion = navegacion;
            // Sugerencia 599 (Paloma): doble clic en la referencia abre la ficha del producto
            AbrirFichaProductoCommand = new RelayCommand<string>(AbrirFichaProducto);
            CargarCommand = new AsyncRelayCommand(CargarAsync);
            PrepararCommand = new AsyncRelayCommand(PrepararAsync, () => PuedePreparar);
            CambiarCantidadCommand = new AsyncRelayCommand<LineaEnviarReposicion>(CambiarCantidadAsync, AsyncRelayCommandOptions.AllowConcurrentExecutions);
            LeerCommand = new RelayCommand<string>(Leer);
            LeerLecturaCommand = new RelayCommand(() =>
            {
                Leer(Lectura);
                Lectura = string.Empty;
            });
            TerminarCommand = new AsyncRelayCommand(TerminarAsync, () => HayReposicion && !EstaOcupado);
            ImprimirCommand = new AsyncRelayCommand(ImprimirAsync, () => HayReposicion && !EstaOcupado);
            EmpezarPuntualCommand = new RelayCommand(EmpezarPuntual, () => PuedeEmpezarPuntual);
            CancelarPuntualCommand = new AsyncRelayCommand(CancelarPuntualAsync, () => EnModoPuntual && !EstaOcupado);
            EnviarPuntualCommand = new AsyncRelayCommand(EnviarPuntualAsync, () => EnModoPuntual && UnidadesPuntual > 0 && !EstaOcupado);
            QuitarLineaPuntualCommand = new RelayCommand<LineaPuntualReposicion>(QuitarLineaPuntual, l => l != null && EnModoPuntual && !EstaOcupado);
            MarcarRecogidaCommand = new AsyncRelayCommand(MarcarRecogidaAsync, () => MostrarBotonRecogida && !EstaOcupado);
            ReimprimirEtiquetasCommand = new AsyncRelayCommand(ReimprimirEtiquetasAsync, () => PuedeReimprimirEtiquetas && !EstaOcupado);
        }

        /// <summary>NestoAPI#577: con qué se crea la reposición (Nesto, Ariadna o el proceso automático).</summary>
        internal const string HERRAMIENTA = "Nesto";

        public string Titulo => "Enviar reposición";

        public string Empresa { get; set; } = Constantes.Empresas.EMPRESA_DEFECTO;

        /// <summary>El reloj (los tests lo fijan): para decir «hoy» o «mañana» en la hora a la que se rellena sola.</summary>
        internal Func<DateTime> Ahora { get; set; } = () => DateTime.Now;

        /// <summary>Las tiendas solo mandan reposiciones a Algete.</summary>
        public string Destino => Constantes.Almacenes.ALMACEN_ALGETE;

        private string _almacen;
        /// <summary>El almacén del usuario (AlmacénPedidoVta): la tienda que manda la reposición.</summary>
        public string Almacen { get => _almacen; private set => SetProperty(ref _almacen, value); }

        public ObservableCollection<LineaEnviarReposicion> Lineas { get; } = new ObservableCollection<LineaEnviarReposicion>();

        private LineaEnviarReposicion _seleccionada;
        public LineaEnviarReposicion Seleccionada { get => _seleccionada; set => SetProperty(ref _seleccionada, value); }

        /// <summary>La vista pone el foco en la cantidad de esa línea (la ha leído el lector).</summary>
        public event Action<LineaEnviarReposicion> PedirFocoEnCantidad;

        /// <summary>La vista vuelve a poner el foco en el cuadro del lector (lo leído no ha entrado).</summary>
        public event Action PedirFocoEnLector;

        private bool _origenValido;

        /// <summary>NestoAPI#577: si el usuario puede rellenar la reposición a mano (los demás esperan a que la rellene la API).</summary>
        private bool _puedeRellenarManual;

        /// <summary>Lo que se dice cuando no hay reposición en preparación (también si se lee un código entonces).</summary>
        private string _textoSinReposicion;

        private bool _hayReposicion;
        public bool HayReposicion
        {
            get => _hayReposicion;
            private set
            {
                if (SetProperty(ref _hayReposicion, value))
                {
                    RefrescarEstado();
                }
            }
        }

        public bool PuedePreparar => _origenValido && _puedeRellenarManual && !HayReposicion && !EnModoPuntual && !EstaOcupado;

        // ---------------------------------------------------------------- Sugerencia 581: reposición puntual

        /// <summary>
        /// Sugerencia 581 (Paloma): después de la hora de cierre alguien pide que se mande un producto concreto a Algete. La
        /// tienda hace una reposición APARTE con solo eso (la del calendario no se toca): se lee cada producto, se pone la
        /// cantidad y se manda de una vez (la API la crea y la termina: queda pendiente de recibir en Algete como cualquier otra).
        /// Mientras la del calendario esté en preparación sin enviar, no: se manda esa primero (la API también lo impide).
        /// </summary>
        public ObservableCollection<LineaPuntualReposicion> LineasPuntual { get; } = new ObservableCollection<LineaPuntualReposicion>();

        private bool _enModoPuntual;
        public bool EnModoPuntual
        {
            get => _enModoPuntual;
            private set
            {
                if (SetProperty(ref _enModoPuntual, value))
                {
                    RefrescarEstado();
                }
            }
        }

        /// <summary>
        /// Sugerencia 581 (Carlos, 13/10/26): solo con la de hoy enviada (nada en preparación) y si la API dice que hoy se puede
        /// (día con viaje, la de hoy ya rellena y el repartidor sin pasar).
        /// </summary>
        public bool PuedeEmpezarPuntual => MostrarBotonPuntual && !HayReposicion && !PuntualBloqueada;

        /// <summary>El botón se ve siempre en la tienda; si no se puede, desactivado y diciendo por qué (al lado y en el tooltip).</summary>
        public bool MostrarBotonPuntual => _origenValido && !EnModoPuntual && !EstaOcupado;

        // ---------------------------------------------------------------- Sugerencia 581 (Carlos, 13/10/26): cuándo se puede y la recogida

        /// <summary>Lo que dice la API de hoy (GET api/Reposiciones/Puntual/Estado). Null: no se sabe (API anterior): sin restricciones.</summary>
        private EstadoReposicionPuntual _estadoPuntual;

        /// <summary>La API dice que hoy no se puede (sin viaje, sin rellenar, sin enviar o ya recogida).</summary>
        public bool PuntualBloqueada => _estadoPuntual != null && !_estadoPuntual.SePuede;

        internal const string TEXTO_PRIMERO_ENVIA = "Primero envía la de hoy.";

        /// <summary>«NUEVAVISION\Paloma» → «Paloma».</summary>
        internal static string SinDominio(string usuario)
        {
            string limpio = usuario?.Trim();
            return string.IsNullOrEmpty(limpio) || !limpio.Contains("\\") ? limpio : limpio.Substring(limpio.LastIndexOf('\\') + 1);
        }

        /// <summary>«El repartidor ya se la ha llevado a las 12:10 (lo marcó Paloma): lo que quede va en la siguiente.»</summary>
        internal static string TextoRecogida(EstadoReposicionPuntual estado)
        {
            if (estado?.FechaRecogida == null)
            {
                return estado?.Motivo;
            }
            string quien = SinDominio(estado.UsuarioRecogida);
            return $"El repartidor ya se la ha llevado a las {estado.FechaRecogida.Value:H:mm}" +
                (string.IsNullOrEmpty(quien) ? string.Empty : $" (lo marcó {quien})") + ": lo que quede va en la siguiente.";
        }

        /// <summary>Junto al botón y en su tooltip: por qué no se puede hacer la puntual. Null si se puede.</summary>
        public string AvisoPuntual
        {
            get
            {
                if (!_origenValido || EnModoPuntual)
                {
                    return null;
                }
                if (_estadoPuntual?.Situacion == EstadoReposicionPuntual.RECOGIDA)
                {
                    return TextoRecogida(_estadoPuntual);
                }
                if (HayReposicion)
                {
                    return TEXTO_PRIMERO_ENVIA;
                }
                return PuntualBloqueada ? _estadoPuntual.Motivo?.Trim() : null;
            }
        }

        internal const string TOOLTIP_PUNTUAL = "Mandar a Algete solo unos productos concretos, aparte de la reposición del calendario " +
            "(por ejemplo, si te lo piden después de enviar la de hoy), hasta que el repartidor se la lleve. Alt+P";

        /// <summary>El tooltip del botón: el motivo si está desactivado.</summary>
        public string ToolTipPuntual => AvisoPuntual ?? TOOLTIP_PUNTUAL;

        /// <summary>«El repartidor ya se la ha llevado»: con la de hoy enviada y sin marcar (y la API con el script lanzado).</summary>
        public bool MostrarBotonRecogida => _origenValido && !EnModoPuntual && !HayReposicion && _estadoPuntual?.PuedeMarcarRecogida == true;

        public IAsyncRelayCommand MarcarRecogidaCommand { get; }

        internal const string TEXTO_CONFIRMAR_RECOGIDA = "¿El repartidor ya se ha llevado la reposición de hoy? " +
            "Desde ahora ya no se podrá hacer otra reposición puntual hoy: lo que quede irá en la siguiente.";

        // Revisión 13/10/26: la versión de la última lectura del estado de la puntual. Si se piden dos seguidas (al volver a
        // la pestaña se carga otra vez) y contesta tarde la primera, su respuesta ya es vieja y no pisa la nueva.
        private int _versionEstadoPuntual;

        private async Task LeerEstadoPuntual()
        {
            int version = ++_versionEstadoPuntual;
            EstadoReposicionPuntual estado;
            try
            {
                estado = await _servicio.LeerEstadoPuntual(Empresa, Almacen).ConfigureAwait(true);
            }
            catch (Exception)
            {
                // Solo es para avisar antes: si no se sabe, la API lo dirá al mandarla
                estado = null;
            }
            if (version != _versionEstadoPuntual)
            {
                return;
            }
            _estadoPuntual = estado;
            RefrescarEstado();
        }

        /// <summary>Si hoy no se puede hacer la puntual lo dice (y desactiva el botón) y devuelve true.</summary>
        private bool AvisarSiPuntualBloqueada()
        {
            if (!PuntualBloqueada)
            {
                return false;
            }
            Mensaje = _estadoPuntual.Situacion == EstadoReposicionPuntual.RECOGIDA ? TextoRecogida(_estadoPuntual) : _estadoPuntual.Motivo;
            _sonidos?.Error();
            RefrescarEstado();
            return true;
        }

        private async Task MarcarRecogidaAsync()
        {
            if (!MostrarBotonRecogida)
            {
                return;
            }
            if (!await _dialogos.ShowConfirmationAsync("El repartidor ya se la ha llevado", TEXTO_CONFIRMAR_RECOGIDA).ConfigureAwait(true))
            {
                return;
            }
            bool hecho = false;
            await Ocupado(async () =>
            {
                EstadoReposicionPuntual estado = await _servicio.MarcarRecogida(new MarcarRecogidaReposicion
                {
                    Empresa = Empresa,
                    Origen = Almacen,
                    Destino = Destino
                }).ConfigureAwait(true);
                // Lo que contesta la API al marcarla manda: una lectura del estado pedida antes ya es vieja
                _versionEstadoPuntual++;
                _estadoPuntual = estado;
                hecho = true;
                Mensaje = TextoRecogida(_estadoPuntual);
            }).ConfigureAwait(true);
            if (!hecho)
            {
                // Ya estaba marcada, hay otra en preparación…: lo que diga ahora la API, sin perder el motivo
                string error = Mensaje;
                await LeerEstadoPuntual().ConfigureAwait(true);
                Mensaje = error;
            }
            RefrescarEstado();
        }

        /// <summary>El cuadro del lector se ve con la reposición del calendario y en la puntual.</summary>
        public bool MostrarLector => HayReposicion || EnModoPuntual;

        public int UnidadesPuntual => LineasPuntual.Sum(l => l.Cantidad);
        public int ProductosPuntual => LineasPuntual.Count(l => l.Cantidad > 0);

        private LineaPuntualReposicion _seleccionadaPuntual;
        public LineaPuntualReposicion SeleccionadaPuntual { get => _seleccionadaPuntual; set => SetProperty(ref _seleccionadaPuntual, value); }

        public IRelayCommand EmpezarPuntualCommand { get; }
        public IAsyncRelayCommand CancelarPuntualCommand { get; }
        public IAsyncRelayCommand EnviarPuntualCommand { get; }
        public IRelayCommand<LineaPuntualReposicion> QuitarLineaPuntualCommand { get; }

        // Revisión 13/10/26: cada puntual que se empieza (o se cancela, o se manda) es una versión nueva. Una búsqueda que
        // contesta tarde, de una puntual que ya no está, no añade nada a la de ahora.
        private int _versionPuntual;

        /// <summary>La lectura de la puntual en curso (los tests la esperan).</summary>
        internal Task LecturaPuntualEnCurso { get; private set; } = Task.CompletedTask;

        internal const string TEXTO_PUNTUAL = "Reposición puntual a Algete: lee (o escribe) cada producto y pon la cantidad. " +
            "Va aparte de la del calendario, que no cambia. Se manda al pulsar «Mandar a Algete».";

        private void EmpezarPuntual()
        {
            if (AvisarSiPuntualBloqueada() || !PuedeEmpezarPuntual)
            {
                return;
            }
            LimpiarPuntual();
            EnModoPuntual = true;
            Mensaje = TEXTO_PUNTUAL;
            PedirFocoEnLector?.Invoke();
        }

        private async Task CancelarPuntualAsync()
        {
            if (!EnModoPuntual)
            {
                return;
            }
            if (UnidadesPuntual > 0 && !await _dialogos.ShowConfirmationAsync("Reposición puntual",
                $"Se descartan {ProductosPuntual} productos sin mandar. ¿Salir?").ConfigureAwait(true))
            {
                return;
            }
            SalirDePuntual();
            Mensaje = _textoSinReposicion;
        }

        private void SalirDePuntual()
        {
            LimpiarPuntual();
            EnModoPuntual = false;
        }

        private void LimpiarPuntual()
        {
            foreach (LineaPuntualReposicion linea in LineasPuntual)
            {
                linea.PropertyChanged -= LineaPuntualCambiada;
            }
            LineasPuntual.Clear();
            _versionPuntual++;
            SeleccionadaPuntual = null;
            RefrescarTotalesPuntual();
        }

        private void QuitarLineaPuntual(LineaPuntualReposicion linea)
        {
            if (linea == null || !LineasPuntual.Contains(linea))
            {
                return;
            }
            linea.PropertyChanged -= LineaPuntualCambiada;
            _ = LineasPuntual.Remove(linea);
            RefrescarTotalesPuntual();
            PedirFocoEnLector?.Invoke();
        }

        /// <summary>
        /// Lo leído en la puntual: si ya está en la lista, suma lo que vale una lectura; si no, se busca en el servidor (la
        /// referencia o cualquiera de sus códigos de barras) y se añade con su stock en la tienda. Sin stock no entra.
        /// </summary>
        internal async Task LeerPuntualAsync(string lectura)
        {
            string codigo = lectura?.Trim();
            if (string.IsNullOrEmpty(codigo) || !EnModoPuntual)
            {
                return;
            }
            List<LineaPuntualReposicion> yaEstan = LineasPuntual.Where(l => l.TieneCodigo(codigo)).ToList();
            if (yaEstan.Count == 1)
            {
                Sumar(yaEstan[0], yaEstan[0].UnidadesPorLectura);
                return;
            }
            List<ProductoReposicionPuntual> encontrados = null;
            int version = _versionPuntual;
            await Ocupado(async () =>
            {
                encontrados = await _servicio.BuscarProductoPuntual(Empresa, Almacen, codigo).ConfigureAwait(true);
            }, avisarEnDialogo: false).ConfigureAwait(true);
            if (version != _versionPuntual || !EnModoPuntual)
            {
                // Revisión 13/10/26: mientras buscaba se ha salido de esa puntual (o se ha empezado otra): no es de esta
                return;
            }
            if (encontrados == null)
            {
                // No ha contestado: Ocupado ya ha puesto el motivo
                _sonidos?.Error();
                PedirFocoEnLector?.Invoke();
                return;
            }
            if (encontrados.Count == 0)
            {
                LecturaFallida($"No hay ningún producto con el código o la referencia {codigo}.");
                return;
            }
            if (encontrados.Count > 1)
            {
                LecturaFallida($"El código {codigo} lo comparten varios productos ({string.Join(", ", encontrados.Select(p => p.Producto?.Trim()))}): " +
                    "escribe la referencia del que es.");
                return;
            }
            ProductoReposicionPuntual producto = encontrados[0];
            int porLectura = Math.Max(1, producto.UnidadesPorLectura);
            LineaPuntualReposicion existente = LineasPuntual.FirstOrDefault(l => string.Equals(l.Producto, producto.Producto?.Trim(), StringComparison.OrdinalIgnoreCase));
            if (existente != null)
            {
                Sumar(existente, porLectura);
                return;
            }
            if (producto.StockOrigen <= 0)
            {
                LecturaFallida($"{producto.Producto?.Trim()} ({producto.Nombre?.Trim()}) no tiene stock en {Almacen}: no se puede mandar.");
                return;
            }
            if ((producto.Disponible ?? producto.StockOrigen) <= 0)
            {
                // Sugerencia 581 (Carlos, 13/10/26): lo que necesitan los pedidos de la tienda no se manda
                LecturaFallida($"{producto.Producto?.Trim()} ({producto.Nombre?.Trim()}): las {producto.StockOrigen} que hay en {Almacen} " +
                    "las necesitan sus pedidos pendientes: no se puede mandar.");
                return;
            }
            var nueva = new LineaPuntualReposicion
            {
                Producto = producto.Producto?.Trim(),
                Nombre = producto.Nombre?.Trim(),
                CodigoBarras = producto.CodigoBarras?.Trim(),
                UnidadesPorLectura = porLectura,
                StockOrigen = producto.StockOrigen,
                Comprometido = producto.Comprometido,
                Disponible = producto.Disponible ?? producto.StockOrigen,
                Cantidad = porLectura
            };
            nueva.PropertyChanged += LineaPuntualCambiada;
            LineasPuntual.Add(nueva);
            RefrescarTotalesPuntual();
            Leida(nueva);
        }

        private void Sumar(LineaPuntualReposicion linea, int unidades)
        {
            linea.Cantidad += unidades;
            Leida(linea);
        }

        private void Leida(LineaPuntualReposicion linea)
        {
            Mensaje = !linea.FaltaStock
                ? TEXTO_PUNTUAL
                : linea.Comprometido > 0
                    ? $"De {linea.Producto} puedes mandar {Math.Max(0, linea.Disponible)} (hay {linea.StockOrigen} en la tienda y {linea.Comprometido} son para sus pedidos) " +
                      $"y pones {linea.Cantidad}: baja la cantidad."
                    : $"De {linea.Producto} hay {linea.StockOrigen} en la tienda y pones {linea.Cantidad}: baja la cantidad.";
            if (linea.FaltaStock)
            {
                _sonidos?.Error();
            }
            else
            {
                _sonidos?.Correcto();
            }
            SeleccionadaPuntual = linea;
            // El cursor se queda en el lector: con el lector de códigos cada lectura suma (la cantidad se cambia en la rejilla)
            PedirFocoEnLector?.Invoke();
        }

        internal string TextoConfirmacionPuntual()
            => $"Se mandan {UnidadesPuntual} unidades de {ProductosPuntual} productos a Algete en una reposición aparte de la del calendario. ¿Mandar?";

        internal static string TextoResultadoPuntual(ResultadoTerminarReposicion resultado)
        {
            int productos = resultado?.Lineas?.Count ?? 0;
            return $"Reposición puntual enviada: traspaso {resultado?.NumTraspaso}, {resultado?.Unidades ?? 0} unidades de {productos} productos camino de Algete. " +
                "Ponlas en la caja que se lleva el repartidor.";
        }

        private async Task EnviarPuntualAsync()
        {
            if (!EnModoPuntual)
            {
                return;
            }
            List<LineaPuntualReposicion> conCantidad = LineasPuntual.Where(l => l.Cantidad > 0).ToList();
            if (!conCantidad.Any())
            {
                Mensaje = "Pon al menos un producto con cantidad.";
                return;
            }
            List<LineaPuntualReposicion> sinStock = conCantidad.Where(l => l.FaltaStock).ToList();
            if (sinStock.Any())
            {
                Mensaje = "No hay stock suficiente en la tienda: " + string.Join(", ", sinStock.Select(l => l.TextoFalta())) + ".";
                return;
            }
            // Sugerencia 581: justo antes de mandarla, por si mientras tanto han marcado que el repartidor se la ha llevado
            await LeerEstadoPuntual().ConfigureAwait(true);
            if (AvisarSiPuntualBloqueada())
            {
                return;
            }
            if (!await _dialogos.ShowConfirmationAsync("Mandar a Algete", TextoConfirmacionPuntual()).ConfigureAwait(true))
            {
                return;
            }
            ResultadoTerminarReposicion enviada = null;
            await Ocupado(async () =>
            {
                ResultadoTerminarReposicion resultado = await _servicio.CrearPuntual(new CrearReposicion
                {
                    Empresa = Empresa,
                    Origen = Almacen,
                    Destino = Destino,
                    Lineas = conCantidad.Select(l => new LineaCrearReposicion { Producto = l.Producto, Cantidad = l.Cantidad }).ToList()
                }).ConfigureAwait(true);
                string texto = TextoResultadoPuntual(resultado);
                SalirDePuntual();
                Mensaje = texto;
                _dialogos.ShowNotification("Reposición puntual enviada", texto);
                enviada = resultado;
            }).ConfigureAwait(true);
            if (enviada != null)
            {
                // Nesto#530: ya se sabe el traspaso: los bultos y sus etiquetas
                await PedirBultosEImprimirAsync(enviada.NumTraspaso).ConfigureAwait(true);
                return;
            }
            if (EnModoPuntual)
            {
                // No se ha mandado (409…): que el botón diga lo que diga ahora la API, sin perder el motivo
                string error = Mensaje;
                await LeerEstadoPuntual().ConfigureAwait(true);
                Mensaje = error;
            }
        }

        private void LineaPuntualCambiada(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(LineaPuntualReposicion.Cantidad))
            {
                RefrescarTotalesPuntual();
            }
        }

        private void RefrescarTotalesPuntual()
        {
            OnPropertyChanged(nameof(UnidadesPuntual));
            OnPropertyChanged(nameof(ProductosPuntual));
            EnviarPuntualCommand?.NotifyCanExecuteChanged();
        }

        public int Unidades => Lineas.Sum(l => l.Cantidad);
        public int Productos => Lineas.Count(l => l.Cantidad > 0);

        private string _mensaje;
        public string Mensaje { get => _mensaje; set => SetProperty(ref _mensaje, value); }

        // Revisión 13/10/26: cuántas operaciones hay en marcha. Con dos a la vez (dos lecturas seguidas del lector en la
        // puntual) la primera que acaba no deja la pantalla como libre mientras la otra sigue.
        private int _operacionesEnCurso;

        private bool _estaOcupado;
        public bool EstaOcupado
        {
            get => _estaOcupado;
            private set
            {
                if (SetProperty(ref _estaOcupado, value))
                {
                    RefrescarEstado();
                }
            }
        }

        private string _lectura;
        /// <summary>El cuadro del lector: Intro lo busca (<see cref="LeerLecturaCommand"/>) y lo vacía.</summary>
        public string Lectura { get => _lectura; set => SetProperty(ref _lectura, value); }

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
        public IAsyncRelayCommand PrepararCommand { get; }
        /// <summary>Se lanza solo al cambiar la cantidad de una línea (Intro o salir del campo en la rejilla).</summary>
        public IAsyncRelayCommand<LineaEnviarReposicion> CambiarCantidadCommand { get; }
        public IRelayCommand<string> LeerCommand { get; }
        public IRelayCommand LeerLecturaCommand { get; }
        public IAsyncRelayCommand TerminarCommand { get; }
        /// <summary>Sugerencia 564: la reposición en papel, para prepararla a mano.</summary>
        public IAsyncRelayCommand ImprimirCommand { get; }

        public async Task CargarAsync()
        {
            Mensaje = null;
            await Ocupado(async () =>
            {
                _origenValido = false;
                AvisoAutomatica = null;
                string almacen = (await _configuracion.leerParametro(Empresa, Parametros.Claves.AlmacenPedidoVta).ConfigureAwait(true))?.Trim();
                if (string.IsNullOrWhiteSpace(almacen))
                {
                    Mensaje = "Tu usuario no tiene almacén (parámetro AlmacénPedidoVta): pide a la oficina que te lo pongan.";
                    return;
                }
                Almacen = almacen.ToUpperInvariant();
                if (Almacen == Constantes.Almacenes.ALMACEN_ALGETE)
                {
                    Mensaje = "Las reposiciones desde Algete se hacen en Ariadna.";
                    return;
                }
                _origenValido = true;
                _puedeRellenarManual = await _servicio.PuedeRellenarManual().ConfigureAwait(true);
                await LeerEstadoPuntual().ConfigureAwait(true);
                await LeerAvisosAutomatica().ConfigureAwait(true);
                ReposicionEnPreparacion reposicion = await _servicio.LeerEnPreparacion(Empresa, Almacen).ConfigureAwait(true);
                Ensenar(reposicion);
                if (HayReposicion && EnModoPuntual)
                {
                    // Sugerencia 581: ha llegado la del calendario: primero se manda esa (la API no deja hacer la puntual)
                    SalirDePuntual();
                }
                if (!HayReposicion)
                {
                    _textoSinReposicion = _puedeRellenarManual
                        ? $"{Almacen} no tiene ninguna reposición en preparación. Pulsa «Preparar reposición» y se propone lo que hay que mandar a Algete."
                        : TextoSeRellenaSola(await LeerCierreProximaReposicion().ConfigureAwait(true), Ahora());
                    Mensaje = EnModoPuntual ? TEXTO_PUNTUAL : _textoSinReposicion;
                }
            }, avisarEnDialogo: false).ConfigureAwait(true);
            RefrescarEstado();
        }

        // ---------------------------------------------------------------- NestoAPI#622: lo que ha hecho hoy la API con la de la tienda

        private string _avisoAutomatica;
        /// <summary>
        /// NestoAPI#622: arriba, lo que ha hecho hoy la API al rellenar la reposición de la tienda cuando hay algo que decir: que
        /// la de hoy no se ha rellenado (y por qué) o que se ha sumado a una que la tienda tenía sin enviar. Null si nada.
        /// </summary>
        public string AvisoAutomatica
        {
            get => _avisoAutomatica;
            private set
            {
                if (SetProperty(ref _avisoAutomatica, value))
                {
                    OnPropertyChanged(nameof(HayAvisoAutomatica));
                }
            }
        }

        public bool HayAvisoAutomatica => !string.IsNullOrWhiteSpace(AvisoAutomatica);

        private async Task LeerAvisosAutomatica()
        {
            List<AvisoReposicionAutomatica> avisos;
            try
            {
                avisos = await _servicio.LeerAvisosAutomatica(Empresa, Almacen).ConfigureAwait(true);
            }
            catch (Exception)
            {
                // Solo es para avisar: si no se sabe, no se dice nada
                avisos = null;
            }
            AvisoAutomatica = TextoAvisos(avisos);
        }

        /// <summary>Un aviso por línea; null si no hay ninguno.</summary>
        internal static string TextoAvisos(IEnumerable<AvisoReposicionAutomatica> avisos)
        {
            List<string> textos = (avisos ?? Enumerable.Empty<AvisoReposicionAutomatica>())
                .Select(a => a?.Texto?.Trim())
                .Where(t => !string.IsNullOrEmpty(t))
                .ToList();
            return textos.Any() ? string.Join(Environment.NewLine, textos) : null;
        }

        /// <summary>Cuándo rellena la API la próxima reposición a Algete (null si la ruta no tiene calendario o no se sabe).</summary>
        private async Task<DateTime?> LeerCierreProximaReposicion()
        {
            try
            {
                ProximaReposicion proxima = await _servicio.LeerProximaLlegada(Empresa, Almacen, Destino).ConfigureAwait(true);
                return proxima?.CierraEl;
            }
            catch (Exception)
            {
                // Es solo para decir la hora: si no se sabe, se dice «a su hora»
                return null;
            }
        }

        internal static string TextoSeRellenaSola(DateTime? cierraEl, DateTime ahora)
        {
            const string inicio = "Todavía no hay reposición para Algete.";
            if (cierraEl == null)
            {
                return $"{inicio} Se rellena sola a su hora.";
            }
            DateTime cierre = cierraEl.Value;
            string dia = cierre.Date == ahora.Date ? "hoy"
                : cierre.Date == ahora.Date.AddDays(1) ? "mañana"
                : "el " + cierre.ToString("dddd d/M", CultureInfo.GetCultureInfo("es-ES"));
            return $"{inicio} Se rellena sola {dia} a las {cierre:HH:mm}.";
        }

        private async Task PrepararAsync()
        {
            Mensaje = null;
            await Ocupado(async () =>
            {
                ReposicionEnPreparacion creada;
                try
                {
                    creada = await _servicio.Crear(new CrearReposicion
                    {
                        Empresa = Empresa,
                        Origen = Almacen,
                        Destino = Destino,
                        Herramienta = HERRAMIENTA
                    }).ConfigureAwait(true);
                }
                catch (EnvioReposicionException ex) when (ex.EsSinPermiso)
                {
                    // Le han quitado el permiso (o la pantalla se cargó antes): el botón deja de verse
                    _puedeRellenarManual = false;
                    throw;
                }
                Ensenar(creada);
                if (!HayReposicion)
                {
                    Mensaje = $"No hay nada que mandar de {Almacen} a Algete.";
                }
            }).ConfigureAwait(true);
        }

        private async Task CambiarCantidadAsync(LineaEnviarReposicion linea)
        {
            if (linea == null || !HayReposicion || linea.Cantidad == linea.Preparada)
            {
                return;
            }
            if (linea.Cantidad > linea.Preparada)
            {
                Mensaje = $"Solo se puede bajar la cantidad: de {linea.Producto} hay preparadas {linea.Preparada}.";
                Restaurar(linea);
                return;
            }
            Mensaje = null;
            bool hecho = false;
            await Ocupado(async () =>
            {
                ReposicionEnPreparacion reposicion = await _servicio.CambiarCantidad(Empresa, Almacen, linea.NumeroOrden, linea.Cantidad).ConfigureAwait(true);
                Actualizar(reposicion);
                hecho = true;
            }).ConfigureAwait(true);
            if (!hecho)
            {
                Restaurar(linea);
            }
        }

        /// <summary>Lo que manda el lector (o se teclea): si el código está en la lista, el foco va a su cantidad.</summary>
        private void Leer(string lectura)
        {
            string codigo = lectura?.Trim();
            if (string.IsNullOrEmpty(codigo))
            {
                return;
            }
            if (EnModoPuntual)
            {
                // Sugerencia 581: en la puntual se busca en el servidor (no está en ninguna lista)
                LecturaPuntualEnCurso = LeerPuntualAsync(codigo);
                return;
            }
            if (!HayReposicion)
            {
                // Incidencia 505: lo leído no puede desaparecer sin decir nada
                LecturaFallida(_puedeRellenarManual
                    ? "Primero prepara la reposición (botón «Preparar reposición») y después lee los códigos."
                    : _textoSinReposicion ?? TextoSeRellenaSola(null, Ahora()));
                return;
            }
            List<LineaEnviarReposicion> candidatas = Lineas.Where(l => l.TieneCodigo(codigo)).ToList();
            if (candidatas.Count == 0)
            {
                LecturaFallida($"El código {codigo} no está en esta reposición.");
                return;
            }
            if (candidatas.Count > 1)
            {
                LecturaFallida($"El código {codigo} lo comparten varios productos: elige en la lista el que es.");
                return;
            }
            Mensaje = null;
            _sonidos?.Correcto();
            Seleccionada = candidatas[0];
            PedirFocoEnCantidad?.Invoke(candidatas[0]);
        }

        /// <summary>Lo leído no entra: se dice, suena el error y el cursor se queda en el lector para la siguiente lectura.</summary>
        private void LecturaFallida(string mensaje)
        {
            Mensaje = mensaje;
            _sonidos?.Error();
            PedirFocoEnLector?.Invoke();
        }

        /// <summary>
        /// Sugerencia 564 (Paloma): la reposición en preparación en un PDF (lo genera la API, como los demás informes) para
        /// prepararla a mano mientras la tienda no tiene Ariadna. Lo que se imprime es lo que tiene el servidor.
        /// </summary>
        internal async Task ImprimirAsync()
        {
            if (!HayReposicion)
            {
                return;
            }
            Mensaje = null;
            await Ocupado(async () =>
            {
                byte[] pdf = await _servicio.DescargarListadoPdf(Empresa, Almacen).ConfigureAwait(true);
                string error = AbridorFicheros.GuardarYAbrir(pdf, $"Reposicion_{Almacen}", _abrirFichero);
                if (error != null)
                {
                    Mensaje = error;
                    _dialogos.ShowError(error);
                }
            }).ConfigureAwait(true);
        }

        private async Task TerminarAsync()
        {
            if (!HayReposicion)
            {
                return;
            }
            if (Unidades == 0)
            {
                Mensaje = "Todas las líneas están a 0: no hay nada que mandar a Algete.";
                return;
            }
            if (!await _dialogos.ShowConfirmationAsync("Terminar y mandar a Algete", TextoConfirmacion()).ConfigureAwait(true))
            {
                return;
            }
            ResultadoTerminarReposicion terminada = null;
            await Ocupado(async () =>
            {
                ResultadoTerminarReposicion resultado = await _servicio.Terminar(Empresa, Almacen).ConfigureAwait(true);
                string texto = TextoResultado(resultado);
                Ensenar(null);
                Mensaje = texto;
                _dialogos.ShowNotification("Reposición terminada", texto);
                // Sugerencia 581: enviada la de hoy, ya se puede hacer la puntual y marcar la recogida
                await LeerEstadoPuntual().ConfigureAwait(true);
                // NestoAPI#622: enviada, el aviso de «tienes una sin enviar» ya no vale
                await LeerAvisosAutomatica().ConfigureAwait(true);
                terminada = resultado;
            }).ConfigureAwait(true);
            if (terminada != null)
            {
                // Nesto#530: ya se sabe el traspaso: los bultos y sus etiquetas
                await PedirBultosEImprimirAsync(terminada.NumTraspaso).ConfigureAwait(true);
            }
        }

        // ---------------------------------------------------------------- Nesto#530 (NestoAPI#645): bultos y etiquetas

        internal const int MAXIMO_BULTOS = 50;
        internal const string TITULO_BULTOS = "Bultos de la reposición";
        internal const string TITULO_ETIQUETAS = "Etiquetas de los bultos";
        internal const string TEXTO_SIN_BULTOS = "No se han hecho etiquetas de bultos: esta reposición se recibirá en Algete sin leer la etiqueta.";

        /// <summary>El traspaso cuyas etiquetas no se han podido imprimir (los bultos ya están creados). Null: ninguno.</summary>
        private string _traspasoSinImprimir;

        /// <summary>Se ve «Reimprimir etiquetas»: los bultos de la última reposición están creados pero sus etiquetas no han salido.</summary>
        public bool PuedeReimprimirEtiquetas => !string.IsNullOrEmpty(_traspasoSinImprimir);

        public IAsyncRelayCommand ReimprimirEtiquetasCommand { get; }

        internal static string PreguntaBultos(string traspaso)
            => $"¿Cuántos bultos lleva la reposición {traspaso}? (de 1 a {MAXIMO_BULTOS}; se imprime una etiqueta por bulto)";

        /// <summary>«3» → 3; vacío, no numérico o fuera de 1-50 → null.</summary>
        internal static int? LeerNumeroBultos(string texto)
            => int.TryParse(texto?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int bultos) && bultos >= 1 && bultos <= MAXIMO_BULTOS
                ? bultos
                : (int?)null;

        internal static string TextoEtiquetasImpresas(BultosReposicion resultado)
        {
            int etiquetas = resultado?.Bultos?.Count ?? 0;
            string impresora = resultado?.Impresora?.Trim();
            string texto = (etiquetas == 1 ? "Se ha impreso 1 etiqueta" : $"Se han impreso {etiquetas} etiquetas")
                + (string.IsNullOrEmpty(impresora) ? "." : $" en {impresora}.");
            IEnumerable<string> avisos = (resultado?.Avisos ?? new List<string>()).Where(a => !string.IsNullOrWhiteSpace(a)).Select(a => a.Trim());
            return string.Join(" ", new[] { texto }.Concat(avisos));
        }

        /// <summary>
        /// Nesto#530: al mandar la reposición (ya se sabe el traspaso) se pregunta cuántos bultos lleva y la API imprime una
        /// etiqueta por bulto. Si se cancela no se crean bultos: esa reposición se recibirá sin leer la etiqueta.
        /// </summary>
        private async Task PedirBultosEImprimirAsync(int numTraspaso)
        {
            string traspaso = numTraspaso.ToString(CultureInfo.InvariantCulture);
            string enviada = Mensaje;
            string pregunta = PreguntaBultos(traspaso);
            int? bultos = null;
            for (int intento = 0; intento < 3 && bultos == null; intento++)
            {
                string tecleado = _dialogos.GetText(TITULO_BULTOS, pregunta, "1");
                if (string.IsNullOrWhiteSpace(tecleado))
                {
                    break;
                }
                bultos = LeerNumeroBultos(tecleado);
                pregunta = $"«{tecleado.Trim()}» no vale: escriba un número de 1 a {MAXIMO_BULTOS}. " + PreguntaBultos(traspaso);
            }
            string texto = bultos == null
                ? TEXTO_SIN_BULTOS
                : await ImprimirEtiquetasAsync(traspaso, () => _servicio.CrearBultos(Empresa, traspaso, bultos.Value)).ConfigureAwait(true);
            Mensaje = string.IsNullOrWhiteSpace(enviada) ? texto : $"{enviada} {texto}";
        }

        private async Task ReimprimirEtiquetasAsync()
        {
            string traspaso = _traspasoSinImprimir;
            if (string.IsNullOrEmpty(traspaso))
            {
                return;
            }
            Mensaje = await ImprimirEtiquetasAsync(traspaso, () => _servicio.ReimprimirBultos(Empresa, traspaso)).ConfigureAwait(true);
        }

        /// <summary>
        /// Crea los bultos (o los reimprime) y devuelve lo que hay que decir. Si la impresora falla (502) los bultos ya están
        /// creados: se ofrece reimprimir, y si no, queda el botón «Reimprimir etiquetas».
        /// </summary>
        private async Task<string> ImprimirEtiquetasAsync(string traspaso, Func<Task<BultosReposicion>> llamada)
        {
            while (true)
            {
                string motivoImpresora;
                _operacionesEnCurso++;
                EstaOcupado = true;
                try
                {
                    BultosReposicion resultado = await llamada().ConfigureAwait(true);
                    TraspasoSinImprimir(null);
                    return TextoEtiquetasImpresas(resultado);
                }
                catch (EnvioReposicionException ex) when (ex.Codigo == 502)
                {
                    TraspasoSinImprimir(traspaso);
                    motivoImpresora = ex.Message?.Trim();
                }
                catch (EnvioReposicionException ex)
                {
                    _dialogos.ShowError(ex.Message);
                    return ex.Message;
                }
                catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
                {
                    string texto = "No se ha podido conectar con el servidor para hacer las etiquetas de los bultos.";
                    _dialogos.ShowError(texto);
                    return texto;
                }
                finally
                {
                    _operacionesEnCurso--;
                    EstaOcupado = _operacionesEnCurso > 0;
                }
                if (!await _dialogos.ShowConfirmationAsync(TITULO_ETIQUETAS,
                    $"{motivoImpresora}{Environment.NewLine}Los bultos ya están creados. ¿Reimprimir las etiquetas?").ConfigureAwait(true))
                {
                    return $"{motivoImpresora} Los bultos ya están creados: pulse «Reimprimir etiquetas» cuando la impresora esté lista.";
                }
                llamada = () => _servicio.ReimprimirBultos(Empresa, traspaso);
            }
        }

        private void TraspasoSinImprimir(string traspaso)
        {
            _traspasoSinImprimir = traspaso;
            OnPropertyChanged(nameof(PuedeReimprimirEtiquetas));
            ReimprimirEtiquetasCommand?.NotifyCanExecuteChanged();
        }

        internal string TextoConfirmacion()
            => $"Se mandan {Unidades} unidades de {Productos} productos a Algete. ¿Terminar?";

        internal static string TextoResultado(ResultadoTerminarReposicion resultado)
        {
            int productos = resultado?.Lineas?.Count ?? 0;
            return $"Reposición terminada: traspaso {resultado?.NumTraspaso}, {resultado?.Unidades ?? 0} unidades de {productos} productos camino de Algete.";
        }

        /// <summary>Enseña la reposición tal y como la tiene el servidor (null: ninguna).</summary>
        private void Ensenar(ReposicionEnPreparacion reposicion)
        {
            foreach (LineaEnviarReposicion linea in Lineas)
            {
                linea.PropertyChanged -= LineaCambiada;
            }
            Lineas.Clear();
            Seleccionada = null;
            foreach (LineaReposicionEnPreparacion linea in reposicion?.Lineas ?? new List<LineaReposicionEnPreparacion>())
            {
                var nueva = new LineaEnviarReposicion
                {
                    NumeroOrden = linea.NumeroOrden,
                    Producto = linea.Producto?.Trim(),
                    Nombre = linea.Nombre?.Trim(),
                    CodigoBarras = linea.CodigoBarras?.Trim(),
                    StockOrigen = linea.StockOrigen,
                    Preparada = linea.Cantidad,
                    Cantidad = linea.Cantidad
                };
                nueva.PropertyChanged += LineaCambiada;
                Lineas.Add(nueva);
            }
            HayReposicion = Lineas.Count > 0;
            RefrescarTotales();
        }

        /// <summary>Pone lo que contesta el servidor sin rehacer la lista (la rejilla puede estar terminando de editar).</summary>
        private void Actualizar(ReposicionEnPreparacion reposicion)
        {
            if (reposicion?.Lineas == null)
            {
                return;
            }
            Dictionary<int, LineaReposicionEnPreparacion> delServidor = reposicion.Lineas
                .GroupBy(l => l.NumeroOrden).ToDictionary(g => g.Key, g => g.First());
            if (delServidor.Count != Lineas.Count || Lineas.Any(l => !delServidor.ContainsKey(l.NumeroOrden)))
            {
                Ensenar(reposicion);
                return;
            }
            _aplicandoServidor = true;
            try
            {
                foreach (LineaEnviarReposicion linea in Lineas)
                {
                    LineaReposicionEnPreparacion servidor = delServidor[linea.NumeroOrden];
                    linea.Preparada = servidor.Cantidad;
                    linea.Cantidad = servidor.Cantidad;
                    linea.StockOrigen = servidor.StockOrigen;
                }
            }
            finally
            {
                _aplicandoServidor = false;
            }
            RefrescarTotales();
        }

        private void Restaurar(LineaEnviarReposicion linea)
        {
            _aplicandoServidor = true;
            try
            {
                linea.Cantidad = linea.Preparada;
            }
            finally
            {
                _aplicandoServidor = false;
            }
            RefrescarTotales();
        }

        private void LineaCambiada(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(LineaEnviarReposicion.Cantidad))
            {
                return;
            }
            RefrescarTotales();
            if (!_aplicandoServidor && sender is LineaEnviarReposicion linea)
            {
                CambiarCantidadCommand.Execute(linea);
            }
        }

        private void RefrescarTotales()
        {
            OnPropertyChanged(nameof(Unidades));
            OnPropertyChanged(nameof(Productos));
        }

        private void RefrescarEstado()
        {
            OnPropertyChanged(nameof(PuedePreparar));
            OnPropertyChanged(nameof(PuedeEmpezarPuntual));
            OnPropertyChanged(nameof(MostrarBotonPuntual));
            OnPropertyChanged(nameof(PuntualBloqueada));
            OnPropertyChanged(nameof(AvisoPuntual));
            OnPropertyChanged(nameof(ToolTipPuntual));
            OnPropertyChanged(nameof(MostrarBotonRecogida));
            OnPropertyChanged(nameof(MostrarLector));
            PrepararCommand?.NotifyCanExecuteChanged();
            TerminarCommand?.NotifyCanExecuteChanged();
            ImprimirCommand?.NotifyCanExecuteChanged();
            EmpezarPuntualCommand?.NotifyCanExecuteChanged();
            CancelarPuntualCommand?.NotifyCanExecuteChanged();
            EnviarPuntualCommand?.NotifyCanExecuteChanged();
            QuitarLineaPuntualCommand?.NotifyCanExecuteChanged();
            MarcarRecogidaCommand?.NotifyCanExecuteChanged();
            ReimprimirEtiquetasCommand?.NotifyCanExecuteChanged();
        }

        /// <summary>Los errores al escribir (preparar, cambiar, terminar) salen además en un diálogo, con el texto del servidor.</summary>
        private async Task Ocupado(Func<Task> accion, bool avisarEnDialogo = true)
        {
            _operacionesEnCurso++;
            EstaOcupado = true;
            try
            {
                await accion().ConfigureAwait(true);
            }
            catch (EnvioReposicionException ex) when (ex.EsSinPermiso)
            {
                // NestoAPI#577: no es un error, es que ese usuario no puede hacerlo; se dice tal cual lo dice el servidor
                Mensaje = ex.Message;
                if (avisarEnDialogo)
                {
                    _dialogos.ShowNotification(Titulo, ex.Message);
                }
            }
            catch (EnvioReposicionException ex)
            {
                Mensaje = ex.Message;
                if (avisarEnDialogo)
                {
                    _dialogos.ShowError(ex.Message);
                }
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
            {
                Mensaje = "No se puede conectar con el servidor. Vuelve a intentarlo.";
                if (avisarEnDialogo)
                {
                    _dialogos.ShowError(Mensaje);
                }
            }
            finally
            {
                _operacionesEnCurso--;
                EstaOcupado = _operacionesEnCurso > 0;
            }
        }
    }
}
