using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.Rapports;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RapportsTests
{
    /// <summary>
    /// NestoAPI#876 (27/11/26): los rapports con «Incluir fichas equivalentes»: la URL con incluirEquivalentes=true, qué rapports son de
    /// otra ficha y que esos se abran solo para leer (no se guardan desde la lista de esta ficha).
    /// </summary>
    [TestClass]
    public class RapportsFichasEquivalentesTests
    {
        [TestMethod]
        public void Url_ConYSinEquivalentes()
        {
            Assert.AreEqual("SeguimientosClientes?empresa=1&cliente=15191&contacto=0", RapportService.UrlRapportsCliente("1", "15191", "0", false));
            Assert.AreEqual("SeguimientosClientes?empresa=1&cliente=15191&contacto=0&incluirEquivalentes=true",
                RapportService.UrlRapportsCliente("1", "15191", "0", true));
        }

        [TestMethod]
        public void MarcarFichas_SoloConLaCasilla_LosDeOtroCliente()
        {
            var propio = new SeguimientoClienteDTO { Id = 1, Cliente = "15191 " };
            var otro = new SeguimientoClienteDTO { Id = 2, Cliente = "8244" };
            var lista = new List<SeguimientoClienteDTO> { propio, otro, null };

            ListaRapportsViewModel.MarcarFichas(lista, true, "15191");
            Assert.IsFalse(propio.EsDeOtraFicha);
            Assert.IsTrue(otro.EsDeOtraFicha);
            Assert.AreEqual("Otra ficha", otro.FichaTexto);
            Assert.AreEqual("Esta ficha", propio.FichaTexto);

            ListaRapportsViewModel.MarcarFichas(lista, false, "15191");
            Assert.IsFalse(otro.EsDeOtraFicha);
            ListaRapportsViewModel.MarcarFichas(null, true, "15191"); // no revienta
        }

        [TestMethod]
        public void RapportDeOtraFicha_NoSeGuarda()
        {
            var configuracion = A.Fake<IConfiguracion>();
            A.CallTo(() => configuracion.usuario).Returns("carlos");
            A.CallTo(() => configuracion.leerParametro(A<string>._, A<string>._)).Returns(Task.FromResult("NV"));
            var vm = new RapportViewModel(configuracion, A.Fake<IRapportService>(), A.Fake<IServicioDialogos>(), new WeakReferenceMessenger());

            vm.AlLlegar(new ParametrosNavegacion { { "rapportParameter", new SeguimientoClienteDTO { Id = 7, Cliente = "15191", Usuario = "carlos" } } });
            Assert.IsTrue(vm.cmdGuardarCambios.CanExecute(null));
            Assert.IsFalse(vm.EsSoloLectura);

            vm.AlLlegar(new ParametrosNavegacion
            {
                { "rapportParameter", new SeguimientoClienteDTO { Id = 8, Cliente = "8244", Usuario = "carlos" } },
                { RapportViewModel.PARAMETRO_SOLO_LECTURA, "Es de otra ficha equivalente." }
            });
            Assert.IsFalse(vm.cmdGuardarCambios.CanExecute(null));
            Assert.IsTrue(vm.EsSoloLectura);
            Assert.AreEqual("Es de otra ficha equivalente.", vm.MotivoSoloLectura);
        }
    }
}
