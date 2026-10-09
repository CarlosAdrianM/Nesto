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
using System.Linq;
using System.Threading.Tasks;
using Unity;

namespace PedidoVentaTests
{
    /// <summary>
    /// NestoAPI#593 (c5): al abrir un pedido de un cliente con cheque regalo disponible y sin la línea del cheque, aviso
    /// con «Añadir el cheque regalo»: añade la línea (−50) y la manda el guardado normal. Si la API la rechaza
    /// (CHEQUE_REGALO_*), se enseña su mensaje.
    /// </summary>
    [TestClass]
    public class ChequeRegaloDetalleTests
    {
        private const string MENSAJE_MINIMO = "Para usar el cheque regalo el pedido tiene que superar 250,00 € de producto computable: lleva 180,00 €, faltan 70,01 €.";

        private static ChequeRegaloClienteDTO Cheque(string estado = ChequeRegaloClienteDTO.ESTADO_DISPONIBLE, int? pedidoCanje = null) => new ChequeRegaloClienteDTO
        {
            Campana = "CHEQUE50_OCT_2026",
            Empresa = "1",
            Cliente = "15191",
            Producto = "CHEQUE50_OCT26",
            Importe = 50M,
            MinimoCanje = 250M,
            CanjeHasta = new DateTime(2026, 11, 7),
            PrefijosNombreExcluidosMinimo = new List<string> { "PACK 26" },
            GruposExcluidosMinimo = new List<string> { "PEL" },
            Estado = estado,
            SePuedeUsar = estado == ChequeRegaloClienteDTO.ESTADO_DISPONIBLE,
            PedidoCanje = pedidoCanje,
            Mensaje = "Tiene un cheque regalo de 50,00 € + IVA para un pedido de más de 250,00 € de producto, hasta el 07/11/2026.",
            TextoLinea = "Cheque regalo 50 € (campaña CHEQUE50_OCT_2026)"
        };

        private static async Task<(DetallePedidoViewModel vm, IPedidoVentaService servicio, IServicioDialogos dialogos, IServicioChequesRegalo cheques)>
            Vm(ChequeRegaloClienteDTO cheque, params LineaPedidoVentaDTO[] lineas)
        {
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            IServicioDialogos dialogos = A.Fake<IServicioDialogos>();
            var vm = new DetallePedidoViewModel(A.Fake<IServicioNavegacion>(), A.Fake<IConfiguracion>(), servicio, new WeakReferenceMessenger(),
                dialogos, A.Fake<IUnityContainer>(), A.Fake<IServicioAutenticacion>());
            vm.ServicioFechaEntregaAgencia = A.Fake<IServicioFechaEntregaAgencia>();
            IServicioChequesRegalo cheques = A.Fake<IServicioChequesRegalo>();
            A.CallTo(() => cheques.LeerDelCliente(A<string>._, A<string>._)).Returns(Task.FromResult(cheque));
            vm.ServicioChequesRegalo = cheques;

            vm.pedido = new PedidoVentaWrapper(new PedidoVentaDTO
            {
                empresa = "1",
                numero = 928200,
                cliente = "15191",
                contacto = "0",
                Lineas = lineas.ToList()
            });
            await vm.CargarChequeRegaloAsync();
            return (vm, servicio, dialogos, cheques);
        }

        private static LineaPedidoVentaDTO Producto(string producto = "12345", short cantidad = 10, decimal precio = 30M) => new LineaPedidoVentaDTO
        {
            id = 1,
            tipoLinea = 1,
            Producto = producto,
            Cantidad = cantidad,
            PrecioUnitario = precio,
            almacen = "ALG",
            formaVenta = "DIR",
            iva = "G21",
            PorcentajeIva = 0.21M,
            estado = 1,
            fechaEntrega = new DateTime(2026, 10, 14)
        };

        [TestMethod]
        public async Task SinCheque_NoHayAviso()
        {
            var (vm, _, _, cheques) = await Vm(null, Producto());

            A.CallTo(() => cheques.LeerDelCliente("1", "15191")).MustHaveHappened();
            Assert.IsFalse(vm.MostrarAvisoChequeRegalo);
            Assert.IsFalse(vm.AnadirChequeRegaloCommand.CanExecute(null));
        }

        [TestMethod]
        public async Task ChequeDisponible_SinLaLinea_SaleElAviso()
        {
            var (vm, _, _, _) = await Vm(Cheque(), Producto());

            Assert.IsTrue(vm.MostrarAvisoChequeRegalo);
            Assert.IsTrue(vm.AnadirChequeRegaloCommand.CanExecute(null));
            Assert.AreEqual("Añadir el cheque regalo de 50 €", vm.ChequeRegalo.TextoAnadir);
        }

