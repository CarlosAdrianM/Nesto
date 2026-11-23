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
    /// Nesto#648 (NestoAPI#885): en el detalle del pedido, un cupón que todavía no vale (Emitido) y que la API deja adelantar
    /// (SePuedeAdelantar) sale con «Usar ya (excepción)»: pide el motivo, lo adelanta en la API y deja el cupón ya activo en el
    /// aviso de siempre, sin añadirlo. Sin SePuedeAdelantar, como antes (no se ve nada).
    /// </summary>
    [TestClass]
    public class AdelantarCuponDetalleTests
    {
        private static CuponDTO Cupon(string estado = "Emitido", bool sePuedeAdelantar = true) => new CuponDTO
        {
            Id = 7,
            Codigo = "EV-2611-7K3Q",
            Empresa = "1",
            Cliente = "15191",
            Importe = 100M,
            PedidoMinimo = 200M,
            Origen = "Evento",
            Evento = "Masterclass Cloasma",
            ValidoDesde = new DateTime(2026, 12, 1),
            Caducidad = new DateTime(2027, 6, 1),
            Estado = estado,
            SePuedeUsar = estado == "Activo",
            SePuedeAdelantar = estado == "Emitido" && sePuedeAdelantar,
            Producto = "CUPONEVENTO",
            Mensaje = estado == "Activo"
                ? "Tiene un cupón de 100,00 € (EV-2611-7K3Q) para un pedido de al menos 200,00 € de productos, hasta el 01/06/2027."
                : "Tiene un cupón de 100 € (EV-2611-7K3Q) que vale desde el 01/12/2026."
        };

        private static async Task<(DetallePedidoViewModel vm, IServicioCupones cupones, IServicioDialogos dialogos)> Vm(CuponDTO cupon)
        {
            IServicioDialogos dialogos = A.Fake<IServicioDialogos>();
            var vm = new DetallePedidoViewModel(A.Fake<IServicioNavegacion>(), A.Fake<IConfiguracion>(), A.Fake<IPedidoVentaService>(), new WeakReferenceMessenger(),
                dialogos, A.Fake<IAbridorPedidos>(), A.Fake<IServicioAutenticacion>());
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
                iva = "G21",
                Lineas = new List<LineaPedidoVentaDTO>
                {
                    new LineaPedidoVentaDTO { id = 1, tipoLinea = 1, Producto = "12345", Cantidad = 10, PrecioUnitario = 30M, almacen = "ALG",
                        formaVenta = "DIR", iva = "G21", PorcentajeIva = 0.21M, estado = 1, fechaEntrega = new DateTime(2026, 11, 23) }
                }
            });
            await vm.CargarCuponAsync();
            return (vm, cupones, dialogos);
        }

        [TestMethod]
        public async Task CuponEmitidoQueSePuedeAdelantar_SaleElBoton_YNoElDeAnadir()
        {
            var (vm, _, _) = await Vm(Cupon());

            Assert.IsTrue(vm.MostrarAvisoCuponAdelantable);
            Assert.IsTrue(vm.AdelantarCuponCommand.CanExecute(null));
            Assert.IsFalse(vm.MostrarAvisoCupon);
            Assert.IsFalse(vm.AnadirCuponCommand.CanExecute(null));
        }

        [TestMethod]
        public async Task SinSePuedeAdelantar_TodoComoAntes()
        {
            var (vm, _, _) = await Vm(Cupon(sePuedeAdelantar: false));
            Assert.IsTrue(vm.Cupon.HayCupon);
            Assert.IsFalse(vm.MostrarAvisoCuponAdelantable);
            Assert.IsFalse(vm.AdelantarCuponCommand.CanExecute(null));
            Assert.IsFalse(vm.MostrarAvisoCupon);

            var (vmActivo, _, _) = await Vm(Cupon("Activo"));
            Assert.IsFalse(vmActivo.MostrarAvisoCuponAdelantable, "Un cupón que ya vale no se adelanta");
            Assert.IsTrue(vmActivo.MostrarAvisoCupon);
        }

        [TestMethod]
        public async Task MotivoVacioOCancelado_NoLlamaALaApi()
        {
            var (vm, cupones, dialogos) = await Vm(Cupon());
            A.CallTo(() => dialogos.GetText(A<string>._, A<string>._)).Returns((string)null);
            await vm.AdelantarCuponAsync();

            A.CallTo(() => dialogos.GetText(A<string>._, A<string>._)).Returns("   ");
            await vm.AdelantarCuponAsync();

            A.CallTo(() => cupones.AdelantarCupon(A<string>._, A<string>._)).MustNotHaveHappened();
            Assert.IsTrue(vm.MostrarAvisoCuponAdelantable);
            Assert.AreEqual("Emitido", vm.Cupon.Cupon.Estado);
        }

        [TestMethod]
        public async Task Adelantado_RefrescaElCupon_YYaSePuedeAnadir_SinAnadirlo()
        {
            var (vm, cupones, dialogos) = await Vm(Cupon());
            A.CallTo(() => dialogos.GetText(A<string>._, A<string>._)).Returns(" La clienta viene hoy. ");
            A.CallTo(() => cupones.AdelantarCupon("EV-2611-7K3Q", "La clienta viene hoy.")).Returns(Task.FromResult(Cupon("Activo")));

            await vm.AdelantarCuponAsync();

            A.CallTo(() => cupones.AdelantarCupon("EV-2611-7K3Q", "La clienta viene hoy.")).MustHaveHappenedOnceExactly();
            Assert.IsFalse(vm.MostrarAvisoCuponAdelantable);
            Assert.IsFalse(vm.AdelantarCuponCommand.CanExecute(null));
            Assert.IsTrue(vm.MostrarAvisoCupon);
            Assert.IsTrue(vm.AnadirCuponCommand.CanExecute(null));
            Assert.IsFalse(vm.PedidoLlevaCupon, "No se añade solo: lo añade el usuario con el botón de siempre");
            A.CallTo(() => dialogos.ShowNotification(A<string>._, "Cupón adelantado: ya se puede añadir al pedido.")).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task ErrorDeLaApi_EnsenaSuMensaje_YNoCambiaNada()
        {
            var (vm, cupones, dialogos) = await Vm(Cupon());
            A.CallTo(() => dialogos.GetText(A<string>._, A<string>._)).Returns("motivo");
            A.CallTo(() => cupones.AdelantarCupon(A<string>._, A<string>._))
                .ThrowsAsync(new InvalidOperationException("No tiene permiso para adelantar cupones."));

            await vm.AdelantarCuponAsync();

            A.CallTo(() => dialogos.ShowError("No tiene permiso para adelantar cupones.")).MustHaveHappenedOnceExactly();
            A.CallTo(() => dialogos.ShowNotification(A<string>._, A<string>._)).MustNotHaveHappened();
            Assert.IsTrue(vm.MostrarAvisoCuponAdelantable);
            Assert.IsFalse(vm.MostrarAvisoCupon);
            Assert.AreEqual("Emitido", vm.Cupon.Cupon.Estado);
            Assert.IsFalse(vm.pedido.Lineas.Any(l => l.Producto == "CUPONEVENTO"));
        }
    }
}
