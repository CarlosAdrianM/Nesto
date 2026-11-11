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
using System.Reflection;
using System.Threading.Tasks;

namespace PlantillaVentaTests
{
    /// <summary>
    /// Feria online 12-16/11/26: al terminar la plantilla (crear pedido), si la API dice que lleva productos de una campaña al
    /// contado y no va al contado, se pregunta. Sí → plazos CONTADO; No → igual. Si la API falla, se crea igual sin preguntar.
    /// </summary>
    [TestClass]
    public class AvisoCondicionesCampanaPlantillaTests
    {
        private const string MENSAJE = "Este pedido lleva productos de la feria online, con condiciones de la feria online, y debería ser al contado para poder llevar esos precios. ¿Cambiar a contado?";

        private IPedidoVentaService _pedidosVenta;
        private IPlantillaVentaService _servicio;
        private IServicioDialogos _dialogos;
        private List<PedidoVentaDTO> _creados;

        [TestInitialize]
        public void Setup()
        {
            _pedidosVenta = A.Fake<IPedidoVentaService>();
            _servicio = A.Fake<IPlantillaVentaService>();
            _dialogos = A.Fake<IServicioDialogos>();
            _creados = new List<PedidoVentaDTO>();
            A.CallTo(() => _servicio.CrearPedido(A<PedidoVentaDTO>._)).ReturnsLazily((PedidoVentaDTO p) =>
            {
                _creados.Add(p);
                return Task.FromResult(new RespuestaCrearPedidoPlantilla { Numero = "930001" });
            });
        }

        private void Responde(bool acepta)
            => A.CallTo(() => _dialogos.ShowConfirmationAsync(A<string>._, A<string>._)).Returns(Task.FromResult(acepta));

        private void LaApiAvisa()
            => A.CallTo(() => _servicio.ConsultarAvisoCondicionesCampana(A<PedidoVentaDTO>._)).Returns(Task.FromResult(new AvisoCondicionesCampanaRespuesta
            {
                Avisar = true,
                Mensaje = MENSAJE,
                PlazosPagoPropuestos = "CONTADO",
                Campana = "FERIA-ONLINE-1126",
                Productos = new List<string> { "45383" }
            }));

