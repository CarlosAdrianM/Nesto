using CommunityToolkit.Mvvm.Messaging;
using ControlesUsuario.Dialogs;
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
using System.Linq;
using System.Threading.Tasks;

namespace PlantillaVentaTests
{
    /// <summary>
    /// Incidencia 1328 (María José, pedido 930358, 18/11/26): modificar con la plantilla un pedido con líneas ya en picking
    /// del 11/11 las mandaba con la fecha de la plantilla (18/11, la mínima de hoy) y NestoAPI lo tomaba por un aplazamiento.
    /// Las líneas existentes con picking viajan con la fecha que tenían; las nuevas y las existentes sin picking, con la de la plantilla.
    /// </summary>
    [TestClass]
    public class FechaEntregaLineasConPickingTests
    {
        private static readonly DateTime FECHA_ORIGINAL = new DateTime(2026, 11, 11);
        private static readonly DateTime FECHA_PLANTILLA = new DateTime(2026, 11, 18);
        private const int ID_CON_PICKING = 330000100;
        private const int ID_SIN_PICKING = 330000200;
        private const int PICKING = 100114;

        /// <summary>El 930358 tal como lo devuelve GET api/PedidosVenta: una línea pendiente con picking y otra sin él, del 11/11.</summary>
        private static PedidoVentaDTO PedidoOriginal()
        {
            var pedido = new PedidoVentaDTO { empresa = "1", numero = 930358 };
            pedido.Lineas.Add(new LineaPedidoVentaDTO { id = ID_CON_PICKING, Producto = "17404", Cantidad = 2, estado = 1, picking = PICKING, fechaEntrega = FECHA_ORIGINAL });
            pedido.Lineas.Add(new LineaPedidoVentaDTO { id = ID_SIN_PICKING, Producto = "45929", Cantidad = 1, estado = 1, picking = 0, fechaEntrega = FECHA_ORIGINAL });
            return pedido;
        }

        /// <summary>Lo que arma la plantilla: todas las líneas con la fecha única de la plantilla.</summary>
        private static PedidoVentaDTO PedidoDeLaPlantilla()
        {
            var pedido = new PedidoVentaDTO { empresa = "1", numero = 930358 };
            pedido.Lineas.Add(new LineaPedidoVentaDTO { id = ID_CON_PICKING, Producto = "17404", Cantidad = 2, fechaEntrega = FECHA_PLANTILLA });
            pedido.Lineas.Add(new LineaPedidoVentaDTO { id = 0, Producto = "41000", Cantidad = 3, fechaEntrega = FECHA_PLANTILLA });
            pedido.Lineas.Add(new LineaPedidoVentaDTO { id = ID_SIN_PICKING, Producto = "45929", Cantidad = 1, fechaEntrega = FECHA_PLANTILLA });
            return pedido;
        }

        private static DateTime Fecha(PedidoVentaDTO pedido, string producto) => pedido.Lineas.Single(l => l.Producto == producto).fechaEntrega;

        [TestMethod]
        public void CasoReal930358_LaLineaConPickingConservaSuFecha_LaNuevaYLaSinPickingLlevanLaDeLaPlantilla()
        {
            PedidoVentaDTO pedido = PedidoDeLaPlantilla();

            int corregidas = FechaEntregaLineasConPicking.ConservarFechaOriginal(pedido, PedidoOriginal());

            Assert.AreEqual(1, corregidas);
            Assert.AreEqual(FECHA_ORIGINAL, Fecha(pedido, "17404"), "La línea con picking 100114 viaja con el 11/11");
            Assert.AreEqual(FECHA_PLANTILLA, Fecha(pedido, "41000"), "La línea nueva, con la fecha de la plantilla");
            Assert.AreEqual(FECHA_PLANTILLA, Fecha(pedido, "45929"), "La existente sin picking, con la fecha de la plantilla");
        }

        [TestMethod]
        public void LineaConPickingYaEnAlbaran_NoSeToca()
        {
            PedidoVentaDTO original = PedidoOriginal();
            original.Lineas.Single(l => l.id == ID_CON_PICKING).estado = 2;
            PedidoVentaDTO pedido = PedidoDeLaPlantilla();

            Assert.AreEqual(0, FechaEntregaLineasConPicking.ConservarFechaOriginal(pedido, original));
            Assert.AreEqual(FECHA_PLANTILLA, Fecha(pedido, "17404"));
        }

        [TestMethod]
        public void LineaConPickingQueYaNoEstaEnElPedido_NoSeToca()
        {
            PedidoVentaDTO original = PedidoOriginal();
            original.Lineas.Remove(original.Lineas.Single(l => l.id == ID_CON_PICKING));
            PedidoVentaDTO pedido = PedidoDeLaPlantilla();

            Assert.AreEqual(0, FechaEntregaLineasConPicking.ConservarFechaOriginal(pedido, original));
            Assert.AreEqual(FECHA_PLANTILLA, Fecha(pedido, "17404"));
        }

