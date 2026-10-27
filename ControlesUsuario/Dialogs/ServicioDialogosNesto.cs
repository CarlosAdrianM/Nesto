using Nesto.Infrastructure.Contracts;
using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Data;

namespace ControlesUsuario.Dialogs
{
    /// <summary>
    /// Nesto#490 (4C.2, paso 3): <see cref="IServicioDialogos"/> con una ventana propia en vez del
    /// IDialogService de Prism. Reproduce lo que hace el DialogService de Prism 8.1.97 (mismo orden
    /// de eventos, misma ventana) para que el cambio no se note:
    /// - Ventana con el título enlazado a <c>Title</c> del ViewModel, centrada sobre la ventana
    ///   activa (que es su dueña) y ajustada al contenido, salvo que la vista traiga su propio
    ///   estilo de ventana (<see cref="DialogoNesto.EstiloVentanaProperty"/>, antes <c>prism:Dialog.WindowStyle</c>),
    ///   que la sustituye entera (como en Prism).
    /// - <c>OnDialogOpened</c> antes de enseñarla; <c>RequestClose</c> se escucha desde que se ha
    ///   cargado; <c>CanCloseDialog</c> puede impedir el cierre (también con la X); al cerrar,
    ///   <c>OnDialogClosed</c> y el callback (con <see cref="ResultadoBoton.None"/> si se cerró sin
    ///   resultado).
    ///
    /// Es la de todos desde el 14/10/26 (antes, piloto por usuario con el parámetro VentanaDialogosPropia).
    /// La vista y su ViewModel (un <see cref="IDialogoNesto"/>) los da <c>resolverVista</c>: en la aplicación,
    /// <see cref="RegistroDialogos.CrearDialogo"/> con lo registrado con <see cref="RegistroDialogos.RegistrarDialogo"/>
    /// (antes, RegisterDialog de Prism y el AutoWire de su ViewModelLocator).
    /// </summary>
    public class ServicioDialogosNesto : ServicioDialogosBase
    {
        private readonly Func<string, object> _resolverVista;

        /// <param name="resolverVista">
        /// Devuelve la vista registrada con ese nombre (una nueva cada vez), con su ViewModel como DataContext;
        /// null si no hay ninguna con ese nombre.
        /// </param>
        public ServicioDialogosNesto(Func<string, object> resolverVista)
        {
            _resolverVista = resolverVista ?? throw new ArgumentNullException(nameof(resolverVista));
        }

        public override void ShowDialog(string name, ParametrosDialogo parameters, Action<ResultadoDialogo> callback)
            => PrepararVentana(name, parameters, callback).ShowDialog();

        // No modal: por contrato se puede llamar desde cualquier hilo (avisos de tareas en segundo plano).
        public override void Show(string name, ParametrosDialogo parameters, Action<ResultadoDialogo> callback)
            => ServicioDialogosEnHiloUi.EnHiloUi(() => PrepararVentana(name, parameters, callback).Show());

        /// <summary>Crea la ventana con el diálogo dentro, lista para enseñar (modal o no).</summary>
        internal VentanaDialogo PrepararVentana(string name, ParametrosDialogo parameters, Action<ResultadoDialogo> callback)
        {
            parameters ??= new ParametrosDialogo();

            var ventana = new VentanaDialogo();
            if (!(_resolverVista(name) is FrameworkElement contenido))
            {
                throw new InvalidOperationException($"El diálogo «{name}» no está registrado o no es un FrameworkElement");
            }

            if (!(contenido.DataContext is IDialogoNesto dialogo))
            {
                throw new InvalidOperationException($"El ViewModel del diálogo «{name}» debe implementar IDialogoNesto");
            }

            ConfigurarEventos(ventana, dialogo, callback);

            Style estilo = DialogoNesto.GetEstiloVentana(contenido);
            if (estilo != null)
            {
                ventana.Style = estilo;
            }
            ventana.Content = contenido;
            ventana.DataContext = contenido.DataContext; // la ventana y el diálogo comparten DataContext (el Title)
            ventana.Owner ??= Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);

            dialogo.OnDialogOpened(parameters);
            return ventana;
        }

