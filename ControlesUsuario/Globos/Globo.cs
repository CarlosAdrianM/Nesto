using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace ControlesUsuario.Globos
{
    /// <summary>
    /// Nesto#592 (NestoAPI#774): la clave estable con la que un control recibe globos («consejos»). En el XAML:
    /// <code>xmlns:globos="clr-namespace:ControlesUsuario.Globos;assembly=ControlesUsuario"
    /// &lt;TextBlock globos:Globo.Ancla="EnviarReposicion.EstadoRecogida" … /&gt;</code>
    /// La clave es «Pantalla.Control»: la pantalla es la vista sin «View» (EnviarReposicionView → EnviarReposicion); la de la
    /// ventana principal es «Principal». Es la misma que se escribe en la columna Ancla de la tabla Globos. No cambiarla al
    /// renombrar el control: los globos ya dados de alta dejarían de encontrarlo (saldrían como tarjeta centrada).
    ///
    /// <para>Si el control está en la vista pero todavía no se ve (otro paso de un asistente, otra pestaña, colapsado), su globo
    /// espera y sale junto a él la primera vez que se vea (<see cref="RegistroAnclas.EnEspera"/>). Para eso el control tiene que
    /// existir desde que se carga la vista: uno que viva dentro de un DataTemplate no existe hasta que se pinta.</para>
    ///
    /// <para><see cref="Pantalla"/> sirve para las vistas que no siguen la convención del nombre (se pone en la raíz).</para>
    /// </summary>
    public static class Globo
    {
        public static readonly DependencyProperty AnclaProperty = DependencyProperty.RegisterAttached(
            "Ancla", typeof(string), typeof(Globo), new PropertyMetadata(null, AlCambiarAncla));

        public static string GetAncla(DependencyObject d) => (string)d.GetValue(AnclaProperty);
        public static void SetAncla(DependencyObject d, string valor) => d.SetValue(AnclaProperty, valor);

        public static readonly DependencyProperty PantallaProperty = DependencyProperty.RegisterAttached(
            "Pantalla", typeof(string), typeof(Globo), new PropertyMetadata(null));

        public static string GetPantalla(DependencyObject d) => (string)d.GetValue(PantallaProperty);
        public static void SetPantalla(DependencyObject d, string valor) => d.SetValue(PantallaProperty, valor);

        private static void AlCambiarAncla(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FrameworkElement elemento)
            {
                RegistroAnclas.Quitar(e.OldValue as string, elemento);
                RegistroAnclas.Poner(e.NewValue as string, elemento);
            }
        }
    }

    /// <summary>
    /// Nesto#592: los controles con <see cref="Globo.AnclaProperty"/>, por clave (referencias débiles: no retienen las vistas
    /// cerradas). Puede haber varios con la misma clave (dos pestañas iguales): vale el que esté a la vista.
    /// </summary>
    public static class RegistroAnclas
    {
        private static readonly Dictionary<string, List<WeakReference<FrameworkElement>>> _anclas =
            new Dictionary<string, List<WeakReference<FrameworkElement>>>(StringComparer.OrdinalIgnoreCase);

        internal static void Poner(string clave, FrameworkElement elemento)
        {
            string limpia = clave?.Trim();
            if (string.IsNullOrEmpty(limpia))
            {
                return;
            }
            if (!_anclas.TryGetValue(limpia, out List<WeakReference<FrameworkElement>> lista))
            {
                lista = new List<WeakReference<FrameworkElement>>();
                _anclas[limpia] = lista;
            }
            _ = lista.RemoveAll(w => !w.TryGetTarget(out FrameworkElement vivo) || ReferenceEquals(vivo, elemento));
            lista.Add(new WeakReference<FrameworkElement>(elemento));
        }

        internal static void Quitar(string clave, FrameworkElement elemento)
        {
            string limpia = clave?.Trim();
            if (string.IsNullOrEmpty(limpia) || !_anclas.TryGetValue(limpia, out List<WeakReference<FrameworkElement>> lista))
            {
                return;
            }
            _ = lista.RemoveAll(w => !w.TryGetTarget(out FrameworkElement vivo) || ReferenceEquals(vivo, elemento));
        }

        /// <summary>El control de esa clave que está a la vista (cargado, visible y con tamaño), o null.</summary>
        public static FrameworkElement Visible(string clave)
        {
            string limpia = clave?.Trim();
            if (string.IsNullOrEmpty(limpia) || !_anclas.TryGetValue(limpia, out List<WeakReference<FrameworkElement>> lista))
            {
                return null;
            }
            return lista
                .Select(w => w.TryGetTarget(out FrameworkElement e) ? e : null)
                .FirstOrDefault(EstaALaVista);
        }

        /// <summary>Cargado, visible y con tamaño.</summary>
        internal static bool EstaALaVista(FrameworkElement e)
            => e != null && e.IsLoaded && e.IsVisible && e.ActualWidth > 0 && e.ActualHeight > 0;

        /// <summary>
        /// Los controles de esa clave que EXISTEN dentro de la vista pero todavía no se ven (están en un paso de un asistente o en
        /// una pestaña que no es la activa, colapsados…): su globo espera a que se vean en vez de salir centrado. Vacía si la
        /// clave no está en esa vista (o si alguno de los suyos ya se ve: entonces el globo va junto a ese).
        /// </summary>
        public static IReadOnlyList<FrameworkElement> EnEspera(string clave, DependencyObject vista)
        {
            string limpia = clave?.Trim();
            if (vista == null || string.IsNullOrEmpty(limpia) || !_anclas.TryGetValue(limpia, out List<WeakReference<FrameworkElement>> lista))
            {
                return Array.Empty<FrameworkElement>();
            }
            List<FrameworkElement> suyos = lista
                .Select(w => w.TryGetTarget(out FrameworkElement e) ? e : null)
                .Where(e => e != null && EstaDentro(e, vista))
                .ToList();
            return suyos.Any(EstaALaVista) ? Array.Empty<FrameworkElement>() : suyos;
        }

        /// <summary>
        /// Si el control cuelga de la vista, aunque no esté pintado: se sube por el árbol visual y, donde no lo hay (la página de
        /// un asistente o la pestaña que no se están enseñando), por el lógico.
        /// </summary>
        internal static bool EstaDentro(DependencyObject elemento, DependencyObject vista)
        {
            // El tope es solo por si un árbol mal montado hiciera un ciclo
            for (int saltos = 0; elemento != null && saltos < 1000; saltos++)
            {
                if (ReferenceEquals(elemento, vista))
                {
                    return true;
                }
                elemento = Padre(elemento);
            }
            return false;
        }

        private static DependencyObject Padre(DependencyObject elemento)
        {
            DependencyObject visual = elemento is System.Windows.Media.Visual || elemento is System.Windows.Media.Media3D.Visual3D
                ? System.Windows.Media.VisualTreeHelper.GetParent(elemento)
                : null;
            return visual
                ?? LogicalTreeHelper.GetParent(elemento)
                ?? (elemento as FrameworkElement)?.TemplatedParent;
        }
    }

    /// <summary>Nesto#592: la clave de pantalla de una vista de Nesto (la regla común está en el núcleo, NucleoGlobos.cs).</summary>
    public static class PantallasNesto
    {
        public const string PRINCIPAL = "Principal";

        /// <summary>
        /// La que se pone a mano (<see cref="Globo.PantallaProperty"/>) o la del tipo sin «View» (EnviarReposicionView →
        /// EnviarReposicion); «MainWindow» → «Principal». Null si no es una pantalla (un control que no acaba en «View»).
        /// </summary>
        public static string Clave(string nombreTipo, string puesta = null)
            => string.IsNullOrWhiteSpace(puesta) && nombreTipo == "MainWindow"
                ? PRINCIPAL
                : Nesto.Infrastructure.Globos.ClavesPantallaGlobos.DeTipo(nombreTipo, puesta, "View");
    }
}