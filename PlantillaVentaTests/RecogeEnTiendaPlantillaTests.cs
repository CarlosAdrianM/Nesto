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
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Threading.Tasks;

namespace PlantillaVentaTests
{
    /// <summary>
    /// Nesto#503 (NestoAPI#571, cortes 1 y 2): la casilla «Recoge en tienda» de la plantilla. En Algete se elige (desmarcada si la
    /// ruta no es de recogida), en Alcobendas y Reina es fija y marcada, en otro almacén o con Glovo no sale. Solo viaja en el
    /// POST si el usuario la toca; marcada, el resumen no enseña portes ni comisión y la petición de portes lleva RecogeEnTienda.
    /// </summary>
    [TestClass]
    public class RecogeEnTiendaPlantillaTests
    {
        private IPedidoVentaService _pedidosVenta;
        private IPlantillaVentaService _servicio;
        private List<PedidoVentaDTO> _creados;

        [TestInitialize]
        public void Setup()
        {
            _pedidosVenta = A.Fake<IPedidoVentaService>();
            _servicio = A.Fake<IPlantillaVentaService>();
            _creados = new List<PedidoVentaDTO>();
            A.CallTo(() => _servicio.CrearPedido(A<PedidoVentaDTO>._)).ReturnsLazily((PedidoVentaDTO p) =>
            {
                _creados.Add(p);
                return Task.FromResult(new RespuestaCrearPedidoPlantilla { Numero = "930001" });
            });
        }

