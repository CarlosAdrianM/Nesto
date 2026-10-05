using Nesto.Infrastructure.Contracts;
using Prism.Mvvm;
using Prism.Services.Dialogs;
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
    ///   <c>prism:Dialog.WindowStyle</c>, que la sustituye entera (como en Prism).
    /// - <c>OnDialogOpened</c> antes de enseñarla; <c>RequestClose</c> se escucha desde que se ha
    ///   cargado; <c>CanCloseDialog</c> puede impedir el cierre (también con la X); al cerrar,
    ///   <c>OnDialogClosed</c> y el callback (con <see cref="ResultadoBoton.None"/> si se cerró sin
    ///   resultado).
    ///
    /// Mientras dure la migración, la vista se sigue registrando con <c>RegisterDialog</c> de Prism
    /// (se obtiene con <c>resolverVista</c>) y su ViewModel puede ser <see cref="IDialogoNesto"/> o
    /// el <see cref="IDialogAware"/> de Prism de siempre.
    /// </summary>
    public class ServicioDialogosNesto : ServicioDialogosBase
    {
        private readonly Func<string, object> _resolverVista;

        /// <param name="resolverVista">Devuelve la vista registrada con ese nombre (una nueva cada vez).</param>
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

            // Igual que MvvmHelpers.AutowireViewModel de Prism: si la vista no trae ViewModel ni lo pide, se le pone.
            if (contenido.DataContext == null && ViewModelLocator.GetAutoWireViewModel(contenido) == null)
            {
                ViewModelLocator.SetAutoWireViewModel(contenido, true);
            }

            IDialogoNesto dialogo = contenido.DataContext switch
            {
                IDialogoNesto propio => propio,
                IDialogAware prism => new DialogoPrismComoNesto(prism),
                _ => throw new InvalidOperationException($"El ViewModel del diálogo «{name}» debe implementar IDialogoNesto")
            };

            ConfigurarEventos(ventana, dialogo, callback);

            Style estilo = Dialog.GetWindowStyle(contenido);
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
                (dialogo as DialogoPrismComoNesto)?.Desconectar();

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
        /// desplazamiento); una vez pintada, se queda con ese tamaño y se suelta el tope, así que el usuario la
        /// puede agrandar y el contenido se ajusta. Solo con el estilo por defecto: una vista con su propio estilo
        /// de ventana manda entero, como en Prism.
        /// </summary>
        private void SoltarElTamanoInicial(object sender, EventArgs e)
        {
            ContentRendered -= SoltarElTamanoInicial;
            if (!ReferenceEquals(Style, EstiloPorDefecto))
            {
                return;
            }
            SizeToContent = SizeToContent.Manual;
            MaxWidth = double.PositiveInfinity;
            MaxHeight = double.PositiveInfinity;
        }

        /// <summary>Resultado con el que pidió cerrarse el diálogo; null si se cerró con la X.</summary>
        public ResultadoDialogo Resultado { get; set; }

        private static Style CrearEstiloPorDefecto()
        {
            var estilo = new Style(typeof(Window));
            estilo.Setters.Add(new Setter(SizeToContentProperty, SizeToContent.WidthAndHeight));
            estilo.Setters.Add(new Setter(ResizeModeProperty, ResizeMode.CanResize));
            estilo.Setters.Add(new Setter(MaxWidthProperty, ANCHO_MAXIMO_INICIAL));
            estilo.Setters.Add(new Setter(MaxHeightProperty, Math.Max(300d, SystemParameters.WorkArea.Height * 0.85)));
            estilo.Seal();
            return estilo;
        }
    }

    /// <summary>
    /// Mientras dure la migración: un ViewModel de diálogo que sigue siendo <see cref="IDialogAware"/>
    /// de Prism, visto como <see cref="IDialogoNesto"/>. Traduce parámetros y resultado.
    /// </summary>
    internal class DialogoPrismComoNesto : IDialogoNesto
    {
        private readonly IDialogAware _prism;

        public DialogoPrismComoNesto(IDialogAware prism)
        {
            _prism = prism;
            _prism.RequestClose += AlPedirCierrePrism;
        }

        public string Title => _prism.Title;

        public event Action<ResultadoDialogo> RequestClose;

        public bool CanCloseDialog() => _prism.CanCloseDialog();

        public void OnDialogClosed() => _prism.OnDialogClosed();

        public void OnDialogOpened(ParametrosDialogo parameters)
            => _prism.OnDialogOpened(ServicioDialogosPrism.AParametrosPrism(parameters));

        /// <summary>Deja de escuchar al ViewModel de Prism (al cerrar la ventana).</summary>
        public void Desconectar() => _prism.RequestClose -= AlPedirCierrePrism;

        private void AlPedirCierrePrism(IDialogResult resultado)
            => RequestClose?.Invoke(ServicioDialogosPrism.DesdeResultadoPrism(resultado) ?? new ResultadoDialogo(ResultadoBoton.None));
    }
}
