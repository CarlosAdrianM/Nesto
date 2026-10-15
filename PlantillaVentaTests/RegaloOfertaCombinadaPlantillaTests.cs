using CommunityToolkit.Mvvm.Messaging;
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
using System.Linq;
using System.Threading.Tasks;
using Unity;

namespace PlantillaVentaTests
{
    /// <summary>
    /// Nesto#524 (NestoAPI#626): «Aplicar oferta» con el regalo de una oferta combinada. Caso real: la 267
    /// «Oferta mascarillas PDRN», 6+1 de la 45664 y de regalo una 45665 que normalmente NO está en el pedido.
    /// </summary>
    [TestClass]
    public class RegaloOfertaCombinadaPlantillaTests
    {
        private const string MASCARILLA = "45664";
        private const string LIMPIADOR = "45665";

        private static (PlantillaVentaViewModel vm, IPlantillaVentaService servicio, IServicioDialogos dialogos) CrearViewModel()
        {
            IConfiguracion configuracion = A.Fake<IConfiguracion>();
            // Una dirección que no contesta: el precio y el stock que se piden al actualizar la línea fallan dentro de su Try
            A.CallTo(() => configuracion.servidorAPI).Returns("http://127.0.0.1:9/api/");
            A.CallTo(() => configuracion.LeerParametroSync(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.AlmacenRuta)).Returns("ALG");
            IServicioDialogos dialogos = A.Fake<IServicioDialogos>();
            IPlantillaVentaService servicio = A.Fake<IPlantillaVentaService>();
            var vm = new PlantillaVentaViewModel(A.Fake<IUnityContainer>(), A.Fake<IServicioNavegacion>(), configuracion, servicio,
                new WeakReferenceMessenger(), dialogos, A.Fake<IPedidoVentaService>(), A.Fake<IBorradorPlantillaVentaService>(),
                A.Fake<IServicioAutenticacion>());
            vm.ListaFiltrableProductos.ListaOriginal = new ObservableCollection<IFiltrableItem>();
            vm._clienteSeleccionado = new ClienteJson { empresa = "1", cliente = "10458", contacto = "0", iva = "G21", cifNif = "12345678A" };
            return (vm, servicio, dialogos);
        }

        private static LineaPlantillaVenta Mascarillas(PlantillaVentaViewModel vm)
        {
            var linea = new LineaPlantillaVenta { producto = MASCARILLA, texto = "MASCARILLA PDRN", cantidad = 6, cantidadOferta = 1, precio = 22M, cantidadVendida = 3 };
            vm.ListaFiltrableProductos.ListaOriginal.Add(linea);
            return linea;
        }

        private static SugerenciaOfertaDTO RegaloLimpiador(int unidades = 1) => new SugerenciaOfertaDTO
        {
            Tipo = TiposSugerenciaOferta.REGALO_COMBINADA_NO_APLICADO,
            Accion = AccionesSugerenciaOferta.ANADIR_LINEA,
            Producto = LIMPIADOR,
            CantidadRegalo = unidades,
            Descuento = 1M,
            OfertaCombinada = 267,
            Texto = "El pedido cumple la oferta «Oferta mascarillas PDRN»: le corresponde 1 unidad del producto 45665 (LIMPIADOR MOUSE PDRN) de regalo y no se está aplicando."
        };

        private static LineaPlantillaVenta Linea(PlantillaVentaViewModel vm, string producto) =>
            vm.ListaFiltrableProductos.ListaOriginal.OfType<LineaPlantillaVenta>().SingleOrDefault(l => l.producto == producto);

