using Prism.Regions;
using System;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Windows;

namespace Nesto.Infrastructure.Shared
{
    /// <summary>
    /// Cuando se quita una vista de una región, anula su <c>DataContext</c> para que WPF
    /// desmonte los <c>BindingExpression</c> de su árbol visual. Esos bindings quedan
    /// registrados en la infraestructura estática de binding de WPF
    /// (<c>PropertyChangedEventManager</c> y el weak-event de <c>CanExecuteChanged</c> de los
    /// comandos); si no se desmontan, mantienen vivo el árbol visual y, vía DataContext, su
    /// ViewModel → fuga de memoria (la memoria sube al abrir/cerrar ventanas y no vuelve a bajar).
    ///
    /// Registrado de forma global en <c>Application.ConfigureDefaultRegionBehaviors</c>, así que
    /// aplica a TODAS las regiones y ventanas. Cubre el cierre por la X (CloseTabAction) y las
    /// quitadas programáticas (<c>region.Remove(...)</c>).
    ///
    /// Nota: esto libera el grueso del leak (la VM y sus datos). Queda una cola más ligera de
    /// vistas retenidas por weak-events internos de WPF (p.ej. el <c>ItemContainerGenerator</c>
    /// de ComboBox/ItemsControl en controles que fijan su propio DataContext); no se persigue
    /// aquí por rendimientos decrecientes y porque esos controles los reescribe la modernización.
    /// </summary>
    public class LimpiarVistaAlQuitarRegionBehavior : RegionBehavior
    {
        public const string BehaviorKey = "LimpiarVistaAlQuitar";

        protected override void OnAttach()
        {
            Region.Views.CollectionChanged += OnViewsCollectionChanged;
        }

        private void OnViewsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action != NotifyCollectionChangedAction.Remove || e.OldItems == null)
            {
                return;
            }

            foreach (var view in e.OldItems)
            {
                LiberarVista(view);
            }
        }

        private static void LiberarVista(object view)
        {
            if (view is not FrameworkElement elemento)
            {
                return;
            }

            // Defensivo: anular el DataContext reevalúa los bindings y podría disparar un callback
            // de control no null-safe; eso no debe impedir cerrar la pestaña.
            try
            {
                elemento.DataContext = null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LimpiarVistaAlQuitar] Error al limpiar DataContext de " +
                    $"{elemento.GetType().Name}: {ex.Message}");
            }
        }
    }
}
