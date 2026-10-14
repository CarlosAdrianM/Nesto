using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.Cliente;
using Nesto.Modulos.Cliente.Models;
using Nesto.Modulos.Cliente.ViewModels;
using System.Collections.Generic;

namespace ClienteTests
{
    /// <summary>
    /// Nesto#490 (4C.2, paso 3, punto 2): los diálogos de Cliente son IDialogoNesto (DialogoNestoBase).
    /// (Hasta el 14/10/26 se probaban también con la ventana de Prism.)
    /// </summary>
    [TestClass]
    public class DialogosClienteTests
    {
        [TestMethod]
        public void NotificacionTelefono_RecibeLosClientesConElMismoTelefono()
        {
            var clientes = new List<ClienteTelefonoLookup> { new() { Empresa = "1", Cliente = "15191", Contacto = "0", Nombre = "Peluquería Pepa" } };
            var vm = new NotificacionTelefonoViewModel();

            vm.OnDialogOpened(new ParametrosDialogo { { "clientesMismoTelefono", clientes } });

            Assert.AreSame(clientes, vm.ClientesMismoTelefono);
            Assert.AreEqual("Clientes con el mismo teléfono:", vm.Title);
        }

        [TestMethod]
        public void NotificacionTelefono_RecibeLosClientesYSeCierraConAceptar()
        {
            var clientes = new List<ClienteTelefonoLookup> { new() { Cliente = "15191" } };
            var vm = new NotificacionTelefonoViewModel();
            IDialogoNesto dialogo = vm;
            ResultadoDialogo resultado = null;
            dialogo.RequestClose += r => resultado = r;

            dialogo.OnDialogOpened(new ParametrosDialogo { { "clientesMismoTelefono", clientes } });
            vm.CloseDialogCommand.Execute("true");

            Assert.AreSame(clientes, vm.ClientesMismoTelefono);
            Assert.AreEqual("Clientes con el mismo teléfono:", dialogo.Title);
            Assert.AreEqual(ResultadoBoton.OK, resultado.Result);
        }

        [TestMethod]
        public void ElegirEvento_DevuelveElIdDelEventoElegido()
        {
            var vm = new ElegirEventoDialogViewModel();
            IDialogoNesto dialogo = vm;
            ResultadoDialogo resultado = null;
            dialogo.RequestClose += r => resultado = r;

            dialogo.OnDialogOpened(new ParametrosDialogo
            {
                { "eventos", new List<EventoModel> { new() { Id = 4 }, new() { Id = 5 } } },
                { "apunte", "101" }
            });
            vm.Seleccionado = vm.Eventos[1];
            vm.AceptarCommand.Execute(null);

            Assert.AreEqual("101", vm.Apunte);
            Assert.AreEqual(ResultadoBoton.OK, resultado.Result);
            Assert.AreEqual(5, resultado.Parameters.GetValue<int>("eventoId"));
        }

        [TestMethod]
        public void ElegirEvento_Cancelar_CierraSinEvento()
        {
            var vm = new ElegirEventoDialogViewModel();
            ResultadoDialogo resultado = null;
            vm.RequestClose += r => resultado = r;

            vm.CancelarCommand.Execute(null);

            Assert.AreEqual(ResultadoBoton.Cancel, resultado.Result);
            Assert.IsFalse(resultado.Parameters.ContainsKey("eventoId"));
        }
    }
}