        [TestMethod]
        public async Task AnadirElCheque_AnadeLaLineaYBajaLaBase_SinGuardarTodavia()
        {
            var (vm, servicio, _, _) = await Vm(Cheque(), Producto());
            Assert.AreEqual(300M, vm.pedido.BaseImponible);

            vm.AnadirChequeRegaloCommand.Execute(null);

            LineaPedidoVentaWrapper linea = vm.pedido.Lineas.Single(l => l.Producto == "CHEQUE50_OCT26");
            Assert.AreEqual((byte?)1, linea.tipoLinea);
            Assert.AreEqual((short)-1, linea.Cantidad);
            Assert.AreEqual(50M, linea.PrecioUnitario);
            Assert.IsFalse(linea.AplicarDescuento);
            Assert.AreEqual(0, linea.id, "Línea nueva: la manda el guardado normal");
            Assert.AreEqual("ALG", linea.Almacen);
            Assert.AreEqual("Cheque regalo 50 € (campaña CHEQUE50_OCT_2026)", linea.texto);
            Assert.IsTrue(vm.pedido.Model.Lineas.Any(l => l.Producto == "CHEQUE50_OCT26"), "Está en el modelo que viaja en el PUT");
            Assert.AreEqual(250M, vm.pedido.BaseImponible);
            Assert.IsFalse(vm.MostrarAvisoChequeRegalo, "Ya lo lleva: fuera el aviso");
            A.CallTo(() => servicio.modificarPedido(A<PedidoVentaDTO>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task PedidoQueYaLlevaLaLinea_NadaEspecial()
        {
            var (vm, _, _, _) = await Vm(Cheque(ChequeRegaloClienteDTO.ESTADO_EN_PEDIDO, 928200), Producto(),
                Producto("CHEQUE50_OCT26 ", -1, 50M));

            Assert.IsFalse(vm.MostrarAvisoChequeRegalo);
            Assert.IsTrue(vm.PedidoLlevaChequeRegalo);
        }

        [TestMethod]
        public async Task QuitarLaLinea_VuelveElAviso()
        {
            var (vm, _, _, _) = await Vm(Cheque(), Producto());
            vm.AnadirChequeRegaloCommand.Execute(null);

            vm.pedido.Lineas.Remove(vm.pedido.Lineas.Single(l => l.Producto == "CHEQUE50_OCT26"));

            Assert.IsTrue(vm.MostrarAvisoChequeRegalo);
        }

        [TestMethod]
        public async Task ChequeEnOtroPedidoOCaducado_SinAviso()
        {
            var (enOtro, _, _, _) = await Vm(Cheque(ChequeRegaloClienteDTO.ESTADO_EN_PEDIDO, 928123), Producto());
            var (caducado, _, _, _) = await Vm(Cheque(ChequeRegaloClienteDTO.ESTADO_CADUCADO), Producto());

            Assert.IsFalse(enOtro.MostrarAvisoChequeRegalo);
            Assert.IsFalse(caducado.MostrarAvisoChequeRegalo);
        }

        [TestMethod]
        public async Task Guardar_ConMinimoNoSuperado_EnsenaElMensajeDeLaApi()
        {
            var (vm, servicio, dialogos, _) = await Vm(Cheque(), Producto(cantidad: 6));
            vm.AnadirChequeRegaloCommand.Execute(null);
            A.CallTo(() => servicio.modificarPedido(A<PedidoVentaDTO>._)).ThrowsAsync(PedidoVentaService.InterpretarRespuestaError(
                "{\"error\":{\"code\":\"CHEQUE_REGALO_MINIMO_NO_SUPERADO\",\"message\":\"" + MENSAJE_MINIMO + "\"," +
                "\"details\":{\"minimo\":250.0,\"baseComputable\":180.0,\"falta\":70.01}}}"));

            await vm.ModificarPedidoAsync();

            A.CallTo(() => dialogos.ShowError(A<string>.That.Contains(MENSAJE_MINIMO))).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void InterpretarRespuestaError_ChequeRegalo_LlevaElMensajeTalCual()
        {
            Exception ex = PedidoVentaService.InterpretarRespuestaError(
                "{\"error\":{\"code\":\"CHEQUE_REGALO_YA_USADO\",\"message\":\"El cheque regalo del cliente 15191 ya está aplicado en el pedido 928123: solo se puede usar una vez.\"," +
                "\"details\":{\"pedidoCanje\":928123}}}");

            StringAssert.Contains(ex.Message, "El cheque regalo del cliente 15191 ya está aplicado en el pedido 928123: solo se puede usar una vez.");
        }
    }
}
