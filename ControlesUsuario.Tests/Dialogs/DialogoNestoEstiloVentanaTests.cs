using ControlesUsuario.Dialogs;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using System;
using System.Linq;
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

        #region Nesto#556 (sugerencia 757, Laura): el botón Guardar de «Parámetros de usuario» quedaba cortado

        [TestMethod]
        public void TrasPintarse_SigueAjustandoElAltoAlContenido_YSueltaElTopeDeAncho()
        {
            // Los parámetros llegan de la API después de pintarse: la ventana tiene que seguir creciendo con ellos
            EjecutarEnSTA(() =>
            {
                var servicio = new ServicioDialogosNesto(nombre => new Grid { DataContext = new DialogoDePrueba() });
                VentanaDialogo ventana = servicio.PrepararVentana("MiDialogo", null, null);

                ventana.AjustarTamanoTrasPintar();

                Assert.AreEqual(SizeToContent.Height, ventana.SizeToContent, "El alto sigue al contenido (antes, Manual: se quedaba cortado)");
                Assert.AreEqual(double.PositiveInfinity, ventana.MaxWidth, "El ancho ya lo puede agrandar el usuario");
                Assert.AreEqual(VentanaDialogo.AltoMaximo(), ventana.MaxHeight, "El tope de alto se mantiene: nunca más alta que la pantalla");
                Assert.IsTrue(ventana.MaxHeight >= 300 && !double.IsInfinity(ventana.MaxHeight));
                ventana.Close();
            });
        }

        [TestMethod]
        public void TrasPintarse_SueltaElTopeDeAnchoDeLosAvisos()
        {
            EjecutarEnSTA(() =>
            {
                var servicio = new ServicioDialogosNesto(nombre => new Grid { MaxWidth = VentanaDialogo.ANCHO_MAXIMO_INICIAL, DataContext = new DialogoDePrueba() });
                VentanaDialogo ventana = servicio.PrepararVentana("MiDialogo", null, null);

                ventana.AjustarTamanoTrasPintar();

                Assert.AreEqual(double.PositiveInfinity, ((FrameworkElement)ventana.Content).MaxWidth);
                ventana.Close();
            });
        }

        [TestMethod]
        public void TrasPintarse_ConEstiloPropio_NoTocaNada()
        {
            EjecutarEnSTA(() =>
            {
                var registrado = new DialogoRegistrado("NovedadesDialog", typeof(NovedadesDialog), typeof(NovedadesDialogViewModel));
                var servicio = new ServicioDialogosNesto(nombre => registrado.Crear(Activator.CreateInstance));
                VentanaDialogo ventana = servicio.PrepararVentana("NovedadesDialog", new ParametrosDialogo(), null);

                ventana.AjustarTamanoTrasPintar();

                Assert.AreEqual(SizeToContent.Manual, ventana.SizeToContent);
                Assert.AreEqual(700d, ventana.Height);
                ventana.Close();
            });
        }

        [TestMethod]
        public void ParametrosUsuario_LoVariableVaEnUnScrollViewer_YLosBotonesEnSuFila()
        {
            // Por si ni con el tope de alto cabe: los parámetros se desplazan dentro y Guardar / Cerrar siguen a la vista
            EjecutarEnSTA(() =>
            {
                var vista = new ParametrosUsuarioDialog();

                var scroll = vista.FindName("ScrollContenido") as ScrollViewer;
                Assert.IsNotNull(scroll, "El contenido variable va en un ScrollViewer");
                var grid = (Grid)scroll.Parent;
                Assert.AreEqual(GridUnitType.Star, grid.RowDefinitions[Grid.GetRow(scroll)].Height.GridUnitType, "En la fila que se encoge");
                Assert.AreEqual(ScrollBarVisibility.Auto, scroll.VerticalScrollBarVisibility);
                int filaBotones = grid.RowDefinitions.Count - 1;
                Assert.IsTrue(grid.RowDefinitions[filaBotones].Height.IsAuto, "Los botones, en su propia fila Auto");
                var botones = grid.Children.OfType<StackPanel>().Single(sp => Grid.GetRow(sp) == filaBotones);
                CollectionAssert.AreEqual(new[] { "Guardar", "Cerrar" }, botones.Children.OfType<Button>().Select(b => (string)b.Content).ToList());
            });
        }

        #endregion

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
