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

        /// <summary>Mientras se ponen las cantidades que manda el servidor (o se deshace un cambio) no se le vuelven a mandar.</summary>
        private bool _aplicandoServidor;

        public EnviarReposicionViewModel(IServicioEnvioReposiciones servicio, IServicioDialogos dialogos, IConfiguracion configuracion,
            ISonidosLector sonidos)
            : this(servicio, dialogos, configuracion, sonidos, null)
        {
        }

        /// <param name="abrirFichero">Abre el PDF descargado (los tests lo cambian). Null: con el visor del sistema.</param>
        internal EnviarReposicionViewModel(IServicioEnvioReposiciones servicio, IServicioDialogos dialogos, IConfiguracion configuracion,
            ISonidosLector sonidos, Action<string> abrirFichero)
        {
            _servicio = servicio;
            _dialogos = dialogos;
            _configuracion = configuracion;
            _sonidos = sonidos;
            _abrirFichero = abrirFichero ?? AbridorFicheros.AbrirConElVisorDelSistema;
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

        public bool PuedePreparar => _origenValido && _puedeRellenarManual && !HayReposicion && !EstaOcupado;

        public int Unidades => Lineas.Sum(l => l.Cantidad);
        public int Productos => Lineas.Count(l => l.Cantidad > 0);

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
                    RefrescarEstado();
                }
            }
        }

        private string _lectura;
        /// <summary>El cuadro del lector: Intro lo busca (<see cref="LeerLecturaCommand"/>) y lo vacía.</summary>
        public string Lectura { get => _lectura; set => SetProperty(ref _lectura, value); }

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
                ReposicionEnPreparacion reposicion = await _servicio.LeerEnPreparacion(Empresa, Almacen).ConfigureAwait(true);
                Ensenar(reposicion);
                if (!HayReposicion)
                {
                    _textoSinReposicion = _puedeRellenarManual
                        ? $"{Almacen} no tiene ninguna reposición en preparación. Pulsa «Preparar reposición» y se propone lo que hay que mandar a Algete."
                        : TextoSeRellenaSola(await LeerCierreProximaReposicion().ConfigureAwait(true), Ahora());
                    Mensaje = _textoSinReposicion;
                }
            }, avisarEnDialogo: false).ConfigureAwait(true);
            RefrescarEstado();
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
            await Ocupado(async () =>
            {
                ResultadoTerminarReposicion resultado = await _servicio.Terminar(Empresa, Almacen).ConfigureAwait(true);
                string texto = TextoResultado(resultado);
                Ensenar(null);
                Mensaje = texto;
                _dialogos.ShowNotification("Reposición terminada", texto);
            }).ConfigureAwait(true);
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
            PrepararCommand?.NotifyCanExecuteChanged();
            TerminarCommand?.NotifyCanExecuteChanged();
            ImprimirCommand?.NotifyCanExecuteChanged();
        }

        /// <summary>Los errores al escribir (preparar, cambiar, terminar) salen además en un diálogo, con el texto del servidor.</summary>
        private async Task Ocupado(Func<Task> accion, bool avisarEnDialogo = true)
        {
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
                EstaOcupado = false;
            }
        }
    }
}