        [TestMethod]
        public void ElRegaloDeLaCombinada_SeAplicaDeUnClic()
        {
            Assert.IsTrue(RegaloLimpiador().EsAplicable);
            Assert.IsFalse(new SugerenciaOfertaDTO { Tipo = TiposSugerenciaOferta.REGALO_COMBINADA_NO_APLICADO, Producto = LIMPIADOR, CantidadRegalo = 1 }.EsAplicable,
                "Sin la acción de añadir línea no se sabe qué hacer");
            Assert.IsFalse(new SugerenciaOfertaDTO { Tipo = TiposSugerenciaOferta.REGALO_COMBINADA_NO_APLICADO, Accion = AccionesSugerenciaOferta.ANADIR_LINEA, Producto = LIMPIADOR }.EsAplicable,
                "Sin unidades no hay nada que añadir");
            Assert.IsTrue(new SugerenciaOfertaDTO { Tipo = TiposSugerenciaOferta.AMPLIAR_CANTIDAD_COMBINADA, Producto = "A", CantidadActual = 2, CantidadSugerida = 3, CantidadRegalo = 1 }.EsAplicable);
            Assert.IsFalse(new SugerenciaOfertaDTO { Tipo = TiposSugerenciaOferta.AMPLIAR_IMPORTE, Producto = "A", CantidadRegalo = 1 }.EsAplicable, "Las de importe siguen sin aplicarse solas");
        }

        [TestMethod]
        public async Task Pdrn267_ElLimpiadorNoEstaEnLaPlantilla_SeBuscaYSeAnadeComoRegalo()
        {
            var (vm, servicio, _) = CrearViewModel();
            LineaPlantillaVenta mascarillas = Mascarillas(vm);
            A.CallTo(() => servicio.BuscarLineaProducto("1", LIMPIADOR))
                .Returns(Task.FromResult(new LineaPlantillaVenta { producto = LIMPIADOR, texto = "LIMPIADOR MOUSE PDRN", precio = 13.8M }));

            await vm.AnadirRegaloSugeridoAsync(RegaloLimpiador());

            LineaPlantillaVenta limpiador = Linea(vm, LIMPIADOR);
            Assert.IsNotNull(limpiador, "El limpiador entra en el pedido");
            Assert.AreEqual(0, limpiador.cantidad, "No se cobra ninguna unidad");
            Assert.AreEqual(1, limpiador.cantidadOferta, "Va de regalo, como la unidad gratis de un N+M");
            Assert.IsTrue(vm.listaProductosPedido.Contains(limpiador));
            Assert.AreEqual(6, mascarillas.cantidad, "Las mascarillas no se tocan");
            Assert.AreEqual(1, mascarillas.cantidadOferta);
        }

