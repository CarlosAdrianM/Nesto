using ControlesUsuario.Dialogs;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Prism.Services.Dialogs;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ControlesUsuario.Tests.Dialogs
{
    /// <summary>
    /// Caso real 21/08/26 (cuadre de banco, regla de línea de riesgo Caixabank): al contabilizar
    /// el apunte saltaba "An unexpected error occured while resolving
    /// 'Prism.Services.Dialogs.IDialogWindow'".
    ///
    /// El arreglo de NestoAPI#384/#386 pasó a ejecutar las reglas dentro de un Task.Run, pero
    /// varias le preguntan cosas al usuario y WPF no puede crear una Window fuera del hilo de UI.
    /// Este envoltorio devuelve los diálogos al hilo bueno.
    /// </summary>
    [TestClass]
    public class DialogServiceEnHiloUiTests
    {
        [TestMethod]
        public void ShowDialog_DelegaEnElServicioEnvuelto()
        {
            IDialogService interno = A.Fake<IDialogService>();
            var envuelto = new DialogServiceEnHiloUi(interno);
            var parametros = new DialogParameters();

            envuelto.ShowDialog("ConfirmationDialog", parametros, null);

            A.CallTo(() => interno.ShowDialog("ConfirmationDialog", parametros, null)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void Show_DelegaEnElServicioEnvuelto()
        {
            IDialogService interno = A.Fake<IDialogService>();
            var envuelto = new DialogServiceEnHiloUi(interno);
            var parametros = new DialogParameters();

            envuelto.Show("NotificationDialog", parametros, null);

            A.CallTo(() => interno.Show("NotificationDialog", parametros, null)).MustHaveHappenedOnceExactly();
        }

        /// <summary>
        /// El caso que motivó todo: la llamada nace en un hilo de pool (Task.Run). Sin Application
        /// no hay dispatcher al que saltar, así que se ejecuta igualmente en vez de reventar: los
        /// tests y cualquier proceso sin UI siguen funcionando.
        /// </summary>
        [TestMethod]
        public async Task ShowDialog_DesdeUnHiloDePool_NoRevientaYLlega()
        {
            IDialogService interno = A.Fake<IDialogService>();
            var envuelto = new DialogServiceEnHiloUi(interno);
            int hiloLlamante = Thread.CurrentThread.ManagedThreadId;
            int hiloEjecucion = 0;
            A.CallTo(() => interno.ShowDialog(A<string>.Ignored, A<IDialogParameters>.Ignored, A<Action<IDialogResult>>.Ignored))
                .Invokes(() => hiloEjecucion = Thread.CurrentThread.ManagedThreadId);

            await Task.Run(() => envuelto.ShowDialog("ConfirmationDialog", new DialogParameters(), null));

            A.CallTo(() => interno.ShowDialog("ConfirmationDialog", A<IDialogParameters>.Ignored, null))
                .MustHaveHappenedOnceExactly();
            Assert.AreNotEqual(0, hiloEjecucion, "El diálogo tiene que haberse llegado a pedir");
        }

        /// <summary>Las extensiones que devuelven valor (GetAmount, ShowConfirmationAnswer) recogen
        /// el resultado en el callback de un ShowDialog modal: el envoltorio tiene que ser SÍNCRONO
        /// o esas extensiones devolverían siempre el valor por defecto.</summary>
        [TestMethod]
        public void ShowDialog_EsSincrono_ElCallbackSeHaEjecutadoAlVolver()
        {
            IDialogService interno = A.Fake<IDialogService>();
            A.CallTo(() => interno.ShowDialog(A<string>.Ignored, A<IDialogParameters>.Ignored, A<Action<IDialogResult>>.Ignored))
                .Invokes((string _, IDialogParameters _, Action<IDialogResult> callback) =>
                {
                    IDialogResult resultado = A.Fake<IDialogResult>();
                    A.CallTo(() => resultado.Result).Returns(ButtonResult.OK);
                    callback?.Invoke(resultado);
                });
            var envuelto = new DialogServiceEnHiloUi(interno);
            bool confirmado = false;

            envuelto.ShowDialog("ConfirmationDialog", new DialogParameters(), r => confirmado = r.Result == ButtonResult.OK);

            Assert.IsTrue(confirmado, "Si no fuese síncrono, GetAmount y ShowConfirmationAnswer devolverían siempre el valor por defecto");
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Constructor_SinServicioInterno_Lanza()
        {
            _ = new DialogServiceEnHiloUi(null);
        }
    }
}
