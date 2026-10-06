using FakeItEasy;
using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.Cajas.Interfaces;
using Nesto.Modulos.Cajas.Models;
using Nesto.Modulos.Cajas.ViewModels;

namespace CajasTests
{
    /// <summary>
    /// Nesto#511 (incidencia 510 de Aida): con el botón derecho en la lista de deudas se copia el número de documento o el
    /// importe pendiente de la fila bajo el cursor (Ctrl+C copiaba la fila entera).
    /// </summary>
    [TestClass]
    public class CajasCopiarDeudaTests
    {
        private IServicioDialogos dialogos;
        private CajasViewModel vm;
        private readonly List<string> copiado = new();

        [TestInitialize]
        public void Preparar()
        {
            dialogos = A.Fake<IServicioDialogos>();
            vm = new CajasViewModel(A.Fake<IContabilidadService>(), A.Fake<IConfiguracion>(), dialogos,
                A.Fake<IClientesService>(), A.Fake<IServicioAutenticacion>());
            vm.CopiarAlPortapapeles = copiado.Add;
        }

        private static ExtractoClienteDTO Deuda() => new()
        {
            Id = 1, Documento = "NV26/012345 ", Importe = 1500M, ImportePendiente = 1234.5M
        };

        [TestMethod]
        public void CopiarNumeroDocumento_CopiaElDocumentoDeLaDeudaBajoElCursorSinEspacios()
        {
            vm.EstablecerDeudaBajoCursor(Deuda());

            Assert.IsTrue(vm.CopiarNumeroDocumentoCommand.CanExecute(null));
            vm.CopiarNumeroDocumentoCommand.Execute(null);

            CollectionAssert.AreEqual(new[] { "NV26/012345" }, copiado);
        }

        [TestMethod]
        public void CopiarImportePendiente_CopiaElPendienteSinMonedaNiMiles()
        {
            vm.EstablecerDeudaBajoCursor(Deuda());

            vm.CopiarImportePendienteCommand.Execute(null);

            CollectionAssert.AreEqual(new[] { "1234,50" }, copiado);
        }

        [TestMethod]
        public void SinDeudaBajoElCursor_NoSePuedeCopiar()
        {
            vm.EstablecerDeudaBajoCursor(null);

            Assert.IsFalse(vm.CopiarNumeroDocumentoCommand.CanExecute(null));
            Assert.IsFalse(vm.CopiarImportePendienteCommand.CanExecute(null));
            vm.CopiarNumeroDocumentoCommand.Execute(null);
            Assert.AreEqual(0, copiado.Count);
        }

        [TestMethod]
        public void CopiarNumeroDocumento_DeudaSinDocumento_NoSePuede()
        {
            vm.EstablecerDeudaBajoCursor(new ExtractoClienteDTO { Documento = "  ", ImportePendiente = 10M });

            Assert.IsFalse(vm.CopiarNumeroDocumentoCommand.CanExecute(null));
            Assert.IsTrue(vm.CopiarImportePendienteCommand.CanExecute(null));
        }

        [TestMethod]
        public void Copiar_SiFallaElPortapapeles_AvisaDelError()
        {
            vm.CopiarAlPortapapeles = _ => throw new InvalidOperationException("ocupado");
            vm.EstablecerDeudaBajoCursor(Deuda());

            vm.CopiarNumeroDocumentoCommand.Execute(null);

            A.CallTo(() => dialogos.ShowError(A<string>.That.Contains("ocupado"))).MustHaveHappenedOnceExactly();
        }
    }
}
