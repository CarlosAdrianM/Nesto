using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Models;
using Nesto.Infrastructure.Services;
using Nesto.Models;
using Nesto.Modulos.PedidoVenta;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity;
using ProductoPedido = Nesto.Modulos.PedidoVenta.PedidoVentaModel.Producto;

namespace PedidoVentaTests
{
    /// <summary>
    /// NestoAPI#581: al meter en una línea del pedido un producto que Compras pide sustituir, se avisa sin bloquear y,
    /// si el usuario quiere, la línea pasa al sustituto (con su precio y su nombre) con un clic.
    /// </summary>
    [TestClass]
    public class SustitucionProductoDetalleTests
    {
        private static SustitucionProductoDTO Sustitucion() => new SustitucionProductoDTO
        {
            Producto = "25539",
            ProductoSustituto = "45685",
            Vigente = true,
            Aviso = "Compras pide servir la 45685 (PANTALON PRESOTERAPIA COD040308) en lugar de la 25539 mientras no haya stock."
        };

        private static (DetallePedidoViewModel vm, IServicioDialogos dialogos, IPedidoVentaService servicio, IServicioSustitucionesProducto sustituciones)
            Vm(bool acepta, SustitucionProductoDTO sustitucion)
        {
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            A.CallTo(() => servicio.cargarProducto("1", "25539", A<string>._, A<string>._, A<short>._))
                .Returns(Task.FromResult(new ProductoPedido { producto = "25539", nombre = "PANTALON PRESOTERAPIA LOTE RT", precio = 10M, iva = "G21" }));
            A.CallTo(() => servicio.cargarProducto("1", "45685", A<string>._, A<string>._, A<short>._))
                .Returns(Task.FromResult(new ProductoPedido { producto = "45685", nombre = "PANTALON PRESOTERAPIA COD040308", precio = 12M, iva = "G21" }));
            IServicioDialogos dialogos = A.Fake<IServicioDialogos>();
            A.CallTo(() => dialogos.ShowConfirmationAnswer(A<string>._, A<string>._)).Returns(acepta);
            var vm = new DetallePedidoViewModel(A.Fake<IServicioNavegacion>(), A.Fake<IConfiguracion>(), servicio, new WeakReferenceMessenger(),
                dialogos, A.Fake<IUnityContainer>(), A.Fake<IServicioAutenticacion>());
            vm.ServicioFechaEntregaAgencia = A.Fake<IServicioFechaEntregaAgencia>();

            IServicioSustitucionesProducto sustituciones = A.Fake<IServicioSustitucionesProducto>();
            A.CallTo(() => sustituciones.LeerVigente(A<string>._, A<string>._, A<int>._)).Returns(Task.FromResult<SustitucionProductoDTO>(null));
            A.CallTo(() => sustituciones.LeerVigente("1", "25539", A<int>._)).Returns(Task.FromResult(sustitucion));
            vm.ComprobadorSustitucion = new ComprobadorSustitucionProducto(sustituciones);

            vm.pedido = new PedidoVentaWrapper(new PedidoVentaDTO
            {
                empresa = "1",
                numero = 927489,
                cliente = "34867",
                contacto = "0",
                Lineas = new List<LineaPedidoVentaDTO>()
            });
            return (vm, dialogos, servicio, sustituciones);
        }

        private static LineaPedidoVentaWrapper Linea() => new LineaPedidoVentaWrapper { tipoLinea = 1, Producto = "25539", Cantidad = 100 };

        [TestMethod]
        public async Task Acepta_LaLineaPasaAlSustitutoConSuPrecioYSuNombre()
        {
            var (vm, dialogos, servicio, _) = Vm(true, Sustitucion());
            LineaPedidoVentaWrapper linea = Linea();

            await vm.CargarDatosProductoEnLinea(linea, "25539", 100, true);

            Assert.AreEqual("45685", linea.Producto);
            Assert.AreEqual("PANTALON PRESOTERAPIA COD040308", linea.texto);
            Assert.AreEqual(12M, linea.PrecioUnitario);
            Assert.AreEqual((short)100, linea.Cantidad);
            A.CallTo(() => servicio.cargarProducto("1", "45685", "34867", "0", 100)).MustHaveHappenedOnceExactly();
            A.CallTo(() => dialogos.ShowConfirmationAnswer(ComprobadorSustitucionProducto.TITULO,
                A<string>.That.Contains("¿Poner la 45685 en su lugar?"))).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task Rechaza_SeQuedaElOriginal_YEnEstePedidoNoSeVuelveAPreguntar()
        {
            var (vm, dialogos, _, _) = Vm(false, Sustitucion());
            LineaPedidoVentaWrapper linea = Linea();

            await vm.CargarDatosProductoEnLinea(linea, "25539", 100, true);
            await vm.CargarDatosProductoEnLinea(linea, "25539", 120, true);

            Assert.AreEqual("25539", linea.Producto);
            Assert.AreEqual(10M, linea.PrecioUnitario);
            A.CallTo(() => dialogos.ShowConfirmationAnswer(A<string>._, A<string>._)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task SinSustitucion_NiSePregunta()
        {
            var (vm, dialogos, _, sustituciones) = Vm(true, null);
            LineaPedidoVentaWrapper linea = Linea();

            await vm.CargarDatosProductoEnLinea(linea, "25539", 100, true);

            Assert.AreEqual("25539", linea.Producto);
            A.CallTo(() => sustituciones.LeerVigente("1", "25539", 100)).MustHaveHappenedOnceExactly();
            A.CallTo(() => dialogos.ShowConfirmationAnswer(A<string>._, A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task ProductoQueNoExiste_NoSePreguntaPorLaSustitucion()
        {
            var (vm, _, servicio, sustituciones) = Vm(true, Sustitucion());
            A.CallTo(() => servicio.cargarProducto("1", "25539", A<string>._, A<string>._, A<short>._)).Returns(Task.FromResult<ProductoPedido>(null));

            await vm.CargarDatosProductoEnLinea(Linea(), "25539", 100, true);

            A.CallTo(() => sustituciones.LeerVigente(A<string>._, A<string>._, A<int>._)).MustNotHaveHappened();
        }
    }
}
