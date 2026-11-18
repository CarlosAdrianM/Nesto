using ControlesUsuario.Services;
using ControlesUsuario.ViewModels;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using System;
using System.Threading;
using System.Windows;

namespace ControlesUsuario.Tests
{
    /// <summary>
    /// Sugerencia 1331 (Reina): el detalle del pedido pinta el nombre y la dirección del cliente fuera del selector, en grande.
    /// El selector tiene MostrarNombreYDireccion (por defecto True, el resto de pantallas igual) para no repetirlos dentro.
    /// </summary>
    [TestClass]
    public class SelectorClienteMostrarNombreYDireccionTests
    {
        [TestMethod]
        public void MostrarNombreYDireccion_PorDefectoTrue()
        {
            Assert.AreEqual(true, SelectorCliente.MostrarNombreYDireccionProperty.DefaultMetadata.DefaultValue);
        }

        [TestMethod]
        public void MostrarNombreYDireccion_PorDefecto_SeVeElBloque_ConFalse_SeOculta()
        {
            EnSta(() =>
            {
                var vm = new SelectorClienteViewModel(A.Fake<IConfiguracion>(), A.Fake<ISelectorClienteService>());
                var sut = new SelectorCliente(vm, A.Fake<IServicioNavegacion>());
                sut.Measure(new Size(400, 400));
                sut.Arrange(new Rect(0, 0, 400, 400));

                Assert.IsTrue(sut.MostrarNombreYDireccion);
                Assert.AreEqual(Visibility.Visible, sut.pnlNombreYDireccion.Visibility);

                sut.MostrarNombreYDireccion = false;
                Assert.AreEqual(Visibility.Collapsed, sut.pnlNombreYDireccion.Visibility);

                sut.MostrarNombreYDireccion = true;
                Assert.AreEqual(Visibility.Visible, sut.pnlNombreYDireccion.Visibility);
            });
        }

        private static void EnSta(Action prueba)
        {
            Exception error = null;
            var hilo = new Thread(() =>
            {
                try
                {
                    prueba();
                }
                catch (Exception ex)
                {
                    error = ex;
                }
            });
            hilo.SetApartmentState(ApartmentState.STA);
            hilo.Start();
            if (!hilo.Join(TimeSpan.FromSeconds(30)))
            {
                Assert.Fail("La prueba no ha terminado");
            }
            if (error != null)
            {
                throw new AssertFailedException(error.ToString(), error);
            }
        }
    }
}
