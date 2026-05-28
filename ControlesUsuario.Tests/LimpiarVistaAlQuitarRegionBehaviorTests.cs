using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Shared;
using Prism.Regions;
using System;
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
