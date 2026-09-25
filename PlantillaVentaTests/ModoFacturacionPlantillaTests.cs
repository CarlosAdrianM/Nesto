using ControlesUsuario.Models;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Models;
using Nesto.Modulos.PedidoVenta;
using Nesto.Modulos.PlantillaVenta;
using CommunityToolkit.Mvvm.Messaging;
using Prism.Regions;
using Prism.Services.Dialogs;
using System.Collections.ObjectModel;
using System.Linq;
using Unity;

namespace PlantillaVentaTests
{
    /// <summary>
    /// Nesto#493 / NestoAPI#542: el combo «Facturación» de la plantilla sustituye a la casilla «Mantener junto»,
    /// que colgaba de la dirección de entrega (como pasaba con Servir junto en Nesto#449). Aquí lo que es del
    /// cliente: el modo vive en Estado, viaja en el DTO con mantenerJunto coherente, se restaura del borrador, se
    /// preselecciona con lo que sugiere la API sin pisar al usuario, y avisa cuando lo elegido deja de valer.
    /// </summary>
    [TestClass]
    public class ModoFacturacionPlantillaTests
    {
        private static PlantillaVentaViewModel CrearViewModel()
        {
            IUnityContainer container = A.Fake<IUnityContainer>();
            IRegionManager regionManager = A.Fake<IRegionManager>();
            IConfiguracion configuracion = A.Fake<IConfiguracion>();
            IPlantillaVentaService servicio = A.Fake<IPlantillaVentaService>();
            IMessenger messenger = new WeakReferenceMessenger();
            IDialogService dialogService = A.Fake<IDialogService>();
            IPedidoVentaService pedidoVentaService = A.Fake<IPedidoVentaService>();
            IBorradorPlantillaVentaService servicioBorradores = A.Fake<IBorradorPlantillaVentaService>();
            A.CallTo(() => configuracion.LeerParametroSync(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.AlmacenRuta)).Returns("ALG");

            var vm = new PlantillaVentaViewModel(container, regionManager, configuracion, servicio,
                messenger, dialogService, pedidoVentaService, servicioBorradores,
                A.Fake<IServicioAutenticacion>());
            vm.ListaFiltrableProductos.ListaOriginal = new ObservableCollection<IFiltrableItem>();
            return vm;
        }

        private static ModoFacturacionSugeridoDTO Sugerencia(byte modo, params byte[] permitidos) => new ModoFacturacionSugeridoDTO
        {
            Modo = modo,
            Nombre = ModosFacturacion.Nombre(modo),
            ModosPermitidos = permitidos.ToList(),
            Modos = ModosFacturacion.Lista.Select(m => new ModoFacturacionPermitidoDTO
            {
                Modo = m.Codigo,
                Nombre = m.Nombre,
                Permitido = permitidos.Contains(m.Codigo),
                Motivo = permitidos.Contains(m.Codigo) ? null : "Los plazos de pago no son los de la ficha del cliente."
            }).ToList()
        };

        [TestMethod]
        public void ModoFacturacion_SinTocar_ArrancaEnElQueDerivaDelMantenerJuntoDeLaFicha()
        {
            var vm = CrearViewModel();

            vm.direccionEntregaSeleccionada = new DireccionesEntregaCliente { mantenerJunto = true };
            Assert.AreEqual(ModosFacturacion.AL_COMPLETAR, vm.ModoFacturacion);
            Assert.IsNull(vm.Estado.ModoFacturacion, "Sin elección ni sugerencia no se manda modo: la API lo deriva de mantenerJunto, como siempre");
            Assert.IsTrue(vm.Estado.MantenerJunto);

            vm.direccionEntregaSeleccionada = new DireccionesEntregaCliente { mantenerJunto = false };
            Assert.AreEqual(ModosFacturacion.POR_ENTREGAS, vm.ModoFacturacion);
            Assert.IsFalse(vm.Estado.MantenerJunto);
        }

        [TestMethod]
        public void ModoFacturacion_ElegirTodoAhora_ViajaEnElDTOConMantenerJuntoFalse()
        {
            var vm = CrearViewModel();
            vm.direccionEntregaSeleccionada = new DireccionesEntregaCliente { mantenerJunto = true };

            vm.ModoFacturacion = ModosFacturacion.TODO_AHORA_Y_LO_PENDIENTE_DESPUES;
            var pedido = vm.Estado.ToPedidoVentaDTO("DIR", () => 1, () => "NV");

            Assert.AreEqual((byte?)3, pedido.modoFacturacion);
            Assert.IsFalse(pedido.mantenerJunto);
            Assert.IsTrue(vm.EsModoFacturacionTodoAhora);
            StringAssert.Contains(vm.AvisoPortesTodoAhora, "portes");
        }

