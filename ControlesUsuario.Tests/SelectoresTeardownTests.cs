using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Reflection;
using System.Threading;
using System.Windows.Controls;

namespace ControlesUsuario.Tests
{
    /// <summary>
    /// Regresión del crash al cerrar ventanas (Nesto#364): el RegionBehavior global
    /// <c>LimpiarVistaAlQuitar</c> anula el DataContext de la vista al quitarla de la región, lo que
    /// reevalúa los bindings y hace que las dependency properties de estos selectores (Configuracion,
    /// Empresa, Cliente...) reviertan a null disparando su callback. El callback llama a
    /// <c>cargarDatos</c>, que es <c>async void</c>: si desreferencia <c>Configuracion.servidorAPI</c>
    /// con Configuracion null, la excepción no llega a ningún try/catch del llamante sino que
    /// <c>AsyncVoidMethodBuilder.SetException</c> la postea al SynchronizationContext (el del
    /// Dispatcher) = excepción no controlada = crash de la app.
    ///
    /// Cada test invoca <c>cargarDatos</c> con Configuracion null bajo un SynchronizationContext que
    /// captura lo que se postea, y exige que NO se postee ninguna excepción (el guard de cargarDatos
    /// debe cortar antes de tocar Configuracion). Sin el guard, el test sale en rojo.
    /// </summary>
    [TestClass]
    public class SelectoresTeardownTests
    {
        [TestMethod]
        public void SelectorVendedor_CargarDatosConConfiguracionNull_NoLanza()
        {
            VerificarCargarDatosNullSafe(() => new SelectorVendedor());
        }

        [TestMethod]
        public void SelectorEmpresa_CargarDatosConConfiguracionNull_NoLanza()
        {
            VerificarCargarDatosNullSafe(() => new SelectorEmpresa());
        }

        [TestMethod]
        public void SelectorPlazosPago_CargarDatosConConfiguracionNull_NoLanza()
        {
            VerificarCargarDatosNullSafe(() => new SelectorPlazosPago());
        }

        // Instancia el selector (Configuracion sin asignar = null, el estado en el teardown), invoca su
        // 'cargarDatos' privado y verifica que no se postee ninguna excepción async-void al contexto.
        private static void VerificarCargarDatosNullSafe(Func<UserControl> crearSelector)
        {
            EjecutarEnSTA(() =>
            {
                var contexto = new ContextoCaptura();
                SynchronizationContext previo = SynchronizationContext.Current;
                SynchronizationContext.SetSynchronizationContext(contexto);
                try
                {
                    UserControl selector = crearSelector();

                    MethodInfo cargarDatos = selector.GetType().GetMethod(
                        "cargarDatos", BindingFlags.Instance | BindingFlags.NonPublic);
                    Assert.IsNotNull(cargarDatos,
                        $"No se encontró el método privado cargarDatos en {selector.GetType().Name}");

                    // 'cargarDatos' es async void: si lanza en su prefijo síncrono, la excepción no
                    // vuelve por aquí sino que se postea al SynchronizationContext actual (contexto).
                    cargarDatos.Invoke(selector, null);
                }
                finally
                {
                    SynchronizationContext.SetSynchronizationContext(previo);
                }

                Assert.IsNull(contexto.Capturada,
                    "cargarDatos lanzó con Configuracion null; sin el guard, esa excepción async-void " +
                    "se postea al Dispatcher y tumba la app al cerrar la ventana");
            });
        }

        // Captura de forma síncrona lo que AsyncVoidMethodBuilder.SetException postee (relanza la
        // excepción dentro del delegate posteado, que aquí ejecutamos y atrapamos).
        private sealed class ContextoCaptura : SynchronizationContext
        {
            public Exception Capturada { get; private set; }

            public override void Post(SendOrPostCallback d, object state) => Ejecutar(d, state);

            public override void Send(SendOrPostCallback d, object state) => Ejecutar(d, state);

            private void Ejecutar(SendOrPostCallback d, object state)
            {
                try
                {
                    d(state);
                }
                catch (Exception ex)
                {
                    Capturada ??= ex;
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
