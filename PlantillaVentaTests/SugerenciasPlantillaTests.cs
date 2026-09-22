using ControlesUsuario.Models;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Events;
using Nesto.Infrastructure.Shared;
using Nesto.Models;
using Nesto.Modulos.PedidoVenta;
using Nesto.Modulos.PlantillaVenta;
using Prism.Events;
using Prism.Regions;
using Prism.Services.Dialogs;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Unity;

namespace PlantillaVentaTests
{
    /// <summary>
    /// Nesto#483 (modo de servicio, NestoAPI#506/#515) y Nesto#465 (ofertas no aplicadas, NestoAPI#457):
    /// las dos cosas las calcula el servidor sobre el pedido que se está montando y la plantilla solo las
    /// enseña. Aquí se prueba lo que sí es del cliente: preseleccionar sin pisar al usuario, no perder la
    /// sugerencia al cambiar de dirección, y aplicar la oferta de un clic.
    /// </summary>
    [TestClass]
    public class SugerenciasPlantillaTests
    {
        private static PlantillaVentaViewModel CrearViewModel()
        {
            IUnityContainer container = A.Fake<IUnityContainer>();
            IRegionManager regionManager = A.Fake<IRegionManager>();
            IConfiguracion configuracion = A.Fake<IConfiguracion>();
            IPlantillaVentaService servicio = A.Fake<IPlantillaVentaService>();
            IEventAggregator eventAggregator = A.Fake<IEventAggregator>();
            IDialogService dialogService = A.Fake<IDialogService>();
            IPedidoVentaService pedidoVentaService = A.Fake<IPedidoVentaService>();
            IBorradorPlantillaVentaService servicioBorradores = A.Fake<IBorradorPlantillaVentaService>();
            A.CallTo(() => configuracion.LeerParametroSync(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.AlmacenRuta)).Returns("ALG");
            var clienteCreadoEvent = A.Fake<ClienteCreadoEvent>();
            A.CallTo(() => eventAggregator.GetEvent<ClienteCreadoEvent>()).Returns(clienteCreadoEvent);

            var vm = new PlantillaVentaViewModel(container, regionManager, configuracion, servicio,
                eventAggregator, dialogService, pedidoVentaService, servicioBorradores,
                A.Fake<IServicioAutenticacion>());
            vm.ListaFiltrableProductos.ListaOriginal = new ObservableCollection<IFiltrableItem>();
            return vm;
        }

        private static ModoServicioSugeridoDTO Sugerencia(byte modo, string motivo = "Lo dice el servidor.")
        {
            return new ModoServicioSugeridoDTO
            {
                Modo = modo,
                Nombre = ModosServicio.Nombre(modo),
                Motivo = motivo
            };
        }

        #region Nesto#483: modo de servicio sugerido

        [TestMethod]
        public void ModoSugerido_ConRespuestaDelServidor_ElSelectorQuedaEnEseModoYEnseñaElMotivo()
        {
            var vm = CrearViewModel();
            vm.direccionEntregaSeleccionada = new DireccionesEntregaCliente { servirJunto = false };
            Assert.AreEqual(ModosServicio.TRAS_REPONER_DE_TIENDAS, vm.ModoServicio, "Arranca en el defecto");

            vm.AplicarSugerenciaModoServicio(Sugerencia(ModosServicio.TODO_JUNTO, "Hay stock de todo en el almacén del pedido: sale todo junto."));

            Assert.AreEqual(ModosServicio.TODO_JUNTO, vm.ModoServicio);
            Assert.AreEqual((byte)1, vm.Estado.ModoServicio);
            Assert.IsTrue(vm.Estado.ServirJunto);
            Assert.IsTrue(vm.direccionEntregaSeleccionada.servirJunto);
            Assert.IsTrue(vm.HayMotivoModoServicio);
            Assert.AreEqual("Hay stock de todo en el almacén del pedido: sale todo junto.", vm.MotivoModoServicio);
        }