        private PlantillaVentaViewModel CrearViewModel(string almacen = "ALG", string ruta = "AT")
        {
            IConfiguracion configuracion = A.Fake<IConfiguracion>();
            A.CallTo(() => configuracion.servidorAPI).Returns("http://127.0.0.1:9/api/");
            A.CallTo(() => configuracion.LeerParametroSync(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.AlmacenRuta)).Returns("ALG");
            var vm = new PlantillaVentaViewModel(A.Fake<IAbridorPedidos>(), A.Fake<IServicioNavegacion>(), configuracion, _servicio,
                new WeakReferenceMessenger(), A.Fake<IServicioDialogos>(), _pedidosVenta, A.Fake<IBorradorPlantillaVentaService>(),
                A.Fake<IServicioAutenticacion>());
            vm.ListaFiltrableProductos.ListaOriginal = new ObservableCollection<IFiltrableItem>();
            vm._clienteSeleccionado = new ClienteJson { empresa = "1", cliente = "9238", contacto = "0", iva = "G21", cifNif = "12345678A" };
            vm.FormaPagoSeleccionada = new FormaPago { formaPago = "EFC" };
            typeof(PlantillaVentaViewModel).GetField("_plazoPagoSeleccionado", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(vm, new PlazosPago { plazoPago = "CONTADO" });
            vm.Estado.PlazosPago = "CONTADO";
            vm.Estado.Ruta = ruta;
            vm.formaVentaSeleccionada = 1;
            vm.almacenSeleccionado = new Sede { Codigo = almacen, Nombre = almacen };
            vm.ListaFiltrableProductos.ListaOriginal.Add(new LineaPlantillaVenta { producto = "45383", texto = "TRIPLE GLOW", cantidad = 1, precio = 219.5M, grupo = "COS", iva = "G21" });
            return vm;
        }

        private static void ConPortesCalculados(PlantillaVentaViewModel vm)
            => typeof(PlantillaVentaViewModel).GetField("_resultadoPortes", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(vm, new ResultadoPortesDTO { ImporteMinimoPedidoSinPortes = 1000, ImportePortes = 6.5M, ComisionReembolso = 3M, EsContraReembolso = true });

        [TestMethod]
        public void Algete_SeOfreceDesmarcadaYSePuedeCambiar()
        {
            var vm = CrearViewModel();

            Assert.IsTrue(vm.RecogeEnTiendaVisible);
            Assert.IsFalse(vm.RecogeEnTiendaFijo);
            Assert.IsTrue(vm.PuedeCambiarRecogeEnTienda);
            Assert.IsFalse(vm.RecogeEnTienda);
        }

        [TestMethod]
        public async Task Algete_SinTocarla_ElPostNoLaLleva()
        {
            var vm = CrearViewModel();

            await vm.CrearPedidoAsync();

            Assert.AreEqual(1, _creados.Count);
            Assert.IsNull(_creados[0].RecogeEnTienda);
            Assert.IsFalse(JsonConvert.SerializeObject(_creados[0]).Contains("RecogeEnTienda"));
        }

        [TestMethod]
        public async Task Algete_Marcada_ElPostLlevaRecogeEnTiendaTrue()
        {
            var vm = CrearViewModel();

            vm.RecogeEnTienda = true;
            await vm.CrearPedidoAsync();

            Assert.AreEqual(1, _creados.Count);
            Assert.AreEqual(true, _creados[0].RecogeEnTienda);
            StringAssert.Contains(JsonConvert.SerializeObject(_creados[0]), "\"RecogeEnTienda\":true");
        }

        [TestMethod]
        public void Algete_Marcada_ElResumenNoEnsenaPortesNiComision()
        {
            var vm = CrearViewModel();
            ConPortesCalculados(vm);
            Assert.AreEqual(3M, vm.ImporteReembolsoMostrar);

            vm.RecogeEnTienda = true;

            Assert.AreEqual(RecogeEnTiendaReglas.TEXTO_PORTES, vm.TextoPortes);
            Assert.IsTrue(vm.PortesGratis);
            Assert.AreEqual(0M, vm.ImportePortesMostrar);
            Assert.AreEqual(0M, vm.ImporteReembolsoMostrar);
        }

        [TestMethod]
        public void Algete_ConRutaDeRecogidaEnLaFicha_SaleMarcadaSinTocarla()
        {
            var vm = CrearViewModel(ruta: "AM");

            Assert.IsTrue(vm.RecogeEnTienda);
            Assert.IsNull(vm.RecogeEnTiendaParaGuardar, "Sin tocarla manda la ruta");
        }

        [TestMethod]
        public void Algete_MarcarYDesmarcar_MandaFalse()
        {
            var vm = CrearViewModel();

            vm.RecogeEnTienda = true;
            vm.RecogeEnTienda = false;

            Assert.AreEqual(false, vm.RecogeEnTiendaParaGuardar);
        }

        [TestMethod]
        public void AlcobendasYReina_MarcadaYFijaYNoSeManda()
        {
            foreach (string almacen in new[] { "ALC", "REI" })
            {
                var vm = CrearViewModel(almacen);

                Assert.IsTrue(vm.RecogeEnTiendaVisible, almacen);
                Assert.IsTrue(vm.RecogeEnTiendaFijo, almacen);
                Assert.IsFalse(vm.PuedeCambiarRecogeEnTienda, almacen);
                Assert.IsTrue(vm.RecogeEnTienda, almacen);
                vm.RecogeEnTienda = false;
                Assert.IsTrue(vm.RecogeEnTienda, almacen);
                Assert.IsNull(vm.RecogeEnTiendaParaGuardar, almacen);
            }
        }

        [TestMethod]
        public void MarcadaEnAlgeteYCambiaAAlcobendas_NoSeManda()
        {
            var vm = CrearViewModel();
            vm.RecogeEnTienda = true;

            vm.almacenSeleccionado = new Sede { Codigo = "ALC", Nombre = "Alcobendas" };

            Assert.IsTrue(vm.RecogeEnTienda);
            Assert.IsNull(vm.RecogeEnTiendaParaGuardar);
        }

        [TestMethod]
        public void OtroAlmacen_NoSeOfrece()
        {
            var vm = CrearViewModel("AMZ");

            Assert.IsFalse(vm.RecogeEnTiendaVisible);
            Assert.IsFalse(vm.RecogeEnTienda);
            vm.RecogeEnTienda = true;
            Assert.IsNull(vm.RecogeEnTiendaParaGuardar);
        }

        [TestMethod]
        public void Borrador_GuardaYRestauraLaCasillaTocada()
        {
            var vm = CrearViewModel();
            vm.RecogeEnTienda = true;
            MethodInfo crear = typeof(PlantillaVentaViewModel).GetMethod("CrearBorradorDesdeEstadoActual", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(crear, "El método que crea el borrador");
            var borrador = (BorradorPlantillaVenta)crear.Invoke(vm, new object[] { null });

            Assert.AreEqual(true, borrador.RecogeEnTienda);
        }
    }
}
