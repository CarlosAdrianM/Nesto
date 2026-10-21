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

namespace PlantillaVentaTests
{
    /// <summary>
    /// NestoAPI#688 (corte 3): en la plantilla, la respuesta de OpcionesModos oculta el combo «Facturación» con
    /// «Todo junto» y enseña la casilla «Servir lo pendiente en cuanto haya stock» solo con «Todo ahora», marcada por
    /// defecto. La casilla vive en el ViewModel y llega al Estado en SincronizarListasAlEstado (lección de Nesto#449),
    /// así que viaja en el pedido; lo que el usuario toca no lo pisa la respuesta siguiente.
    /// </summary>
    [TestClass]
    public class ServirPendienteAlHaberStockPlantillaTests
    {
        private static PlantillaVentaViewModel CrearViewModel()
        {
            IConfiguracion configuracion = A.Fake<IConfiguracion>();
            A.CallTo(() => configuracion.LeerParametroSync(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.AlmacenRuta)).Returns("ALG");
            var vm = new PlantillaVentaViewModel(A.Fake<IAbridorPedidos>(), A.Fake<IServicioNavegacion>(), configuracion,
                A.Fake<IPlantillaVentaService>(), new WeakReferenceMessenger(), A.Fake<IServicioDialogos>(),
                A.Fake<IPedidoVentaService>(), A.Fake<IBorradorPlantillaVentaService>(), A.Fake<IServicioAutenticacion>());
            vm.ListaFiltrableProductos.ListaOriginal = new ObservableCollection<IFiltrableItem>();
            vm.direccionEntregaSeleccionada = new DireccionesEntregaCliente { contacto = "0", mantenerJunto = false };
            return vm;
        }

        private static OpcionesModosDTO Opciones(byte modoFacturacion, bool facturacionVisible = true, bool casillaVisible = false, bool? valor = null,
            ModoServicioSugeridoDTO servicio = null) => new OpcionesModosDTO
        {
            Servicio = servicio,
            Facturacion = new OpcionesFacturacionDTO
            {
                Modo = modoFacturacion,
                Visible = facturacionVisible,
                ModosPermitidos = new List<byte> { 1, 2, 3 }
            },
            ServirPendienteAlHaberStock = new OpcionServirPendienteDTO { Visible = casillaVisible, Valor = valor }
        };

        private static PedidoVentaDTO PedidoQueSaldria(PlantillaVentaViewModel vm)
        {
            vm.SincronizarListasAlEstado();
            return vm.Estado.ToPedidoVentaDTO("DIR", () => 1, () => "NV");
        }

        [TestMethod]
        public void ConTodoJunto_LaFacturacionNoSeEnsena_YSinRespuestaSi()
        {
            var vm = CrearViewModel();
            Assert.IsTrue(vm.FacturacionVisible);

            vm.AplicarOpcionesModos(Opciones(ModosFacturacion.AL_COMPLETAR, facturacionVisible: false), conLineas: true);
            Assert.IsFalse(vm.FacturacionVisible);

            vm.AplicarOpcionesModos(null, conLineas: true);
            Assert.IsTrue(vm.FacturacionVisible, "Si la API falla, el combo se ve (como antes)");
            Assert.IsFalse(vm.ServirPendienteAlHaberStock.Visible);
        }

        [TestMethod]
        public void ApiAnterior_FacturacionVisibleYSinCasilla_YNoViajaNada()
        {
            var vm = CrearViewModel();
            vm.ModoFacturacion = ModosFacturacion.TODO_AHORA_Y_LO_PENDIENTE_DESPUES;

            vm.AplicarOpcionesModos(OpcionesModosDTO.DesdeApiAnterior(null,
                new ModoFacturacionSugeridoDTO { Modo = 3, ModosPermitidos = new List<byte> { 1, 2, 3 } }), conLineas: true);

            Assert.IsTrue(vm.FacturacionVisible);
            Assert.IsFalse(vm.ServirPendienteAlHaberStock.Visible);
            Assert.IsNull(PedidoQueSaldria(vm).servirPendienteAlHaberStock);
        }

        [TestMethod]
        public void ConTodoAhora_LaCasillaSaleMarcada_YViajaEnElPedido()
        {
            var vm = CrearViewModel();
            vm.ModoFacturacion = ModosFacturacion.TODO_AHORA_Y_LO_PENDIENTE_DESPUES;

            vm.AplicarOpcionesModos(Opciones(ModosFacturacion.TODO_AHORA_Y_LO_PENDIENTE_DESPUES, casillaVisible: true, valor: true), conLineas: true);

            Assert.IsTrue(vm.ServirPendienteAlHaberStock.Visible);
            Assert.IsTrue(vm.ServirPendienteAlHaberStock.Valor);
            Assert.AreEqual(true, PedidoQueSaldria(vm).servirPendienteAlHaberStock);
        }

