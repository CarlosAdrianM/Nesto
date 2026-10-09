using ControlesUsuario.Dialogs;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Prism.Services.Dialogs;
using System;

namespace ControlesUsuario.Tests.Dialogs
{
    /// <summary>
    /// NestoAPI#582: pedir una fecha (la de entrega de lo que se deja en carpeta), con la opción «Todavía no se sabe».
    /// </summary>
    [TestClass]
    public class InputDateDialogTests
    {
        [TestMethod]
        public void ViewModel_AlAbrir_TomaTituloMensajeYFechaPropuesta()
        {
            var vm = new InputDateDialogViewModel();

            vm.OnDialogOpened(new ParametrosDialogo { { "title", "Fecha de entrega" }, { "message", "¿Cuándo?" }, { "defaultDate", new DateTime(2026, 10, 2) } });

            Assert.AreEqual("Fecha de entrega", vm.Title);
            Assert.AreEqual("¿Cuándo?", vm.Message);
            Assert.AreEqual(new DateTime(2026, 10, 2), vm.Fecha);
        }

        [TestMethod]
        public void ViewModel_Aceptar_DevuelveLaFecha()
        {
            var vm = new InputDateDialogViewModel { Fecha = new DateTime(2026, 10, 6) };
            ResultadoDialogo resultado = null;
            vm.RequestClose += r => resultado = r;

            vm.AcceptCommand.Execute(null);

            Assert.AreEqual(ResultadoBoton.OK, resultado.Result);
            Assert.AreEqual(new DateTime(2026, 10, 6), resultado.Parameters.GetValue<DateTime>("date"));
        }

        [TestMethod]
        public void ViewModel_SinFechaElegida_NoSePuedeAceptar()
        {
            var vm = new InputDateDialogViewModel { Fecha = null };

            Assert.IsFalse(vm.AcceptCommand.CanExecute(null));
        }

        [TestMethod]
        public void ViewModel_TodaviaNoSeSabe_CierraSinFecha()
        {
            var vm = new InputDateDialogViewModel { Fecha = new DateTime(2026, 10, 6) };
            ResultadoDialogo resultado = null;
            vm.RequestClose += r => resultado = r;

            vm.NoSeSabeCommand.Execute(null);

            Assert.AreEqual(ResultadoBoton.Cancel, resultado.Result);
        }

        [TestMethod]
        public void GetDate_AbreInputDateDialogYDevuelveLaFechaElegida()
        {
            IDialogService prism = A.Fake<IDialogService>();
            string nombre = null;
            IDialogParameters enviados = null;
            A.CallTo(() => prism.ShowDialog(A<string>._, A<IDialogParameters>._, A<Action<IDialogResult>>._))
                .Invokes((string n, IDialogParameters p, Action<IDialogResult> cb) =>
                {
                    nombre = n;
                    enviados = p;
                    cb(new DialogResult(ButtonResult.OK, new DialogParameters { { "date", new DateTime(2026, 10, 6) } }));
                });

            DateTime? fecha = new ServicioDialogosPrism(prism).GetDate("Fecha de entrega", "¿Cuándo?", new DateTime(2026, 10, 2));

            Assert.AreEqual("InputDateDialog", nombre);
            Assert.AreEqual(new DateTime(2026, 10, 2), enviados.GetValue<DateTime>("defaultDate"));
            Assert.AreEqual(new DateTime(2026, 10, 6), fecha);
        }

        [TestMethod]
        public void GetDate_TodaviaNoSeSabe_DevuelveNull()
        {
            IDialogService prism = A.Fake<IDialogService>();
            A.CallTo(() => prism.ShowDialog(A<string>._, A<IDialogParameters>._, A<Action<IDialogResult>>._))
                .Invokes((string n, IDialogParameters p, Action<IDialogResult> cb) => cb(new DialogResult(ButtonResult.Cancel)));

            Assert.IsNull(new ServicioDialogosPrism(prism).GetDate("t", "m", DateTime.Today));
        }
    }
}
