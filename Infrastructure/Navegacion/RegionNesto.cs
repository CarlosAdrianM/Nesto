using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Nesto.Infrastructure.Navegacion
{
    /// <summary>
    /// Nesto#490 (4C.4, región propia): una región de Nesto, sin Prism. Hace lo que hacía <c>Prism.Regions.Region</c> con
    /// los comportamientos que Nesto usa:
    /// <list type="bullet">
    /// <item>las vistas en orden de entrada, cada una como mucho una vez y con un nombre opcional que no se repite
    /// (Clientes, Clientes2…);</item>
    /// <item>las vistas activas (en Nesto, una como mucho: la pestaña seleccionada o el contenido del ContentControl);</item>
    /// <item><see cref="IConActivacion"/> a la vista y a su DataContext al activarla y al desactivarla o quitarla (el
    /// <c>RegionActiveAwareBehavior</c> de Prism con <c>IActiveAware</c>);</item>
    /// <item>la limpieza del DataContext al quitar la vista (<see cref="LimpiezaVistas"/>, lo mismo que hacía el
    /// <c>LimpiarVistaAlQuitarRegionBehavior</c> en las regiones de Prism, por las fugas de memoria);</item>
    /// <item>el gestor de regiones colgado de la vista (<see cref="GestorRegiones.GestorProperty"/>), para que las regiones
    /// que declare dentro se registren en el suyo, como <c>RegionManager.RegionManagerProperty</c>.</item>
    /// </list>
    /// Las dos clases concretas son los adaptadores: <see cref="RegionPestanas"/> (TabControl y cualquier Selector) y
    /// <see cref="RegionContenido"/> (ContentControl).
    /// </summary>
    public abstract class RegionNesto
    {
        private readonly ObservableCollection<object> _vistas = new ObservableCollection<object>();
        private readonly Dictionary<object, string> _nombres = new Dictionary<object, string>(ReferenceEqualityComparer.Instance);
        private readonly List<object> _activas = new List<object>();

        protected RegionNesto(string nombre, GestorRegiones gestor)
        {
            if (string.IsNullOrWhiteSpace(nombre))
            {
                throw new ArgumentException("La región necesita un nombre", nameof(nombre));
            }
            Nombre = nombre;
            Gestor = gestor ?? throw new ArgumentNullException(nameof(gestor));
            Vistas = new ReadOnlyObservableCollection<object>(_vistas);
        }

        public string Nombre { get; }

        /// <summary>El gestor de regiones (el ámbito) en el que está registrada esta región.</summary>
        public GestorRegiones Gestor { get; }

        /// <summary>Las vistas de la región, en el orden en que entraron.</summary>
        public ReadOnlyObservableCollection<object> Vistas { get; }

        /// <summary>Las vistas activas (en Nesto, como mucho una).</summary>
        public IReadOnlyList<object> VistasActivas => _activas.ToList();

        public bool Contiene(object vista) => vista != null && _nombres.ContainsKey(vista);

        public bool EsActiva(object vista) => vista != null && _activas.Contains(vista);

        /// <summary>La vista que entró con ese nombre; null si no hay ninguna.</summary>
        public object Vista(string nombre)
        {
            if (string.IsNullOrEmpty(nombre))
            {
                throw new ArgumentException("Falta el nombre de la vista", nameof(nombre));
            }
            return _nombres.FirstOrDefault(e => e.Value == nombre).Key;
        }

        /// <summary>
        /// Mete la vista en la región, sin activarla (salvo lo que haga el adaptador: el ContentControl activa la primera).
        /// Como Prism, lanza si la vista ya está o si ya hay otra con ese nombre. <paramref name="gestorDeLaVista"/> es el
        /// gestor que se cuelga de la vista: el de la región, o uno nuevo si la vista abre su propio ámbito.
        /// </summary>
        public void Anadir(object vista, string nombre = null, GestorRegiones gestorDeLaVista = null)
        {
            if (vista == null)
            {
                throw new ArgumentNullException(nameof(vista));
            }
            if (Contiene(vista))
            {
                throw new InvalidOperationException($"La vista ya está en la región {Nombre}");
            }
            if (!string.IsNullOrEmpty(nombre) && Vista(nombre) != null)
            {
                throw new InvalidOperationException($"Ya hay una vista llamada {nombre} en la región {Nombre}");
            }
            if (vista is DependencyObject elemento)
            {
                GestorRegiones.SetGestor(elemento, gestorDeLaVista ?? Gestor);
            }
            _nombres.Add(vista, string.IsNullOrEmpty(nombre) ? null : nombre);
            _vistas.Add(vista);
            AlAnadir(vista);
        }

        /// <summary>
        /// Saca la vista de la región: si estaba activa la desactiva (y avisa a su <see cref="IConActivacion"/>), y luego
        /// limpia su DataContext. Como Prism, lanza si la vista no está.
        /// </summary>
        public void Quitar(object vista)
        {
            ComprobarQueEsta(vista);
            if (_activas.Remove(vista))
            {
                AvisarActivacion(vista, false);
            }
            _nombres.Remove(vista);
            _vistas.Remove(vista);
            // Como Prism: solo se descuelga el gestor si es el de esta región (no el ámbito propio de un maestro-detalle)
            if (vista is DependencyObject elemento && ReferenceEquals(GestorRegiones.GetGestor(elemento), Gestor))
            {
                elemento.ClearValue(GestorRegiones.GestorProperty);
            }
            LimpiezaVistas.Liberar(vista);
        }

        /// <summary>Activa la vista (en Nesto, la única activa: la anterior se desactiva). Lanza si no está en la región.</summary>
        public void Activar(object vista)
        {
            ComprobarQueEsta(vista);
            if (_activas.Contains(vista))
            {
                return;
            }
            AntesDeActivar(vista);
            _activas.Add(vista);
            AvisarActivacion(vista, true);
            AlActivar(vista);
        }

        /// <summary>Desactiva la vista, si estaba activa. Lanza si no está en la región.</summary>
        public void Desactivar(object vista)
        {
            ComprobarQueEsta(vista);
            if (!_activas.Remove(vista))
            {
                return;
            }
            AvisarActivacion(vista, false);
            AlDesactivar(vista);
        }

        /// <summary>Lo que hace el adaptador al entrar una vista (después de meterla en <see cref="Vistas"/>).</summary>
        protected virtual void AlAnadir(object vista)
        {
        }

        /// <summary>Antes de activar una vista: el ContentControl desactiva la anterior.</summary>
        protected virtual void AntesDeActivar(object vista)
        {
        }

        /// <summary>Después de activar una vista: el adaptador la muestra.</summary>
        protected abstract void AlActivar(object vista);

        /// <summary>Después de desactivar una vista: el adaptador deja de mostrarla.</summary>
        protected abstract void AlDesactivar(object vista);

        private void ComprobarQueEsta(object vista)
        {
            if (!Contiene(vista))
            {
                throw new ArgumentException($"La vista no está en la región {Nombre}", nameof(vista));
            }
        }

        // Como RegionActiveAwareBehavior de Prism con IActiveAware: a la vista y a su DataContext, a cada uno si lo pide
        private static void AvisarActivacion(object vista, bool activa)
        {
            if (vista is IConActivacion conActivacion)
            {
                conActivacion.IsActive = activa;
            }
            if (vista is FrameworkElement elemento && elemento.DataContext is IConActivacion contexto
                && !ReferenceEquals(contexto, vista))
            {
                contexto.IsActive = activa;
            }
        }
    }

    /// <summary>
    /// Nesto#490 (4C.4, región propia): la región de un <see cref="Selector"/> (el TabControl MainRegion), como el
    /// <c>SelectorRegionAdapter</c> de Prism con su <c>SelectorItemsSourceSyncBehavior</c>: las vistas son los elementos
    /// (<see cref="ItemsControl.ItemsSource"/>) y la activa es la seleccionada, en los dos sentidos: activar una vista la
    /// selecciona, y elegir otra pestaña la activa (y desactiva la anterior).
    /// </summary>
    public sealed class RegionPestanas : RegionNesto
    {
        private readonly Selector _selector;

        public RegionPestanas(string nombre, Selector selector, GestorRegiones gestor) : base(nombre, gestor)
        {
            _selector = selector ?? throw new ArgumentNullException(nameof(selector));
            if (selector.ItemsSource != null || selector.Items.Count > 0)
            {
                // Como Prism: los elementos de la región son sus vistas y nada más
                throw new InvalidOperationException($"El control de la región {nombre} ya tiene elementos");
            }
            selector.ItemsSource = Vistas;
            selector.SelectionChanged += AlCambiarSeleccion;
        }

        protected override void AlActivar(object vista)
        {
            // Al seleccionarla, el Selector quita la anterior de la selección y AlCambiarSeleccion la desactiva
            _selector.SelectedItem = vista;
        }

        protected override void AlDesactivar(object vista)
        {
            if (ReferenceEquals(_selector.SelectedItem, vista))
            {
                _selector.SelectedItem = null;
            }
        }

        private void AlCambiarSeleccion(object sender, SelectionChangedEventArgs e)
        {
            // Solo la selección del propio Selector (los SelectionChanged de los controles de dentro también suben hasta aquí)
            if (!ReferenceEquals(e.OriginalSource, _selector))
            {
                return;
            }
            foreach (object quitada in e.RemovedItems)
            {
                if (EsActiva(quitada))
                {
                    Desactivar(quitada);
                }
            }
            foreach (object anadida in e.AddedItems)
            {
                if (Contiene(anadida))
                {
                    Activar(anadida);
                }
            }
        }
    }

    /// <summary>
    /// Nesto#490 (4C.4, región propia): la región de un <see cref="ContentControl"/> (las de lista y detalle de los
    /// maestro-detalle y la del detalle de rapports), como el <c>ContentControlRegionAdapter</c> de Prism: una sola vista
    /// activa, que es el contenido; la primera vista que entra en la región vacía se activa sola.
    /// </summary>
    public sealed class RegionContenido : RegionNesto
    {
        private readonly ContentControl _control;

        public RegionContenido(string nombre, ContentControl control, GestorRegiones gestor) : base(nombre, gestor)
        {
            _control = control ?? throw new ArgumentNullException(nameof(control));
            if (control.Content != null)
            {
                throw new InvalidOperationException($"El control de la región {nombre} ya tiene contenido");
            }
        }

        protected override void AlAnadir(object vista)
        {
            if (VistasActivas.Count == 0)
            {
                Activar(vista);
            }
        }

        protected override void AntesDeActivar(object vista)
        {
            // Como SingleActiveRegion de Prism: primero se desactiva la que estaba
            foreach (object activa in VistasActivas)
            {
                Desactivar(activa);
            }
        }

        protected override void AlActivar(object vista) => _control.Content = vista;

        protected override void AlDesactivar(object vista) => _control.Content = VistasActivas.FirstOrDefault();
    }
}
