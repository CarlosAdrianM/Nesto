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
using ControlesUsuario.Dialogs;

namespace PedidoVentaTests
{
    /// <summary>
    /// Nesto#518 (NestoAPI#613): en el detalle del pedido, si el cliente tiene un cupón que se puede usar y el pedido no lleva
    /// ninguno, aviso con «Añadir el cupón»: añade la línea con el código (−50 € con el IVA incluido) y la manda el guardado
    /// normal. La línea del cupón se ve y, mientras no esté servida ni en picking, se puede quitar (vuelve a Activo al guardar).
    /// </summary>
    [TestClass]
    public class CuponDetalleTests
    {
        private static CuponDTO Cupon(string estado = "Activo", int? usadoEnPedido = null) => new CuponDTO
        {
            Id = 7,
            Codigo = "EV-2610-7K3Q",
            Empresa = "1",
            Cliente = "15191",
            Importe = 50M,
            PedidoMinimo = 100M,
            Origen = "Evento",
            Evento = "Masterclass Cloasma",
            ValidoDesde = new DateTime(2026, 10, 30),
            Caducidad = new DateTime(2027, 4, 30),
            Estado = estado,
            SePuedeUsar = estado == "Activo",
            UsadoEnPedido = usadoEnPedido,
            Producto = "CUPONEVENTO",
            TextoLinea = "Cupón EV-2610-7K3Q (Masterclass Cloasma)",
            Mensaje = "Tiene un cupón de 50,00 € (EV-2610-7K3Q) para un pedido de al menos 100,00 € de productos, hasta el 30/04/2027."
        };

        private static async Task<(DetallePedidoViewModel vm, IServicioCupones cupones)> Vm(CuponDTO cupon, string iva, params LineaPedidoVentaDTO[] lineas)
        {
            var vm = new DetallePedidoViewModel(A.Fake<IServicioNavegacion>(), A.Fake<IConfiguracion>(), A.Fake<IPedidoVentaService>(), new WeakReferenceMessenger(),
                A.Fake<IServicioDialogos>(), A.Fake<IAbridorPedidos>(), A.Fake<IServicioAutenticacion>());
            vm.ServicioFechaEntregaAgencia = A.Fake<IServicioFechaEntregaAgencia>();
            vm.ServicioChequesRegalo = A.Fake<IServicioChequesRegalo>();
            IServicioCupones cupones = A.Fake<IServicioCupones>();
            A.CallTo(() => cupones.LeerDelCliente(A<string>._, A<string>._, A<bool>._))
                .Returns(Task.FromResult(cupon == null ? new List<CuponDTO>() : new List<CuponDTO> { cupon }));
            vm.ServicioCupones = cupones;

            vm.pedido = new PedidoVentaWrapper(new PedidoVentaDTO
            {
                empresa = "1",
                numero = 930001,
                cliente = "15191",
                contacto = "0",
                iva = iva,
                Lineas = lineas.ToList()
            });
            await vm.CargarCuponAsync();
            return (vm, cupones);
        }

        private static LineaPedidoVentaDTO Producto(decimal porcentajeIva = 0.21M, decimal porcentajeRecargo = 0M) => new LineaPedidoVentaDTO
        {
            id = 1,
            tipoLinea = 1,
            Producto = "12345",
            Cantidad = 10,
            PrecioUnitario = 30M,
            almacen = "ALG",
            formaVenta = "DIR",
            iva = "G21",
            PorcentajeIva = porcentajeIva,
            PorcentajeRecargoEquivalencia = porcentajeRecargo,
            estado = 1,
            fechaEntrega = new DateTime(2026, 10, 14)
        };

        private static LineaPedidoVentaDTO LineaCupon(short estado = 1, int picking = 0) => new LineaPedidoVentaDTO
        {
            id = 2,
            tipoLinea = 1,
            Producto = "CUPONEVENTO",
            Cantidad = -1,
            PrecioUnitario = 41.3223M,
            almacen = "ALG",
            formaVenta = "DIR",
            iva = "G21",
            PorcentajeIva = 0.21M,
            estado = estado,
            picking = picking,
            texto = "Cupón EV-2610-7K3Q (Masterclass Cloasma)",
            fechaEntrega = new DateTime(2026, 10, 14)
        };

        [TestMethod]
        public async Task SinCupon_NoHayAviso()
        {
            var (vm, cupones) = await Vm(null, "G21", Producto());

            A.CallTo(() => cupones.LeerDelCliente("1", "15191", true)).MustHaveHappened();
            Assert.IsFalse(vm.MostrarAvisoCupon);
            Assert.IsFalse(vm.PedidoLlevaCupon);
            Assert.IsFalse(vm.AnadirCuponCommand.CanExecute(null));
        }

