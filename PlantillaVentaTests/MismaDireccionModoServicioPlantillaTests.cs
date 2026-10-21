using CommunityToolkit.Mvvm.Messaging;
using ControlesUsuario.Models;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Models;
using Nesto.Modulos.PedidoVenta;
using Nesto.Modulos.PlantillaVenta;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using ControlesUsuario.Dialogs;

namespace PlantillaVentaTests
{
    /// <summary>
    /// Nesto#525 (pedido 928170 de David): el SelectorDireccionEntrega vuelve a empujar la dirección (otra instancia del
    /// MISMO contacto) al recargar su lista, y la plantilla lo trataba como un cambio de dirección: perdía el modo que había
    /// elegido el usuario («Todo junto») y la siguiente sugerencia del servidor (4) lo pisaba sin avisar.
    /// Además, ningún cambio de modo que no haga el usuario después de haber elegido uno puede ser silencioso.
    /// </summary>
    [TestClass]
    public class MismaDireccionModoServicioPlantillaTests
    {
        private static PlantillaVentaViewModel CrearViewModel(IServicioDialogos dialogService = null, IPedidoVentaService pedidoVentaService = null)
        {
            IConfiguracion configuracion = A.Fake<IConfiguracion>();
            A.CallTo(() => configuracion.LeerParametroSync(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.AlmacenRuta)).Returns("ALG");
            var vm = new PlantillaVentaViewModel(A.Fake<IAbridorPedidos>(), A.Fake<IServicioNavegacion>(), configuracion, A.Fake<IPlantillaVentaService>(),
                new WeakReferenceMessenger(), dialogService ?? A.Fake<IServicioDialogos>(), pedidoVentaService ?? A.Fake<IPedidoVentaService>(),
                A.Fake<IBorradorPlantillaVentaService>(), A.Fake<IServicioAutenticacion>());
            vm.ListaFiltrableProductos.ListaOriginal = new ObservableCollection<IFiltrableItem>();
            return vm;
        }

        private static DireccionesEntregaCliente Direccion(string contacto, bool servirJunto = false, bool mantenerJunto = false,
            string formaPago = "EFC", string plazosPago = "CONTADO", string ccc = "1")
        {
            return new DireccionesEntregaCliente
            {
                contacto = contacto,
                servirJunto = servirJunto,
                mantenerJunto = mantenerJunto,
                formaPago = formaPago,
                plazosPago = plazosPago,
                ccc = ccc
            };
        }

        private static ModoServicioSugeridoDTO SugerenciaServicio(byte modo, params byte[] permitidos)
        {
            return new ModoServicioSugeridoDTO
            {
                Modo = modo,
                Nombre = ModosServicio.Nombre(modo),
                Motivo = "Lo dice el servidor.",
                ModosPermitidos = permitidos.ToList(),
                Modos = ModosServicio.Lista.Select(m => new ModoServicioPermitidoDTO
                {
                    Modo = m.Codigo,
                    Nombre = m.Nombre,
                    Permitido = permitidos.Contains(m.Codigo),
                    Motivo = permitidos.Contains(m.Codigo) ? null : "Motivo del servidor."
                }).ToList()
            };
        }

        private static ModoFacturacionSugeridoDTO SugerenciaFacturacion(byte modo, params byte[] permitidos) => new ModoFacturacionSugeridoDTO
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

        #region La misma dirección (otra instancia del mismo contacto) no reinicia nada

