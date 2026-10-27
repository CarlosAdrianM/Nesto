using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Nesto.Infrastructure.Contracts
{
    /// <summary>
    /// Nesto#490 (panel lateral, tramo 1): una sección del menú lateral (Ventas, Clientes, Almacén…). Las secciones van
    /// por rol, no por las pestañas de la cinta: el reparto está en <c>MapaMenuPorRol</c>. Es C# puro (sin WPF-UI): el
    /// panel del ejecutable la pinta.
    /// </summary>
    public sealed class SeccionMenu : ObservableObject
    {
        private bool _hayAlgoQueMostrar;

        public SeccionMenu(string titulo, string icono, int orden)
        {
            Titulo = titulo ?? throw new ArgumentNullException(nameof(titulo));
            Icono = icono;
            Orden = orden;
        }

        public string Titulo { get; }

        /// <summary>Nombre del icono (un <c>SymbolRegular</c> de WPF-UI, como texto para no depender de él aquí).</summary>
        public string Icono { get; }

        public int Orden { get; }

        public ObservableCollection<GrupoMenu> Grupos { get; } = new ObservableCollection<GrupoMenu>();

        /// <summary>Todos los elementos de la sección, en orden (grupo a grupo).</summary>
        public IEnumerable<ElementoMenuBase> Elementos => Grupos.SelectMany(g => g.Elementos);

        /// <summary>true si algún elemento de la sección se puede mostrar (si no, la sección no se pinta).</summary>
        public bool HayAlgoQueMostrar
        {
            get => _hayAlgoQueMostrar;
            private set => SetProperty(ref _hayAlgoQueMostrar, value);
        }

        internal void Anadir(GrupoMenu grupo)
        {
            Grupos.Add(grupo);
            grupo.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(GrupoMenu.HayAlgoQueMostrar))
                {
                    Recalcular();
                }
            };
            Recalcular();
        }

        private void Recalcular() => HayAlgoQueMostrar = Grupos.Any(g => g.HayAlgoQueMostrar);
    }

    /// <summary>Nesto#490 (panel lateral): un grupo de elementos dentro de una sección.</summary>
    public sealed class GrupoMenu : ObservableObject
    {
        private bool _hayAlgoQueMostrar;

        public GrupoMenu(string titulo)
        {
            Titulo = titulo ?? string.Empty;
        }

        public string Titulo { get; }

        public ObservableCollection<ElementoMenuBase> Elementos { get; } = new ObservableCollection<ElementoMenuBase>();

        public bool HayAlgoQueMostrar
        {
            get => _hayAlgoQueMostrar;
            private set => SetProperty(ref _hayAlgoQueMostrar, value);
        }

        internal void Anadir(ElementoMenuBase elemento)
        {
            Elementos.Add(elemento);
            elemento.MostrarCambiado += (_, _) => Recalcular();
            Recalcular();
        }

        private void Recalcular() => HayAlgoQueMostrar = Elementos.Any(e => e.Mostrar);
    }

    /// <summary>
    /// Nesto#490 (panel lateral): lo común a un botón (<see cref="ElementoMenu"/>) y a lo que no es un botón
    /// (<see cref="ContenidoMenu"/>). Las propiedades son de dependencia para poder enlazarlas: en el tramo 1, el
    /// <c>LectorCinta</c> las enlaza con las del elemento de la cinta, así que siguen vivas (si el ViewModel cambia el
    /// texto o la visibilidad del botón, cambia también aquí).
    /// <para>
    /// Se ve si el elemento, su grupo y su pestaña están visibles (<see cref="Visibility.Visible"/>: Hidden y Collapsed
    /// cuentan como no visibles). <see cref="Mostrar"/> es lo que mira el panel.
    /// </para>
    /// </summary>
    public abstract class ElementoMenuBase : DependencyObject
    {
        protected ElementoMenuBase(string clave)
        {
            Clave = clave ?? throw new ArgumentNullException(nameof(clave));
        }

        /// <summary>«Pestaña › Grupo › Botón» en la cinta: la clave con la que se busca en <c>MapaMenuPorRol</c>.</summary>
        public string Clave { get; }

        /// <summary>El DataContext del elemento en la cinta (el ViewModel del módulo que lo puso): la misma instancia.</summary>
        public object Fuente { get; set; }

        /// <summary>El elemento de la cinta del que sale (tramo 1). Null cuando el módulo declare su menú.</summary>
        public object Origen { get; set; }

        /// <summary>La sección y el grupo en los que lo ha puesto el mapa (o «Otros» si no estaba en él).</summary>
        public string Seccion { get; internal set; }

        public string Grupo { get; internal set; }

        public int Orden { get; internal set; }

        /// <summary>Se lanza cuando cambia <see cref="Mostrar"/>.</summary>
        public event EventHandler MostrarCambiado;

        public static readonly DependencyProperty TituloProperty = DependencyProperty.Register(nameof(Titulo),
            typeof(string), typeof(ElementoMenuBase), new PropertyMetadata(string.Empty));

        public string Titulo
        {
            get => (string)GetValue(TituloProperty);
            set => SetValue(TituloProperty, value);
        }

        public static readonly DependencyProperty AyudaProperty = DependencyProperty.Register(nameof(Ayuda),
            typeof(object), typeof(ElementoMenuBase), new PropertyMetadata(null));

        /// <summary>El ToolTip del botón en la cinta.</summary>
        public object Ayuda
        {
            get => GetValue(AyudaProperty);
            set => SetValue(AyudaProperty, value);
        }

        public static readonly DependencyProperty IconoProperty = DependencyProperty.Register(nameof(Icono),
            typeof(ImageSource), typeof(ElementoMenuBase), new PropertyMetadata(null));

        /// <summary>La imagen del botón en la cinta (LargeImageSource), si tiene.</summary>
        public ImageSource Icono
        {
            get => (ImageSource)GetValue(IconoProperty);
            set => SetValue(IconoProperty, value);
        }

        public static readonly DependencyProperty VisibilidadProperty = DependencyProperty.Register(nameof(Visibilidad),
            typeof(Visibility), typeof(ElementoMenuBase), new PropertyMetadata(Visibility.Visible, AlCambiarCondicion));

        /// <summary>La visibilidad del propio elemento.</summary>
        public Visibility Visibilidad
        {
            get => (Visibility)GetValue(VisibilidadProperty);
            set => SetValue(VisibilidadProperty, value);
        }

        public static readonly DependencyProperty VisibilidadGrupoProperty = DependencyProperty.Register(nameof(VisibilidadGrupo),
            typeof(Visibility), typeof(ElementoMenuBase), new PropertyMetadata(Visibility.Visible, AlCambiarCondicion));

        /// <summary>La visibilidad de su grupo en la cinta (varios menús la controlan en el grupo: VideosVisible…).</summary>
        public Visibility VisibilidadGrupo
        {
            get => (Visibility)GetValue(VisibilidadGrupoProperty);
            set => SetValue(VisibilidadGrupoProperty, value);
        }

        public static readonly DependencyProperty VisibilidadPestanaProperty = DependencyProperty.Register(nameof(VisibilidadPestana),
            typeof(Visibility), typeof(ElementoMenuBase), new PropertyMetadata(Visibility.Visible, AlCambiarCondicion));

        public Visibility VisibilidadPestana
        {
            get => (Visibility)GetValue(VisibilidadPestanaProperty);
            set => SetValue(VisibilidadPestanaProperty, value);
        }

        private static readonly DependencyPropertyKey VisiblePropertyKey = DependencyProperty.RegisterReadOnly(nameof(Visible),
            typeof(bool), typeof(ElementoMenuBase), new PropertyMetadata(true));

        public static readonly DependencyProperty VisibleProperty = VisiblePropertyKey.DependencyProperty;

        /// <summary>El elemento, su grupo y su pestaña están visibles.</summary>
        public bool Visible => (bool)GetValue(VisibleProperty);

        private static readonly DependencyPropertyKey MostrarPropertyKey = DependencyProperty.RegisterReadOnly(nameof(Mostrar),
            typeof(bool), typeof(ElementoMenuBase), new PropertyMetadata(true, AlCambiarMostrar));

        public static readonly DependencyProperty MostrarProperty = MostrarPropertyKey.DependencyProperty;

        /// <summary>
        /// Lo que mira el panel. Carlos 27/10/26 (decisión 3): lo que no se puede usar no se ve, así que en un botón es
        /// <see cref="Visible"/> y que su comando se pueda ejecutar; en el panel no hay elementos deshabilitados.
        /// </summary>
        public bool Mostrar => (bool)GetValue(MostrarProperty);

        /// <summary>Si, además de visible, se puede usar (en un botón, el CanExecute de su comando).</summary>
        protected virtual bool SePuedeUsar => true;

        protected static void AlCambiarCondicion(DependencyObject d, DependencyPropertyChangedEventArgs e)
            => ((ElementoMenuBase)d).Recalcular();

        protected void Recalcular()
        {
            bool visible = Visibilidad == Visibility.Visible
                && VisibilidadGrupo == Visibility.Visible
                && VisibilidadPestana == Visibility.Visible;
            SetValue(VisiblePropertyKey, visible);
            SetValue(MostrarPropertyKey, visible && SePuedeUsar);
        }

        private static void AlCambiarMostrar(DependencyObject d, DependencyPropertyChangedEventArgs e)
            => ((ElementoMenuBase)d).MostrarCambiado?.Invoke(d, EventArgs.Empty);
    }

    /// <summary>
    /// Nesto#490 (panel lateral): un botón del menú, que abre una pantalla. Lleva el MISMO <see cref="ICommand"/> que el
    /// botón de la cinta (trampa 2 del plan: los menús en VB usan ViewModels completos y MS.DI los registra
    /// transitorios, así que no se resuelve nada: se usa la instancia de la cinta).
    /// </summary>
    public sealed class ElementoMenu : ElementoMenuBase
    {
        private ICommand _comandoEscuchado;

        public ElementoMenu(string clave) : base(clave)
        {
            Recalcular(); // sin comando no se puede usar: no se muestra
        }

        public static readonly DependencyProperty ComandoProperty = DependencyProperty.Register(nameof(Comando),
            typeof(ICommand), typeof(ElementoMenu), new PropertyMetadata(null, AlCambiarComando));

        public ICommand Comando
        {
            get => (ICommand)GetValue(ComandoProperty);
            set => SetValue(ComandoProperty, value);
        }

        public static readonly DependencyProperty ParametroComandoProperty = DependencyProperty.Register(nameof(ParametroComando),
            typeof(object), typeof(ElementoMenu), new PropertyMetadata(null, AlCambiarCondicion));

        public object ParametroComando
        {
            get => GetValue(ParametroComandoProperty);
            set => SetValue(ParametroComandoProperty, value);
        }

        private static readonly DependencyPropertyKey PuedeEjecutarPropertyKey = DependencyProperty.RegisterReadOnly(nameof(PuedeEjecutar),
            typeof(bool), typeof(ElementoMenu), new PropertyMetadata(false));

        public static readonly DependencyProperty PuedeEjecutarProperty = PuedeEjecutarPropertyKey.DependencyProperty;

        /// <summary>El CanExecute del comando (false si no tiene comando). Se actualiza con su CanExecuteChanged.</summary>
        public bool PuedeEjecutar => (bool)GetValue(PuedeEjecutarProperty);

        protected override bool SePuedeUsar
        {
            get
            {
                bool puede = false;
                try
                {
                    puede = Comando?.CanExecute(ParametroComando) == true;
                }
                catch
                {
                    // Un CanExecute que lanza (p. ej. sin dominio) no rompe el menú: el elemento no se muestra
                }
                SetValue(PuedeEjecutarPropertyKey, puede);
                return puede;
            }
        }

        private static void AlCambiarComando(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var elemento = (ElementoMenu)d;
            if (elemento._comandoEscuchado != null)
            {
                elemento._comandoEscuchado.CanExecuteChanged -= elemento.AlCambiarPuedeEjecutar;
            }
            elemento._comandoEscuchado = e.NewValue as ICommand;
            if (elemento._comandoEscuchado != null)
            {
                elemento._comandoEscuchado.CanExecuteChanged += elemento.AlCambiarPuedeEjecutar;
            }
            elemento.Recalcular();
        }

        private void AlCambiarPuedeEjecutar(object sender, EventArgs e)
        {
            // CanExecuteChanged puede llegar desde otro hilo: las propiedades de dependencia, en el suyo
            if (Dispatcher.CheckAccess())
            {
                Recalcular();
            }
            else
            {
                _ = Dispatcher.BeginInvoke(new Action(Recalcular));
            }
        }
    }

    /// <summary>
    /// Nesto#490 (panel lateral): lo que en la cinta no es un botón y guarda estado que usan otros botones: «Fechas»
    /// (Actual / Anterior / Personalizar, con sus dos fechas) e «Incluir Global» (trampa 4 del plan). No se ejecuta:
    /// el panel lo pinta con controles enlazados a <see cref="ElementoMenuBase.Fuente"/>.
    /// </summary>
    public sealed class ContenidoMenu : ElementoMenuBase
    {
        public ContenidoMenu(string clave) : base(clave)
        {
        }
    }
}
