using ControlesUsuario.Dialogs;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace ControlesUsuario.Tests.Dialogs
{
    /// <summary>
    /// Nesto#490 (4C.2, paso 3, punto 3): el estilo de ventana de un diálogo se pone con la propiedad adjunta propia
    /// <see cref="DialogoNesto.EstiloVentanaProperty"/> (antes, prism:Dialog.WindowStyle) y la ventana propia lo usa
    /// en vez del suyo, entero, como hacía Prism.
    /// </summary>
    [TestClass]
    public class DialogoNestoEstiloVentanaTests
    {
        private class DialogoDePrueba : DialogoNestoBase
        {
        }

        [TestMethod]
        public void SinEstilo_EsNull()
        {
            EjecutarEnSTA(() => Assert.IsNull(DialogoNesto.GetEstiloVentana(new Grid())));
        }

        [TestMethod]
        public void SinEstilo_LaVentanaPropiaUsaElSuyo()
        {
            EjecutarEnSTA(() =>
            {
                var servicio = new ServicioDialogosNesto(nombre => new Grid { DataContext = new DialogoDePrueba() });

                VentanaDialogo ventana = servicio.PrepararVentana("MiDialogo", null, null);

                Assert.AreEqual(SizeToContent.WidthAndHeight, ventana.SizeToContent);
                Assert.AreEqual(VentanaDialogo.ANCHO_MAXIMO_INICIAL, ventana.MaxWidth);
                ventana.Close();
            });
        }

        [TestMethod]
        public void NovedadesDialog_TraeSuEstilo_YLaVentanaPropiaLoUsa()
        {
            EjecutarEnSTA(() =>
            {
                var registrado = new DialogoRegistrado("NovedadesDialog", typeof(NovedadesDialog), typeof(NovedadesDialogViewModel));
                var servicio = new ServicioDialogosNesto(nombre => registrado.Crear(Activator.CreateInstance));

                VentanaDialogo ventana = servicio.PrepararVentana("NovedadesDialog", new ParametrosDialogo(), null);

                Style estilo = DialogoNesto.GetEstiloVentana((FrameworkElement)ventana.Content);
                Assert.IsNotNull(estilo, "NovedadesDialog trae su estilo de ventana");
                Assert.AreSame(estilo, ventana.Style);
                Assert.AreEqual(SizeToContent.Manual, ventana.SizeToContent);
                Assert.AreEqual(760d, ventana.Width);
                Assert.AreEqual(700d, ventana.Height);
                Assert.AreEqual(560d, ventana.MinWidth);
                Assert.AreEqual(420d, ventana.MinHeight);
                Assert.AreEqual(ResizeMode.CanResize, ventana.ResizeMode);
                ventana.Close();
            });
        }

        private static void EjecutarEnSTA(Action accion)
        {
            Exception error = null;
            var hilo = new Thread(() =>
            {
                try
                {
                    accion();
                }
                catch (Exception ex)
                {
                    error = ex;
                }
                finally
                {
                    Dispatcher.CurrentDispatcher.InvokeShutdown();
                }
            });
            hilo.SetApartmentState(ApartmentState.STA);
            hilo.Start();
            hilo.Join();
            if (error != null)
            {
                throw new AssertFailedException(error.Message, error);
            }
        }
    }
}