        [TestMethod]
        public void MismaDireccion_OtraInstanciaConRelleno_ConservaTodoJunto_YLaSugerencia4NoLoPisa()
        {
            // El caso del 928170: elige «Todo junto», el selector recarga y vuelve a fijar el contacto, llega un 4.
            var vm = CrearViewModel();
            vm.Estado.Cliente = "2043";
            vm.direccionEntregaSeleccionada = Direccion("2");
            vm.ModoServicio = ModosServicio.TODO_JUNTO; // elección del usuario

            vm.Estado.Cliente = "2043      ";
            vm.direccionEntregaSeleccionada = Direccion("2  ");
            vm.AplicarSugerenciaModoServicio(SugerenciaServicio(ModosServicio.AHORA_LO_QUE_HAY_Y_EL_RESTO_DE_UNA_VEZ, 1, 2, 3, 4));

            Assert.AreEqual(ModosServicio.TODO_JUNTO, vm.ModoServicio, "Manda lo que eligió el usuario");
            Assert.IsTrue(vm.direccionEntregaSeleccionada.servirJunto, "La instancia nueva hereda el servirJunto");
            vm.SincronizarListasAlEstado();
            Assert.AreEqual((byte)1, vm.Estado.ModoServicio);
            Assert.IsTrue(vm.Estado.ServirJunto);
            Assert.IsFalse(vm.HayAvisoModoServicio, "No ha cambiado nada: no hay nada que avisar");
        }

        [TestMethod]
        public void MismaDireccion_ContactoEnMayusculasDistintas_EsLaMisma()
        {
            var vm = CrearViewModel();
            vm.direccionEntregaSeleccionada = Direccion("a");
            vm.ModoServicio = ModosServicio.SEGUN_VAYA_ENTRANDO;

            vm.direccionEntregaSeleccionada = Direccion(" A ");
            vm.AplicarSugerenciaModoServicio(SugerenciaServicio(ModosServicio.TRAS_REPONER_DE_TIENDAS, 1, 2, 3, 4));

            Assert.AreEqual(ModosServicio.SEGUN_VAYA_ENTRANDO, vm.ModoServicio);
        }

        [TestMethod]
        public void MismaDireccion_ConservaElModoSugeridoYaAplicado_SinElegirNada()
        {
            // Sin elección del usuario manda la sugerencia; volver a fijar la dirección no lo devuelve al defecto.
            var vm = CrearViewModel();
            vm.direccionEntregaSeleccionada = Direccion("0");
            vm.AplicarSugerenciaModoServicio(SugerenciaServicio(ModosServicio.SEGUN_VAYA_ENTRANDO, 1, 2, 3, 4));

            vm.direccionEntregaSeleccionada = Direccion("0", servirJunto: true);

            Assert.AreEqual(ModosServicio.SEGUN_VAYA_ENTRANDO, vm.ModoServicio);
            Assert.IsFalse(vm.direccionEntregaSeleccionada.servirJunto, "Se copia el servirJunto del pedido, no el de la ficha");
        }

        [TestMethod]
        public void MismaDireccion_ConservaElModoDeFacturacionElegido()
        {
            var vm = CrearViewModel();
            vm.direccionEntregaSeleccionada = Direccion("0", mantenerJunto: true);
            vm.ModoFacturacion = ModosFacturacion.TODO_AHORA_Y_LO_PENDIENTE_DESPUES; // elección del usuario

            vm.direccionEntregaSeleccionada = Direccion("0 ", mantenerJunto: true);
            vm.AplicarSugerenciaModoFacturacion(SugerenciaFacturacion(ModosFacturacion.AL_COMPLETAR, 1, 2, 3));

            Assert.AreEqual(ModosFacturacion.TODO_AHORA_Y_LO_PENDIENTE_DESPUES, vm.ModoFacturacion);
            Assert.AreEqual(ModosFacturacion.TODO_AHORA_Y_LO_PENDIENTE_DESPUES, vm.Estado.ModoFacturacion);
            Assert.IsFalse(vm.Estado.MantenerJunto, "El MantenerJunto sigue al modo elegido, no a la ficha");
        }

