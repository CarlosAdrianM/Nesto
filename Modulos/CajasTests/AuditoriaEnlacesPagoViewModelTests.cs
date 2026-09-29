using FakeItEasy;
using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.Cajas.Interfaces;
using Nesto.Modulos.Cajas.Models;
using Nesto.Modulos.Cajas.Services;
using Nesto.Modulos.Cajas.ViewModels;

namespace CajasTests
{
    /// <summary>
    /// Nesto#261: consulta de auditoría de enlaces de pago para administración (solo lectura).
    /// </summary>
    [TestClass]
    public class AuditoriaEnlacesPagoViewModelTests
    {
        private IAuditoriaEnlacesPagoService servicio = null!;
        private IServicioDialogos dialogos = null!;
        private AuditoriaEnlacesPagoViewModel vm = null!;
        private FiltroAuditoriaEnlacesPago? filtroRecibido;

        [TestInitialize]
        public void Setup()
        {
            servicio = A.Fake<IAuditoriaEnlacesPagoService>();
            dialogos = A.Fake<IServicioDialogos>();
            filtroRecibido = null;
            vm = new AuditoriaEnlacesPagoViewModel(servicio, dialogos);
        }

        private static EnlacePagoAuditoriaModel Enlace(string numeroOrden, decimal importe = 100m) => new EnlacePagoAuditoriaModel
        {
            NumeroOrden = numeroOrden,
            Usuario = "NUEVAVISION\\JuanPerez",
            FechaCreacion = new DateTime(2025, 1, 15, 10, 30, 0),
            Cliente = "32366",
            NombreCliente = "PELUQUERÍA PEPA",
            Importe = importe,
            Estado = "Pendiente",
            Correo = "cliente@email.com"
        };

        private void DevolverAlBuscar(params EnlacePagoAuditoriaModel[] enlaces)
        {
            A.CallTo(() => servicio.Buscar(A<FiltroAuditoriaEnlacesPago>._))
                .Invokes((FiltroAuditoriaEnlacesPago f) => filtroRecibido = f)
                .Returns(enlaces.ToList());
        }

        [TestMethod]
        public void AlCrear_FiltraLosUltimos30Dias()
        {
            Assert.AreEqual(DateTime.Today, vm.FechaHasta);
            Assert.AreEqual(DateTime.Today.AddDays(-30), vm.FechaDesde);
            Assert.AreEqual(string.Empty, vm.Resumen, "Sin buscar todavía no se dice nada");
        }

        [TestMethod]
        public async Task Buscar_PasaLosFiltrosYPintaElResumen()
        {
            DevolverAlBuscar(Enlace("AAAAAAC1", 100m), Enlace("BBBBBBC2", 50.5m));
            vm.Cliente = " 32366 ";
            vm.Usuario = "Juan";
            vm.Estado = "Pendiente";

            await vm.BuscarAsync();

            Assert.IsNotNull(filtroRecibido);
            Assert.AreEqual("32366", filtroRecibido!.Cliente);
            Assert.AreEqual("Juan", filtroRecibido.Usuario);
            Assert.AreEqual("Pendiente", filtroRecibido.Estado);
            Assert.AreEqual(DateTime.Today.AddDays(-30), filtroRecibido.FechaDesde);
            Assert.AreEqual(2, vm.Enlaces.Count);
            Assert.AreEqual($"2 enlaces por {150.5m:N2} €.", vm.Resumen);
            Assert.IsNull(vm.EnlaceSeleccionado);
            Assert.IsFalse(vm.EstaOcupado);
        }

        [TestMethod]
        public async Task Buscar_EstadoVacio_NoFiltraPorEstado()
        {
            DevolverAlBuscar();
            vm.Estado = string.Empty;

            await vm.BuscarAsync();

            Assert.IsNull(filtroRecibido!.Estado);
            Assert.AreEqual("No hay enlaces de pago con esos filtros.", vm.Resumen);
        }

