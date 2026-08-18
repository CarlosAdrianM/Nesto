using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Shared;
using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace Infrastructure.Tests
{
    /// <summary>
    /// Nesto#435: los elementos WPF exigen hilo STA, así que cada test corre en uno propio.
    /// </summary>
    [TestClass]
    public class ArbolVisualHelperTests
    {
        private static void EnSta(ThreadStart accion)
        {
            Thread hilo = new(accion);
            hilo.SetApartmentState(ApartmentState.STA);
            hilo.Start();
            hilo.Join();
        }

        [TestMethod]
        public void ObtenerPadreSeguro_ConUnRunDentroDeUnTextBlock_DevuelveElTextBlockSinLanzar()
        {
            // El caso real del crash: doble clic sobre el texto de una celda (Run no es Visual).
            Exception capturada = null;
            EnSta(() =>
            {
                try
                {
                    Run run = new("texto");
                    TextBlock textBlock = new(run);
                    DependencyObject padre = ArbolVisualHelper.ObtenerPadreSeguro(run);
                    Assert.AreSame(textBlock, padre);
                }
                catch (Exception ex)
                {
                    capturada = ex;
                }
            });
            Assert.IsNull(capturada, capturada?.ToString());
        }

        [TestMethod]
        public void ObtenerPadreSeguro_ConUnVisual_DevuelveElPadreVisual()
        {
            Exception capturada = null;
            EnSta(() =>
            {
                try
                {
                    TextBlock hijo = new();
                    StackPanel panel = new();
                    panel.Children.Add(hijo);
                    DependencyObject padre = ArbolVisualHelper.ObtenerPadreSeguro(hijo);
                    Assert.AreSame(panel, padre);
                }
                catch (Exception ex)
                {
                    capturada = ex;
                }
            });
            Assert.IsNull(capturada, capturada?.ToString());
        }

        [TestMethod]
        public void ObtenerPadreSeguro_ConAlgoQueNoEsDependencyObject_DevuelveNull()
        {
            Assert.IsNull(ArbolVisualHelper.ObtenerPadreSeguro("una cadena"));
            Assert.IsNull(ArbolVisualHelper.ObtenerPadreSeguro(null));
        }
    }
}