        [TestMethod]
        public void MismaDireccion_RespetaFormaPagoPlazosYCccQueCambioElUsuario()
        {
            var vm = CrearViewModel();
            vm.direccionEntregaSeleccionada = Direccion("0", formaPago: "EFC", plazosPago: "CONTADO", ccc: "1");
            vm.FormaPagoCliente = "TAR";
            vm.PlazoPagoCliente = "PRE";
            vm.CccSeleccionado = "2";

            vm.direccionEntregaSeleccionada = Direccion("0", formaPago: "EFC", plazosPago: "CONTADO", ccc: "1");

            Assert.AreEqual("TAR", vm.FormaPagoCliente);
            Assert.AreEqual("PRE", vm.PlazoPagoCliente);
            Assert.AreEqual("2", vm.CccSeleccionado);
        }

        #endregion

        #region Un cambio de contacto de verdad sí reinicia (y avisa si se pierde la elección)

        [TestMethod]
        public void OtroContacto_ReiniciaElModo_LaSugerenciaVuelveAMandar_YSeAvisa()
        {
            var vm = CrearViewModel();
            vm.direccionEntregaSeleccionada = Direccion("0");
            vm.ModoServicio = ModosServicio.TODO_JUNTO;

            vm.direccionEntregaSeleccionada = Direccion("1");

            Assert.AreEqual(ModosServicio.TRAS_REPONER_DE_TIENDAS, vm.ModoServicio, "Arranca en el defecto");
            Assert.IsTrue(vm.HayAvisoModoServicio, "Se ha perdido la elección del usuario: se le avisa");
            StringAssert.Contains(vm.AvisoModoServicio, "Todo junto");
            StringAssert.Contains(vm.AvisoModoServicio, "dirección de entrega");

            vm.AplicarSugerenciaModoServicio(SugerenciaServicio(ModosServicio.AHORA_LO_QUE_HAY_Y_EL_RESTO_DE_UNA_VEZ, 1, 2, 3, 4));
            Assert.AreEqual(ModosServicio.AHORA_LO_QUE_HAY_Y_EL_RESTO_DE_UNA_VEZ, vm.ModoServicio);
        }

        [TestMethod]
        public void OtroContacto_SinEleccionDelUsuario_NoAvisa()
        {
            var vm = CrearViewModel();
            vm.direccionEntregaSeleccionada = Direccion("0");

            vm.direccionEntregaSeleccionada = Direccion("1");

            Assert.IsFalse(vm.HayAvisoModoServicio, "Preselección: no hay elección que perder");
        }

        [TestMethod]
        public void OtroCliente_MismoCodigoDeContacto_Reinicia()
        {
            var vm = CrearViewModel();
            vm.Estado.Cliente = "15191";
            vm.direccionEntregaSeleccionada = Direccion("0");
            vm.ModoServicio = ModosServicio.TODO_JUNTO;
            vm.ModoFacturacion = ModosFacturacion.TODO_AHORA_Y_LO_PENDIENTE_DESPUES;

            vm.Estado.Cliente = "2043";
            vm.direccionEntregaSeleccionada = Direccion("0", mantenerJunto: true);

            Assert.AreEqual(ModosServicio.TRAS_REPONER_DE_TIENDAS, vm.ModoServicio);
            Assert.AreEqual(ModosFacturacion.AL_COMPLETAR, vm.ModoFacturacion, "Arranca en lo de la ficha del otro cliente");
        }

        [TestMethod]
        public void OtroContacto_ElModoDeFacturacionSeReinicia()
        {
            var vm = CrearViewModel();
            vm.direccionEntregaSeleccionada = Direccion("0", mantenerJunto: false);
            vm.ModoFacturacion = ModosFacturacion.TODO_AHORA_Y_LO_PENDIENTE_DESPUES;

            vm.direccionEntregaSeleccionada = Direccion("1", mantenerJunto: true);
            vm.AplicarSugerenciaModoFacturacion(SugerenciaFacturacion(ModosFacturacion.POR_ENTREGAS, 1, 2, 3));

            Assert.AreEqual(ModosFacturacion.POR_ENTREGAS, vm.ModoFacturacion);
        }

