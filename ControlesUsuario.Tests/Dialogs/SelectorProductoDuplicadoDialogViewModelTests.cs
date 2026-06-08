using ControlesUsuario.Dialogs;
using ControlesUsuario.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Prism.Services.Dialogs;
using System.Collections.Generic;
using System.Linq;

namespace ControlesUsuario.Tests.Dialogs
{
    /// <summary>
    /// Tests del VM del selector de código de barras duplicado (Nesto#368).
    /// </summary>
    [TestClass]
    public class SelectorProductoDuplicadoDialogViewModelTests
    {
        private static DialogParameters CrearParametros(params ProductoCodigoBarrasDuplicado[] candidatos)
        {
            return new DialogParameters
            {
                { "candidatos", candidatos.ToList() }
            };
        }

        private static ProductoCodigoBarrasDuplicado Candidato(string producto, string nombre)
        {
            return new ProductoCodigoBarrasDuplicado { Producto = producto, Nombre = nombre };
        }

        [TestMethod]
        public void OnDialogOpened_ConCandidatos_RellenaListaYSeleccionaElPrimero()
        {
            var sut = new SelectorProductoDuplicadoDialogViewModel();

            sut.OnDialogOpened(CrearParametros(
                Candidato("45114", "Producto A"),
                Candidato("45115", "Producto B")));

            Assert.AreEqual(2, sut.Candidatos.Count);
            Assert.IsNotNull(sut.Seleccionado);
            Assert.AreEqual("45114", sut.Seleccionado.Producto);
        }

        [TestMethod]
        public void OnDialogOpened_DosVeces_NoAcumulaCandidatos()
        {
            var sut = new SelectorProductoDuplicadoDialogViewModel();

            sut.OnDialogOpened(CrearParametros(Candidato("45114", "A"), Candidato("45115", "B")));
            sut.OnDialogOpened(CrearParametros(Candidato("45116", "C")));

            Assert.AreEqual(1, sut.Candidatos.Count);
            Assert.AreEqual("45116", sut.Seleccionado.Producto);
        }

        [TestMethod]
        public void AceptarCommand_SinSeleccion_NoSePuedeEjecutar()
        {
            var sut = new SelectorProductoDuplicadoDialogViewModel();

            // Sin OnDialogOpened no hay candidatos ni selección
            Assert.IsFalse(sut.AceptarCommand.CanExecute());
        }

        [TestMethod]
        public void AceptarCommand_ConSeleccion_SePuedeEjecutar()
        {
            var sut = new SelectorProductoDuplicadoDialogViewModel();
            sut.OnDialogOpened(CrearParametros(Candidato("45114", "A")));

            Assert.IsTrue(sut.AceptarCommand.CanExecute());
        }

        [TestMethod]
        public void Aceptar_DevuelveOkConElNumeroElegido()
        {
            var sut = new SelectorProductoDuplicadoDialogViewModel();
            sut.OnDialogOpened(CrearParametros(
                Candidato("45114", "Producto A"),
                Candidato("45115", "Producto B")));
            sut.Seleccionado = sut.Candidatos.Single(c => c.Producto == "45115");

            IDialogResult resultadoCapturado = null;
            sut.RequestClose += r => resultadoCapturado = r;

            sut.AceptarCommand.Execute();

            Assert.IsNotNull(resultadoCapturado);
            Assert.AreEqual(ButtonResult.OK, resultadoCapturado.Result);
            Assert.AreEqual("45115", resultadoCapturado.Parameters.GetValue<string>("producto"));
        }

        [TestMethod]
        public void Cancelar_DevuelveCancelSinProducto()
        {
            var sut = new SelectorProductoDuplicadoDialogViewModel();
            sut.OnDialogOpened(CrearParametros(Candidato("45114", "A")));

            IDialogResult resultadoCapturado = null;
            sut.RequestClose += r => resultadoCapturado = r;

            sut.CancelarCommand.Execute();

            Assert.IsNotNull(resultadoCapturado);
            Assert.AreEqual(ButtonResult.Cancel, resultadoCapturado.Result);
        }
    }
}