        [TestMethod]
        public async Task CuponActivo_SinLaLinea_SaleElAviso()
        {
            var (vm, _) = await Vm(Cupon(), "G21", Producto());

            Assert.IsTrue(vm.MostrarAvisoCupon);
            Assert.IsTrue(vm.AnadirCuponCommand.CanExecute(null));
            Assert.AreEqual("Añadir el cupón de 50 €", vm.Cupon.TextoAnadir);
        }

        [TestMethod]
        public async Task AnadirElCupon_LineaConElCodigo_YElTotalBaja50Euros()
        {
            var (vm, _) = await Vm(Cupon(), "G21", Producto());
            decimal totalAntes = vm.pedido.Total;

            vm.AnadirCuponCommand.Execute(null);

            LineaPedidoVentaWrapper linea = vm.pedido.Lineas.Single(l => l.Producto == "CUPONEVENTO");
            Assert.AreEqual("EV-2610-7K3Q", linea.Model.CodigoCupon);
            Assert.AreEqual((short)-1, linea.Cantidad);
            Assert.AreEqual(41.3223M, linea.PrecioUnitario);
            Assert.IsFalse(linea.AplicarDescuento);
            Assert.AreEqual(0, linea.id, "Línea nueva: la manda el guardado normal");
            Assert.IsTrue(vm.pedido.Model.Lineas.Any(l => l.CodigoCupon == "EV-2610-7K3Q"), "Está en el modelo que viaja en el PUT");
            Assert.AreEqual(totalAntes - 50M, Math.Round(vm.pedido.Total, 2));
            Assert.IsFalse(vm.MostrarAvisoCupon);
            Assert.IsTrue(vm.PedidoLlevaCupon);
            Assert.IsTrue(vm.SePuedeQuitarCupon);
        }

        [TestMethod]
        public async Task ConRecargo_LaBaseDescuentaLos50Exactos()
        {
            var (vm, _) = await Vm(Cupon(), "G21", Producto(0.21M, 0.052M));
            decimal totalAntes = vm.pedido.Total;

            vm.AnadirCuponCommand.Execute(null);

            Assert.AreEqual(39.6197M, vm.pedido.Lineas.Single(l => l.Producto == "CUPONEVENTO").PrecioUnitario);
            Assert.AreEqual(totalAntes - 50M, Math.Round(vm.pedido.Total, 2));
        }

        [TestMethod]
        public async Task PedidoSinIva_LaBaseEsElImporte()
        {
            var (vm, _) = await Vm(Cupon(), null, Producto(0M));

            vm.AnadirCuponCommand.Execute(null);

            Assert.AreEqual(50M, vm.pedido.Lineas.Single(l => l.Producto == "CUPONEVENTO").PrecioUnitario);
        }

        [TestMethod]
        public async Task PedidoQueYaLoLleva_SeVeYSePuedeQuitar()
        {
            var (vm, _) = await Vm(Cupon("Usado", 930001), "G21", Producto(), LineaCupon());

            Assert.IsTrue(vm.PedidoLlevaCupon);
            Assert.IsFalse(vm.MostrarAvisoCupon);
            StringAssert.Contains(vm.TextoCuponAplicado, "el cupón EV-2610-7K3Q de 50 € (IVA incluido)");
            Assert.IsTrue(vm.QuitarCuponCommand.CanExecute(null));

            vm.QuitarCuponCommand.Execute(null);

            Assert.IsFalse(vm.pedido.Lineas.Any(l => l.Producto == "CUPONEVENTO"));
            Assert.IsFalse(vm.pedido.Model.Lineas.Any(l => l.Producto == "CUPONEVENTO"), "El PUT va sin la línea: el servidor suelta el cupón");
            Assert.IsFalse(vm.PedidoLlevaCupon);
        }

        [TestMethod]
        public async Task LineaServidaOEnPicking_NoSeQuita()
        {
            var (vm, _) = await Vm(Cupon("Usado", 930001), "G21", Producto(), LineaCupon(estado: 2));
            Assert.IsTrue(vm.PedidoLlevaCupon);
            Assert.IsFalse(vm.SePuedeQuitarCupon);
            Assert.IsFalse(vm.QuitarCuponCommand.CanExecute(null));
            StringAssert.Contains(vm.TextoCuponAplicado, "no se puede quitar");

            var (vm2, _) = await Vm(Cupon("Usado", 930001), "G21", Producto(), LineaCupon(picking: 81000));
            Assert.IsFalse(vm2.SePuedeQuitarCupon);
        }

        [TestMethod]
        public async Task CuponUsadoEnOtroPedido_NiAvisoNiBotones()
        {
            var (vm, _) = await Vm(Cupon("Usado", 929999), "G21", Producto());

            Assert.IsFalse(vm.Cupon.HayCupon);
            Assert.IsFalse(vm.MostrarAvisoCupon);
            Assert.IsFalse(vm.PedidoLlevaCupon);
        }
    }
}
