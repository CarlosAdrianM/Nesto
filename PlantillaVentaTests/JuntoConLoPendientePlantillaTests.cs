using ControlesUsuario.Dialogs;
using ControlesUsuario.Models;
using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Models;
using Nesto.Modulos.PedidoVenta;
using Nesto.Modulos.PlantillaVenta;
using System.Collections.Generic;
using System.Linq;

namespace PlantillaVentaTests
{
    /// <summary>
    /// Sugerencia 1340 (Paloma): el modo de servicio 6, «Junto con lo pendiente del cliente», en el combo «Servir» de la
    /// plantilla. Solo aparece cuando la API lo ofrece (nunca lo propone), con su explicación debajo, y el pedido viaja
    /// con modoServicio 6 y servirJunto marcado.
    /// </summary>
    [TestClass]
    public class JuntoConLoPendientePlantillaTests
    {
        private const string EXPLICACION = "Saldrá junto con la nota de entrega 929701 y el pedido 931020.";

        private static PlantillaVentaViewModel CrearViewModel()
        {
            IConfiguracion configuracion = A.Fake<IConfiguracion>();
            A.CallTo(() => configuracion.LeerParametroSync(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.AlmacenRuta)).Returns("ALG");
            return new PlantillaVentaViewModel(A.Fake<IAbridorPedidos>(), A.Fake<IServicioNavegacion>(), configuracion,
                A.Fake<IPlantillaVentaService>(), new WeakReferenceMessenger(), A.Fake<IServicioDialogos>(),
                A.Fake<IPedidoVentaService>(), A.Fake<IBorradorPlantillaVentaService>(), A.Fake<IServicioAutenticacion>());
        }

        private static ModoServicioSugeridoDTO Sugerencia(bool ofreceElSeis, byte modo = ModosServicio.TRAS_REPONER_DE_TIENDAS)
        {
            var permitidos = new List<byte> { 1, 2, 3, 4 };
            var modos = permitidos.Select(m => new ModoServicioPermitidoDTO { Modo = m, Permitido = true }).ToList();
            if (ofreceElSeis)
            {
                permitidos.Add(ModosServicio.JUNTO_CON_LO_PENDIENTE);
                modos.Add(new ModoServicioPermitidoDTO
                {
                    Modo = ModosServicio.JUNTO_CON_LO_PENDIENTE,
                    Permitido = true,
                    Explicacion = EXPLICACION,
                    PedidosConLosQueSeJunta = new List<int> { 929701, 931020 }
                });
            }
            return new ModoServicioSugeridoDTO { Modo = modo, Motivo = "Motivo.", ModosPermitidos = permitidos, Modos = modos };
        }

        private static OpcionModo Seis(PlantillaVentaViewModel vm) =>
            vm.OpcionesModoServicio.Single(o => o.Codigo == ModosServicio.JUNTO_CON_LO_PENDIENTE);

        [TestMethod]
        public void Plantilla_SinRespuestaOSinNadaPendiente_ElSeisNoAparece()
        {
            var vm = CrearViewModel();
            vm.direccionEntregaSeleccionada = new DireccionesEntregaCliente { servirJunto = false };
            Assert.IsFalse(Seis(vm).Visible, "Sin respuesta de la API (o con una API vieja) no aparece nunca");

            vm.AplicarSugerenciaModoServicio(Sugerencia(ofreceElSeis: false));

            Assert.IsFalse(Seis(vm).Visible);
            Assert.IsTrue(vm.OpcionesModoServicio.Where(o => o.Codigo != ModosServicio.JUNTO_CON_LO_PENDIENTE).All(o => o.Visible));
        }

        [TestMethod]
        public void Plantilla_LaApiLoOfrece_SeElige_ViajaConSeisYServirJuntoYSeExplica()
        {
            var vm = CrearViewModel();
            vm.direccionEntregaSeleccionada = new DireccionesEntregaCliente { servirJunto = false };
            vm.AplicarSugerenciaModoServicio(Sugerencia(ofreceElSeis: true));

            Assert.IsTrue(Seis(vm).Visible);
            Assert.AreEqual(ModosServicio.TRAS_REPONER_DE_TIENDAS, vm.ModoServicio, "La sugerencia nunca propone el 6");
            Assert.IsFalse(vm.HayExplicacionModoServicio);

            vm.ModoServicio = ModosServicio.JUNTO_CON_LO_PENDIENTE;
            vm.SincronizarListasAlEstado();

            Assert.AreEqual(ModosServicio.JUNTO_CON_LO_PENDIENTE, vm.ModoServicio, "No se convierte en «Todo junto»");
            Assert.IsTrue(vm.direccionEntregaSeleccionada.servirJunto);
            Assert.AreEqual(EXPLICACION, vm.ExplicacionModoServicio);
            var pedido = vm.Estado.ToPedidoVentaDTO("DIR", () => 0, () => "NV");
            Assert.AreEqual((byte?)ModosServicio.JUNTO_CON_LO_PENDIENTE, pedido.modoServicio);
            Assert.IsTrue(pedido.servirJunto);
        }

        [TestMethod]
        public void Plantilla_ElegidoElSeisYLaApiDejaDeOfrecerlo_PasaAlSugeridoYAvisa()
        {
            var vm = CrearViewModel();
            vm.direccionEntregaSeleccionada = new DireccionesEntregaCliente { servirJunto = false };
            vm.AplicarSugerenciaModoServicio(Sugerencia(ofreceElSeis: true, modo: ModosServicio.TODO_JUNTO));
            vm.ModoServicio = ModosServicio.JUNTO_CON_LO_PENDIENTE;

            vm.AplicarSugerenciaModoServicio(Sugerencia(ofreceElSeis: false, modo: ModosServicio.TODO_JUNTO));

            Assert.AreEqual(ModosServicio.TODO_JUNTO, vm.ModoServicio);
            Assert.IsTrue(vm.HayAvisoModoServicio);
            Assert.IsFalse(Seis(vm).Visible);
            Assert.IsFalse(vm.HayExplicacionModoServicio);
        }

        [TestMethod]
        public void Plantilla_VueltaATodoJunto_DesdeElSeisVuelveAlSeis()
        {
            Assert.AreEqual("No se puede. El pedido se queda en «Junto con lo pendiente del cliente».",
                PlantillaVentaViewModel.AnadirVueltaATodoJunto("No se puede", ModosServicio.JUNTO_CON_LO_PENDIENTE));
            Assert.AreEqual("El pedido se queda en «Todo junto».", PlantillaVentaViewModel.AnadirVueltaATodoJunto(null));
        }
    }
}