        private PlantillaVentaViewModel CrearViewModel()
        {
            IConfiguracion configuracion = A.Fake<IConfiguracion>();
            A.CallTo(() => configuracion.servidorAPI).Returns("http://127.0.0.1:9/api/");
            A.CallTo(() => configuracion.LeerParametroSync(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.AlmacenRuta)).Returns("ALG");
            var vm = new PlantillaVentaViewModel(A.Fake<IAbridorPedidos>(), A.Fake<IServicioNavegacion>(), configuracion, _servicio,
                new WeakReferenceMessenger(), _dialogos, _pedidosVenta, A.Fake<IBorradorPlantillaVentaService>(),
                A.Fake<IServicioAutenticacion>());
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

        [TestMethod]
        public async Task Crear_LaApiAvisaYDiceQueSi_SeCreaAlContadoSinTocarLaFormaDePago()
        {
            LaApiAvisa();
            Responde(true);
            var vm = CrearViewModel();

            await vm.CrearPedidoAsync();

            A.CallTo(() => _dialogos.ShowConfirmationAsync(AvisoCondicionesCampana.TITULO, MENSAJE)).MustHaveHappenedOnceExactly();
            Assert.AreEqual(1, _creados.Count);
            Assert.AreEqual("CONTADO", _creados[0].plazosPago);
            Assert.AreEqual("RCB", _creados[0].formaPago);
        }

        [TestMethod]
        public async Task Crear_LaApiAvisaYDiceQueNo_SeCreaComoEstaba()
        {
            LaApiAvisa();
            Responde(false);
            var vm = CrearViewModel();

            await vm.CrearPedidoAsync();

            A.CallTo(() => _dialogos.ShowConfirmationAsync(AvisoCondicionesCampana.TITULO, MENSAJE)).MustHaveHappenedOnceExactly();
            Assert.AreEqual(1, _creados.Count);
            Assert.AreEqual("1/30", _creados[0].plazosPago);
        }

        [TestMethod]
        public async Task Crear_LaApiNoAvisa_NoPregunta()
        {
            A.CallTo(() => _servicio.ConsultarAvisoCondicionesCampana(A<PedidoVentaDTO>._))
                .Returns(Task.FromResult(new AvisoCondicionesCampanaRespuesta { Avisar = false }));
            Responde(true);
            var vm = CrearViewModel();

            await vm.CrearPedidoAsync();

            A.CallTo(() => _dialogos.ShowConfirmationAsync(AvisoCondicionesCampana.TITULO, A<string>._)).MustNotHaveHappened();
            Assert.AreEqual(1, _creados.Count);
            Assert.AreEqual("1/30", _creados[0].plazosPago);
        }

        [TestMethod]
        public async Task Crear_LaApiFalla_SeCreaIgualSinPreguntar()
        {
            A.CallTo(() => _servicio.ConsultarAvisoCondicionesCampana(A<PedidoVentaDTO>._)).ThrowsAsync(new InvalidOperationException("404"));
            Responde(true);
            var vm = CrearViewModel();

            await vm.CrearPedidoAsync();

            A.CallTo(() => _dialogos.ShowConfirmationAsync(AvisoCondicionesCampana.TITULO, A<string>._)).MustNotHaveHappened();
            Assert.AreEqual(1, _creados.Count);
        }

        [TestMethod]
        public async Task Modificar_NoPregunta()
        {
            LaApiAvisa();
            Responde(true);
            var vm = CrearViewModel();
            vm.NumeroPedidoEnEdicion = 929678;
            A.CallTo(() => _servicio.CargarPedidoParaPlantilla("1", 929678)).Returns(Task.FromResult(new PedidoParaPlantillaModel
            {
                Lineas = new List<LineaParaPlantillaModel>()
            }));
            A.CallTo(() => _pedidosVenta.modificarPedido(A<PedidoVentaDTO>._)).Returns(Task.FromResult(new List<AvisoPedidoModel>()));

            await vm.CrearPedidoAsync();

            A.CallTo(() => _servicio.ConsultarAvisoCondicionesCampana(A<PedidoVentaDTO>._)).MustNotHaveHappened();
        }

        // ---------- El núcleo común (Nesto.Models) ----------

        [TestMethod]
        public void Peticion_SoloProductosSinRepetir_YConPrepago()
        {
            var pedido = new PedidoVentaDTO
            {
                empresa = "1  ",
                formaPago = "RCB",
                plazosPago = "1/30 ",
                Lineas = new List<LineaPedidoVentaDTO>
                {
                    new LineaPedidoVentaDTO { Producto = "45383 ", tipoLinea = 1 },
                    new LineaPedidoVentaDTO { Producto = "45383", tipoLinea = 1 },
                    new LineaPedidoVentaDTO { Producto = "62400003", tipoLinea = 2 },
                    new LineaPedidoVentaDTO { Producto = "45670", tipoLinea = 1 }
                }
            };
            pedido.Prepagos.Add(new PrepagoDTO());

            string json = Newtonsoft.Json.JsonConvert.SerializeObject(AvisoCondicionesCampana.Peticion(pedido));

            StringAssert.Contains(json, "\"Empresa\":\"1\"");
            StringAssert.Contains(json, "\"Productos\":[\"45383\",\"45670\"]");
            StringAssert.Contains(json, "\"PlazosPago\":\"1/30\"");
            StringAssert.Contains(json, "\"TienePrepago\":true");
            StringAssert.Contains(json, "\"DescuentoPP\":0.0");
        }

        [TestMethod]
        public void Peticion_LlevaElDescuentoPPDelPedido()
        {
            // Carlos 11/11/26: con pronto pago no se avisa; la API decide con este campo (y con los plazos con PP de su tabla).
            var pedido = new PedidoVentaDTO { empresa = "1", formaPago = "RCB", plazosPago = "1/30", DescuentoPP = 0.03M };

            string json = Newtonsoft.Json.JsonConvert.SerializeObject(AvisoCondicionesCampana.Peticion(pedido));

            StringAssert.Contains(json, "\"DescuentoPP\":0.03");
        }
    }
}
