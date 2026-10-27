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
using System.Collections.ObjectModel;
using System.Linq;

namespace PlantillaVentaTests
{
    /// <summary>
    /// NestoAPI#704 (caso 929152): la plantilla le dice a la API de dónde salió el modo de servicio con el que graba
    /// (OrigenModoServicio) y qué sugirió el servidor (ModoServicioSugerido). La API solo lo registra: con eso se sabe si
    /// un pedido «raro» lo eligió el vendedor o venía puesto.
    /// </summary>
    [TestClass]
    public class OrigenModoServicioPlantillaTests
    {
        private static PlantillaVentaViewModel CrearViewModel()
        {
            IConfiguracion configuracion = A.Fake<IConfiguracion>();
            A.CallTo(() => configuracion.LeerParametroSync(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.AlmacenRuta)).Returns("ALG");
            var vm = new PlantillaVentaViewModel(A.Fake<IAbridorPedidos>(), A.Fake<IServicioNavegacion>(), configuracion, A.Fake<IPlantillaVentaService>(),
                new WeakReferenceMessenger(), A.Fake<IServicioDialogos>(), A.Fake<IPedidoVentaService>(),
                A.Fake<IBorradorPlantillaVentaService>(), A.Fake<IServicioAutenticacion>());
            vm.ListaFiltrableProductos.ListaOriginal = new ObservableCollection<IFiltrableItem>();
            vm.direccionEntregaSeleccionada = new DireccionesEntregaCliente { contacto = "0", formaPago = "EFC", plazosPago = "CONTADO", ccc = "1" };
            return vm;
        }

        private static ModoServicioSugeridoDTO Sugerencia(byte modo, params byte[] permitidos) => new ModoServicioSugeridoDTO
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

        [TestMethod]
        public void SinSugerenciaNiEleccion_EsPorDefecto()
        {
            var vm = CrearViewModel();

            Assert.AreEqual(OrigenesModoServicio.POR_DEFECTO, vm.CalcularOrigenModoServicio());
        }

        [TestMethod]
        public void LaSugerenciaDelServidorSinTocarla_EsSugerido()
        {
            var vm = CrearViewModel();

            vm.AplicarSugerenciaModoServicio(Sugerencia(ModosServicio.AHORA_LO_QUE_HAY_Y_EL_RESTO_DE_UNA_VEZ, 1, 2, 3, 4));

            Assert.AreEqual(ModosServicio.AHORA_LO_QUE_HAY_Y_EL_RESTO_DE_UNA_VEZ, vm.ModoServicio);
            Assert.AreEqual(OrigenesModoServicio.SUGERIDO, vm.CalcularOrigenModoServicio());
        }

        [TestMethod]
        public void ElVendedorEligeOtroModo_EsUsuario()
        {
            // El caso del 929152: si David hubiera elegido «Todo junto», la API lo vería como Usuario con el 4 sugerido.
            var vm = CrearViewModel();
            vm.AplicarSugerenciaModoServicio(Sugerencia(ModosServicio.AHORA_LO_QUE_HAY_Y_EL_RESTO_DE_UNA_VEZ, 1, 2, 3, 4));

            vm.ModoServicio = ModosServicio.TODO_JUNTO;

            Assert.AreEqual(OrigenesModoServicio.USUARIO, vm.CalcularOrigenModoServicio());
        }

        [TestMethod]
        public void ElVendedorEligeYLlegaUnaSugerenciaQueNoLoPermite_EsCorregido()
        {
            // Nesto#484: eligió «Según vaya entrando», el servidor dice que solo cabe «Todo junto»: se cambia avisando.
            var vm = CrearViewModel();
            vm.ModoServicio = ModosServicio.SEGUN_VAYA_ENTRANDO;

            vm.AplicarSugerenciaModoServicio(Sugerencia(ModosServicio.TODO_JUNTO, 1));

            Assert.AreEqual(ModosServicio.TODO_JUNTO, vm.ModoServicio);
            Assert.IsTrue(vm.HayAvisoModoServicio);
            Assert.AreEqual(OrigenesModoServicio.CORREGIDO, vm.CalcularOrigenModoServicio());
        }

        [TestMethod]
        public void ElVendedorVuelveAElegirTrasLaCorreccion_EsUsuario()
        {
            var vm = CrearViewModel();
            vm.ModoServicio = ModosServicio.SEGUN_VAYA_ENTRANDO;
            vm.AplicarSugerenciaModoServicio(Sugerencia(ModosServicio.TODO_JUNTO, 1, 3));

            vm.ModoServicio = ModosServicio.TRAS_REPONER_DE_TIENDAS;

            Assert.IsFalse(vm.HayAvisoModoServicio, "Elegir a mano quita el aviso del cambio automático");
            Assert.AreEqual(OrigenesModoServicio.USUARIO, vm.CalcularOrigenModoServicio());
        }
    }
}
