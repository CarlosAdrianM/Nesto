using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Modulos.Rapports;
using Prism.Services.Dialogs;

namespace RapportsTests
{
    /// <summary>
    /// Nesto#490 (4C.2, paso 3, punto 2): «Copiar seguimientos a otro cliente» ya es IDialogoNesto
    /// (DialogoNestoBase) y tiene que abrirse igual con la ventana de Prism (usuarios sin el piloto
    /// VentanaDialogosPropia), que lo ve como IDialogAware.
    /// </summary>
    [TestClass]
    public class CopiarSeguimientosDialogoTests
    {
        private static CopiarSeguimientosViewModel CrearViewModel()
            => new(A.Fake<IRapportService>(), A.Fake<IConfiguracion>());

        [TestMethod]
        public void ConLaVentanaDePrism_RecibeElClienteDeOrigenYSeCierraConAceptar()
        {
            var vm = CrearViewModel();
            IDialogAware prism = vm;
            IDialogResult resultado = null;
            prism.RequestClose += r => resultado = r;

            prism.OnDialogOpened(new DialogParameters { { "empresa", "3" }, { "cliente", "15191" }, { "contacto", "2" } });
            vm.CerrarCommand.Execute(null);

            Assert.AreEqual("Copiar seguimientos a otro cliente", prism.Title);
            Assert.AreEqual("3", vm.Empresa);
            Assert.AreEqual("15191", vm.ClienteOrigen);
            Assert.AreEqual("2", vm.ContactoOrigen);
            Assert.AreEqual(ButtonResult.OK, resultado.Result);
        }

        [TestMethod]
        public void ConLaVentanaPropia_SinEmpresa_SeQuedaLaDeSiempre()
        {
            var vm = CrearViewModel();
            ResultadoDialogo resultado = null;
            vm.RequestClose += r => resultado = r;

            vm.OnDialogOpened(new ParametrosDialogo { { "cliente", "15191" } });
            vm.CerrarCommand.Execute(null);

            Assert.AreEqual(Constantes.Empresas.EMPRESA_DEFECTO, vm.Empresa);
            Assert.AreEqual("15191", vm.ClienteOrigen);
            Assert.AreEqual(ResultadoBoton.OK, resultado.Result);
        }

        [TestMethod]
        public void MientrasCopia_NoSePuedeCerrar()
        {
            var vm = CrearViewModel();

            vm.EstaProcesando = true;

            Assert.IsFalse(((IDialogAware)vm).CanCloseDialog());
            Assert.IsFalse(((IDialogoNesto)vm).CanCloseDialog());
        }
    }
}
