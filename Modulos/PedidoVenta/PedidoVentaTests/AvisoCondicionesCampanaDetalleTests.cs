using CommunityToolkit.Mvvm.Messaging;
using ControlesUsuario.Dialogs;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Models;
using Nesto.Modulos.PedidoVenta;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PedidoVentaTests
{
    /// <summary>
    /// Feria online 12-16/11/26: al CREAR un pedido desde el detalle, si la API dice que lleva productos de una campaña al
    /// contado y no va al contado, se pregunta. Sí → plazos CONTADO (la forma de pago no se toca); No → igual. Si la API falla,
    /// se crea igual sin preguntar. Al modificar un pedido que ya existe, no se pregunta.
    /// </summary>
    [TestClass]
    public class AvisoCondicionesCampanaDetalleTests
    {
        private const string MENSAJE = "Este pedido lleva productos de la feria online, con condiciones de la feria online, y debería ser al contado para poder llevar esos precios. ¿Cambiar a contado?";

        private static PedidoVentaDTO Pedido(int numero) => new PedidoVentaDTO
        {
            empresa = "1",
            numero = numero,
            cliente = "6316",
            contacto = "0",
            formaPago = "RCB",
            plazosPago = "1/30",
            Lineas = new List<LineaPedidoVentaDTO>
            {
                new LineaPedidoVentaDTO { id = 1, Producto = "45383", Cantidad = 1, almacen = "ALG", tipoLinea = 1, fechaEntrega = DateTime.Today }
            }
        };

        private static IServicioDialogos DialogoQueResponde(bool acepta)
        {
            IServicioDialogos dialogService = A.Fake<IServicioDialogos>();
            A.CallTo(() => dialogService.ShowConfirmationAsync(A<string>._, A<string>._)).Returns(Task.FromResult(acepta));
            return dialogService;
        }

        private static DetallePedidoViewModel Vm(IPedidoVentaService servicio, IServicioDialogos dialogService, int numero)
        {
            var vm = new DetallePedidoViewModel(A.Fake<IServicioNavegacion>(), A.Fake<IConfiguracion>(), servicio, new WeakReferenceMessenger(),
                dialogService, A.Fake<IAbridorPedidos>(), A.Fake<IServicioAutenticacion>());
            vm.pedido = new PedidoVentaWrapper(Pedido(numero));
            vm.MarcarComoGuardado();
            return vm;
        }

        private static IPedidoVentaService ServicioQueAvisa(List<PedidoVentaDTO> creados)
        {
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            A.CallTo(() => servicio.ConsultarAvisoCondicionesCampana(A<PedidoVentaDTO>._)).Returns(Task.FromResult(new AvisoCondicionesCampanaRespuesta
            {
                Avisar = true,
                Mensaje = MENSAJE,
                PlazosPagoPropuestos = "CONTADO",
                Campana = "FERIA-ONLINE-1126",
                Productos = new List<string> { "45383" }
            }));
            A.CallTo(() => servicio.CrearPedido(A<PedidoVentaDTO>._)).ReturnsLazily((PedidoVentaDTO p) =>
            {
                creados.Add(new PedidoVentaDTO { plazosPago = p.plazosPago, formaPago = p.formaPago });
                return Task.FromResult(930001);
            });
            return servicio;
        }

        [TestMethod]
        public async Task Crear_LaApiAvisaYDiceQueSi_SeCreaAlContado()
        {
            var creados = new List<PedidoVentaDTO>();
            IPedidoVentaService servicio = ServicioQueAvisa(creados);
            IServicioDialogos dialogos = DialogoQueResponde(true);
            DetallePedidoViewModel vm = Vm(servicio, dialogos, 0);

            await vm.ModificarPedidoAsync();

            A.CallTo(() => dialogos.ShowConfirmationAsync(AvisoCondicionesCampana.TITULO, MENSAJE)).MustHaveHappenedOnceExactly();
            Assert.AreEqual(1, creados.Count);
            Assert.AreEqual("CONTADO", creados[0].plazosPago);
            Assert.AreEqual("RCB", creados[0].formaPago, "La forma de pago no se toca");
            Assert.AreEqual("CONTADO", vm.pedido.plazosPago, "La pantalla lo enseña");
        }

        [TestMethod]
        public async Task Crear_LaApiAvisaYDiceQueNo_SeCreaComoEstaba()
        {
            var creados = new List<PedidoVentaDTO>();
            IPedidoVentaService servicio = ServicioQueAvisa(creados);
            IServicioDialogos dialogos = DialogoQueResponde(false);
            DetallePedidoViewModel vm = Vm(servicio, dialogos, 0);

            await vm.ModificarPedidoAsync();

            A.CallTo(() => dialogos.ShowConfirmationAsync(AvisoCondicionesCampana.TITULO, MENSAJE)).MustHaveHappenedOnceExactly();
            Assert.AreEqual(1, creados.Count);
            Assert.AreEqual("1/30", creados[0].plazosPago);
        }

        [TestMethod]
        public async Task Crear_LaApiNoAvisa_NoPregunta()
        {
            var creados = new List<PedidoVentaDTO>();
            IPedidoVentaService servicio = ServicioQueAvisa(creados);
            A.CallTo(() => servicio.ConsultarAvisoCondicionesCampana(A<PedidoVentaDTO>._))
                .Returns(Task.FromResult(new AvisoCondicionesCampanaRespuesta { Avisar = false }));
            IServicioDialogos dialogos = DialogoQueResponde(true);
            DetallePedidoViewModel vm = Vm(servicio, dialogos, 0);

            await vm.ModificarPedidoAsync();

            A.CallTo(() => dialogos.ShowConfirmationAsync(AvisoCondicionesCampana.TITULO, A<string>._)).MustNotHaveHappened();
            Assert.AreEqual("1/30", creados[0].plazosPago);
        }

        [TestMethod]
        public async Task Crear_LaApiFalla_SeCreaIgualSinPreguntar()
        {
            var creados = new List<PedidoVentaDTO>();
            IPedidoVentaService servicio = ServicioQueAvisa(creados);
            A.CallTo(() => servicio.ConsultarAvisoCondicionesCampana(A<PedidoVentaDTO>._)).ThrowsAsync(new InvalidOperationException("404"));
            IServicioDialogos dialogos = DialogoQueResponde(true);
            DetallePedidoViewModel vm = Vm(servicio, dialogos, 0);

            await vm.ModificarPedidoAsync();

            A.CallTo(() => dialogos.ShowConfirmationAsync(AvisoCondicionesCampana.TITULO, A<string>._)).MustNotHaveHappened();
            Assert.AreEqual(1, creados.Count);
            Assert.AreEqual(930001, vm.pedido.numero);
        }

        [TestMethod]
        public async Task Modificar_PedidoQueYaExiste_NoPregunta()
        {
            var creados = new List<PedidoVentaDTO>();
            IPedidoVentaService servicio = ServicioQueAvisa(creados);
            A.CallTo(() => servicio.modificarPedido(A<PedidoVentaDTO>._)).Returns(Task.FromResult(new List<AvisoPedidoModel>()));
            IServicioDialogos dialogos = DialogoQueResponde(true);
            DetallePedidoViewModel vm = Vm(servicio, dialogos, 929057);

            await vm.ModificarPedidoAsync();

            A.CallTo(() => servicio.ConsultarAvisoCondicionesCampana(A<PedidoVentaDTO>._)).MustNotHaveHappened();
            A.CallTo(() => dialogos.ShowConfirmationAsync(AvisoCondicionesCampana.TITULO, A<string>._)).MustNotHaveHappened();
        }
    }
}
