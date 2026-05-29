using Prism.Regions;
using System;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Threading;
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

            // Anular el DataContext reevalúa los bindings del árbol visual y puede disparar callbacks
            // de controles no null-safe. Si ese callback es 'async void', su excepción NO llega al
            // try/catch de abajo: el compilador la entrega a AsyncVoidMethodBuilder.SetException, que
            // la postea al SynchronizationContext capturado (el del Dispatcher) y termina como
            // excepción no controlada = crash de la app. Para que un control sin guard no tumbe Nesto
            // al cerrar la pestaña, instalamos durante el nulling un SynchronizationContext que
            // envuelve al real y neutraliza (registra) ese relanzamiento posteado. El blindaje solo
            // dura el nulling y no enmascara excepciones del resto de la aplicación.
            SynchronizationContext contextoPrevio = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(new BlindajeTeardownSyncContext(contextoPrevio));
            try
            {
                elemento.DataContext = null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LimpiarVistaAlQuitar] Error al limpiar DataContext de " +
                    $"{elemento.GetType().Name}: {ex.Message}");
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(contextoPrevio);
            }
        }

        /// <summary>
        /// SynchronizationContext que se instala momentáneamente mientras se anula el DataContext de
        /// una vista que sale de una región. Reenvía todo al contexto real (el del Dispatcher) pero
        /// envuelve cada callback en un try/catch: así, si un control no null-safe lanza desde un
        /// método <c>async void</c> al reevaluarse sus bindings, la excepción que
        /// <c>AsyncVoidMethodBuilder.SetException</c> postea aquí se registra en vez de propagarse
        /// como excepción no controlada (que tumbaría la app). El blindaje solo dura el nulling y no
        /// enmascara excepciones del resto de la aplicación.
        /// </summary>
        private sealed class BlindajeTeardownSyncContext : SynchronizationContext
        {
            private readonly SynchronizationContext _real;

            public BlindajeTeardownSyncContext(SynchronizationContext real)
            {
                _real = real;
            }

            public override void Post(SendOrPostCallback d, object state)
            {
                SendOrPostCallback envuelto = Envolver(d);
                if (_real != null)
                {
                    _real.Post(envuelto, state);
                }
                else
                {
                    base.Post(envuelto, state);
                }
            }

            public override void Send(SendOrPostCallback d, object state)
            {
                SendOrPostCallback envuelto = Envolver(d);
                if (_real != null)
                {
                    _real.Send(envuelto, state);
                }
                else
                {
                    base.Send(envuelto, state);
                }
            }

            public override SynchronizationContext CreateCopy() => this;

            public override void OperationStarted() => _real?.OperationStarted();

            public override void OperationCompleted() => _real?.OperationCompleted();

            private static SendOrPostCallback Envolver(SendOrPostCallback d) => state =>
            {
                try
                {
                    d(state);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("[LimpiarVistaAlQuitar] Excepción async-void neutralizada durante " +
                        $"el teardown de una vista: {ex}");
                }
            };
        }
    }
}
