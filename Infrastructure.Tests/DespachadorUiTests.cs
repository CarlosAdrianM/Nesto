using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Shared;
using System;
using System.Threading;
using System.Windows.Threading;

namespace Nesto.Infrastructure.Tests
{
    /// <summary>
    /// Nesto#490 (4C.1): DespachadorUi reproduce ThreadOption.UIThread de Prism para los receptores
    /// de mensajes del Messenger, que entrega en el hilo de quien envía.
    /// </summary>
    [TestClass]
    public class DespachadorUiTests
    {
        [TestMethod]
        public void EnHiloUi_SinDispatcher_EjecutaDirectamenteEnElMismoHilo()
        {
            int hiloEjecucion = -1;

            DespachadorUi.EnHiloUi((Dispatcher)null, () => hiloEjecucion = Environment.CurrentManagedThreadId);

            Assert.AreEqual(Environment.CurrentManagedThreadId, hiloEjecucion);
        }

        [TestMethod]
        public void EnHiloUi_ConDispatcher_EjecutaEnElHiloDelDispatcher()
        {
            using var dispatcherListo = new ManualResetEventSlim();
            using var ejecutado = new ManualResetEventSlim();
            Dispatcher dispatcher = null;
            int hiloUi = -1;
            int hiloEjecucion = -1;
            var hilo = new Thread(() =>
            {
                dispatcher = Dispatcher.CurrentDispatcher;
                hiloUi = Environment.CurrentManagedThreadId;
                dispatcherListo.Set();
                Dispatcher.Run();
            });
            hilo.SetApartmentState(ApartmentState.STA);
            hilo.IsBackground = true;
            hilo.Start();
            Assert.IsTrue(dispatcherListo.Wait(TimeSpan.FromSeconds(5)));

            try
            {
                DespachadorUi.EnHiloUi(dispatcher, () =>
                {
                    hiloEjecucion = Environment.CurrentManagedThreadId;
                    ejecutado.Set();
                });

                Assert.IsTrue(ejecutado.Wait(TimeSpan.FromSeconds(5)), "La acción debe llegar al hilo de la UI");
                Assert.AreEqual(hiloUi, hiloEjecucion);
                Assert.AreNotEqual(Environment.CurrentManagedThreadId, hiloEjecucion);
            }
            finally
            {
                dispatcher.InvokeShutdown();
            }
        }

        [TestMethod]
        public void EnHiloUi_DesdeElPropioHiloDeLaUi_SeEncolaNoSeEjecutaEnElActo()
        {
            // Como Prism (SynchronizationContext.Post): aunque se envíe desde el hilo de la UI, el
            // receptor no se ejecuta dentro del Send, sino después, cuando el hilo queda libre.
            bool ejecutadoDentroDeLaLlamada = true;
            bool ejecutadoDespues = false;
            Exception error = null;
            var hilo = new Thread(() =>
            {
                try
                {
                    Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
                    bool ejecutado = false;
                    DespachadorUi.EnHiloUi(dispatcher, () => ejecutado = true);
                    ejecutadoDentroDeLaLlamada = ejecutado;

                    var frame = new DispatcherFrame();
                    _ = dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false));
                    Dispatcher.PushFrame(frame);
                    ejecutadoDespues = ejecutado;
                }
                catch (Exception ex)
                {
                    error = ex;
                }
            });
            hilo.SetApartmentState(ApartmentState.STA);
            hilo.Start();
            Assert.IsTrue(hilo.Join(TimeSpan.FromSeconds(5)));

            Assert.IsNull(error, error?.ToString());
            Assert.IsFalse(ejecutadoDentroDeLaLlamada);
            Assert.IsTrue(ejecutadoDespues);
        }
    }
}