        [TestMethod]
        public async Task Buscar_PorIdentificador_IgnoraLasFechasYSeleccionaElEnlace()
        {
            DevolverAlBuscar(Enlace("B9BC22C32366"));
            vm.NumeroOrden = " B9BC22C32366 ";

            await vm.BuscarAsync();

            Assert.AreEqual("B9BC22C32366", filtroRecibido!.NumeroOrden);
            Assert.IsNull(filtroRecibido.FechaDesde);
            Assert.IsNull(filtroRecibido.FechaHasta);
            Assert.IsNotNull(vm.EnlaceSeleccionado);
            Assert.IsTrue(vm.HayEnlaceSeleccionado);
            StringAssert.Contains(vm.DetalleSeleccionado, "creado por NUEVAVISION\\JuanPerez el 15/01/2025 a las 10:30");
        }

        [TestMethod]
        public async Task Buscar_FechasAlReves_AvisaYNoLlamaALaApi()
        {
            vm.FechaDesde = new DateTime(2026, 9, 29);
            vm.FechaHasta = new DateTime(2026, 9, 1);

            await vm.BuscarAsync();

            A.CallTo(() => servicio.Buscar(A<FiltroAuditoriaEnlacesPago>._)).MustNotHaveHappened();
            A.CallTo(() => dialogos.ShowError(A<string>.That.Contains("fecha desde"))).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task Buscar_SiLaApiFalla_EnsenaElErrorYVaciaLaLista()
        {
            A.CallTo(() => servicio.Buscar(A<FiltroAuditoriaEnlacesPago>._))
                .ThrowsAsync(new Exception("No se pudieron consultar los enlaces de pago: solo pueden consultarlos Administración y Dirección"));

            await vm.BuscarAsync();

            Assert.AreEqual(0, vm.Enlaces.Count);
            Assert.IsFalse(vm.EstaOcupado);
            A.CallTo(() => dialogos.ShowError(A<string>.That.Contains("Administración"))).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void Describir_CuentaQuienCuandoYAdondeSeEnvio()
        {
            EnlacePagoAuditoriaModel enlace = Enlace("B9BC22C32366");
            enlace.Movil = "600123456";

            string texto = AuditoriaEnlacesPagoViewModel.Describir(enlace);

            StringAssert.Contains(texto, "Enlace B9BC22C32366: creado por NUEVAVISION\\JuanPerez el 15/01/2025 a las 10:30");
            StringAssert.Contains(texto, "cliente 32366 (PELUQUERÍA PEPA)");
            StringAssert.Contains(texto, "Enviado por correo a cliente@email.com y por SMS al 600123456.");
        }

        [TestMethod]
        public void Describir_SinCorreoNiMovil_LoDice()
        {
            EnlacePagoAuditoriaModel enlace = Enlace("X");
            enlace.Correo = null;

            StringAssert.Contains(AuditoriaEnlacesPagoViewModel.Describir(enlace), "No consta que se enviara por correo ni por SMS.");
            Assert.AreEqual(string.Empty, AuditoriaEnlacesPagoViewModel.Describir(null));
        }

        [TestMethod]
        public void ConstruirUrl_SoloLosFiltrosInformadosYEscapados()
        {
            Assert.AreEqual("Pagos/Auditoria", AuditoriaEnlacesPagoService.ConstruirUrl(new FiltroAuditoriaEnlacesPago()));
            Assert.AreEqual("Pagos/Auditoria?fechaDesde=2026-09-01&fechaHasta=2026-09-29&usuario=NUEVAVISION%5CSancho",
                AuditoriaEnlacesPagoService.ConstruirUrl(new FiltroAuditoriaEnlacesPago
                {
                    FechaDesde = new DateTime(2026, 9, 1),
                    FechaHasta = new DateTime(2026, 9, 29, 18, 0, 0),
                    Usuario = " NUEVAVISION\\Sancho ",
                    Cliente = "  "
                }));
        }
    }
}
