using ControlesUsuario.Dialogs;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.PedidoVenta;
using Nesto.Modulos.PedidoVenta.Services;
using System;
using System.Threading.Tasks;

namespace PedidoVentaTests
{
    /// <summary>
    /// Nesto#640 (NestoAPI#864): el diálogo «Pago a plazos»: canal (la app solo si el cliente la tiene), «Solicitar» sin cerrar,
    /// y después el enlace con «Copiar enlace» o a cuántos dispositivos se avisó.
    /// </summary>
    [TestClass]
    public class PagoAPlazosDialogViewModelTests
    {
        private const int NUMERO = 930500;

        private static DisponibilidadPagoAPlazosModel Disponibilidad(bool tieneApp = true, string canal = "App", string aviso = null, bool pruebas = false)
            => new DisponibilidadPagoAPlazosModel
            {
                Disponible = true,
                Proveedor = "Prueba",
                NombreProveedor = "Financiera Prueba",
                Importe = 250.5m,
                ClienteTieneApp = tieneApp,
                Canal = canal,
                Aviso = aviso,
                Pruebas = pruebas
            };

        private static PagoAPlazosDialogViewModel Vm(IServicioPagoAPlazos servicio, DisponibilidadPagoAPlazosModel disponibilidad, IPortapapelesTexto portapapeles = null)
        {
            var vm = new PagoAPlazosDialogViewModel(servicio) { Portapapeles = portapapeles ?? A.Fake<IPortapapelesTexto>() };
            vm.OnDialogOpened(new ParametrosDialogo
            {
                { PagoAPlazosDialogViewModel.PARAMETRO_EMPRESA, "1" },
                { PagoAPlazosDialogViewModel.PARAMETRO_PEDIDO, NUMERO },
                { PagoAPlazosDialogViewModel.PARAMETRO_DISPONIBILIDAD, disponibilidad }
            });
            return vm;
        }

        private static IServicioPagoAPlazos ServicioQueDevuelve(SolicitudPagoAPlazosModel solicitud)
        {
            IServicioPagoAPlazos servicio = A.Fake<IServicioPagoAPlazos>();
            A.CallTo(() => servicio.Solicitar(A<string>._, A<int>._, A<string>._, A<string>._)).Returns(Task.FromResult(solicitud));
            return servicio;
        }

        #region Al abrir

        [TestMethod]
        public void AlAbrir_EnsenaProveedorImporteYAviso()
        {
            var vm = Vm(A.Fake<IServicioPagoAPlazos>(), Disponibilidad(aviso: "El pedido va contra reembolso.", pruebas: true));

            Assert.AreEqual("Pago a plazos del pedido 930500", vm.Title);
            Assert.AreEqual("Financiera Prueba", vm.NombreProveedor);
            Assert.AreEqual("Financiar 250,50 € con Financiera Prueba", vm.TextoOferta);
            Assert.IsTrue(vm.HayAviso);
            Assert.AreEqual("El pedido va contra reembolso.", vm.Aviso);
            Assert.IsTrue(vm.EsPruebas);
            Assert.IsTrue(vm.Eligiendo);
            Assert.IsFalse(vm.Solicitada);
            Assert.IsTrue(vm.SolicitarCommand.CanExecute(null));
            Assert.AreEqual("Cancelar", vm.TextoBotonCerrar);
        }

        [TestMethod]
        public void AlAbrir_ClienteConAppYCanalSugeridoApp_PreseleccionaLaApp()
        {
            var vm = Vm(A.Fake<IServicioPagoAPlazos>(), Disponibilidad(tieneApp: true, canal: "App"));

            Assert.IsTrue(vm.ClienteTieneApp);
            Assert.IsTrue(vm.EsPorApp);
            Assert.IsFalse(vm.EsPorEnlace);
        }

        [TestMethod]
        public void AlAbrir_ClienteSinApp_SoloEnlace()
        {
            var vm = Vm(A.Fake<IServicioPagoAPlazos>(), Disponibilidad(tieneApp: false, canal: "Enlace"));

            Assert.IsFalse(vm.ClienteTieneApp);
            Assert.IsTrue(vm.EsPorEnlace);

            vm.EsPorApp = true; // no se puede elegir la app si el cliente no la tiene

            Assert.IsTrue(vm.EsPorEnlace);
            Assert.AreEqual("Enlace", vm.Canal);
        }

        [TestMethod]
        public void CambiarDeCanal_ConApp_SePuedePasarAEnlaceYVolver()
        {
            var vm = Vm(A.Fake<IServicioPagoAPlazos>(), Disponibilidad());

            vm.EsPorEnlace = true;
            Assert.AreEqual("Enlace", vm.Canal);
            Assert.IsFalse(vm.EsPorApp);

            vm.EsPorApp = true;
            Assert.AreEqual("App", vm.Canal);
        }

