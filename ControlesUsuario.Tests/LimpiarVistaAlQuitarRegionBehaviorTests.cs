using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Shared;
using Prism.Regions;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Controls;

namespace ControlesUsuario.Tests
{
    [TestClass]
    public class LimpiarVistaAlQuitarRegionBehaviorTests
    {
        // Fuga de memoria: al quitar una vista de una región hay que limpiar su DataContext
        // para que WPF desmonte los BindingExpression que, si no, la mantienen viva.
        [TestMethod]
        public void Behavior_AlQuitarVistaDeLaRegion_LimpiaElDataContext()
        {
            EjecutarEnSTA(() =>
            {
                IRegion region = new Region();
                var behavior = new LimpiarVistaAlQuitarRegionBehavior { Region = region };
                behavior.Attach();

                var vista = new ContentControl { DataContext = new object() };
                region.Add(vista);
                Assert.IsNotNull(vista.DataContext, "Precondición: la vista tiene DataContext al añadirse");

                // Act
                region.Remove(vista);

                // Assert
                Assert.IsNull(vista.DataContext, "Al quitar la vista de la región su DataContext debe quedar a null");
            });
        }

        // No debe fallar si lo que se quita no es un FrameworkElement (no tiene DataContext).
        [TestMethod]
        public void Behavior_AlQuitarVistaQueNoEsFrameworkElement_NoLanza()
        {
            EjecutarEnSTA(() =>
            {
                IRegion region = new Region();
                var behavior = new LimpiarVistaAlQuitarRegionBehavior { Region = region };
                behavior.Attach();

                var vistaNoVisual = new object();
                region.Add(vistaNoVisual);

                // Act + Assert: no debe lanzar
                region.Remove(vistaNoVisual);
            });
        }

        // Blindaje: una vista cuyo callback de teardown es 'async void' y lanza (un control no
        // null-safe, como eran los selectores) NO debe tumbar la app. El behavior instala durante el
        // nulling del DataContext un SynchronizationContext que envuelve al real y neutraliza el
        // relanzamiento que AsyncVoidMethodBuilder.SetException postea.
        //
        // Se usa un contexto "real" asíncrono (encola y se drena después), igual que el Dispatcher:
        // así, sin el blindaje, la excepción NO la atrapa el try/catch del behavior (se postea para
        // más tarde) y revienta al drenar; con el blindaje, el delegate posteado va envuelto en
        // try/catch y drenar no lanza.
        [TestMethod]
        public void Behavior_VistaConAsyncVoidQueFallaEnTeardown_NoTumbaLaApp()
        {
            EjecutarEnSTA(() =>
            {
                var contextoReal = new ColaAsincronaContext();
                SynchronizationContext previo = SynchronizationContext.Current;
                SynchronizationContext.SetSynchronizationContext(contextoReal);
                try
                {
                    IRegion region = new Region();
                    var behavior = new LimpiarVistaAlQuitarRegionBehavior { Region = region };
                    behavior.Attach();

                    var vista = new ControlQueFallaEnTeardown { DataContext = new object() };
                    region.Add(vista);

                    // Act: al quitar la vista, el behavior pone DataContext=null -> dispara el async void que lanza.
                    region.Remove(vista);

                    // El relanzamiento quedó posteado (como en el Dispatcher real). Drenarlo NO debe
                    // lanzar: el blindaje del behavior lo envolvió en try/catch.
                    contextoReal.Drenar();

                    Assert.IsNull(vista.DataContext, "El DataContext debe quedar a null (fix de fuga intacto)");
                }
                finally
                {
                    SynchronizationContext.SetSynchronizationContext(previo);
                }
            });
        }

        // ContentControl que reproduce el patrón roto de los selectores: al revertir su DataContext a
        // null (lo que hace el behavior en el teardown) lanza desde un método 'async void'.
        private sealed class ControlQueFallaEnTeardown : ContentControl
        {
            public ControlQueFallaEnTeardown()
            {
                DataContextChanged += (s, e) =>
                {
                    if (e.NewValue == null)
                    {
                        FallarEnTeardown();
                    }
                };
            }

#pragma warning disable CS1998 // async void sin await: simulamos el NRE en el prefijo síncrono
            private async void FallarEnTeardown()
            {
                throw new NullReferenceException("simulado: control no null-safe en teardown");
            }
#pragma warning restore CS1998
        }

        // SynchronizationContext asíncrono que imita al del Dispatcher: Post encola en vez de ejecutar;
        // Drenar ejecuta lo encolado (y propaga lo que lance, igual que el bucle de mensajes del Dispatcher).
        private sealed class ColaAsincronaContext : SynchronizationContext
        {
            private readonly Queue<(SendOrPostCallback Callback, object State)> _cola = new();

            public override void Post(SendOrPostCallback d, object state) => _cola.Enqueue((d, state));

            public void Drenar()
            {
                while (_cola.Count > 0)
                {
                    (SendOrPostCallback callback, object state) = _cola.Dequeue();
                    callback(state);
                }
            }
        }

        private static void EjecutarEnSTA(Action action)
        {
            Exception capturada = null;
            var hilo = new Thread(() =>
            {
                try { action(); }
                catch (Exception ex) { capturada = ex; }
            });
            hilo.SetApartmentState(ApartmentState.STA);
            hilo.Start();
            hilo.Join();
            if (capturada != null)
            {
                throw new AssertFailedException(capturada.Message, capturada);
            }
        }
    }
}