        [TestMethod]
        public void ModoSugerido_SiElUsuarioYaEligio_NoSeLePisaElModo_PeroSiVeElMotivo()
        {
            var vm = CrearViewModel();
            vm.direccionEntregaSeleccionada = new DireccionesEntregaCliente { servirJunto = false };
            vm.ModoServicio = ModosServicio.AHORA_LO_QUE_HAY_Y_EL_RESTO_DE_UNA_VEZ;

            vm.AplicarSugerenciaModoServicio(Sugerencia(ModosServicio.TODO_JUNTO));

            Assert.AreEqual(ModosServicio.AHORA_LO_QUE_HAY_Y_EL_RESTO_DE_UNA_VEZ, vm.ModoServicio, "Manda lo que eligió el usuario");
            Assert.IsTrue(vm.HayMotivoModoServicio, "El motivo se enseña igual");
        }

        [TestMethod]
        public void ModoSugerido_AlCambiarDeDireccionDeEntrega_SeConservaElModoSugerido()
        {
            // Trampa 2 de NestoApp#184: al cambiar de contacto la app volvía a su defecto local y perdía
            // lo calculado por el servidor. Con respuesta, el «por defecto» del pedido ES esa respuesta.
            var vm = CrearViewModel();
            vm.direccionEntregaSeleccionada = new DireccionesEntregaCliente { servirJunto = false };
            vm.AplicarSugerenciaModoServicio(Sugerencia(ModosServicio.AHORA_LO_QUE_HAY_Y_EL_RESTO_DE_UNA_VEZ));

            vm.direccionEntregaSeleccionada = new DireccionesEntregaCliente { servirJunto = true };

            Assert.AreEqual(ModosServicio.AHORA_LO_QUE_HAY_Y_EL_RESTO_DE_UNA_VEZ, vm.ModoServicio);
            Assert.AreEqual((byte)4, vm.Estado.ModoServicio);
            Assert.IsFalse(vm.direccionEntregaSeleccionada.servirJunto, "El 4 no es servir junto");
        }

        [TestMethod]
        public void ModoSugerido_TrasCambiarDeDireccion_LaSiguienteSugerenciaVuelveAMandar()
        {
            // Al reiniciarse el modo se reinicia también la elección manual: si no, el pedido se quedaría
            // congelado en el modo de un contacto anterior.
            var vm = CrearViewModel();
            vm.direccionEntregaSeleccionada = new DireccionesEntregaCliente { servirJunto = false };
            vm.ModoServicio = ModosServicio.AHORA_LO_QUE_HAY_Y_EL_RESTO_DE_UNA_VEZ;

            vm.direccionEntregaSeleccionada = new DireccionesEntregaCliente { servirJunto = false };
            vm.AplicarSugerenciaModoServicio(Sugerencia(ModosServicio.TODO_JUNTO));

            Assert.AreEqual(ModosServicio.TODO_JUNTO, vm.ModoServicio);
        }

        [TestMethod]
        public void ModoSugerido_SiElServidorNoContesta_SeQuedaElDefectoDeSiempre()
        {
            var vm = CrearViewModel();
            vm.direccionEntregaSeleccionada = new DireccionesEntregaCliente { servirJunto = false };

            vm.AplicarSugerenciaModoServicio(null);

            Assert.AreEqual(ModosServicio.TRAS_REPONER_DE_TIENDAS, vm.ModoServicio);
            Assert.IsFalse(vm.HayMotivoModoServicio);
        }

        [TestMethod]
        public void ModoSugerido_UnModoQueNoExisteSeIgnora()
        {
            var vm = CrearViewModel();
            vm.direccionEntregaSeleccionada = new DireccionesEntregaCliente { servirJunto = false };

            vm.AplicarSugerenciaModoServicio(Sugerencia(7));

            Assert.AreEqual(ModosServicio.TRAS_REPONER_DE_TIENDAS, vm.ModoServicio);
            Assert.IsFalse(vm.HayMotivoModoServicio);
        }

        #endregion

        #region Nesto#465: ofertas que el pedido podría aplicar

        private static SugerenciaOfertaDTO Ampliar(string producto, int actual, int sugerida, int regalo)
        {
            return new SugerenciaOfertaDTO
            {
                Tipo = TiposSugerenciaOferta.AMPLIAR_CANTIDAD,
                Producto = producto,
                CantidadActual = actual,
                CantidadSugerida = sugerida,
                CantidadRegalo = regalo,
                Texto = $"Con {sugerida - actual} unidad(es) más del producto {producto} te llevas {regalo} de regalo."
            };
        }

