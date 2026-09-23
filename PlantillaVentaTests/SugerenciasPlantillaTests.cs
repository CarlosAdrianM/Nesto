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

        #region NestoAPI#517: ninguna avalancha de peticiones con el pedido quieto

        private static (PlantillaVentaViewModel vm, IPlantillaVentaService servicio) CrearViewModelConPedido(byte modoQueSugiereElServidor)
        {
            IUnityContainer container = A.Fake<IUnityContainer>();
            IConfiguracion configuracion = A.Fake<IConfiguracion>();
            IPlantillaVentaService servicio = A.Fake<IPlantillaVentaService>();
            IEventAggregator eventAggregator = A.Fake<IEventAggregator>();
            A.CallTo(() => configuracion.LeerParametroSync(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.AlmacenRuta)).Returns("ALG");
            A.CallTo(() => eventAggregator.GetEvent<ClienteCreadoEvent>()).Returns(A.Fake<ClienteCreadoEvent>());
            A.CallTo(() => servicio.ModoServicioSugerido(A<PedidoVentaDTO>._)).Returns(Sugerencia(modoQueSugiereElServidor));
            A.CallTo(() => servicio.OfertasSugeridas(A<PedidoVentaDTO>._)).Returns(new List<SugerenciaOfertaDTO> { Ampliar("38093", 5, 6, 1) });

            var vm = new PlantillaVentaViewModel(container, A.Fake<IRegionManager>(), configuracion, servicio,
                eventAggregator, A.Fake<IDialogService>(), A.Fake<IPedidoVentaService>(), A.Fake<IBorradorPlantillaVentaService>(),
                A.Fake<IServicioAutenticacion>());
            vm.ListaFiltrableProductos.ListaOriginal = new ObservableCollection<IFiltrableItem>();
            vm._clienteSeleccionado = new ClienteJson { empresa = "1", cliente = "15191", contacto = "0", iva = "G21", cifNif = "12345678A" };
            vm.direccionEntregaSeleccionada = new DireccionesEntregaCliente { contacto = "0", servirJunto = false };
            vm.ListaFiltrableProductos.ListaOriginal.Add(new LineaPlantillaVenta { producto = "38093", cantidad = 5, precio = 10M });
            return (vm, servicio);
        }

        [TestMethod]
        public async System.Threading.Tasks.Task Sugerencias_ConElPedidoQuieto_NoSeVuelvenAPedir()
        {
            // 23/09/26: RDS2016 al 100 % de CPU. Con el pedido sin tocar, ninguna respuesta del servidor puede
            // provocar otra petición: la segunda y la tercera se descartan por la huella del pedido.
            var (vm, servicio) = CrearViewModelConPedido(ModosServicio.TRAS_REPONER_DE_TIENDAS);

            await vm.RefrescarSugerencias();
            await vm.RefrescarSugerencias();
            await vm.RefrescarSugerencias();

            A.CallTo(() => servicio.OfertasSugeridas(A<PedidoVentaDTO>._)).MustHaveHappenedOnceExactly();
            Assert.AreEqual(1, vm.PeticionesSugerenciasEnviadas);
        }

        [TestMethod]
        public async System.Threading.Tasks.Task Sugerencias_AunqueElServidorCambieElModo_SeEstabilizan()
        {
            // Aplicar el modo sugerido cambia el pedido (una petición más para confirmarlo) y ahí se para.
            var (vm, servicio) = CrearViewModelConPedido(ModosServicio.AHORA_LO_QUE_HAY_Y_EL_RESTO_DE_UNA_VEZ);

            for (int i = 0; i < 6; i++)
            {
                await vm.RefrescarSugerencias();
            }

            Assert.IsTrue(vm.PeticionesSugerenciasEnviadas <= 2, $"Se han pedido {vm.PeticionesSugerenciasEnviadas} veces con el pedido quieto");
        }

        [TestMethod]
        public async System.Threading.Tasks.Task Sugerencias_SiCambiaUnaLinea_SeVuelvenAPedir()
        {
            var (vm, servicio) = CrearViewModelConPedido(ModosServicio.TRAS_REPONER_DE_TIENDAS);
            await vm.RefrescarSugerencias();

            ((LineaPlantillaVenta)vm.ListaFiltrableProductos.ListaOriginal[0]).cantidad = 6;
            await vm.RefrescarSugerencias();

            Assert.AreEqual(2, vm.PeticionesSugerenciasEnviadas);
        }

        #endregion

        #region Nesto#484 / NestoAPI#518: solo se pueden elegir los modos con sentido

        private static ModoServicioSugeridoDTO SugerenciaConPermitidos(byte modo, params byte[] permitidos)
        {
            var s = Sugerencia(modo);
            s.ModosPermitidos = permitidos.ToList();
            s.Modos = ModosServicio.Lista.Select(m => new ModoServicioPermitidoDTO
            {
                Modo = m.Codigo,
                Nombre = m.Nombre,
                Permitido = permitidos.Contains(m.Codigo),
                Motivo = permitidos.Contains(m.Codigo) ? null : "Motivo del servidor."
            }).ToList();
            return s;
        }

        [TestMethod]
        public void ModosPermitidos_TodoVerde_SoloTodoJuntoHabilitado_ConElMotivoEnLosDemas()
        {
            var vm = CrearViewModel();
            vm.direccionEntregaSeleccionada = new DireccionesEntregaCliente { servirJunto = false };

            vm.AplicarSugerenciaModoServicio(SugerenciaConPermitidos(ModosServicio.TODO_JUNTO, ModosServicio.TODO_JUNTO));

            CollectionAssert.AreEqual(new[] { ModosServicio.TODO_JUNTO },
                vm.OpcionesModoServicio.Where(o => o.Habilitado).Select(o => o.Codigo).ToArray());
            Assert.AreEqual("Motivo del servidor.", vm.OpcionesModoServicio.Single(o => o.Codigo == ModosServicio.TRAS_REPONER_DE_TIENDAS).Ayuda);
            Assert.AreEqual(ModosServicio.TODO_JUNTO, vm.ModoServicio);
            Assert.IsFalse(vm.HayAvisoModoServicio, "Sin elección del usuario es la preselección de siempre: sin aviso");
        }

        [TestMethod]
        public void ModosPermitidos_EnTienda_SoloSegunVayaEntrando()
        {
            var vm = CrearViewModel();
            vm.direccionEntregaSeleccionada = new DireccionesEntregaCliente { servirJunto = false };

            vm.AplicarSugerenciaModoServicio(SugerenciaConPermitidos(ModosServicio.SEGUN_VAYA_ENTRANDO, ModosServicio.SEGUN_VAYA_ENTRANDO));

            CollectionAssert.AreEqual(new[] { ModosServicio.SEGUN_VAYA_ENTRANDO },
                vm.OpcionesModoServicio.Where(o => o.Habilitado).Select(o => o.Codigo).ToArray());
            Assert.AreEqual(ModosServicio.SEGUN_VAYA_ENTRANDO, vm.ModoServicio);
        }

        [TestMethod]
        public void ModosPermitidos_SiLoQueEligioElUsuarioDejaDeValer_SePasaAlSugeridoYSeLeAvisa()
        {
            var vm = CrearViewModel();
            vm.direccionEntregaSeleccionada = new DireccionesEntregaCliente { servirJunto = false };
            vm.ModoServicio = ModosServicio.AHORA_LO_QUE_HAY_Y_EL_RESTO_DE_UNA_VEZ; // elección del usuario (distinta del defecto)

            // Cambian las líneas (o el almacén) y ahora hay stock de todo en Algete: solo vale «Todo junto».
            vm.AplicarSugerenciaModoServicio(SugerenciaConPermitidos(ModosServicio.TODO_JUNTO, ModosServicio.TODO_JUNTO));

            Assert.AreEqual(ModosServicio.TODO_JUNTO, vm.ModoServicio);
            Assert.IsTrue(vm.HayAvisoModoServicio);
            StringAssert.Contains(vm.AvisoModoServicio, "Ahora lo que hay");
            StringAssert.Contains(vm.AvisoModoServicio, "Motivo del servidor");
        }

        [TestMethod]
        public void ModosPermitidos_SiLoQueEligioElUsuarioSigueValiendo_NoSeLeToca()
        {
            var vm = CrearViewModel();
            vm.direccionEntregaSeleccionada = new DireccionesEntregaCliente { servirJunto = false };
            vm.ModoServicio = ModosServicio.SEGUN_VAYA_ENTRANDO;

            vm.AplicarSugerenciaModoServicio(SugerenciaConPermitidos(ModosServicio.TRAS_REPONER_DE_TIENDAS, 1, 2, 3, 4));

            Assert.AreEqual(ModosServicio.SEGUN_VAYA_ENTRANDO, vm.ModoServicio);
            Assert.IsFalse(vm.HayAvisoModoServicio);
        }

        [TestMethod]
        public void ModosPermitidos_ApiSinLaListaNueva_TodosHabilitados()
        {
            var vm = CrearViewModel();
            vm.direccionEntregaSeleccionada = new DireccionesEntregaCliente { servirJunto = false };

            vm.AplicarSugerenciaModoServicio(Sugerencia(ModosServicio.TODO_JUNTO));

            Assert.IsTrue(vm.OpcionesModoServicio.All(o => o.Habilitado));
        }

        [TestMethod]
        public void LeerModoSugerido_DelRechazoDeLaApi()
        {
            var json = Newtonsoft.Json.Linq.JObject.Parse(
                "{\"error\":{\"code\":\"MODO_SERVICIO_NO_PERMITIDO\",\"message\":\"x\",\"details\":{\"modoSugerido\":1,\"modosPermitidos\":[1]}}}");

            Assert.AreEqual((byte?)ModosServicio.TODO_JUNTO, PlantillaVentaService.LeerModoSugerido(json));
            Assert.IsNull(PlantillaVentaService.LeerModoSugerido(Newtonsoft.Json.Linq.JObject.Parse("{\"error\":{\"code\":\"OTRO\"}}")));
        }

        #endregion
    }
}
