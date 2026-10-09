using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.Cliente;
using Nesto.Modulos.Cliente.Models;
using Nesto.Modulos.Cliente.ViewModels;
using Prism.Services.Dialogs;
using System.Collections.Generic;

namespace ClienteTests
{
    /// <summary>
    /// Nesto#490 (4C.2, paso 3, punto 2): los diálogos de Cliente ya son IDialogoNesto (DialogoNestoBase) y
    /// tienen que abrirse igual con la ventana de Prism (usuarios sin el piloto VentanaDialogosPropia), que
    /// los ve como IDialogAware.
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
        public void NotificacionTelefono_ConLaVentanaDePrism_RecibeLosClientesYSeCierraConAceptar()
        {
            var clientes = new List<ClienteTelefonoLookup> { new() { Cliente = "15191" } };
            var vm = new NotificacionTelefonoViewModel();
            IDialogAware prism = vm;
            IDialogResult resultado = null;
            prism.RequestClose += r => resultado = r;

            prism.OnDialogOpened(new DialogParameters { { "clientesMismoTelefono", clientes } });
            vm.CloseDialogCommand.Execute("true");

            Assert.AreSame(clientes, vm.ClientesMismoTelefono);
            Assert.AreEqual("Clientes con el mismo teléfono:", prism.Title);
            Assert.AreEqual(ButtonResult.OK, resultado.Result);
        }

        [TestMethod]
        public void ElegirEvento_ConLaVentanaDePrism_DevuelveElIdDelEventoElegido()
        {
            var vm = new ElegirEventoDialogViewModel();
            IDialogAware prism = vm;
            IDialogResult resultado = null;
            prism.RequestClose += r => resultado = r;

            prism.OnDialogOpened(new DialogParameters
            {
                { "eventos", new List<EventoModel> { new() { Id = 4 }, new() { Id = 5 } } },
                { "apunte", "101" }
            });
            vm.Seleccionado = vm.Eventos[1];
            vm.AceptarCommand.Execute(null);

            Assert.AreEqual("101", vm.Apunte);
            Assert.AreEqual(ButtonResult.OK, resultado.Result);
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
