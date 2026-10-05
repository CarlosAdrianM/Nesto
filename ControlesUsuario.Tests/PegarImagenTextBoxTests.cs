using ControlesUsuario.Dialogs;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Input;

namespace ControlesUsuario.Tests
{
    /// <summary>
    /// Nesto#504: con una imagen en el portapapeles, «Pegar» del menú de botón derecho del cuadro de texto tiene que
    /// salir HABILITADO y, al pulsarlo, adjuntar la imagen. Con texto, el pegado de siempre.
    /// </summary>
    [TestClass]
    public class PegarImagenTextBoxTests
    {
        private sealed class ComandoFalso : ICommand
        {
            public int Veces { get; private set; }
            public event EventHandler CanExecuteChanged { add { } remove { } }
            public bool CanExecute(object parameter) => true;
            public void Execute(object parameter) => Veces++;
        }

        private static (TextBox Cuadro, ComandoFalso Comando) Cuadro(bool hayImagen, bool hayTexto)
        {
            PegarImagenTextBox.HayImagenSinTexto = () => hayImagen && !hayTexto;
            var comando = new ComandoFalso();
            var cuadro = new TextBox();
            PegarImagenTextBox.SetComando(cuadro, comando);
            return (cuadro, comando);
        }

        [TestCleanup]
        public void Limpiar() => PegarImagenTextBox.HayImagenSinTexto = null;

        [TestMethod]
        public void ConUnaImagenCopiada_PegarSeHabilitaYAdjuntaLaImagen()
        {
            EjecutarEnSTA(() =>
            {
                var (cuadro, comando) = Cuadro(hayImagen: true, hayTexto: false);

                Assert.IsTrue(ApplicationCommands.Paste.CanExecute(null, cuadro), "«Pegar» tiene que salir activo en el menú");
                ApplicationCommands.Paste.Execute(null, cuadro);

                Assert.AreEqual(1, comando.Veces);
                Assert.AreEqual(string.Empty, cuadro.Text);
            });
        }

        [TestMethod]
        public void ConTextoCopiado_NoAdjuntaNada()
        {
            EjecutarEnSTA(() =>
            {
                var (cuadro, comando) = Cuadro(hayImagen: true, hayTexto: true);

                ApplicationCommands.Paste.Execute(null, cuadro);

                Assert.AreEqual(0, comando.Veces, "Con texto en el portapapeles se pega el texto, como siempre");
            });
        }

        private static void EjecutarEnSTA(Action accion)
        {
            Exception error = null;
            var hilo = new Thread(() =>
            {
                try { accion(); }
                catch (Exception ex) { error = ex; }
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