        [TestMethod]
        public void SinAviso_NoSePinta()
        {
            var vm = Vm(A.Fake<IServicioPagoAPlazos>(), Disponibilidad());

            Assert.IsFalse(vm.HayAviso);
            Assert.IsFalse(vm.EsPruebas);
        }

        #endregion

        #region Solicitar

        [TestMethod]
        public async Task Solicitar_PorEnlace_MandaCanalYCorreoYEnsenaElEnlace()
        {
            IServicioPagoAPlazos servicio = ServicioQueDevuelve(new SolicitudPagoAPlazosModel
            {
                HaySolicitud = true,
                Estado = "Pendiente",
                Canal = "Enlace",
                UrlPago = "https://pago.prueba.test/PAP-7",
                Aviso = "Si el cliente tuviera la app, podría terminar el pago ahí."
            });
            var vm = Vm(servicio, Disponibilidad(tieneApp: false, canal: "Enlace"));
            vm.Correo = "cliente@prueba.test";

            await vm.SolicitarAsync();

            A.CallTo(() => servicio.Solicitar("1", NUMERO, "Enlace", "cliente@prueba.test")).MustHaveHappenedOnceExactly();
            Assert.IsTrue(vm.Solicitada);
            Assert.IsFalse(vm.Eligiendo);
            Assert.IsTrue(vm.HayEnlace);
            Assert.AreEqual("https://pago.prueba.test/PAP-7", vm.UrlPago);
            Assert.IsTrue(vm.CopiarEnlaceCommand.CanExecute(null));
            Assert.IsTrue(vm.HayAvisoResultado);
            Assert.AreEqual("Si el cliente tuviera la app, podría terminar el pago ahí.", vm.AvisoResultado);
            StringAssert.StartsWith(vm.TextoResultado, "Solicitud creada. Copie el enlace");
            Assert.IsFalse(vm.SolicitarCommand.CanExecute(null));
            Assert.AreEqual("Cerrar", vm.TextoBotonCerrar);
            Assert.IsFalse(vm.HayError);
        }

        [TestMethod]
        public async Task Solicitar_PorApp_DiceACuantosDispositivosSeAviso_YNoHayEnlace()
        {
            IServicioPagoAPlazos servicio = ServicioQueDevuelve(new SolicitudPagoAPlazosModel
            {
                HaySolicitud = true,
                Estado = "Pendiente",
                Canal = "App",
                DispositivosAvisados = 2
            });
            var vm = Vm(servicio, Disponibilidad());

            await vm.SolicitarAsync();

            A.CallTo(() => servicio.Solicitar("1", NUMERO, "App", null)).MustHaveHappenedOnceExactly();
            Assert.AreEqual("Aviso enviado a la app del cliente (2 dispositivos): el cliente termina el pago ahí.", vm.TextoResultado);
            Assert.IsFalse(vm.HayEnlace);
            Assert.IsFalse(vm.CopiarEnlaceCommand.CanExecute(null));
            Assert.IsFalse(vm.HayAvisoResultado);
        }

        [TestMethod]
        public async Task Solicitar_PorAppSinLlegarANadie_VieneElEnlaceYSeEnsena()
        {
            IServicioPagoAPlazos servicio = ServicioQueDevuelve(new SolicitudPagoAPlazosModel
            {
                HaySolicitud = true,
                Estado = "Pendiente",
                Canal = "App",
                DispositivosAvisados = 0,
                UrlPago = "https://pago.prueba.test/PAP-8",
                Aviso = "No se ha podido avisar al cliente en la app: mándele el enlace."
            });
            var vm = Vm(servicio, Disponibilidad());

            await vm.SolicitarAsync();

            Assert.IsTrue(vm.HayEnlace);
            Assert.AreEqual("El aviso no ha llegado a ningún dispositivo del cliente. Copie el enlace y mándeselo usted.", vm.TextoResultado);
            Assert.AreEqual("No se ha podido avisar al cliente en la app: mándele el enlace.", vm.AvisoResultado);
        }

        [TestMethod]
        public async Task Solicitar_LaApiLoRechaza_SaleSuMensajeTalCualYSePuedeReintentar()
        {
            IServicioPagoAPlazos servicio = A.Fake<IServicioPagoAPlazos>();
            A.CallTo(() => servicio.Solicitar(A<string>._, A<int>._, A<string>._, A<string>._))
                .ThrowsAsync(new Exception("El cliente no tiene correo: indique uno."));
            var vm = Vm(servicio, Disponibilidad());

            await vm.SolicitarAsync();

            Assert.IsTrue(vm.HayError);
            Assert.AreEqual("El cliente no tiene correo: indique uno.", vm.TextoError);
            Assert.IsFalse(vm.Solicitada);
            Assert.IsFalse(vm.Trabajando);
            Assert.IsTrue(vm.SolicitarCommand.CanExecute(null));
        }