        [TestMethod]
        public void ModoFacturacion_ElegirAlCompletar_MarcaMantenerJuntoEnElDTO()
        {
            var vm = CrearViewModel();
            vm.direccionEntregaSeleccionada = new DireccionesEntregaCliente { mantenerJunto = false };

            vm.ModoFacturacion = ModosFacturacion.AL_COMPLETAR;
            var pedido = vm.Estado.ToPedidoVentaDTO("DIR", () => 1, () => "NV");

            Assert.AreEqual((byte?)2, pedido.modoFacturacion);
            Assert.IsTrue(pedido.mantenerJunto);
            Assert.IsFalse(vm.EsModoFacturacionTodoAhora);
        }

        [TestMethod]
        public void Sugerencia_SinEleccionDelUsuario_PreseleccionaElSugeridoSinAviso()
        {
            var vm = CrearViewModel();
            vm.direccionEntregaSeleccionada = new DireccionesEntregaCliente { mantenerJunto = false };

            vm.AplicarSugerenciaModoFacturacion(Sugerencia(ModosFacturacion.AL_COMPLETAR, ModosFacturacion.AL_COMPLETAR, ModosFacturacion.TODO_AHORA_Y_LO_PENDIENTE_DESPUES));

            Assert.AreEqual(ModosFacturacion.AL_COMPLETAR, vm.ModoFacturacion);
            Assert.IsTrue(vm.Estado.MantenerJunto);
            Assert.IsFalse(vm.HayAvisoModoFacturacion);
            CollectionAssert.AreEqual(new[] { ModosFacturacion.AL_COMPLETAR, ModosFacturacion.TODO_AHORA_Y_LO_PENDIENTE_DESPUES },
                vm.OpcionesModoFacturacion.Where(o => o.Habilitado).Select(o => o.Codigo).ToArray());
            Assert.AreEqual("Los plazos de pago no son los de la ficha del cliente.", vm.OpcionesModoFacturacion.Single(o => o.Codigo == ModosFacturacion.POR_ENTREGAS).Ayuda);
        }

        [TestMethod]
        public void Sugerencia_SiLoQueEligioElUsuarioDejaDeValer_SePasaAlSugeridoYSeLeAvisa()
        {
            var vm = CrearViewModel();
            vm.direccionEntregaSeleccionada = new DireccionesEntregaCliente { mantenerJunto = true };
            vm.ModoFacturacion = ModosFacturacion.POR_ENTREGAS; // elección del usuario

            // Cambia los plazos a unos que no son de la ficha: «por entregas» deja de poder elegirse.
            vm.AplicarSugerenciaModoFacturacion(Sugerencia(ModosFacturacion.AL_COMPLETAR, ModosFacturacion.AL_COMPLETAR, ModosFacturacion.TODO_AHORA_Y_LO_PENDIENTE_DESPUES));

            Assert.AreEqual(ModosFacturacion.AL_COMPLETAR, vm.ModoFacturacion);
            Assert.IsTrue(vm.HayAvisoModoFacturacion);
            StringAssert.Contains(vm.AvisoModoFacturacion, "Por entregas");
            StringAssert.Contains(vm.AvisoModoFacturacion, "Al completar el pedido");
        }

        [TestMethod]
        public void Sugerencia_SiLoQueEligioElUsuarioSigueValiendo_NoSeLeToca()
        {
            var vm = CrearViewModel();
            vm.direccionEntregaSeleccionada = new DireccionesEntregaCliente { mantenerJunto = false };
            vm.ModoFacturacion = ModosFacturacion.TODO_AHORA_Y_LO_PENDIENTE_DESPUES;

            vm.AplicarSugerenciaModoFacturacion(Sugerencia(ModosFacturacion.POR_ENTREGAS, 1, 2, 3));

            Assert.AreEqual(ModosFacturacion.TODO_AHORA_Y_LO_PENDIENTE_DESPUES, vm.ModoFacturacion);
            Assert.IsFalse(vm.HayAvisoModoFacturacion);
        }

        [TestMethod]
        public void Sugerencia_ApiSinRespuesta_TodosHabilitadosYSinCambios()
        {
            var vm = CrearViewModel();
            vm.direccionEntregaSeleccionada = new DireccionesEntregaCliente { mantenerJunto = false };

            vm.AplicarSugerenciaModoFacturacion(null);

            Assert.IsTrue(vm.OpcionesModoFacturacion.All(o => o.Habilitado));
            Assert.AreEqual(ModosFacturacion.POR_ENTREGAS, vm.ModoFacturacion);
        }