        [TestMethod]
        public void SiLaApiNoDiceValor_MarcadaPorDefecto()
        {
            var vm = CrearViewModel();
            vm.ServirPendienteAlHaberStock.Valor = false;
            vm.ModoFacturacion = ModosFacturacion.TODO_AHORA_Y_LO_PENDIENTE_DESPUES; // al elegir «Todo ahora», marcada (decisión 12)

            vm.AplicarOpcionesModos(Opciones(ModosFacturacion.TODO_AHORA_Y_LO_PENDIENTE_DESPUES, casillaVisible: true, valor: null), conLineas: true);

            Assert.IsTrue(vm.ServirPendienteAlHaberStock.Valor);
        }

        [TestMethod]
        public void LoQueTocaElUsuario_NoLoPisaLaRespuestaSiguiente_YEsLoQueViaja()
        {
            var vm = CrearViewModel();
            vm.ModoFacturacion = ModosFacturacion.TODO_AHORA_Y_LO_PENDIENTE_DESPUES;
            vm.AplicarOpcionesModos(Opciones(ModosFacturacion.TODO_AHORA_Y_LO_PENDIENTE_DESPUES, casillaVisible: true, valor: true), conLineas: true);

            vm.ServirPendienteAlHaberStock.Valor = false; // poco a poco: no le caben los 200 rollos
            vm.AplicarOpcionesModos(Opciones(ModosFacturacion.TODO_AHORA_Y_LO_PENDIENTE_DESPUES, casillaVisible: true, valor: true), conLineas: true);

            Assert.IsFalse(vm.ServirPendienteAlHaberStock.Valor);
            Assert.AreEqual(false, PedidoQueSaldria(vm).servirPendienteAlHaberStock);
        }

        [TestMethod]
        public void VolverAElegirTodoAhora_VuelveMarcada()
        {
            var vm = CrearViewModel();
            vm.ModoFacturacion = ModosFacturacion.TODO_AHORA_Y_LO_PENDIENTE_DESPUES;
            vm.AplicarOpcionesModos(Opciones(ModosFacturacion.TODO_AHORA_Y_LO_PENDIENTE_DESPUES, casillaVisible: true, valor: true), conLineas: true);
            vm.ServirPendienteAlHaberStock.Valor = false;

            vm.ModoFacturacion = ModosFacturacion.POR_ENTREGAS;
            vm.AplicarOpcionesModos(Opciones(ModosFacturacion.POR_ENTREGAS), conLineas: true);
            Assert.IsFalse(vm.ServirPendienteAlHaberStock.Visible);
            Assert.IsNull(PedidoQueSaldria(vm).servirPendienteAlHaberStock, "Sin «Todo ahora» no se manda");

            vm.ModoFacturacion = ModosFacturacion.TODO_AHORA_Y_LO_PENDIENTE_DESPUES;
            vm.AplicarOpcionesModos(Opciones(ModosFacturacion.TODO_AHORA_Y_LO_PENDIENTE_DESPUES, casillaVisible: true, valor: true), conLineas: true);
            Assert.IsTrue(vm.ServirPendienteAlHaberStock.Valor);
        }

        [TestMethod]
        public void SinLineas_ElModoDeServicioDeLaRespuestaNoSeAplica()
        {
            var vm = CrearViewModel();
            byte antes = vm.ModoServicio;
            byte otro = antes == ModosServicio.TODO_JUNTO ? ModosServicio.SEGUN_VAYA_ENTRANDO : ModosServicio.TODO_JUNTO;
            var servicio = new ModoServicioSugeridoDTO { Modo = otro, ModosPermitidos = new List<byte> { otro } };

            vm.AplicarOpcionesModos(Opciones(ModosFacturacion.POR_ENTREGAS, servicio: servicio), conLineas: false);
            Assert.AreEqual(antes, vm.ModoServicio, "Sin líneas no hay stock que mirar");

            vm.AplicarOpcionesModos(Opciones(ModosFacturacion.POR_ENTREGAS, servicio: servicio), conLineas: true);
            Assert.AreEqual(otro, vm.ModoServicio);
        }

        [TestMethod]
        public void CambiarDeDireccion_LaCasillaSeReinicia()
        {
            var vm = CrearViewModel();
            vm.ModoFacturacion = ModosFacturacion.TODO_AHORA_Y_LO_PENDIENTE_DESPUES;
            vm.AplicarOpcionesModos(Opciones(ModosFacturacion.TODO_AHORA_Y_LO_PENDIENTE_DESPUES, casillaVisible: true, valor: true), conLineas: true);
            vm.ServirPendienteAlHaberStock.Valor = false;

            vm.direccionEntregaSeleccionada = new DireccionesEntregaCliente { contacto = "1", mantenerJunto = false };

            Assert.IsFalse(vm.ServirPendienteAlHaberStock.Visible, "Hasta que conteste la API");
            Assert.IsFalse(vm.ServirPendienteAlHaberStock.Tocada);
            Assert.IsTrue(vm.ServirPendienteAlHaberStock.Valor);
        }
    }
}
