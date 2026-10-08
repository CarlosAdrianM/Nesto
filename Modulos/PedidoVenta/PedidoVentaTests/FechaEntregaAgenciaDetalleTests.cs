using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Models;
using Nesto.Infrastructure.Services;
using Nesto.Models;
using Nesto.Modulos.PedidoVenta;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity;

namespace PedidoVentaTests
{
    /// <summary>
    /// NestoAPI#606 (corte 2): el detalle del pedido enseña qué día se entrega a la agencia, calculado ahora
    /// (GET api/PedidosVenta/{empresa}/{numero}/FechaEntregaAgencia), y la fecha que se prometió al crearlo si no coincide.
    /// Se pide al abrir el pedido y al guardarlo.
    /// </summary>
    [TestClass]
    public class FechaEntregaAgenciaDetalleTests
    {
        private static readonly DateTime Hoy = new DateTime(2026, 10, 13); // martes

        private static PedidoVentaDTO Pedido(int numero) => new PedidoVentaDTO
        {
            empresa = "1",
            numero = numero,
            cliente = "15191",
            contacto = "0",
            modoServicio = ModosServicio.SEGUN_VAYA_ENTRANDO,
            Lineas = new List<LineaPedidoVentaDTO> { new LineaPedidoVentaDTO { id = 1, Producto = "38093", Cantidad = 2, almacen = "ALG", tipoLinea = 1 } }
        };

        private static FechaEntregaAgenciaDTO Fecha(DateTime? dia, DateTime? prometida = null) => new FechaEntregaAgenciaDTO
        {
            FechaEntregaAgencia = dia,
            PrimeraEntrega = dia,
            EntregaCompleta = dia,
            Aplica = FechaEntregaAgenciaDTO.APLICA_PRIMERA,
            Motivo = "Falta la reposición de Reina.",
            FechaPrometida = prometida
        };

        private static (DetallePedidoViewModel vm, IServicioFechaEntregaAgencia servicioFecha, IPedidoVentaService servicio) Vm(FechaEntregaAgenciaDTO respuesta)
        {
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            var vm = new DetallePedidoViewModel(A.Fake<IServicioNavegacion>(), A.Fake<IConfiguracion>(), servicio, new WeakReferenceMessenger(),
                A.Fake<IServicioDialogos>(), A.Fake<IUnityContainer>(), A.Fake<IServicioAutenticacion>());
            var servicioFecha = A.Fake<IServicioFechaEntregaAgencia>();
            A.CallTo(() => servicioFecha.CalcularPedido(A<string>._, A<int>._)).Returns(Task.FromResult(respuesta));
            vm.ServicioFechaEntregaAgencia = servicioFecha;
            vm.Hoy = () => Hoy;
            return (vm, servicioFecha, servicio);
        }

        [TestMethod]
        public void AlAbrirUnPedido_SePideYSeEnsenaConElMotivo()
        {
            var (vm, servicioFecha, _) = Vm(Fecha(new DateTime(2026, 10, 15)));

            vm.pedido = new PedidoVentaWrapper(Pedido(928020));

            A.CallTo(() => servicioFecha.CalcularPedido("1", 928020)).MustHaveHappenedOnceExactly();
            Assert.AreEqual("Se entrega a la agencia el jueves 15/10", vm.FechaEntregaAgencia.Texto);
            Assert.AreEqual("Falta la reposición de Reina.", vm.FechaEntregaAgencia.Motivo);
            Assert.IsFalse(vm.FechaEntregaAgencia.HayPrometida, "Sin prometida no se dice nada");
        }

        [TestMethod]
        public void HoyYManana()
        {
            var (vmHoy, _, _) = Vm(Fecha(Hoy));
            var (vmManana, _, _) = Vm(Fecha(Hoy.AddDays(1)));

            vmHoy.pedido = new PedidoVentaWrapper(Pedido(1));
            vmManana.pedido = new PedidoVentaWrapper(Pedido(2));

            Assert.AreEqual("Se entrega a la agencia hoy", vmHoy.FechaEntregaAgencia.Texto);
            Assert.AreEqual("Se entrega a la agencia mañana", vmManana.FechaEntregaAgencia.Texto);
        }

