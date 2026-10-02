using ControlesUsuario.Dialogs;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using System;
using System.Threading.Tasks;

namespace ControlesUsuario.Tests.Dialogs
{
    /// <summary>
    /// Nesto#490 (4C.2): el envoltorio de hilo de UI del cuadre de banco (caso real 21/08/26) pasa
    /// de IDialogService a IServicioDialogos. Mismas garantías que tenía el envoltorio de Prism (DialogServiceEnHiloUi, borrado el 02/10/26).
    /// </summary>
    [TestClass]
    public class ServicioDialogosEnHiloUiTests
    {
        [TestMethod]
        public void ShowNotification_DelegaEnElServicioEnvuelto()
        {
            IServicioDialogos interno = A.Fake<IServicioDialogos>();
            var envuelto = new ServicioDialogosEnHiloUi(interno);

            envuelto.ShowNotification("Aviso", "Hola");

            A.CallTo(() => interno.ShowNotification("Aviso", "Hola")).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void GetAmount_DevuelveLoQueContestaElServicioEnvuelto()
        {
            IServicioDialogos interno = A.Fake<IServicioDialogos>();
            A.CallTo(() => interno.GetAmount("Financiación parcial", A<string>._)).Returns(18546.63m);
            var envuelto = new ServicioDialogosEnHiloUi(interno);

            Assert.AreEqual(18546.63m, envuelto.GetAmount("Financiación parcial", "Importe"));
        }

        /// <summary>El caso que motivó el envoltorio: la regla pregunta desde un Task.Run. Sin
        /// Application no hay dispatcher, así que se ejecuta directamente y la respuesta llega.</summary>
        [TestMethod]
        public async Task ShowConfirmationAnswer_DesdeUnHiloDePool_NoRevientaYDevuelveLaRespuesta()
        {
            IServicioDialogos interno = A.Fake<IServicioDialogos>();
            A.CallTo(() => interno.ShowConfirmationAnswer(A<string>._, A<string>._)).Returns(true);
            var envuelto = new ServicioDialogosEnHiloUi(interno);

            bool respuesta = await Task.Run(() => envuelto.ShowConfirmationAnswer("Contabilizar", "¿Seguro?"));

            Assert.IsTrue(respuesta);
        }

        [TestMethod]
        public void ShowDialog_EsSincrono_ElCallbackSeHaEjecutadoAlVolver()
        {
            IServicioDialogos interno = A.Fake<IServicioDialogos>();
            A.CallTo(() => interno.ShowDialog(A<string>._, A<ParametrosDialogo>._, A<Action<ResultadoDialogo>>._))
                .Invokes((string _, ParametrosDialogo _, Action<ResultadoDialogo> callback) => callback?.Invoke(new ResultadoDialogo(ResultadoBoton.OK)));
            var envuelto = new ServicioDialogosEnHiloUi(interno);
            bool confirmado = false;

            envuelto.ShowDialog("ConfirmationDialog", new ParametrosDialogo(), r => confirmado = r.Result == ResultadoBoton.OK);

            Assert.IsTrue(confirmado);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Constructor_SinServicioInterno_Lanza()
        {
            _ = new ServicioDialogosEnHiloUi(null);
        }
    }
}
