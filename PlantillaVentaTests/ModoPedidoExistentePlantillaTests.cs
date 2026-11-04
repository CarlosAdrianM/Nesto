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
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Threading.Tasks;

namespace PlantillaVentaTests
{
    /// <summary>
    /// Nesto#613 (Reina, pedido 929678 con picking, 04/11/26): ampliando o modificando un pedido con la plantilla se
    /// mandaba el modo de entrega de la plantilla (el por defecto o el sugerido), no el del pedido; la API lo rechazaba
    /// por el picking y la ampliación, con el cheque, se perdía. Ahora viaja el modo del pedido salvo que el usuario lo
    /// cambie a propósito y, si aun así se rechaza, tras la pregunta de almacén se vuelve a grabar con el modo del pedido.
    /// </summary>
    [TestClass]
    public class ModoPedidoExistentePlantillaTests
    {
        private const int PEDIDO = 929678;

        private IPedidoVentaService _pedidosVenta;
        private IPlantillaVentaService _servicio;
        private IServicioDialogos _dialogos;

        [TestInitialize]
        public void Setup()
        {
            _pedidosVenta = A.Fake<IPedidoVentaService>();
            _servicio = A.Fake<IPlantillaVentaService>();
            _dialogos = A.Fake<IServicioDialogos>();
            A.CallTo(() => _dialogos.ShowConfirmationAsync(A<string>._, A<string>._)).Returns(Task.FromResult(false));
        }

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
            return vm;
        }

        private void PedidoGrabadoCon(byte? modo, bool servirJunto)
            => A.CallTo(() => _pedidosVenta.cargarPedido("1", PEDIDO)).Returns(Task.FromResult(
                new PedidoVentaDTO { empresa = "1", numero = PEDIDO, cliente = "9238", modoServicio = modo, servirJunto = servirJunto, Lineas = new List<LineaPedidoVentaDTO>() }));

        [TestMethod]
        public async Task AmpliarUnPedidoConPicking_ElSelectorPasaASuModo_YLaAmpliacionLoMandaTalCual()
        {
            PedidoGrabadoCon(ModosServicio.AHORA_LO_QUE_HAY_Y_EL_RESTO_DE_UNA_VEZ, false);
            var vm = CrearViewModel();
            Assert.AreEqual(ModosServicio.POR_DEFECTO, vm.ModoServicio, "la plantilla arranca en el modo por defecto");

            vm.PedidoPendienteSeleccionado = PEDIDO;
            await vm.CargarModoDelPedidoAmpliadoAsync(PEDIDO);

            Assert.AreEqual(ModosServicio.AHORA_LO_QUE_HAY_Y_EL_RESTO_DE_UNA_VEZ, vm.ModoServicio);
            Assert.AreEqual(ModosServicio.AHORA_LO_QUE_HAY_Y_EL_RESTO_DE_UNA_VEZ, vm.ModoDelPedidoBase);
            Assert.IsFalse(vm.ModoCambiadoAPropositoEnPedidoBase);
            Assert.IsTrue(vm.HayAvisoModoServicio, "Nesto#525: el cambio de modo no es en silencio");

            var ampliacion = new PedidoVentaDTO { modoServicio = ModosServicio.TRAS_REPONER_DE_TIENDAS, servirJunto = false };
            vm.MandarModoDelPedidoExistente(ampliacion, ModosServicio.AHORA_LO_QUE_HAY_Y_EL_RESTO_DE_UNA_VEZ, false);
            Assert.AreEqual(ModosServicio.AHORA_LO_QUE_HAY_Y_EL_RESTO_DE_UNA_VEZ, ampliacion.modoServicio);
            Assert.IsFalse(ampliacion.servirJunto);
        }

        [TestMethod]
        public async Task AmpliandoUnPedido_LaSugerenciaDelServidorNoLeCambiaElModo()
        {
            PedidoGrabadoCon(ModosServicio.AHORA_LO_QUE_HAY_Y_EL_RESTO_DE_UNA_VEZ, false);
            var vm = CrearViewModel();
            vm.PedidoPendienteSeleccionado = PEDIDO;
            await vm.CargarModoDelPedidoAmpliadoAsync(PEDIDO);

            vm.AplicarSugerenciaModoServicio(new ModoServicioSugeridoDTO { Modo = ModosServicio.SEGUN_VAYA_ENTRANDO });

            Assert.AreEqual(ModosServicio.AHORA_LO_QUE_HAY_Y_EL_RESTO_DE_UNA_VEZ, vm.ModoServicio);
        }

        [TestMethod]
        public async Task AmpliandoUnPedido_SiElUsuarioCambiaElModoAProposito_SeMandaElSuyo()
        {
            PedidoGrabadoCon(ModosServicio.AHORA_LO_QUE_HAY_Y_EL_RESTO_DE_UNA_VEZ, false);
            var vm = CrearViewModel();
            vm.PedidoPendienteSeleccionado = PEDIDO;
            await vm.CargarModoDelPedidoAmpliadoAsync(PEDIDO);

            vm.ModoServicio = ModosServicio.SEGUN_VAYA_ENTRANDO; // elegido en el selector

            Assert.IsTrue(vm.ModoCambiadoAPropositoEnPedidoBase);
            var ampliacion = new PedidoVentaDTO { modoServicio = ModosServicio.SEGUN_VAYA_ENTRANDO };
            vm.MandarModoDelPedidoExistente(ampliacion, ModosServicio.AHORA_LO_QUE_HAY_Y_EL_RESTO_DE_UNA_VEZ, false);
            Assert.AreEqual(ModosServicio.SEGUN_VAYA_ENTRANDO, ampliacion.modoServicio);

            vm.ModoServicio = ModosServicio.AHORA_LO_QUE_HAY_Y_EL_RESTO_DE_UNA_VEZ; // vuelve al del pedido
            Assert.IsFalse(vm.ModoCambiadoAPropositoEnPedidoBase);
        }

