using CommunityToolkit.Mvvm.Messaging;
using ControlesUsuario.Dialogs;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Models;
using Nesto.Modulos.PedidoVenta;
using Nesto.Modulos.PedidoVenta.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PedidoVentaTests
{
    /// <summary>
    /// Nesto#640 (NestoAPI#864): el pago a plazos en el detalle del pedido. La acción «Pago a plazos…» solo sale si la API lo
    /// ofrece (deshabilitada con el motivo si este pedido no se puede), el estado se lee al abrir el pedido y tras cada acción, y
    /// «Cancelar pago a plazos» pide el motivo. Nada rompe la carga del pedido.
    /// </summary>
    [TestClass]
    public class PagoAPlazosDetalleTests
    {
        private const int NUMERO = 930500;

        private static PedidoVentaDTO Pedido(int numero = NUMERO) => new PedidoVentaDTO
        {
            empresa = "1",
            numero = numero,
            cliente = "10000",
            contacto = "0",
            Lineas = new List<LineaPedidoVentaDTO>
            {
                new LineaPedidoVentaDTO { id = 1, Producto = "38093", Cantidad = 2, almacen = "ALG", tipoLinea = 1, estado = -1 }
            }
        };

        private static DisponibilidadPagoAPlazosModel Disponibilidad(bool disponible = true, string motivo = null, string nombre = "Financiera Prueba")
            => new DisponibilidadPagoAPlazosModel
            {
                Disponible = disponible,
                Motivo = motivo,
                Proveedor = nombre == null ? null : "Prueba",
                NombreProveedor = nombre,
                Importe = 250.5m,
                ClienteTieneApp = true,
                Canal = "App"
            };

        private static EstadoPagoAPlazosModel Estado(string estado) => new EstadoPagoAPlazosModel
        {
            Empresa = "1",
            Pedido = NUMERO,
            HaySolicitud = estado != "SinSolicitud",
            Estado = estado,
            NombreProveedor = "Financiera Prueba"
        };

        private static IServicioPagoAPlazos Servicio(EstadoPagoAPlazosModel estado, DisponibilidadPagoAPlazosModel disponibilidad)
        {
            IServicioPagoAPlazos servicio = A.Fake<IServicioPagoAPlazos>();
            A.CallTo(() => servicio.LeerEstado(A<string>._, A<int>._)).Returns(Task.FromResult(estado));
            A.CallTo(() => servicio.LeerDisponible(A<string>._, A<int>._)).Returns(Task.FromResult(disponibilidad));
            return servicio;
        }

        private static DetallePedidoViewModel Vm(IServicioPagoAPlazos servicioPagoAPlazos, IServicioDialogos dialogos = null, PedidoVentaDTO pedido = null,
            IPedidoVentaService servicio = null)
        {
            if (servicio == null)
            {
                servicio = A.Fake<IPedidoVentaService>();
                A.CallTo(() => servicio.cargarPedido(A<string>._, A<int>._)).ReturnsLazily(() => Task.FromResult(Pedido()));
            }
            var vm = new DetallePedidoViewModel(A.Fake<IServicioNavegacion>(), A.Fake<IConfiguracion>(), servicio, new WeakReferenceMessenger(),
                dialogos ?? A.Fake<IServicioDialogos>(), A.Fake<IAbridorPedidos>(), A.Fake<IServicioAutenticacion>());
            vm.ServicioPagoAPlazos = servicioPagoAPlazos;
            vm.pedido = new PedidoVentaWrapper(pedido ?? Pedido());
            return vm;
        }

        #region Carga

        [TestMethod]
        public async Task Cargar_Disponible_SaleLaAccionHabilitadaYSinEstado()
        {
            var vm = Vm(Servicio(Estado("SinSolicitud"), Disponibilidad()));

            await vm.CargarPagoAPlazosAsync();

            Assert.IsTrue(vm.MostrarPagoAPlazos);
            Assert.IsTrue(vm.PuedePedirPagoAPlazos);
            Assert.IsTrue(vm.PagoAPlazosCommand.CanExecute(null));
            StringAssert.StartsWith(vm.TextoAyudaPagoAPlazos, "Financiar 250,50 € con Financiera Prueba");
            Assert.IsFalse(vm.HayEstadoPagoAPlazos);
            Assert.AreEqual(string.Empty, vm.TextoEstadoPagoAPlazos);
            Assert.IsFalse(vm.HayPagoAPlazosVivo);
            Assert.IsFalse(vm.CancelarPagoAPlazosCommand.CanExecute(null));
        }

        [TestMethod]
        public async Task Cargar_NoDisponible_SaleDeshabilitadaConElMotivoDeLaApi()
        {
            var vm = Vm(Servicio(Estado("SinSolicitud"), Disponibilidad(disponible: false, motivo: "El pedido ya está pagado.")));

            await vm.CargarPagoAPlazosAsync();

            Assert.IsTrue(vm.MostrarPagoAPlazos);
            Assert.IsFalse(vm.PuedePedirPagoAPlazos);
            Assert.IsFalse(vm.PagoAPlazosCommand.CanExecute(null));
            Assert.AreEqual("El pedido ya está pagado.", vm.TextoAyudaPagoAPlazos);
        }

        [TestMethod]
        public async Task Cargar_InterruptorApagado_LaAccionNoSale()
        {
            var vm = Vm(Servicio(Estado("SinSolicitud"), Disponibilidad(disponible: false, motivo: "El pago a plazos no está disponible.", nombre: null)));

            await vm.CargarPagoAPlazosAsync();

            Assert.IsFalse(vm.MostrarPagoAPlazos);
            Assert.IsFalse(vm.PagoAPlazosCommand.CanExecute(null));
            Assert.IsFalse(vm.HayEstadoPagoAPlazos);
        }

        [TestMethod]
        public async Task Cargar_LaApiDa503_NiAccionNiEstado_NoRompeYNoPreguntaLaDisponibilidad()
        {
            IServicioPagoAPlazos servicio = A.Fake<IServicioPagoAPlazos>();
            A.CallTo(() => servicio.LeerEstado(A<string>._, A<int>._)).ThrowsAsync(new Exception("Error 503 de la API."));
            IServicioDialogos dialogos = A.Fake<IServicioDialogos>();
            var vm = Vm(servicio, dialogos);

            await vm.CargarPagoAPlazosAsync();

            Assert.IsFalse(vm.MostrarPagoAPlazos);
            Assert.IsFalse(vm.HayEstadoPagoAPlazos);
            A.CallTo(() => servicio.LeerDisponible(A<string>._, A<int>._)).MustNotHaveHappened();
            A.CallTo(() => dialogos.ShowError(A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task Cargar_FallaSoloLaDisponibilidad_ElEstadoSeVeYLaAccionNo()
        {
            IServicioPagoAPlazos servicio = Servicio(Estado("Capturado"), null);
            A.CallTo(() => servicio.LeerDisponible(A<string>._, A<int>._)).ThrowsAsync(new Exception("Sin red"));
            var vm = Vm(servicio);

            await vm.CargarPagoAPlazosAsync();

            Assert.IsFalse(vm.MostrarPagoAPlazos);
            Assert.IsTrue(vm.HayEstadoPagoAPlazos);
            Assert.AreEqual("Pago a plazos (Financiera Prueba): capturado", vm.TextoEstadoPagoAPlazos);
        }

        [TestMethod]
        public async Task Cargar_PedidoSinGuardar_NoSePregunta()
        {
            IServicioPagoAPlazos servicio = Servicio(Estado("Pendiente"), Disponibilidad());
            var vm = Vm(servicio, pedido: Pedido(numero: 0));

            await vm.CargarPagoAPlazosAsync();

            A.CallTo(() => servicio.LeerEstado(A<string>._, A<int>._)).MustNotHaveHappened();
            A.CallTo(() => servicio.LeerDisponible(A<string>._, A<int>._)).MustNotHaveHappened();
            Assert.IsFalse(vm.MostrarPagoAPlazos);
        }

        [TestMethod]
        public void AlAbrirElPedido_SeLeeElEstadoYLaDisponibilidad()
        {
            IServicioPagoAPlazos servicio = Servicio(Estado("Pendiente"), Disponibilidad());

            var vm = Vm(servicio); // poner el pedido ya lo pregunta (las respuestas del doble son inmediatas)

            A.CallTo(() => servicio.LeerEstado("1", NUMERO)).MustHaveHappenedOnceExactly();
            A.CallTo(() => servicio.LeerDisponible("1", NUMERO)).MustHaveHappenedOnceExactly();
            Assert.AreEqual("Pago a plazos (Financiera Prueba): pendiente del cliente", vm.TextoEstadoPagoAPlazos);
        }

        [TestMethod]
        public async Task Cargar_SeAbreOtroPedidoMientras_LaRespuestaDelAnteriorSeDescarta()
        {
            var respuestaLenta = new TaskCompletionSource<EstadoPagoAPlazosModel>();
            IServicioPagoAPlazos servicio = A.Fake<IServicioPagoAPlazos>();
            A.CallTo(() => servicio.LeerEstado(A<string>._, NUMERO)).Returns(respuestaLenta.Task);
            A.CallTo(() => servicio.LeerEstado(A<string>._, 111)).Returns(Task.FromResult(Estado("SinSolicitud")));
            A.CallTo(() => servicio.LeerDisponible(A<string>._, A<int>._)).Returns(Task.FromResult(Disponibilidad(nombre: null)));
            var vm = Vm(servicio);

            vm.pedido = new PedidoVentaWrapper(Pedido(numero: 111));
            respuestaLenta.SetResult(Estado("Aprobado"));
            await Task.Yield();

            Assert.IsFalse(vm.HayEstadoPagoAPlazos);
        }

        [DataTestMethod]
        [DataRow("Pendiente", true)]
        [DataRow("Aprobado", true)]
        [DataRow("Capturado", false)]
        [DataRow("Rechazado", false)]
        [DataRow("Caducado", false)]
        [DataRow("Cancelado", false)]
        public async Task Cargar_ConSolicitud_SeVeElEstadoYCancelarSoloSiEstaViva(string estado, bool viva)
        {
            var vm = Vm(Servicio(Estado(estado), Disponibilidad()));

            await vm.CargarPagoAPlazosAsync();

            Assert.IsTrue(vm.HayEstadoPagoAPlazos);
            StringAssert.StartsWith(vm.TextoEstadoPagoAPlazos, "Pago a plazos (Financiera Prueba): ");
            Assert.AreEqual(viva, vm.HayPagoAPlazosVivo);
            Assert.AreEqual(viva, vm.CancelarPagoAPlazosCommand.CanExecute(null));
        }

        #endregion

        #region Pedir

        [TestMethod]
        public async Task Pedir_AbreElDialogoConElPedidoYLaDisponibilidad_YLuegoRecargaElEstado()
        {
            DisponibilidadPagoAPlazosModel disponibilidad = Disponibilidad();
            IServicioPagoAPlazos servicio = Servicio(Estado("SinSolicitud"), disponibilidad);
            IServicioDialogos dialogos = A.Fake<IServicioDialogos>();
            ParametrosDialogo parametros = null;
            A.CallTo(() => dialogos.ShowDialogAsync(PagoAPlazosDialogViewModel.NOMBRE, A<ParametrosDialogo>._))
                .Invokes((string _, ParametrosDialogo p) =>
                {
                    parametros = p;
                    // mientras el diálogo está abierto se abre la solicitud
                    A.CallTo(() => servicio.LeerEstado(A<string>._, A<int>._)).Returns(Task.FromResult(Estado("Pendiente")));
                })
                .Returns(Task.FromResult(new ResultadoDialogo(ResultadoBoton.OK)));
            var vm = Vm(servicio, dialogos);
            await vm.CargarPagoAPlazosAsync();

            await vm.PedirPagoAPlazosAsync();

            Assert.AreEqual("1", parametros.GetValue<string>(PagoAPlazosDialogViewModel.PARAMETRO_EMPRESA));
            Assert.AreEqual(NUMERO, parametros.GetValue<int>(PagoAPlazosDialogViewModel.PARAMETRO_PEDIDO));
            Assert.AreSame(disponibilidad, parametros.GetValue<DisponibilidadPagoAPlazosModel>(PagoAPlazosDialogViewModel.PARAMETRO_DISPONIBILIDAD));
            Assert.AreEqual("Pago a plazos (Financiera Prueba): pendiente del cliente", vm.TextoEstadoPagoAPlazos);
            Assert.IsTrue(vm.HayPagoAPlazosVivo);
        }

        [TestMethod]
        public async Task Pedir_NoDisponible_NoAbreNada()
        {
            IServicioDialogos dialogos = A.Fake<IServicioDialogos>();
            var vm = Vm(Servicio(Estado("SinSolicitud"), Disponibilidad(disponible: false, motivo: "El pedido ya está pagado.")), dialogos);
            await vm.CargarPagoAPlazosAsync();

            await vm.PedirPagoAPlazosAsync();

            A.CallTo(() => dialogos.ShowDialogAsync(A<string>._, A<ParametrosDialogo>._)).MustNotHaveHappened();
        }

        #endregion

        #region Cancelar

        [TestMethod]
        public async Task Cancelar_Pendiente_PideElMotivo_LlamaALaApiYRecargaSoloElEstado()
        {
            IServicioPagoAPlazos servicio = Servicio(Estado("Pendiente"), Disponibilidad());
            A.CallTo(() => servicio.Cancelar(A<string>._, A<int>._, A<string>._)).ReturnsLazily(() =>
            {
                A.CallTo(() => servicio.LeerEstado(A<string>._, A<int>._)).Returns(Task.FromResult(Estado("Cancelado")));
                return Task.FromResult(Estado("Cancelado"));
            });
            IServicioDialogos dialogos = A.Fake<IServicioDialogos>();
            A.CallTo(() => dialogos.GetText(A<string>._, A<string>._)).Returns("  El cliente prefiere tarjeta ");
            IPedidoVentaService servicioPedidos = A.Fake<IPedidoVentaService>();
            var vm = Vm(servicio, dialogos, servicio: servicioPedidos);
            await vm.CargarPagoAPlazosAsync();

            await vm.CancelarPagoAPlazosAsync();

            A.CallTo(() => dialogos.GetText("Cancelar pago a plazos", A<string>.That.Contains("Indique el motivo"))).MustHaveHappenedOnceExactly();
            A.CallTo(() => servicio.Cancelar("1", NUMERO, "El cliente prefiere tarjeta")).MustHaveHappenedOnceExactly();
            Assert.AreEqual("Pago a plazos (Financiera Prueba): cancelado", vm.TextoEstadoPagoAPlazos);
            Assert.IsFalse(vm.HayPagoAPlazosVivo);
            A.CallTo(() => servicioPedidos.cargarPedido(A<string>._, A<int>._)).MustNotHaveHappened();
            A.CallTo(() => dialogos.ShowError(A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task Cancelar_Aprobado_RecargaElPedidoEntero_PorqueLaApiQuitaElPrepago()
        {
            IServicioPagoAPlazos servicio = Servicio(Estado("Aprobado"), Disponibilidad(disponible: false, motivo: "Este pedido ya está financiado con Financiera Prueba."));
            IServicioDialogos dialogos = A.Fake<IServicioDialogos>();
            A.CallTo(() => dialogos.GetText(A<string>._, A<string>._)).Returns("Se ha equivocado de pedido");
            IPedidoVentaService servicioPedidos = A.Fake<IPedidoVentaService>();
            A.CallTo(() => servicioPedidos.cargarPedido(A<string>._, A<int>._)).ReturnsLazily(() => Task.FromResult(Pedido()));
            var vm = Vm(servicio, dialogos, servicio: servicioPedidos);
            await vm.CargarPagoAPlazosAsync();

            await vm.CancelarPagoAPlazosAsync();

            A.CallTo(() => dialogos.GetText(A<string>._, A<string>.That.Contains("se quitará el prepago del pedido"))).MustHaveHappenedOnceExactly();
            A.CallTo(() => servicio.Cancelar("1", NUMERO, "Se ha equivocado de pedido")).MustHaveHappenedOnceExactly();
            A.CallTo(() => servicioPedidos.cargarPedido("1", NUMERO)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task Cancelar_ElUsuarioCierraElCuadro_NoSeCancela()
        {
            IServicioPagoAPlazos servicio = Servicio(Estado("Pendiente"), Disponibilidad());
            IServicioDialogos dialogos = A.Fake<IServicioDialogos>();
            A.CallTo(() => dialogos.GetText(A<string>._, A<string>._)).Returns(null);
            var vm = Vm(servicio, dialogos);
            await vm.CargarPagoAPlazosAsync();

            await vm.CancelarPagoAPlazosAsync();

            A.CallTo(() => servicio.Cancelar(A<string>._, A<int>._, A<string>._)).MustNotHaveHappened();
            Assert.IsTrue(vm.HayPagoAPlazosVivo);
        }

        [TestMethod]
        public async Task Cancelar_SinMotivo_SeAvisaYNoSeCancela()
        {
            IServicioPagoAPlazos servicio = Servicio(Estado("Pendiente"), Disponibilidad());
            IServicioDialogos dialogos = A.Fake<IServicioDialogos>();
            A.CallTo(() => dialogos.GetText(A<string>._, A<string>._)).Returns("   ");
            var vm = Vm(servicio, dialogos);
            await vm.CargarPagoAPlazosAsync();

            await vm.CancelarPagoAPlazosAsync();

            A.CallTo(() => dialogos.ShowError("Para cancelar el pago a plazos hay que indicar el motivo.")).MustHaveHappenedOnceExactly();
            A.CallTo(() => servicio.Cancelar(A<string>._, A<int>._, A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task Cancelar_LaApiLoRechaza_SaleSuMensajeTalCualYSeRecargaElEstado()
        {
            IServicioPagoAPlazos servicio = Servicio(Estado("Aprobado"), Disponibilidad());
            A.CallTo(() => servicio.Cancelar(A<string>._, A<int>._, A<string>._))
                .ThrowsAsync(new Exception("El pago a plazos de este pedido ya está cobrado: no se puede cancelar desde aquí."));
            IServicioDialogos dialogos = A.Fake<IServicioDialogos>();
            A.CallTo(() => dialogos.GetText(A<string>._, A<string>._)).Returns("Motivo");
            IPedidoVentaService servicioPedidos = A.Fake<IPedidoVentaService>();
            var vm = Vm(servicio, dialogos, servicio: servicioPedidos);
            await vm.CargarPagoAPlazosAsync();
            Fake.ClearRecordedCalls(servicio);

            await vm.CancelarPagoAPlazosAsync();

            A.CallTo(() => dialogos.ShowError("El pago a plazos de este pedido ya está cobrado: no se puede cancelar desde aquí.")).MustHaveHappenedOnceExactly();
            A.CallTo(() => servicio.LeerEstado("1", NUMERO)).MustHaveHappenedOnceExactly();
            A.CallTo(() => servicioPedidos.cargarPedido(A<string>._, A<int>._)).MustNotHaveHappened();
            Assert.IsTrue(vm.CancelarPagoAPlazosCommand.CanExecute(null));
        }

        [TestMethod]
        public async Task Cancelar_SinSolicitudViva_NoHaceNada()
        {
            IServicioPagoAPlazos servicio = Servicio(Estado("Capturado"), Disponibilidad());
            IServicioDialogos dialogos = A.Fake<IServicioDialogos>();
            var vm = Vm(servicio, dialogos);
            await vm.CargarPagoAPlazosAsync();

            await vm.CancelarPagoAPlazosAsync();

            A.CallTo(() => dialogos.GetText(A<string>._, A<string>._)).MustNotHaveHappened();
            A.CallTo(() => servicio.Cancelar(A<string>._, A<int>._, A<string>._)).MustNotHaveHappened();
        }

        #endregion
    }
}
