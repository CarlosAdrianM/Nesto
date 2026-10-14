using Prism.Regions;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace Nesto.Infrastructure.Navegacion
{
    /// <summary>
    /// Nesto#490 (4C.4, tramo 3): la propiedad adjunta con la que el XAML declara una región, en lugar de
    /// <c>prism:RegionManager.RegionName</c>: <c>nav:Regiones.Nombre="MainRegion"</c>.
    /// <list type="bullet">
    /// <item><b>Piloto apagado</b> (<see cref="Propias"/> = false, lo de siempre): solo pasa el nombre a
    /// <c>RegionManager.RegionName</c>, y Prism hace con el control exactamente lo mismo que si el XAML lo dijera.</item>
    /// <item><b>Piloto encendido</b> (parámetro <c>RegionesPropias</c> = "1"): Prism no se entera, y la región se registra
    /// en el <see cref="GestorRegiones"/> que encuentre subiendo por el árbol desde el control (el colgado del shell o de la
    /// vista de una región, <see cref="GestorRegiones.GestorProperty"/>).</item>
    /// </list>
    /// No pueden ir las dos juntas en el mismo control: Prism adaptaría el control también (y su ContentControlRegionAdapter
    /// lanza si la región propia ya le ha puesto contenido), y registraría ListaPedidosRegion de cada pestaña en el gestor
    /// del shell (la segunda pestaña lanzaría por nombre repetido).
    ///
    /// La creación es diferida, como el <c>DelayedRegionCreationBehavior</c> y el <c>RegionManagerRegistrationBehavior</c>
    /// de Prism: el control se queda pendiente hasta que se encuentra su gestor, y se vuelve a intentar al cargarse
    /// (<c>Loaded</c>) Y cada vez que alguien busca una región (<see cref="GestorRegiones.Region"/>, que en Prism es el
    /// <c>UpdatingRegions</c> de <c>Regions[...]</c>). Por eso <c>PedidoVentaView_Loaded</c> encuentra ListaPedidosRegion
    /// aunque el ContentControl de dentro aún no haya recibido su Loaded. Y como Prism, si el control deja de encontrar el
    /// gestor en el que se registró (su vista se ha cerrado y se le ha descolgado el del shell), su región sale de ese gestor
    /// en la siguiente búsqueda: así, al cerrar Rapports y volver a abrirlo, RapportDetailRegion es la de la pestaña nueva.
    /// </summary>
    public static class Regiones
    {
        private sealed class Entrada
        {
            public Entrada(DependencyObject control, string nombre)
            {
                Control = new WeakReference<DependencyObject>(control);
                Nombre = nombre;
            }

            public WeakReference<DependencyObject> Control { get; }
            public string Nombre { get; }
            /// <summary>Dónde se registró (null mientras está pendiente).</summary>
            public GestorRegiones Gestor { get; set; }
            public RegionNesto Region { get; set; }
        }

        private static readonly List<Entrada> _controles = new List<Entrada>();
        private static readonly object _cerrojo = new object();
        [ThreadStatic]
        private static bool _actualizando;

        /// <summary>
        /// El piloto: true = región propia; false (por defecto) = Prism. Se fija una vez, antes de crear la ventana principal
        /// (<see cref="ConmutadorRegiones.AntesDeCrearShell"/>): solo cuenta para los controles que se crean después.
        /// </summary>
        public static bool Propias { get; set; }

        /// <summary>Nombre de la región y error de un registro que ha fallado (por defecto, a la traza).</summary>
        public static Action<string, Exception> AlFallarRegistro { get; set; }

        public static readonly DependencyProperty NombreProperty = DependencyProperty.RegisterAttached(
            "Nombre", typeof(string), typeof(Regiones), new PropertyMetadata(null, AlCambiarNombre));

        public static string GetNombre(DependencyObject control)
            => (string)(control ?? throw new ArgumentNullException(nameof(control))).GetValue(NombreProperty);

        public static void SetNombre(DependencyObject control, string nombre)
            => (control ?? throw new ArgumentNullException(nameof(control))).SetValue(NombreProperty, nombre);

        /// <summary>Cuántos controles con región propia están pendientes de encontrar su gestor (para las pruebas).</summary>
        internal static int Pendientes(Func<DependencyObject, bool> filtro = null)
        {
            lock (_cerrojo)
            {
                return _controles.Count(e => e.Gestor == null && e.Control.TryGetTarget(out DependencyObject c)
                    && (filtro == null || filtro(c)));
            }
        }

        private static void AlCambiarNombre(DependencyObject control, DependencyPropertyChangedEventArgs e)
        {
            string nombre = (string)e.NewValue;
            if (!Propias)
            {
                // Piloto apagado: lo de siempre, por Prism
                RegionManager.SetRegionName(control, nombre);
                return;
            }
            if (string.IsNullOrEmpty(nombre) || DesignerProperties.GetIsInDesignMode(control))
            {
                return;
            }
            lock (_cerrojo)
            {
                _controles.Add(new Entrada(control, nombre));
            }
            if (control is FrameworkElement elemento)
            {
                elemento.Loaded += AlCargar;
            }
        }

        private static void AlCargar(object sender, RoutedEventArgs e)
        {
            ((FrameworkElement)sender).Loaded -= AlCargar;
            Actualizar();
        }

        /// <summary>
        /// Registra los controles pendientes que ya encuentran su gestor, y saca de su gestor las regiones de los controles
        /// que ya no lo encuentran. Lo llama <see cref="GestorRegiones"/> en cada búsqueda. Solo toca los controles de este hilo.
        /// </summary>
        internal static void Actualizar()
        {
            if (_actualizando)
            {
                return;
            }
            List<Entrada> copia;
            lock (_cerrojo)
            {
                if (_controles.Count == 0)
                {
                    return;
                }
                copia = _controles.ToList();
            }
            _actualizando = true;
            try
            {
                var quitar = new List<Entrada>();
                var vivos = new List<(Entrada entrada, DependencyObject control, GestorRegiones encontrado)>();
                foreach (Entrada entrada in copia)
                {
                    if (!entrada.Control.TryGetTarget(out DependencyObject control) || !control.Dispatcher.Thread.IsAlive)
                    {
                        // Se lo ha llevado el GC (o es de un hilo que ya no existe): su región, fuera
                        if (entrada.Gestor != null && control != null && control.CheckAccess())
                        {
                            entrada.Gestor.QuitarRegion(entrada.Nombre, entrada.Region);
                        }
                        quitar.Add(entrada);
                        continue;
                    }
                    if (!control.CheckAccess())
                    {
                        continue;
                    }
                    vivos.Add((entrada, control, BuscarGestor(control)));
                }
                // Primero salen las regiones que ya no encuentran su gestor, para que las nuevas del mismo nombre quepan
                foreach (var (entrada, _, encontrado) in vivos)
                {
                    if (entrada.Gestor != null && !ReferenceEquals(entrada.Gestor, encontrado))
                    {
                        entrada.Gestor.QuitarRegion(entrada.Nombre, entrada.Region);
                        quitar.Add(entrada);
                    }
                }
                foreach (var (entrada, control, encontrado) in vivos)
                {
                    if (entrada.Gestor != null || encontrado == null || quitar.Contains(entrada))
                    {
                        continue;
                    }
                    try
                    {
                        entrada.Region = encontrado.Registrar(entrada.Nombre, control);
                        entrada.Gestor = encontrado;
                    }
                    catch (Exception ex)
                    {
                        // Como Prism lanzaría (nombre repetido, control con contenido): se avisa una vez y no se reintenta
                        quitar.Add(entrada);
                        Avisar(entrada.Nombre, ex);
                    }
                }
                if (quitar.Count > 0)
                {
                    lock (_cerrojo)
                    {
                        _ = _controles.RemoveAll(quitar.Contains);
                    }
                }
            }
            finally
            {
                _actualizando = false;
            }
        }

        // Como FindRegionManager de Prism: el gestor colgado del control o del primero de sus padres que lo tenga
        private static GestorRegiones BuscarGestor(DependencyObject control)
        {
            for (DependencyObject actual = control; actual != null; actual = Padre(actual))
            {
                GestorRegiones gestor = GestorRegiones.GetGestor(actual);
                if (gestor != null)
                {
                    return gestor;
                }
            }
            return null;
        }

        private static DependencyObject Padre(DependencyObject elemento)
            => LogicalTreeHelper.GetParent(elemento)
               ?? (elemento is Visual || elemento is Visual3D ? VisualTreeHelper.GetParent(elemento) : null);

        private static void Avisar(string nombre, Exception error)
        {
            Debug.WriteLine($"[Regiones] No se ha podido registrar la región {nombre}: {error}");
            try
            {
                AlFallarRegistro?.Invoke(nombre, error);
            }
            catch
            {
                // El aviso nunca rompe la navegación
            }
        }
    }
}