        [TestMethod]
        public async Task PedidoNuevo_NoHayModoDelPedido()
        {
            PedidoGrabadoCon(ModosServicio.AHORA_LO_QUE_HAY_Y_EL_RESTO_DE_UNA_VEZ, false);
            var vm = CrearViewModel();
            vm.PedidoPendienteSeleccionado = PEDIDO;
            await vm.CargarModoDelPedidoAmpliadoAsync(PEDIDO);

            vm.PedidoPendienteSeleccionado = 0;
            await vm.CargarModoDelPedidoAmpliadoAsync(0);

            Assert.IsNull(vm.ModoDelPedidoBase);
        }

        [TestMethod]
        public async Task ModificandoConPicking_SiLaApiRechazaElModo_SeVuelveAGrabarConElModoDelPedido()
        {
            var vm = CrearViewModel();
            vm.FormaPagoSeleccionada = new FormaPago { formaPago = "EFC" };
            typeof(PlantillaVentaViewModel).GetField("_plazoPagoSeleccionado", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(vm, new PlazosPago { plazoPago = "CONTADO" });
            vm.formaVentaSeleccionada = 1;
            vm.almacenSeleccionado = new Sede { Codigo = "ALG", Nombre = "Algete" };
            vm.ListaFiltrableProductos.ListaOriginal.Add(new LineaPlantillaVenta { producto = "12345", texto = "CREMA", cantidad = 10, precio = 30M, grupo = "COS", iva = "G21" });
            vm.NumeroPedidoEnEdicion = PEDIDO;
            vm.FijarModoDelPedidoBase(ModosServicio.AHORA_LO_QUE_HAY_Y_EL_RESTO_DE_UNA_VEZ);
            vm.ModoServicio = ModosServicio.SEGUN_VAYA_ENTRANDO; // el usuario lo cambia a propósito
            A.CallTo(() => _servicio.CargarPedidoParaPlantilla("1", PEDIDO)).Returns(Task.FromResult(new PedidoParaPlantillaModel
            {
                ModoServicio = ModosServicio.AHORA_LO_QUE_HAY_Y_EL_RESTO_DE_UNA_VEZ,
                ServirJunto = false,
                Lineas = new List<LineaParaPlantillaModel>()
            }));
            var modosMandados = new List<byte?>();
            A.CallTo(() => _pedidosVenta.modificarPedido(A<PedidoVentaDTO>._)).ReturnsLazily((PedidoVentaDTO p) =>
            {
                modosMandados.Add(p.modoServicio);
                if (modosMandados.Count == 1)
                {
                    throw new ModoConPickingException("Este pedido ya está en preparación (tiene picking).");
                }
                return Task.FromResult(new List<AvisoPedidoModel>());
            });

            await vm.CrearPedidoAsync();

            CollectionAssert.AreEqual(new List<byte?> { ModosServicio.SEGUN_VAYA_ENTRANDO, ModosServicio.AHORA_LO_QUE_HAY_Y_EL_RESTO_DE_UNA_VEZ }, modosMandados,
                "primero el que eligió el usuario; tras el rechazo, el del pedido, para que las líneas no se pierdan");
            A.CallTo(() => _dialogos.ShowConfirmationAsync(SolicitudCambioModoAlmacen.TITULO, A<string>._)).MustHaveHappenedOnceExactly();
            Assert.IsNull(vm.NumeroPedidoEnEdicion, "grabado");
        }

        [TestMethod]
        public async Task ModificandoSinCambiarElModo_SeMandaElDelPedidoAunqueLaPlantillaTengaOtro()
        {
            var vm = CrearViewModel();
            vm.FormaPagoSeleccionada = new FormaPago { formaPago = "EFC" };
            typeof(PlantillaVentaViewModel).GetField("_plazoPagoSeleccionado", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(vm, new PlazosPago { plazoPago = "CONTADO" });
            vm.formaVentaSeleccionada = 1;
            vm.almacenSeleccionado = new Sede { Codigo = "ALG", Nombre = "Algete" };
            vm.ListaFiltrableProductos.ListaOriginal.Add(new LineaPlantillaVenta { producto = "12345", texto = "CREMA", cantidad = 10, precio = 30M, grupo = "COS", iva = "G21" });
            vm.NumeroPedidoEnEdicion = PEDIDO;
            A.CallTo(() => _servicio.CargarPedidoParaPlantilla("1", PEDIDO)).Returns(Task.FromResult(new PedidoParaPlantillaModel
            {
                ModoServicio = ModosServicio.AHORA_LO_QUE_HAY_Y_EL_RESTO_DE_UNA_VEZ,
                ServirJunto = false,
                Lineas = new List<LineaParaPlantillaModel>()
            }));
            var modosMandados = new List<byte?>();
            A.CallTo(() => _pedidosVenta.modificarPedido(A<PedidoVentaDTO>._)).ReturnsLazily((PedidoVentaDTO p) =>
            {
                modosMandados.Add(p.modoServicio);
                return Task.FromResult(new List<AvisoPedidoModel>());
            });

            await vm.CrearPedidoAsync(); // la plantilla está en el modo por defecto (3), el pedido en el 4

            CollectionAssert.AreEqual(new List<byte?> { ModosServicio.AHORA_LO_QUE_HAY_Y_EL_RESTO_DE_UNA_VEZ }, modosMandados);
        }
    }
}