        [TestMethod]
        public void CambiarDeDireccion_ReiniciaElModo_YLaSugerenciaVuelveAMandar()
        {
            var vm = CrearViewModel();
            vm.direccionEntregaSeleccionada = new DireccionesEntregaCliente { contacto = "0", mantenerJunto = false };
            vm.ModoFacturacion = ModosFacturacion.TODO_AHORA_Y_LO_PENDIENTE_DESPUES;

            vm.direccionEntregaSeleccionada = new DireccionesEntregaCliente { contacto = "1", mantenerJunto = true };

            Assert.AreEqual(ModosFacturacion.AL_COMPLETAR, vm.ModoFacturacion, "Con la dirección nueva arranca en lo de su ficha");
            vm.AplicarSugerenciaModoFacturacion(Sugerencia(ModosFacturacion.POR_ENTREGAS, 1, 2, 3));
            Assert.AreEqual(ModosFacturacion.POR_ENTREGAS, vm.ModoFacturacion, "La elección anterior ya no cuenta: manda la sugerencia");
        }

        [TestMethod]
        public void Borrador_RestauraElModoFacturacion_YLaSugerenciaNoLoPisa()
        {
            var vm = CrearViewModel();
            vm.direccionEntregaSeleccionada = new DireccionesEntregaCliente { mantenerJunto = true };

            vm.RestaurarModoFacturacion(ModosFacturacion.TODO_AHORA_Y_LO_PENDIENTE_DESPUES, mantenerJunto: false);

            Assert.AreEqual(ModosFacturacion.TODO_AHORA_Y_LO_PENDIENTE_DESPUES, vm.ModoFacturacion);
            Assert.IsFalse(vm.Estado.MantenerJunto);
            vm.AplicarSugerenciaModoFacturacion(Sugerencia(ModosFacturacion.POR_ENTREGAS, 1, 2, 3));
            Assert.AreEqual(ModosFacturacion.TODO_AHORA_Y_LO_PENDIENTE_DESPUES, vm.ModoFacturacion, "Lo guardado en el borrador es una elección, no un defecto");
        }

        [TestMethod]
        public void Borrador_AnteriorAlModo_MandaSuMantenerJunto()
        {
            var vm = CrearViewModel();
            vm.direccionEntregaSeleccionada = new DireccionesEntregaCliente { mantenerJunto = false };

            vm.RestaurarModoFacturacion(null, mantenerJunto: true);

            Assert.AreEqual(ModosFacturacion.AL_COMPLETAR, vm.ModoFacturacion);
            Assert.IsNull(vm.Estado.ModoFacturacion);
            Assert.IsTrue(vm.Estado.ToPedidoVentaDTO("DIR", () => 1, () => "NV").mantenerJunto);
        }

        [TestMethod]
        public void CrearBorradorDesdePedido_ConservaElModoDeFacturacion()
        {
            var servicio = new BorradorPlantillaVentaService(A.Fake<IConfiguracion>());
            var pedido = new PedidoParaPlantillaModel
            {
                Empresa = "1", Cliente = "15191", Contacto = "0", NumeroPedido = 926346,
                MantenerJunto = false, ModoFacturacion = 3, Almacen = "ALG"
            };

            var borrador = servicio.CrearBorradorDesdePedido(pedido);

            Assert.AreEqual((byte?)3, borrador.ModoFacturacion);
            Assert.IsFalse(borrador.MantenerJunto);
        }

        [TestMethod]
        public void Huella_SoloCambiaConLoQueMiraLaApi()
        {
            var a = new PedidoVentaDTO { empresa = "1", cliente = "15191", contacto = "0", plazosPago = "30", periodoFacturacion = "NRM" };
            var b = new PedidoVentaDTO { empresa = "1", cliente = "15191", contacto = "0", plazosPago = "30", periodoFacturacion = "NRM" };
            b.Lineas.Add(new LineaPedidoVentaDTO { Producto = "38093", Cantidad = 2 });
            var c = new PedidoVentaDTO { empresa = "1", cliente = "15191", contacto = "0", plazosPago = "60", periodoFacturacion = "NRM" };

            Assert.AreEqual(PlantillaVentaViewModel.HuellaPedidoModoFacturacion(a), PlantillaVentaViewModel.HuellaPedidoModoFacturacion(b), "Montar líneas no vuelve a preguntar");
            Assert.AreNotEqual(PlantillaVentaViewModel.HuellaPedidoModoFacturacion(a), PlantillaVentaViewModel.HuellaPedidoModoFacturacion(c), "Cambiar los plazos sí");
        }
    }
}