        [TestMethod]
        public void PrometidaDistinta_SeEnsenaAlLado()
        {
            var (vm, _, _) = Vm(Fecha(new DateTime(2026, 10, 16), prometida: new DateTime(2026, 10, 15)));

            vm.pedido = new PedidoVentaWrapper(Pedido(928020));

            Assert.AreEqual("Se entrega a la agencia el viernes 16/10", vm.FechaEntregaAgencia.Texto);
            Assert.IsTrue(vm.FechaEntregaAgencia.HayPrometida);
            Assert.AreEqual("Prometida: jueves 15/10", vm.FechaEntregaAgencia.TextoPrometida);
        }

        [TestMethod]
        public void PrometidaIgual_NoSeRepite()
        {
            var (vm, _, _) = Vm(Fecha(new DateTime(2026, 10, 15), prometida: new DateTime(2026, 10, 15)));

            vm.pedido = new PedidoVentaWrapper(Pedido(928020));

            Assert.IsFalse(vm.FechaEntregaAgencia.HayPrometida);
        }

        [TestMethod]
        public void SinFecha_DiceSinFechaTodavia()
        {
            var (vm, _, _) = Vm(Fecha(null));

            vm.pedido = new PedidoVentaWrapper(Pedido(928020));

            Assert.AreEqual("Sin fecha todavía", vm.FechaEntregaAgencia.Texto);
        }

        [TestMethod]
        public void ApiAntiguaSinElEndpoint_NoSeEnsenaNada()
        {
            var (vm, _, _) = Vm(null); // el servicio convierte el 404 en null

            vm.pedido = new PedidoVentaWrapper(Pedido(928020));

            Assert.IsFalse(vm.FechaEntregaAgencia.HayTexto);
            Assert.IsFalse(vm.FechaEntregaAgencia.HayPrometida);
        }

        [TestMethod]
        public void PedidoNuevoSinGrabar_NoSePregunta()
        {
            var (vm, servicioFecha, _) = Vm(Fecha(Hoy));

            vm.pedido = new PedidoVentaWrapper(Pedido(0));

            A.CallTo(() => servicioFecha.CalcularPedido(A<string>._, A<int>._)).MustNotHaveHappened();
            Assert.IsFalse(vm.FechaEntregaAgencia.HayTexto);
        }

        [TestMethod]
        public void AlAbrirOtroPedido_NoSeQuedaLaFechaDelAnterior()
        {
            var (vm, servicioFecha, _) = Vm(Fecha(Hoy));
            vm.pedido = new PedidoVentaWrapper(Pedido(928020));
            A.CallTo(() => servicioFecha.CalcularPedido(A<string>._, A<int>._)).Returns(Task.FromResult<FechaEntregaAgenciaDTO>(null));

            vm.pedido = new PedidoVentaWrapper(Pedido(928021));

            Assert.IsFalse(vm.FechaEntregaAgencia.HayTexto);
        }

        [TestMethod]
        public async Task AlGuardar_SeVuelveAPedir()
        {
            var (vm, servicioFecha, servicio) = Vm(Fecha(Hoy));
            vm.pedido = new PedidoVentaWrapper(Pedido(928020));
            A.CallTo(() => servicioFecha.CalcularPedido(A<string>._, A<int>._)).Returns(Task.FromResult(Fecha(Hoy.AddDays(1))));

            await vm.ModificarPedidoAsync();

            A.CallTo(() => servicio.modificarPedido(A<PedidoVentaDTO>._)).MustHaveHappenedOnceExactly();
            A.CallTo(() => servicioFecha.CalcularPedido("1", 928020)).MustHaveHappenedTwiceExactly();
            Assert.AreEqual("Se entrega a la agencia mañana", vm.FechaEntregaAgencia.Texto);
        }

        [TestMethod]
        public void SiElServicioRevienta_NoSePropaga()
        {
            var (vm, servicioFecha, _) = Vm(Fecha(Hoy));
            A.CallTo(() => servicioFecha.CalcularPedido(A<string>._, A<int>._)).Throws(new InvalidOperationException("boom"));

            vm.pedido = new PedidoVentaWrapper(Pedido(928020));

            Assert.IsFalse(vm.FechaEntregaAgencia.HayTexto);
        }
    }
}
