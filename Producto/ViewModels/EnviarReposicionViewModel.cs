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
    /// </summary>
    public class EnviarReposicionViewModel : ObservableObject
    {
        private readonly IServicioEnvioReposiciones _servicio;
        private readonly IServicioDialogos _dialogos;
        private readonly IConfiguracion _configuracion;

        /// <summary>Mientras se ponen las cantidades que manda el servidor (o se deshace un cambio) no se le vuelven a mandar.</summary>
        private bool _aplicandoServidor;

        public EnviarReposicionViewModel(IServicioEnvioReposiciones servicio, IServicioDialogos dialogos, IConfiguracion configuracion)
        {
            _servicio = servicio;
            _dialogos = dialogos;
            _configuracion = configuracion;
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
        }

        public string Titulo => "Enviar reposición";

        public string Empresa { get; set; } = Constantes.Empresas.EMPRESA_DEFECTO;

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

        private bool _origenValido;

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

        public bool PuedePreparar => _origenValido && !HayReposicion && !EstaOcupado;

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
                ReposicionEnPreparacion reposicion = await _servicio.LeerEnPreparacion(Empresa, Almacen).ConfigureAwait(true);
                Ensenar(reposicion);
                if (!HayReposicion)
                {
                    Mensaje = $"{Almacen} no tiene ninguna reposición en preparación. Pulsa «Preparar reposición» y se propone lo que hay que mandar a Algete.";
                }
            }, avisarEnDialogo: false).ConfigureAwait(true);
            RefrescarEstado();
        }

        private async Task PrepararAsync()
        {
            Mensaje = null;
            await Ocupado(async () =>
            {
                ReposicionEnPreparacion creada = await _servicio.Crear(new CrearReposicion
                {
                    Empresa = Empresa,
                    Origen = Almacen,
                    Destino = Destino
                }).ConfigureAwait(true);
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
                Mensaje = "Primero prepara la reposición (botón «Preparar reposición») y después lee los códigos.";
                return;
            }
            List<LineaEnviarReposicion> candidatas = Lineas.Where(l => l.TieneCodigo(codigo)).ToList();
            if (candidatas.Count == 0)
            {
                Mensaje = $"El código {codigo} no está en esta reposición.";
                return;
            }
            if (candidatas.Count > 1)
            {
                Mensaje = $"El código {codigo} lo comparten varios productos: elige en la lista el que es.";
                return;
            }
            Mensaje = null;
            Seleccionada = candidatas[0];
            PedirFocoEnCantidad?.Invoke(candidatas[0]);
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
        }

        /// <summary>Los errores al escribir (preparar, cambiar, terminar) salen además en un diálogo, con el texto del servidor.</summary>
        private async Task Ocupado(Func<Task> accion, bool avisarEnDialogo = true)
        {
            EstaOcupado = true;
            try
            {
                await accion().ConfigureAwait(true);
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