        [TestMethod]
        public async Task Solicitar_TrasUnError_AlReintentarSeBorraElError()
        {
            IServicioPagoAPlazos servicio = A.Fake<IServicioPagoAPlazos>();
            A.CallTo(() => servicio.Solicitar(A<string>._, A<int>._, A<string>._, A<string>._))
                .ThrowsAsync(new Exception("El cliente no tiene correo: indique uno.")).Once()
                .Then.Returns(Task.FromResult(new SolicitudPagoAPlazosModel { HaySolicitud = true, Estado = "Pendiente", Canal = "App", DispositivosAvisados = 1 }));
            var vm = Vm(servicio, Disponibilidad());

            await vm.SolicitarAsync();
            vm.Correo = "cliente@prueba.test";
            await vm.SolicitarAsync();

            Assert.IsFalse(vm.HayError);
            Assert.IsTrue(vm.Solicitada);
        }

        [TestMethod]
        public async Task Solicitar_YaSolicitada_NoVuelveALlamar()
        {
            IServicioPagoAPlazos servicio = ServicioQueDevuelve(new SolicitudPagoAPlazosModel { HaySolicitud = true, Estado = "Pendiente", Canal = "App", DispositivosAvisados = 1 });
            var vm = Vm(servicio, Disponibilidad());

            await vm.SolicitarAsync();
            await vm.SolicitarAsync();

            A.CallTo(() => servicio.Solicitar(A<string>._, A<int>._, A<string>._, A<string>._)).MustHaveHappenedOnceExactly();
        }

        #endregion

        #region Copiar y cerrar

        [TestMethod]
        public async Task CopiarEnlace_LoCopiaAlPortapapelesYLoDice()
        {
            IPortapapelesTexto portapapeles = A.Fake<IPortapapelesTexto>();
            var vm = Vm(ServicioQueDevuelve(new SolicitudPagoAPlazosModel { HaySolicitud = true, Estado = "Pendiente", Canal = "Enlace", UrlPago = "https://pago.prueba.test/PAP-7" }),
                Disponibilidad(tieneApp: false, canal: "Enlace"), portapapeles);
            await vm.SolicitarAsync();

            vm.CopiarEnlaceCommand.Execute(null);

            A.CallTo(() => portapapeles.CopiarTexto("https://pago.prueba.test/PAP-7")).MustHaveHappenedOnceExactly();
            Assert.AreEqual("Enlace copiado. Péguelo en el mensaje para el cliente.", vm.TextoCopiado);
        }

        [TestMethod]
        public async Task CopiarEnlace_ElPortapapelesFalla_NoRompeYLoDice()
        {
            IPortapapelesTexto portapapeles = A.Fake<IPortapapelesTexto>();
            A.CallTo(() => portapapeles.CopiarTexto(A<string>._)).Throws(new Exception("Portapapeles ocupado"));
            var vm = Vm(ServicioQueDevuelve(new SolicitudPagoAPlazosModel { HaySolicitud = true, Estado = "Pendiente", Canal = "Enlace", UrlPago = "https://pago.prueba.test/PAP-7" }),
                Disponibilidad(tieneApp: false, canal: "Enlace"), portapapeles);
            await vm.SolicitarAsync();

            vm.CopiarEnlaceCommand.Execute(null);

            Assert.AreEqual("No se ha podido copiar el enlace. Selecciónelo y cópielo a mano.", vm.TextoCopiado);
        }

        [TestMethod]
        public void Cerrar_SinSolicitar_Cancel()
        {
            var vm = Vm(A.Fake<IServicioPagoAPlazos>(), Disponibilidad());
            ResultadoDialogo resultado = null;
            ((IDialogoNesto)vm).RequestClose += r => resultado = r;

            vm.CerrarCommand.Execute(null);

            Assert.AreEqual(ResultadoBoton.Cancel, resultado.Result);
        }

        [TestMethod]
        public async Task Cerrar_TrasSolicitar_Ok()
        {
            var vm = Vm(ServicioQueDevuelve(new SolicitudPagoAPlazosModel { HaySolicitud = true, Estado = "Pendiente", Canal = "App", DispositivosAvisados = 1 }), Disponibilidad());
            ResultadoDialogo resultado = null;
            ((IDialogoNesto)vm).RequestClose += r => resultado = r;
            await vm.SolicitarAsync();

            vm.CerrarCommand.Execute(null);

            Assert.AreEqual(ResultadoBoton.OK, resultado.Result);
        }

        #endregion
    }
}