        [TestMethod]
        public void Pdrn267_ElLimpiadorYaEstaEnLaPlantillaDelCliente_SeUsaEsaLineaSinBuscar()
        {
            var (vm, servicio, _) = CrearViewModel();
            Mascarillas(vm);
            var limpiador = new LineaPlantillaVenta { producto = LIMPIADOR, texto = "LIMPIADOR MOUSE PDRN", precio = 13.8M, cantidadVendida = 2 };
            vm.ListaFiltrableProductos.ListaOriginal.Add(limpiador);

            vm.OnAplicarOfertaSugerida(RegaloLimpiador());

            Assert.AreEqual(1, limpiador.cantidadOferta);
            Assert.AreEqual(0, limpiador.cantidad);
            A.CallTo(() => servicio.BuscarLineaProducto(A<string>._, A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task SiYaLlevaRegalosDelProducto_SeSumanLasQueFaltan()
        {
            var (vm, _, _) = CrearViewModel();
            var limpiador = new LineaPlantillaVenta { producto = LIMPIADOR, precio = 13.8M, cantidadOferta = 1, cantidadVendida = 2 };
            vm.ListaFiltrableProductos.ListaOriginal.Add(limpiador);

            await vm.AnadirRegaloSugeridoAsync(RegaloLimpiador());

            Assert.AreEqual(2, limpiador.cantidadOferta);
        }

        [TestMethod]
        public async Task ElRegaloNoSeEncuentra_AvisaYNoTocaNada()
        {
            var (vm, servicio, dialogos) = CrearViewModel();
            Mascarillas(vm);
            A.CallTo(() => servicio.BuscarLineaProducto("1", LIMPIADOR)).Returns(Task.FromResult<LineaPlantillaVenta>(null));

            await vm.AnadirRegaloSugeridoAsync(RegaloLimpiador());

            Assert.IsNull(Linea(vm, LIMPIADOR));
            A.CallTo(() => dialogos.ShowError(A<string>.That.Contains("Búsquelo a mano"))).MustHaveHappenedOnceExactly();
        }

        private static SugerenciaOfertaDTO RegaloDeGrupo() => new SugerenciaOfertaDTO
        {
            Tipo = TiposSugerenciaOferta.REGALO_COMBINADA_NO_APLICADO,
            Accion = AccionesSugerenciaOferta.ANADIR_LINEA,
            Producto = "B2",
            CantidadRegalo = 1,
            Alternativas = new List<string> { "B2", "B3" },
            Texto = "Le corresponde 1 unidad a elegir entre los productos B2, B3 de regalo."
        };

        [TestMethod]
        public async Task GrupoDeAlternativas_PreguntaCualYAnadeLaElegida()
        {
            var (vm, _, dialogos) = CrearViewModel();
            var b2 = new LineaPlantillaVenta { producto = "B2", precio = 5M, cantidadVendida = 1 };
            var b3 = new LineaPlantillaVenta { producto = "B3", precio = 5M, cantidadVendida = 1 };
            vm.ListaFiltrableProductos.ListaOriginal.Add(b2);
            vm.ListaFiltrableProductos.ListaOriginal.Add(b3);
            A.CallTo(() => dialogos.GetText(A<string>._, A<string>.That.Contains("B2, B3"), "B2")).Returns(" b3 ");

            await vm.AnadirRegaloSugeridoAsync(RegaloDeGrupo());

            Assert.AreEqual(0, b2.cantidadOferta);
            Assert.AreEqual(1, b3.cantidadOferta);
        }

        [TestMethod]
        public async Task GrupoDeAlternativas_SiCancela_NoSeAnadeNada()
        {
            var (vm, servicio, dialogos) = CrearViewModel();
            A.CallTo(() => dialogos.GetText(A<string>._, A<string>._, A<string>._)).Returns(null);

            await vm.AnadirRegaloSugeridoAsync(RegaloDeGrupo());

            Assert.IsFalse(vm.ListaFiltrableProductos.ListaOriginal.Any());
            A.CallTo(() => servicio.BuscarLineaProducto(A<string>._, A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task GrupoDeAlternativas_UnaReferenciaQueNoEsDeLaOferta_AvisaYNoAnade()
        {
            var (vm, servicio, dialogos) = CrearViewModel();
            A.CallTo(() => dialogos.GetText(A<string>._, A<string>._, A<string>._)).Returns("99999");

            await vm.AnadirRegaloSugeridoAsync(RegaloDeGrupo());

            A.CallTo(() => dialogos.ShowError(A<string>.That.Contains("no es ninguna de las de la oferta"))).MustHaveHappenedOnceExactly();
            A.CallTo(() => servicio.BuscarLineaProducto(A<string>._, A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public void AmpliarLaParteCobradaDeLaCombinada_SubeLaCantidadSinPonerRegaloEnEseProducto()
        {
            var (vm, _, _) = CrearViewModel();
            var linea = new LineaPlantillaVenta { producto = "A", cantidad = 2, precio = 10M, cantidadVendida = 1 };
            vm.ListaFiltrableProductos.ListaOriginal.Add(linea);

            vm.OnAplicarOfertaSugerida(new SugerenciaOfertaDTO
            {
                Tipo = TiposSugerenciaOferta.AMPLIAR_CANTIDAD_COMBINADA,
                Accion = AccionesSugerenciaOferta.MODIFICAR_LINEA,
                Producto = "A",
                CantidadActual = 2,
                CantidadSugerida = 3,
                CantidadRegalo = 1,
                OfertaCombinada = 300,
                Texto = "Con 1 unidad más del producto A completa la oferta «Tres cremas y regalo» y se lleva de regalo 1 unidad del producto B."
            });

            Assert.AreEqual(3, linea.cantidad);
            Assert.AreEqual(0, linea.cantidadOferta, "El regalo es de otro producto: lo propondrá la API cuando el pedido llegue");
        }
    }
}