        [TestMethod]
        public void SinPedidoOriginal_NoHaceNada()
        {
            PedidoVentaDTO pedido = PedidoDeLaPlantilla();

            Assert.AreEqual(0, FechaEntregaLineasConPicking.ConservarFechaOriginal(pedido, null));
            Assert.AreEqual(0, FechaEntregaLineasConPicking.ConservarFechaOriginal(null, PedidoOriginal()));
            Assert.IsTrue(pedido.Lineas.All(l => l.fechaEntrega == FECHA_PLANTILLA));
        }

        [TestMethod]
        public void HayLineasConPicking_MiraPagoOfertaYRegalos()
        {
            Assert.IsFalse(FechaEntregaLineasConPicking.HayLineasConPicking(null));
            Assert.IsFalse(FechaEntregaLineasConPicking.HayLineasConPicking(new PedidoParaPlantillaModel
            {
                Lineas = new List<LineaParaPlantillaModel> { new LineaParaPlantillaModel { IdLineaPago = 1 } }
            }));
            Assert.IsTrue(FechaEntregaLineasConPicking.HayLineasConPicking(new PedidoParaPlantillaModel
            {
                Lineas = new List<LineaParaPlantillaModel> { new LineaParaPlantillaModel { IdLineaPago = 1, PagoTienePicking = true } }
            }));
            Assert.IsTrue(FechaEntregaLineasConPicking.HayLineasConPicking(new PedidoParaPlantillaModel
            {
                Lineas = new List<LineaParaPlantillaModel> { new LineaParaPlantillaModel { IdLineaPago = 1, IdLineaOferta = 2, OfertaTienePicking = true } }
            }));
            Assert.IsTrue(FechaEntregaLineasConPicking.HayLineasConPicking(new PedidoParaPlantillaModel
            {
                Regalos = new List<RegaloParaPlantillaModel> { new RegaloParaPlantillaModel { IdLinea = 3, TienePicking = true } }
            }));
        }

        [TestMethod]
        public void ViewModel_LeeElPedidoYPoneLaFechaOriginalALaLineaConPicking() => EnSta(async () =>
        {
            IPedidoVentaService servicioPedidos = A.Fake<IPedidoVentaService>();
            A.CallTo(() => servicioPedidos.cargarPedido("1", 930358)).ReturnsLazily(() => Task.FromResult(PedidoOriginal()));
            PlantillaVentaViewModel vm = CrearViewModel(servicioPedidos);
            PedidoVentaDTO pedido = PedidoDeLaPlantilla();

            await vm.ConservarFechaLineasConPickingAsync(pedido, "1", 930358);

            Assert.AreEqual(FECHA_ORIGINAL, Fecha(pedido, "17404"));
            Assert.AreEqual(FECHA_PLANTILLA, Fecha(pedido, "41000"));
            Assert.AreEqual(FECHA_PLANTILLA, Fecha(pedido, "45929"));
        });

        [TestMethod]
        public void ViewModel_SiNoSePuedeLeerElPedido_GuardaComoAntesYLoDejaEnElmah() => EnSta(async () =>
        {
            IPedidoVentaService servicioPedidos = A.Fake<IPedidoVentaService>();
            A.CallTo(() => servicioPedidos.cargarPedido(A<string>._, A<int>._)).ThrowsAsync(new Exception("Sin conexión"));
            IServicioRegistroErrores errores = A.Fake<IServicioRegistroErrores>();
            PlantillaVentaViewModel vm = CrearViewModel(servicioPedidos);
            vm.ServicioRegistroErrores = errores;
            PedidoVentaDTO pedido = PedidoDeLaPlantilla();

            await vm.ConservarFechaLineasConPickingAsync(pedido, "1", 930358);

            Assert.IsTrue(pedido.Lineas.All(l => l.fechaEntrega == FECHA_PLANTILLA));
            A.CallTo(() => errores.RegistrarErrorAsync(A<Exception>._, A<string>.That.Contains("930358"))).MustHaveHappenedOnceExactly();
        });

        private static PlantillaVentaViewModel CrearViewModel(IPedidoVentaService servicioPedidos)
        {
            IConfiguracion configuracion = A.Fake<IConfiguracion>();
            A.CallTo(() => configuracion.servidorAPI).Returns("http://127.0.0.1:9/api/");
            A.CallTo(() => configuracion.LeerParametroSync(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.AlmacenRuta)).Returns("ALG");
            return new PlantillaVentaViewModel(A.Fake<IAbridorPedidos>(), A.Fake<IServicioNavegacion>(), configuracion, A.Fake<IPlantillaVentaService>(),
                new WeakReferenceMessenger(), A.Fake<IServicioDialogos>(), servicioPedidos, new BorradorPlantillaVentaService(configuracion),
                A.Fake<IServicioAutenticacion>());
        }

        private static void EnSta(Func<Task> prueba)
        {
            Exception error = null;
            var hilo = new System.Threading.Thread(() =>
            {
                try
                {
                    prueba().GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    error = ex;
                }
            });
            hilo.SetApartmentState(System.Threading.ApartmentState.STA);
            hilo.Start();
            if (!hilo.Join(TimeSpan.FromSeconds(60)))
            {
                Assert.Fail("La prueba no ha terminado");
            }
            if (error != null)
            {
                throw new AssertFailedException(error.ToString(), error);
            }
        }
    }
}
