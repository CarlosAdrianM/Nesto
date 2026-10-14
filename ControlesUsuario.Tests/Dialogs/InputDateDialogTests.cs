using ControlesUsuario.Dialogs;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
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
            var servicio = new ServicioDialogosDePrueba
            {
                Respuesta = new ResultadoDialogo(ResultadoBoton.OK, new ParametrosDialogo { { "date", new DateTime(2026, 10, 6) } })
            };

            DateTime? fecha = servicio.GetDate("Fecha de entrega", "¿Cuándo?", new DateTime(2026, 10, 2));

            Assert.AreEqual("InputDateDialog", servicio.Nombre);
            Assert.AreEqual(new DateTime(2026, 10, 2), servicio.Parametros.GetValue<DateTime>("defaultDate"));
            Assert.AreEqual(new DateTime(2026, 10, 6), fecha);
        }

        [TestMethod]
        public void GetDate_TodaviaNoSeSabe_DevuelveNull()
        {
            var servicio = new ServicioDialogosDePrueba { Respuesta = new ResultadoDialogo(ResultadoBoton.Cancel) };

            Assert.IsNull(servicio.GetDate("t", "m", DateTime.Today));
        }
    }
}