        private static void ConfigurarEventos(VentanaDialogo ventana, IDialogoNesto dialogo, Action<ResultadoDialogo> callback)
        {
            void alPedirCierre(ResultadoDialogo resultado)
            {
                ventana.Resultado = resultado;
                ventana.Close();
            }

            void alCargar(object sender, RoutedEventArgs e)
            {
                ventana.Loaded -= alCargar;
                dialogo.RequestClose += alPedirCierre;
            }

            void alCerrando(object sender, CancelEventArgs e)
            {
                if (!dialogo.CanCloseDialog())
                {
                    e.Cancel = true;
                }
            }

            void alCerrar(object sender, EventArgs e)
            {
                ventana.Closed -= alCerrar;
                ventana.Closing -= alCerrando;
                dialogo.RequestClose -= alPedirCierre;

                dialogo.OnDialogClosed();
                callback?.Invoke(ventana.Resultado ?? new ResultadoDialogo(ResultadoBoton.None));

                ventana.DataContext = null;
                ventana.Content = null;
            }

            ventana.Loaded += alCargar;
            ventana.Closing += alCerrando;
            ventana.Closed += alCerrar;
        }
    }

    /// <summary>
    /// Ventana que aloja un diálogo de <see cref="ServicioDialogosNesto"/>. La misma que el
    /// DialogWindow de Prism: título del ViewModel, centrada sobre su dueña, ajustada al contenido.
    /// </summary>
    internal class VentanaDialogo : Window
    {
        public const double ANCHO_MAXIMO_INICIAL = 600;

        private static readonly Style EstiloPorDefecto = CrearEstiloPorDefecto();

        public VentanaDialogo()
        {
            SetBinding(TitleProperty, new Binding(nameof(IDialogoNesto.Title)));
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Style = EstiloPorDefecto;
            ContentRendered += SoltarElTamanoInicial;
        }

        /// <summary>
        /// Carlos 05/10/26: con Prism un texto largo no salía entero y al agrandar la ventana el texto no se
        /// ajustaba. La ventana nace ajustada al contenido pero con un tope (<see cref="ANCHO_MAXIMO_INICIAL"/> de
        /// ancho y casi toda la pantalla de alto, para que el texto se envuelva y, si no cabe, salga la barra de
        /// desplazamiento); una vez pintada, se queda con ese ancho y se suelta el tope, así que el usuario la
        /// puede agrandar y el contenido se ajusta. Solo con el estilo por defecto: una vista con su propio estilo
        /// de ventana manda entero, como en Prism.
        ///
        /// Nesto#556 (sugerencia 757, Laura): el alto sigue ajustándose al contenido (<see cref="SizeToContent.Height"/>)
        /// en vez de quedarse fijo. «Parámetros de usuario» carga sus parámetros de la API DESPUÉS de pintarse: con el
        /// alto fijo la ventana no crecía y el botón Guardar quedaba cortado. El tope de alto se mantiene (casi toda la
        /// pantalla): si el contenido no cabe, la vista tiene que llevar su propio ScrollViewer en una fila * (como
        /// ParametrosUsuarioDialog y CopiarSeguimientosView). Si el usuario redimensiona la ventana, WPF pasa
        /// SizeToContent a Manual solo y se queda con el tamaño que él le dé.
        /// </summary>
        private void SoltarElTamanoInicial(object sender, EventArgs e)
        {
            ContentRendered -= SoltarElTamanoInicial;
            AjustarTamanoTrasPintar();
        }

        /// <summary>Lo que hace <see cref="SoltarElTamanoInicial"/> una vez pintada (separado para poder probarlo).</summary>
        internal void AjustarTamanoTrasPintar()
        {
            if (!ReferenceEquals(Style, EstiloPorDefecto))
            {
                return;
            }
            SizeToContent = SizeToContent.Height;
            MaxWidth = double.PositiveInfinity;
            SizeChanged += MantenerDentroDeLaPantalla;
            Closed += (s, e) => SizeChanged -= MantenerDentroDeLaPantalla;
            // Los diálogos de aviso llevan su propio tope (se les puso para la ventana de Prism, que crecía sin límite
            // con un texto largo; también sirve para que nazcan con ese ancho): una vez pintada ya no hace falta
            if (Content is FrameworkElement vista && vista.MaxWidth == ANCHO_MAXIMO_INICIAL)
            {
                vista.MaxWidth = double.PositiveInfinity;
            }
        }

        /// <summary>
        /// Nesto#556: la ventana nace centrada sobre su dueña y, al crecer con el contenido, crece hacia abajo; si se
        /// saliera del área de trabajo, se sube para que el borde de abajo (los botones) siga a la vista.
        /// </summary>
        private void MantenerDentroDeLaPantalla(object sender, SizeChangedEventArgs e)
        {
            if (!e.HeightChanged || double.IsNaN(Top) || double.IsNaN(ActualHeight))
            {
                return;
            }
            // Solo en el monitor principal (WorkArea es el suyo): en otro monitor no se sabe dónde acaba la pantalla
            Rect area = SystemParameters.WorkArea;
            if (area.Contains(new Point(Left, Top)) && Top + ActualHeight > area.Bottom)
            {
                Top = Math.Max(area.Top, area.Bottom - ActualHeight);
            }
        }

        /// <summary>Nesto#556: tope de alto de la ventana con el estilo por defecto (casi toda el área de trabajo).</summary>
        internal static double AltoMaximo() => Math.Max(300d, SystemParameters.WorkArea.Height * 0.85);

        /// <summary>Resultado con el que pidió cerrarse el diálogo; null si se cerró con la X.</summary>
        public ResultadoDialogo Resultado { get; set; }

        private static Style CrearEstiloPorDefecto()
        {
            var estilo = new Style(typeof(Window));
            estilo.Setters.Add(new Setter(SizeToContentProperty, SizeToContent.WidthAndHeight));
            estilo.Setters.Add(new Setter(ResizeModeProperty, ResizeMode.CanResize));
            estilo.Setters.Add(new Setter(MaxWidthProperty, ANCHO_MAXIMO_INICIAL));
            estilo.Setters.Add(new Setter(MaxHeightProperty, AltoMaximo()));
            estilo.Seal();
            return estilo;
        }
    }
}
