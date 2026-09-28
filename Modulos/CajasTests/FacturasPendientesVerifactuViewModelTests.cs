using FakeItEasy;
using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.Cajas.Interfaces;
using Nesto.Modulos.Cajas.Models;
using Nesto.Modulos.Cajas.ViewModels;

namespace CajasTests
{
    /// <summary>
    /// NestoAPI#522: ventana de administración con las facturas pendientes de Verifactu (sin registrar o
    /// incorrectas en la AEAT), su motivo y el reintento del envío.
    /// </summary>
    [TestClass]
    public class FacturasPendientesVerifactuViewModelTests
    {
        private IFacturasVerifactuService servicio = null!;
        private IServicioDialogos dialogos = null!;
        private FacturasPendientesVerifactuViewModel vm = null!;

        [TestInitialize]
        public void Setup()
        {
            servicio = A.Fake<IFacturasVerifactuService>();
            dialogos = A.Fake<IServicioDialogos>();
            vm = new FacturasPendientesVerifactuViewModel(servicio, dialogos);
        }

        private static FacturaPendienteVerifactuModel Factura(string numero, string situacion = "Pendiente de enviar",
            bool puedeReintentar = true, string? motivo = null)
            => new FacturaPendienteVerifactuModel
            {
                Empresa = "1",
                Numero = numero,
                Fecha = new DateTime(2026, 9, 25),
                Cliente = "30676",
                Situacion = situacion,
                Motivo = motivo,
                PuedeReintentar = puedeReintentar
            };

        private async Task CargarCon(params FacturaPendienteVerifactuModel[] facturas)
        {
            A.CallTo(() => servicio.LeerFacturasPendientes()).Returns(facturas.ToList());
            await vm.CargarAsync();
        }

        [TestMethod]
        public async Task Cargar_PintaLasFacturasYElResumen()
        {
            await CargarCon(Factura("NV2615001"), Factura("NV2615002"), Factura("NV2615003", "Incorrecta en la AEAT"));

            Assert.AreEqual(3, vm.Facturas.Count);
            Assert.AreEqual("3 facturas: 2 sin registrar y 1 incorrecta en la AEAT.", vm.Resumen);
            Assert.IsFalse(vm.EstaOcupado);
        }

        [TestMethod]
        public async Task Cargar_SinPendientes_LoDice()
        {
            await CargarCon();

            Assert.AreEqual("No hay facturas pendientes de Verifactu.", vm.Resumen);
        }

        [TestMethod]
        public async Task Cargar_FallaLaApi_MuestraElErrorYDejaLaListaVacia()
        {
            A.CallTo(() => servicio.LeerFacturasPendientes()).Throws(new Exception("solo pueden verlas Administración y Dirección"));

            await vm.CargarAsync();

            Assert.AreEqual(0, vm.Facturas.Count);
            A.CallTo(() => dialogos.ShowError(A<string>.That.Contains("Administración"))).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task Reintentar_SoloConUnaFacturaQueSePuedaReintentar()
        {
            await CargarCon(Factura("NV2615001"), Factura("NV2615004", "Incidencia técnica de hace más de un día", puedeReintentar: false));

            Assert.IsFalse(vm.ReintentarCommand.CanExecute(null), "Sin factura seleccionada");
            vm.FacturaSeleccionada = vm.Facturas[1];
            Assert.IsFalse(vm.ReintentarCommand.CanExecute(null), "No hay camino para reenviarla desde aquí");
            vm.FacturaSeleccionada = vm.Facturas[0];
            Assert.IsTrue(vm.ReintentarCommand.CanExecute(null));
        }

        [TestMethod]
        public async Task Reintentar_Registrada_SaleDeLaListaYSeAvisa()
        {
            await CargarCon(Factura("NV2615001"), Factura("NV2615002"));
            vm.FacturaSeleccionada = vm.Facturas[0];
            A.CallTo(() => servicio.ReintentarFactura("1", "NV2615001"))
                .Returns(new ResultadoReintentoVerifactuModel { Exitoso = true, Mensaje = "La factura NV2615001 se ha enviado a Verifactu." });

            await vm.ReintentarAsync();

            Assert.AreEqual(1, vm.Facturas.Count);
            Assert.AreEqual("NV2615002", vm.Facturas[0].Numero);
            Assert.IsNull(vm.FacturaSeleccionada);
            Assert.AreEqual("1 factura: 1 sin registrar.", vm.Resumen);
            A.CallTo(() => dialogos.ShowNotification("Verifactu", "La factura NV2615001 se ha enviado a Verifactu.")).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task Reintentar_VuelveARechazarse_SeQuedaConElMotivoNuevoYSeMuestraElError()
        {
            await CargarCon(Factura("NV2615006", "Incorrecta en la AEAT", motivo: "AEAT (Incorrecto): NIF"));
            vm.FacturaSeleccionada = vm.Facturas[0];
            FacturaPendienteVerifactuModel actualizada = Factura("NV2615006", "Incorrecta en la AEAT", motivo: "(400) El campo nombre es obligatorio");
            A.CallTo(() => servicio.ReintentarFactura("1", "NV2615006"))
                .Returns(new ResultadoReintentoVerifactuModel { Exitoso = false, Mensaje = "Verifactu no ha aceptado la factura NV2615006: 400 El campo nombre es obligatorio", Factura = actualizada });

            await vm.ReintentarAsync();

            Assert.AreEqual(1, vm.Facturas.Count);
            Assert.AreSame(actualizada, vm.Facturas[0]);
            Assert.AreSame(actualizada, vm.FacturaSeleccionada);
            A.CallTo(() => dialogos.ShowError(A<string>.That.Contains("El campo nombre es obligatorio"))).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task Cargar_ConservaLaFacturaSeleccionada()
        {
            await CargarCon(Factura("NV2615001"), Factura("NV2615002"));
            vm.FacturaSeleccionada = vm.Facturas[1];

            await CargarCon(Factura("NV2615001"), Factura("NV2615002"));

            Assert.AreEqual("NV2615002", vm.FacturaSeleccionada?.Numero);
        }
    }
}
