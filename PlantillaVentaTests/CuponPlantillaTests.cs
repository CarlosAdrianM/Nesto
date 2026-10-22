using CommunityToolkit.Mvvm.Messaging;
using ControlesUsuario.Models;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Models;
using Nesto.Infrastructure.Services;
using Nesto.Infrastructure.Shared;
using Nesto.Models;
using Nesto.Modulos.PedidoVenta;
using Nesto.Modulos.PlantillaVenta;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using ControlesUsuario.Dialogs;

namespace PlantillaVentaTests
{
    /// <summary>
    /// Nesto#518 (NestoAPI#613): al elegir el cliente en la plantilla se pregunta por sus cupones. Con uno que se puede usar sale
    /// el aviso con la casilla «Usar el cupón de 50 €»: marcarla añade la línea con el código al pedido que se manda y baja el
    /// total 50 € (IVA incluido); desmarcarla la quita. El que todavía no vale, solo el texto.
    /// </summary>
    [TestClass]
    public class CuponPlantillaTests
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
            Mensaje = estado == "Activo"
                ? "Tiene un cupón de 50,00 € (EV-2610-7K3Q) para un pedido de al menos 100,00 € de productos, hasta el 30/04/2027."
                : "Tiene un cupón de 50,00 € (EV-2610-7K3Q) que vale desde el 30/10/2026 hasta el 30/04/2027."
        };

        private static (PlantillaVentaViewModel vm, IServicioCupones cupones) CrearViewModel(params CuponDTO[] cupones)
        {
            IConfiguracion configuracion = A.Fake<IConfiguracion>();
            A.CallTo(() => configuracion.servidorAPI).Returns("http://127.0.0.1:9/api/");
            A.CallTo(() => configuracion.LeerParametroSync(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.AlmacenRuta)).Returns("ALG");
            var vm = new PlantillaVentaViewModel(A.Fake<IAbridorPedidos>(), A.Fake<IServicioNavegacion>(), configuracion, A.Fake<IPlantillaVentaService>(),
                new WeakReferenceMessenger(), A.Fake<IServicioDialogos>(), A.Fake<IPedidoVentaService>(), A.Fake<IBorradorPlantillaVentaService>(),
                A.Fake<IServicioAutenticacion>());
            vm.ListaFiltrableProductos.ListaOriginal = new ObservableCollection<IFiltrableItem>();
            vm._clienteSeleccionado = new ClienteJson { empresa = "1", cliente = "15191", contacto = "0", iva = "G21", cifNif = "12345678A" };

            IServicioCupones servicio = A.Fake<IServicioCupones>();
            A.CallTo(() => servicio.LeerDelCliente(A<string>._, A<string>._, A<bool>._)).Returns(Task.FromResult(cupones.ToList()));
            vm.ServicioCupones = servicio;
            return (vm, servicio);
        }

        private static LineaPlantillaVenta Linea(PlantillaVentaViewModel vm, string producto, int cantidad, decimal precio, string grupo = "COS")
        {
            var linea = new LineaPlantillaVenta { producto = producto, texto = "CREMA MASAJE 1L", cantidad = cantidad, precio = precio, grupo = grupo, iva = "G21" };
            vm.ListaFiltrableProductos.ListaOriginal.Add(linea);
            return linea;
        }

        private static PedidoVentaDTO PedidoConUnaLinea() => new PedidoVentaDTO
        {
            empresa = "1",
            cliente = "15191",
            Lineas = new List<LineaPedidoVentaDTO>
            {
                new LineaPedidoVentaDTO { tipoLinea = 1, Producto = "12345", Cantidad = 10, PrecioUnitario = 30M, almacen = "ALG", formaVenta = "DIR", iva = "G21", fechaEntrega = new DateTime(2026, 10, 14) }
            }
        };

        [TestMethod]
        public async Task SinCupon_NoHayAviso_NiSePuedeMarcar()
        {
            var (vm, servicio) = CrearViewModel();
            Linea(vm, "12345", 10, 30M);

            await vm.CargarCuponAsync();

            A.CallTo(() => servicio.LeerDelCliente("1", "15191", false)).MustHaveHappenedOnceExactly();
            Assert.IsFalse(vm.Cupon.HayCupon);
            Assert.IsFalse(vm.PuedeUsarCupon);
            vm.UsarCupon = true;
            Assert.IsFalse(vm.UsarCupon);
            Assert.AreEqual(300M, vm.baseImponiblePedido);
        }

        [TestMethod]
        public async Task CuponActivo_MarcarloBajaElTotal50Euros_YLaLineaLlevaElCodigo()
        {
            var (vm, _) = CrearViewModel(Cupon());
            Linea(vm, "12345", 10, 30M);

            await vm.CargarCuponAsync();

            Assert.IsTrue(vm.PuedeUsarCupon);
            Assert.AreEqual("Usar el cupón de 50 €", vm.Cupon.TextoUsar);
            Assert.IsFalse(vm.UsarCupon, "Sale desmarcada: la decide el vendedor");

            vm.UsarCupon = true;

            Assert.AreEqual(300M - 41.3223M, vm.baseImponiblePedido);
            Assert.AreEqual(363M - 50M, Math.Round(vm.totalPedido, 2), "Descuenta los 50 € con el IVA incluido, no 50 + IVA");
            LineaPlantillaVenta virtual_ = vm.listaProductosPedidoConPortes.Single(l => l.esLineaCupon);
            Assert.AreEqual(-1, virtual_.cantidad);
            Assert.AreEqual(41.3223M, virtual_.precio);
            Assert.AreEqual("Cupón EV-2610-7K3Q (Masterclass Cloasma)", virtual_.texto);

            PedidoVentaDTO pedido = PedidoConUnaLinea();
            vm.AnadirLineaCupon(pedido);
            LineaPedidoVentaDTO linea = pedido.Lineas.Single(l => l.Producto == "CUPONEVENTO");
            Assert.AreEqual("EV-2610-7K3Q", linea.CodigoCupon);
            Assert.AreEqual((byte?)1, linea.tipoLinea);
            Assert.AreEqual(-1, linea.Cantidad);
            Assert.AreEqual(41.3223M, linea.PrecioUnitario);
            Assert.IsFalse(linea.AplicarDescuento);
            Assert.AreEqual("ALG", linea.almacen);
            Assert.AreEqual("DIR", linea.formaVenta);
        }

        [TestMethod]
        public async Task Desmarcar_QuitaLaLineaYElTotalVuelve()
        {
            var (vm, _) = CrearViewModel(Cupon());
            Linea(vm, "12345", 10, 30M);
            await vm.CargarCuponAsync();
            vm.UsarCupon = true;

            vm.UsarCupon = false;

            Assert.AreEqual(300M, vm.baseImponiblePedido);
            Assert.IsFalse(vm.listaProductosPedidoConPortes.Any(l => l.esLineaCupon));
            PedidoVentaDTO pedido = PedidoConUnaLinea();
            vm.AnadirLineaCupon(pedido);
            Assert.AreEqual(1, pedido.Lineas.Count);
        }

        [TestMethod]
        public async Task ClienteSinIva_LaBaseEsElImporte()
        {
            var (vm, _) = CrearViewModel(Cupon());
            vm._clienteSeleccionado.iva = null;
            Linea(vm, "12345", 10, 30M);
            await vm.CargarCuponAsync();

            vm.UsarCupon = true;

            Assert.AreEqual(250M, vm.baseImponiblePedido);
        }

        [TestMethod]
        public async Task CuponQueTodaviaNoVale_SoloElTexto()
        {
            var (vm, _) = CrearViewModel(Cupon("Emitido"));
            Linea(vm, "12345", 10, 30M);

            await vm.CargarCuponAsync();

            Assert.IsTrue(vm.Cupon.HayCupon);
            Assert.IsFalse(vm.PuedeUsarCupon);
            StringAssert.Contains(vm.Cupon.Mensaje, "vale desde el 30/10/2026");
            vm.UsarCupon = true;
            Assert.IsFalse(vm.UsarCupon);
        }

        [TestMethod]
        public async Task ModificandoElPedidoQueLoLleva_PideTodos_YSaleMarcada()
        {
            var (vm, servicio) = CrearViewModel(Cupon("Usado", 930001));
            vm.NumeroPedidoEnEdicion = 930001;
            Linea(vm, "12345", 10, 30M);

            await vm.CargarCuponAsync();

            A.CallTo(() => servicio.LeerDelCliente("1", "15191", true)).MustHaveHappenedOnceExactly();
            Assert.IsTrue(vm.PuedeUsarCupon);
            Assert.IsTrue(vm.UsarCupon, "Para que el PUT no lo suelte");
            PedidoVentaDTO pedido = PedidoConUnaLinea();
            vm.AnadirLineaCupon(pedido);
            Assert.AreEqual("EV-2610-7K3Q", pedido.Lineas.Single(l => l.Producto == "CUPONEVENTO").CodigoCupon);
        }

        [TestMethod]
        public async Task PedidoMinimo_DiceCuantoFalta_SinCursos()
        {
            var (vm, _) = CrearViewModel(Cupon());
            Linea(vm, "12345", 1, 50M);           // 60,50 € con IVA
            Linea(vm, "CURSO1", 1, 100M, "CUR");  // los cursos no cuentan
            await vm.CargarCuponAsync();

            Assert.AreEqual(60.5M, vm.TotalComputableCupon);
            Assert.IsTrue(vm.HayTextoFaltaCupon);
            StringAssert.StartsWith(vm.TextoFaltaCupon, "Faltan 39,50 €");

            Linea(vm, "12346", 1, 40M);           // 108,90 € con IVA
            Assert.IsFalse(vm.HayTextoFaltaCupon);
        }

        [TestMethod]
        public async Task ElCuponBajaLaBaseDeGanavisiones_ComoElCheque()
        {
            var (vm, _) = CrearViewModel(Cupon());
            Linea(vm, "12345", 10, 30M);
            await vm.CargarCuponAsync();
            decimal antes = vm.BaseImponibleBonificable;

            vm.UsarCupon = true;

            Assert.AreEqual(antes - 41.3223M, vm.BaseImponibleBonificable);
        }

        [TestMethod]
        public async Task UnaVezPorCliente()
        {
            var (vm, servicio) = CrearViewModel(Cupon());
            await vm.CargarCuponAsync();
            await vm.CargarCuponAsync();

            A.CallTo(() => servicio.LeerDelCliente(A<string>._, A<string>._, A<bool>._)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task ConvieneConElChequeRegalo_LosDosRestan()
        {
            var (vm, _) = CrearViewModel(Cupon());
            IServicioChequesRegalo cheques = A.Fake<IServicioChequesRegalo>();
            A.CallTo(() => cheques.LeerDelCliente(A<string>._, A<string>._)).Returns(Task.FromResult(new ChequeRegaloClienteDTO
            {
                Campana = "CHEQUE50_OCT_2026", Producto = "CHEQUE50_OCT26", Importe = 50M, MinimoCanje = 250M, Estado = "Disponible", SePuedeUsar = true
            }));
            vm.ServicioChequesRegalo = cheques;
            Linea(vm, "12345", 10, 30M);
            await vm.CargarChequeRegaloAsync();
            await vm.CargarCuponAsync();

            vm.UsarChequeRegalo = true;
            vm.UsarCupon = true;

            Assert.AreEqual(300M - 50M - 41.3223M, vm.baseImponiblePedido);
            PedidoVentaDTO pedido = PedidoConUnaLinea();
            vm.AnadirLineaChequeRegalo(pedido);
            vm.AnadirLineaCupon(pedido);
            Assert.AreEqual(3, pedido.Lineas.Count);
        }
    }
}
