using ControlesUsuario.Dialogs;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using System;
using System.Collections.Generic;

namespace ControlesUsuario.Tests.Dialogs
{
    /// <summary>
    /// 25/11/26 (Paloma rellenó sin querer el inventario de Alcobendas): confirmar escribiendo el nombre o el código. Sin
    /// mayúsculas ni tildes; «Cancelar» por defecto; el botón de hacerlo solo se activa con lo que casa.
    /// </summary>
    [TestClass]
    public class ConfirmacionEscribiendoTests
    {
        private static readonly string[] Aceptables = { "ALC", "Alcobendas" };

        [DataTestMethod]
        [DataRow("ALC")]
        [DataRow("alc")]
        [DataRow("  Alcobendas ")]
        [DataRow("ALCOBENDAS")]
        [DataRow("Álcobendás")]
        public void Coincide_NombreOCodigoSinMayusculasNiTildes(string escrito)
        {
            Assert.IsTrue(ConfirmacionEscribiendo.Coincide(escrito, Aceptables));
        }

        [DataTestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("   ")]
        [DataRow("Alcoben")]
        [DataRow("REI")]
        [DataRow("sí")]
        public void Coincide_LoDemasNo(string escrito)
        {
            Assert.IsFalse(ConfirmacionEscribiendo.Coincide(escrito, Aceptables));
        }

        [TestMethod]
        public void Coincide_SinAceptables_No()
        {
            Assert.IsFalse(ConfirmacionEscribiendo.Coincide("ALC", null));
            Assert.IsFalse(ConfirmacionEscribiendo.Coincide("ALC", new string[0]));
        }

        [TestMethod]
        public void ViewModel_AceptarSoloConLoQueCasa_YDevuelveLoEscrito()
        {
            var vm = new ConfirmacionEscribiendoDialogViewModel();
            ResultadoDialogo resultado = null;
            vm.RequestClose += r => resultado = r;
            vm.OnDialogOpened(new ParametrosDialogo
            {
                { "title", "Rellenar inventario" },
                { "message", "Va a crear 3.032 filas" },
                { ConfirmacionEscribiendoDialogViewModel.PARAMETRO_TEXTO_ACEPTAR, "Rellenar" },
                { ConfirmacionEscribiendoDialogViewModel.PARAMETRO_ACEPTABLES, new List<string>(Aceptables) }
            });

            Assert.AreEqual("Rellenar", vm.TextoAceptar);
            Assert.IsFalse(vm.AceptarCommand.CanExecute(null));
            vm.Texto = "Alcob";
            Assert.IsFalse(vm.AceptarCommand.CanExecute(null));
            vm.Texto = "alcobendas ";
            Assert.IsTrue(vm.AceptarCommand.CanExecute(null));

            vm.AceptarCommand.Execute(null);

            Assert.AreEqual(ResultadoBoton.OK, resultado.Result);
            Assert.AreEqual("alcobendas", resultado.Parameters.GetValue<string>("text"));
        }

        [TestMethod]
        public void ViewModel_Cancelar_CierraConCancel()
        {
            var vm = new ConfirmacionEscribiendoDialogViewModel();
            ResultadoDialogo resultado = null;
            vm.RequestClose += r => resultado = r;

            vm.CancelarCommand.Execute(null);

            Assert.AreEqual(ResultadoBoton.Cancel, resultado.Result);
        }

        [TestMethod]
        public void Pedir_SoloTrueSiAceptaConLoQueCasa()
        {
            var dialogos = A.Fake<IServicioDialogos>();
            string escrito = "REI";
            A.CallTo(() => dialogos.ShowDialog(ConfirmacionEscribiendoDialogViewModel.NOMBRE, A<ParametrosDialogo>._, A<Action<ResultadoDialogo>>._))
                .Invokes((string n, ParametrosDialogo p, Action<ResultadoDialogo> cb) =>
                    cb(new ResultadoDialogo(ResultadoBoton.OK, new ParametrosDialogo { { "text", escrito } })));

            Assert.IsFalse(ConfirmacionEscribiendo.Pedir(dialogos, "t", "m", "i", "Rellenar", Aceptables));
            escrito = "alc";
            Assert.IsTrue(ConfirmacionEscribiendo.Pedir(dialogos, "t", "m", "i", "Rellenar", Aceptables));
        }

        [TestMethod]
        public void Pedir_Cancela_False()
        {
            var dialogos = A.Fake<IServicioDialogos>();
            A.CallTo(() => dialogos.ShowDialog(A<string>._, A<ParametrosDialogo>._, A<Action<ResultadoDialogo>>._))
                .Invokes((string n, ParametrosDialogo p, Action<ResultadoDialogo> cb) => cb(new ResultadoDialogo(ResultadoBoton.Cancel)));

            Assert.IsFalse(ConfirmacionEscribiendo.Pedir(dialogos, "t", "m", "i", "Rellenar", Aceptables));
        }
    }
}
