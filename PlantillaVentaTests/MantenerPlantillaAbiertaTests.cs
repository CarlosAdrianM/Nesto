using CommunityToolkit.Mvvm.Messaging;
using ControlesUsuario.Dialogs;
using ControlesUsuario.Models;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Models;
using Nesto.Modulos.PedidoVenta;
using Nesto.Modulos.PlantillaVenta;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace PlantillaVentaTests
{
    /// <summary>
    /// Sugerencia 1261 (Lidia, 13/11/26): con el parámetro de usuario PlantillaMantenerAbierta = «1», al grabar el pedido
    /// queda abierta una plantilla limpia (una NUEVA, por la navegación de siempre: sin restos del pedido grabado) además
    /// del pedido. Apagado, sin fila o si no se puede leer: la plantilla se cierra y se abre el pedido, como siempre.
    /// Cargar un borrador nunca cierra la pestaña ni abre otra.
    /// </summary>
    [TestClass]
    public class MantenerPlantillaAbiertaTests
    {
        private IPedidoVentaService _pedidosVenta;
        private IPlantillaVentaService _servicio;
        private IServicioDialogos _dialogos;
        private IServicioNavegacion _navegacion;
        private IAbridorPedidos _abridorPedidos;
        private IConfiguracion _configuracion;
        private IBorradorPlantillaVentaService _borradores;

        [TestInitialize]
        public void Setup()
        {
            _pedidosVenta = A.Fake<IPedidoVentaService>();
            _servicio = A.Fake<IPlantillaVentaService>();
            _dialogos = A.Fake<IServicioDialogos>();
            _navegacion = A.Fake<IServicioNavegacion>();
            _abridorPedidos = A.Fake<IAbridorPedidos>();
            _configuracion = A.Fake<IConfiguracion>();
            _borradores = A.Fake<IBorradorPlantillaVentaService>();
            A.CallTo(() => _configuracion.servidorAPI).Returns("http://127.0.0.1:9/api/");
            A.CallTo(() => _configuracion.LeerParametroSync(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.AlmacenRuta)).Returns("ALG");
            A.CallTo(() => _servicio.CrearPedido(A<PedidoVentaDTO>._))
                .Returns(Task.FromResult(new RespuestaCrearPedidoPlantilla { Numero = "930001" }));
            A.CallTo(() => _servicio.ConsultarAvisoCondicionesCampana(A<PedidoVentaDTO>._))
                .Returns(Task.FromResult(new AvisoCondicionesCampanaRespuesta { Avisar = false }));
        }

        private void ElParametroVale(string valor)
            => A.CallTo(() => _configuracion.leerParametro(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.PlantillaMantenerAbierta))
                .Returns(Task.FromResult(valor));

        private PlantillaVentaViewModel CrearViewModel()
            => new PlantillaVentaViewModel(_abridorPedidos, _navegacion, _configuracion, _servicio,
                new WeakReferenceMessenger(), _dialogos, _pedidosVenta, _borradores, A.Fake<IServicioAutenticacion>());

        private PlantillaVentaViewModel ListoParaGrabar()
        {
            var vm = CrearViewModel();
            vm.ListaFiltrableProductos.ListaOriginal = new ObservableCollection<IFiltrableItem>();
            vm._clienteSeleccionado = new ClienteJson { empresa = "1", cliente = "9238", contacto = "0", iva = "G21", cifNif = "12345678A" };
            vm.FormaPagoSeleccionada = new FormaPago { formaPago = "RCB" };
            typeof(PlantillaVentaViewModel).GetField("_plazoPagoSeleccionado", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(vm, new PlazosPago { plazoPago = "1/30" });
            vm.Estado.PlazosPago = "1/30";
            vm.formaVentaSeleccionada = 1;
            vm.almacenSeleccionado = new Sede { Codigo = "ALG", Nombre = "Algete" };
            vm.ListaFiltrableProductos.ListaOriginal.Add(new LineaPlantillaVenta { producto = "45383", texto = "TRIPLE GLOW", cantidad = 1, precio = 219.5M, grupo = "COS", iva = "G21" });
            return vm;
        }

        private void SeCierraYSeAbreElPedidoSinPlantillaNueva()
        {
            A.CallTo(() => _navegacion.CerrarVistaActiva("MainRegion")).MustHaveHappenedOnceExactly();
            A.CallTo(() => _abridorPedidos.Abrir("1", 930001)).MustHaveHappenedOnceExactly();
            A.CallTo(() => _navegacion.RequestNavigate(A<string>._, A<string>._)).MustNotHaveHappened();
            A.CallTo(() => _navegacion.RequestNavigate(A<string>._, A<string>._, A<ParametrosNavegacion>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task Grabar_ParametroApagado_SeCierraLaPlantillaYSeAbreElPedido_ComoSiempre()
        {
            ElParametroVale("0");
            var vm = ListoParaGrabar();

            await vm.CrearPedidoAsync();

            SeCierraYSeAbreElPedidoSinPlantillaNueva();
        }

        [TestMethod]
        public async Task Grabar_SinFilaDelParametro_SeCierraLaPlantillaYSeAbreElPedido_ComoSiempre()
        {
            ElParametroVale(null);
            var vm = ListoParaGrabar();

            await vm.CrearPedidoAsync();

            SeCierraYSeAbreElPedidoSinPlantillaNueva();
        }

        [TestMethod]
        public async Task Grabar_ElParametroNoSePuedeLeer_SeCierraLaPlantillaYSeAbreElPedido_ComoSiempre()
        {
            A.CallTo(() => _configuracion.leerParametro(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.PlantillaMantenerAbierta))
                .ThrowsAsync(new InvalidOperationException("sin red"));
            var vm = ListoParaGrabar();

            await vm.CrearPedidoAsync();

            Assert.AreEqual(1, Fake.GetCalls(_servicio).Count(c => c.Method.Name == nameof(IPlantillaVentaService.CrearPedido)), "El pedido se graba igual");
            SeCierraYSeAbreElPedidoSinPlantillaNueva();
        }

        [TestMethod]
        public async Task Grabar_ParametroEncendido_QuedaUnaPlantillaNuevaYElPedidoSeAbreComoSiempre()
        {
            ElParametroVale("1 ");
            var vm = ListoParaGrabar();

            await vm.CrearPedidoAsync();

            // La plantilla del pedido grabado se quita, entra una NUEVA (sin nada del pedido anterior) y luego el pedido
            A.CallTo(() => _navegacion.CerrarVistaActiva("MainRegion")).MustHaveHappenedOnceExactly()
                .Then(A.CallTo(() => _navegacion.RequestNavigate("MainRegion", "PlantillaVentaView")).MustHaveHappenedOnceExactly())
                .Then(A.CallTo(() => _abridorPedidos.Abrir("1", 930001)).MustHaveHappenedOnceExactly());
        }

        [TestMethod]
        public async Task Grabar_ParametroEncendido_LaPlantillaQueQuedaEsOtraSinRestosDelPedido()
        {
            // La navegación a «PlantillaVentaView» crea siempre vista y ViewModel nuevos (registro Transitorio y pestaña
            // nueva): la que queda abierta nace como recién abierta del menú, sin cliente, líneas ni borrador.
            ElParametroVale("1");
            var vm = ListoParaGrabar();
            PlantillaVentaViewModel nueva = null;
            A.CallTo(() => _navegacion.RequestNavigate("MainRegion", "PlantillaVentaView")).Invokes(() => nueva = CrearViewModel());

            await vm.CrearPedidoAsync();

            Assert.IsNotNull(nueva, "Tiene que abrirse una plantilla nueva");
            Assert.AreNotSame(vm, nueva);
            Assert.IsNull(nueva.clienteSeleccionado);
            Assert.IsFalse(nueva.NumeroPedidoEnEdicion.HasValue);
            Assert.AreEqual(0, nueva.ListaFiltrableProductos.ListaOriginal.Count);
            Assert.IsTrue(typeof(IReceptorNavegacionPestanaNueva).IsAssignableFrom(typeof(PlantillaVentaViewModel)),
                "La plantilla tiene que seguir pidiendo pestaña nueva en cada navegación: es lo que garantiza que nace limpia");
        }

        [TestMethod]
        public async Task Grabar_ParametroEncendidoPeroElPedidoFalla_NiSeCierraNiSeAbreOtraPlantilla()
        {
            ElParametroVale("1");
            A.CallTo(() => _servicio.CrearPedido(A<PedidoVentaDTO>._)).ThrowsAsync(new InvalidOperationException("Error del servidor"));
            var vm = ListoParaGrabar();

            await vm.CrearPedidoAsync();

            A.CallTo(() => _navegacion.CerrarVistaActiva(A<string>._)).MustNotHaveHappened();
            A.CallTo(() => _navegacion.RequestNavigate(A<string>._, A<string>._)).MustNotHaveHappened();
            A.CallTo(() => _abridorPedidos.Abrir(A<string>._, A<int>._)).MustNotHaveHappened();
            Assert.IsNotNull(vm.clienteSeleccionado, "La plantilla sigue con su pedido para corregirlo");
        }

        [TestMethod]
        public async Task MantenerPlantillaAbierta_SoloConUnUno()
        {
            var vm = CrearViewModel();

            ElParametroVale("1");
            Assert.IsTrue(await vm.MantenerPlantillaAbiertaAsync());
            ElParametroVale("0");
            Assert.IsFalse(await vm.MantenerPlantillaAbiertaAsync());
            ElParametroVale("");
            Assert.IsFalse(await vm.MantenerPlantillaAbiertaAsync());
            ElParametroVale("SI");
            Assert.IsFalse(await vm.MantenerPlantillaAbiertaAsync());
        }

        [TestMethod]
        public void CargarBorrador_SeCargaEnLaMismaPestana_NiSeCierraNiSeAbreOtra()
        {
            // Ya era así antes de la sugerencia (el borrador se carga sobre la plantilla abierta): se deja fijado.
            foreach (string valor in new[] { "0", "1" })
            {
                Fake.ClearRecordedCalls(_navegacion);
                ElParametroVale(valor);
                var borrador = new BorradorPlantillaVenta
                {
                    Id = "borrador-1",
                    Cliente = "9238",
                    Contacto = "0",
                    LineasProducto = new List<LineaPlantillaVenta> { new LineaPlantillaVenta { producto = "45383", cantidad = 1 } }
                };
                A.CallTo(() => _borradores.CargarBorrador("borrador-1")).Returns(borrador);
                var vm = CrearViewModel();

                vm.CargarBorradorCommand.Execute(borrador);

                Assert.IsFalse(Fake.GetCalls(_navegacion).Any(), $"Con el parámetro a «{valor}», cargar un borrador no navega ni cierra nada");
            }
        }
    }
}