        #endregion

        #region Nada de cambiar el modo en silencio después de que el usuario haya elegido

        [TestMethod]
        public void VueltaATodoJunto_TrasUnCambioAutomatico_ElAvisoDiceLoQueHaPasado()
        {
            // El usuario eligió «Todo junto»; el servidor deja de permitirlo y se pasa a 3 con aviso. Si luego la
            // validación de salir de «Todo junto» lo devuelve a 1, el aviso de «se ha cambiado a 3» ya no es verdad.
            var vm = CrearViewModel();
            vm.direccionEntregaSeleccionada = Direccion("0");
            vm.ModoServicio = ModosServicio.TODO_JUNTO;
            vm.AplicarSugerenciaModoServicio(SugerenciaServicio(ModosServicio.TRAS_REPONER_DE_TIENDAS, 2, 3, 4));
            Assert.AreEqual(ModosServicio.TRAS_REPONER_DE_TIENDAS, vm.ModoServicio);
            Assert.IsTrue(vm.HayAvisoModoServicio);

            vm.RevertirAServirJunto();

            Assert.AreEqual(ModosServicio.TODO_JUNTO, vm.ModoServicio);
            Assert.IsTrue(vm.HayAvisoModoServicio);
            StringAssert.Contains(vm.AvisoModoServicio, "se queda en «Todo junto»");
        }

        [TestMethod]
        public void VueltaATodoJunto_TrasUnCambioDelPropioUsuario_NoAñadeAviso()
        {
            // Si fue el usuario quien salió de «Todo junto», ya ve el diálogo del servidor (o canceló él).
            var vm = CrearViewModel();
            vm.direccionEntregaSeleccionada = Direccion("0");
            vm.ModoServicio = ModosServicio.TODO_JUNTO;
            vm.ModoServicio = ModosServicio.TRAS_REPONER_DE_TIENDAS;

            vm.RevertirAServirJunto();

            Assert.AreEqual(ModosServicio.TODO_JUNTO, vm.ModoServicio);
            Assert.IsFalse(vm.HayAvisoModoServicio);
        }

        [TestMethod]
        public async Task ConPicking_VolverAlModoGrabado_SeAvisa()
        {
            IServicioDialogos dialogService = A.Fake<IServicioDialogos>();
            A.CallTo(() => dialogService.ShowConfirmationAsync(A<string>._, A<string>._)).Returns(Task.FromResult(false));
            var vm = CrearViewModel(dialogService);
            vm.direccionEntregaSeleccionada = Direccion("0");
            vm.ModoServicio = ModosServicio.TODO_JUNTO;

            await vm.ResolverModoConPickingAsync(new ModoConPickingException("Ya tiene picking."), "1", 926879,
                ModosServicio.TODO_JUNTO, ModosServicio.SEGUN_VAYA_ENTRANDO);

            Assert.AreEqual(ModosServicio.SEGUN_VAYA_ENTRANDO, vm.ModoServicio);
            Assert.IsTrue(vm.HayAvisoModoServicio);
            StringAssert.Contains(vm.AvisoModoServicio, "picking");
        }

        [TestMethod]
        public void PreseleccionInicial_SinEleccionDelUsuario_NoAvisa()
        {
            var vm = CrearViewModel();
            vm.direccionEntregaSeleccionada = Direccion("0");

            vm.AplicarSugerenciaModoServicio(SugerenciaServicio(ModosServicio.AHORA_LO_QUE_HAY_Y_EL_RESTO_DE_UNA_VEZ, 1, 2, 3, 4));

            Assert.AreEqual(ModosServicio.AHORA_LO_QUE_HAY_Y_EL_RESTO_DE_UNA_VEZ, vm.ModoServicio);
            Assert.IsFalse(vm.HayAvisoModoServicio);
        }

        #endregion
    }
}
