using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Modulos.Rapports;

namespace RapportsTests
{
    /// <summary>
    /// Nesto#490 (4C.2, paso 3, punto 2): «Copiar seguimientos a otro cliente» es IDialogoNesto
    /// (DialogoNestoBase). (Hasta el 14/10/26 se probaba también con la ventana de Prism.)
    /// </summary>
    [TestClass]
    public class CopiarSeguimientosDialogoTests
    {
        private static CopiarSeguimientosViewModel CrearViewModel()
            => new(A.Fake<IRapportService>(), A.Fake<IConfiguracion>());

        [TestMethod]
        public void RecibeElClienteDeOrigenYSeCierraConAceptar()
        {
            var vm = CrearViewModel();
            IDialogoNesto dialogo = vm;
            ResultadoDialogo resultado = null;
            dialogo.RequestClose += r => resultado = r;

            dialogo.OnDialogOpened(new ParametrosDialogo { { "empresa", "3" }, { "cliente", "15191" }, { "contacto", "2" } });
            vm.CerrarCommand.Execute(null);

            Assert.AreEqual("Copiar seguimientos a otro cliente", dialogo.Title);
            Assert.AreEqual("3", vm.Empresa);
            Assert.AreEqual("15191", vm.ClienteOrigen);
            Assert.AreEqual("2", vm.ContactoOrigen);
            Assert.AreEqual(ResultadoBoton.OK, resultado.Result);
        }

        [TestMethod]
        public void SinEmpresa_SeQuedaLaDeSiempre()
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

            Assert.IsFalse(((IDialogoNesto)vm).CanCloseDialog());
        }
    }
}