        [TestMethod]
        public void OfertasSugeridas_LleganDelServidorYSeEnseñan()
        {
            var vm = CrearViewModel();
            Assert.IsFalse(vm.HayOfertasSugeridas);

            vm.AplicarOfertasSugeridas(new List<SugerenciaOfertaDTO> { Ampliar("38093", 5, 6, 1) });

            Assert.IsTrue(vm.HayOfertasSugeridas);
            Assert.AreEqual(1, vm.OfertasSugeridas.Count);
        }

        [TestMethod]
        public void OfertasSugeridas_LasQueVienenSinTextoNoSeEnseñan()
        {
            // El texto lo escribe el servidor; sin texto no hay nada que decirle al comercial.
            var vm = CrearViewModel();

            vm.AplicarOfertasSugeridas(new List<SugerenciaOfertaDTO>
            {
                new SugerenciaOfertaDTO { Tipo = TiposSugerenciaOferta.AMPLIAR_CANTIDAD, Producto = "38093", Texto = "  " }
            });

            Assert.IsFalse(vm.HayOfertasSugeridas);
        }

        [TestMethod]
        public void OfertasSugeridas_UnaListaVaciaBorraLasAnteriores()
        {
            var vm = CrearViewModel();
            vm.AplicarOfertasSugeridas(new List<SugerenciaOfertaDTO> { Ampliar("38093", 5, 6, 1) });

            vm.AplicarOfertasSugeridas(new List<SugerenciaOfertaDTO>());

            Assert.IsFalse(vm.HayOfertasSugeridas);
        }

        [TestMethod]
        public void AplicarOferta_SubeLaCantidadYPoneLasUnidadesDeRegalo()
        {
            var vm = CrearViewModel();
            var linea = new LineaPlantillaVenta { producto = "38093", cantidad = 5, precio = 10M };
            vm.ListaFiltrableProductos.ListaOriginal.Add(linea);

            vm.OnAplicarOfertaSugerida(Ampliar("38093", 5, 6, 1));

            Assert.AreEqual(6, linea.cantidad);
            Assert.AreEqual(1, linea.cantidadOferta);
        }

        [TestMethod]
        public void AplicarOferta_SiYaHayUnidadesDeSobra_SoloPoneElRegalo()
        {
            var vm = CrearViewModel();
            var linea = new LineaPlantillaVenta { producto = "38093", cantidad = 6, precio = 10M };
            vm.ListaFiltrableProductos.ListaOriginal.Add(linea);

            vm.OnAplicarOfertaSugerida(new SugerenciaOfertaDTO
            {
                Tipo = TiposSugerenciaOferta.OFERTA_NO_APLICADA,
                Producto = "38093",
                CantidadActual = 6,
                CantidadSugerida = 6,
                CantidadRegalo = 1,
                Texto = "El producto 38093 tiene un 6+1 y no lo estás aplicando."
            });

            Assert.AreEqual(6, linea.cantidad, "No se toca lo que ya hay");
            Assert.AreEqual(1, linea.cantidadOferta);
        }

        [TestMethod]
        public void AplicarOferta_LasDeImporteYLasEscalonadasNoSeAplicanSolas()
        {
            // Dicen lo que falta, pero no qué añadir: se enseñan y decide el comercial.
            var vm = CrearViewModel();
            var linea = new LineaPlantillaVenta { producto = "38093", cantidad = 5, precio = 10M };
            vm.ListaFiltrableProductos.ListaOriginal.Add(linea);

            var deImporte = new SugerenciaOfertaDTO
            {
                Tipo = TiposSugerenciaOferta.AMPLIAR_IMPORTE,
                Producto = "38093",
                CantidadRegalo = 1,
                ImporteQueFalta = 12M,
                Texto = "Añadiendo 12,00 € más al pedido te llevas 1 unidad de regalo."
            };

            Assert.IsFalse(deImporte.EsAplicable);
            vm.OnAplicarOfertaSugerida(deImporte);

            Assert.AreEqual(5, linea.cantidad);
            Assert.AreEqual(0, linea.cantidadOferta);
        }

        [TestMethod]
        public void AplicarOferta_SiElProductoYaNoEstaEnElPedido_AvisaYNoRevienta()
        {
            var vm = CrearViewModel();

            vm.OnAplicarOfertaSugerida(Ampliar("38093", 5, 6, 1));

            Assert.IsFalse(vm.ListaFiltrableProductos.ListaOriginal.Any());
        }

        #endregion
    }
}
