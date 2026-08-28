using FakeItEasy;
using Nesto.Infrastructure.Contracts;
using Nesto.Modules.Producto;
using Nesto.Modules.Producto.Models;
using Nesto.Modules.Producto.ViewModels;
using Prism.Events;
using Prism.Regions;
using Prism.Services.Dialogs;
using System.Windows.Controls;

namespace Producto.Tests
{
    [TestClass]
    public class ProductoViewModelTests
    {
        [TestMethod]
        public void ProductoViewModel_AlCambiarDeProducto_SiLaPestannaSeleccionadaEsKitsPeroElNuevoProductoNoEsKitSeleccionaOtraPestanna()
        {
            // Arrange
            var regionManager = A.Fake<IRegionManager>();
            var configuracion = A.Fake<IConfiguracion>();
            var servicio = A.Fake<IProductoService>();
            var eventAggregator = A.Fake<IEventAggregator>();
            var dialogService = A.Fake<IDialogService>();
            A.CallTo(() => servicio.LeerProducto("KIT")).Returns(new ProductoModel
            {
                Producto = "KIT",
                ProductosKit = new List<ProductoKit>()
                {
                    new ProductoKit
                    {
                        ProductoId = "OTRO_PROD",
                        Cantidad = 1
                    }
                }
            });
            A.CallTo(() => servicio.LeerProducto("NO_KIT")).Returns(new ProductoModel
            {
                Producto = "NO_KIT"
            });
            var servicioAutenticacion = A.Fake<IServicioAutenticacion>();
            var sut = new ProductoViewModel(regionManager, configuracion, servicio, eventAggregator, dialogService, servicioAutenticacion);
            sut.ReferenciaBuscar = "KIT";
            sut.PestannaSeleccionada = Pestannas.Kits;

            // Act
            sut.ReferenciaBuscar = "NO_KIT";

            // Assert
            Assert.AreEqual(Pestannas.Filtros, sut.PestannaSeleccionada);
        }

        // Issue #341: la búsqueda contextual debe respetar los filtros de Familia
        // y Subgrupo activos en el panel de filtros (Contains case-insensitive).

        [TestMethod]
        public void OnBuscarContextual_ConFiltroFamilia_FiltraResultadosPorFamilia()
        {
            // Arrange
            var sut = CrearViewModelConContextualesMockeados(out _, out var servicio);
            ICollection<ProductoModel> resultadoServicio = new List<ProductoModel>
            {
                CrearProductoConStock("P1", "Eva Visnú", "Cremas"),
                CrearProductoConStock("P2", "Lisap", "Cremas"),
                CrearProductoConStock("P3", "Eva Visnú", "Otros")
            };
            A.CallTo(() => servicio.BuscarProductosContextual("crema", false))
                .Returns(Task.FromResult(resultadoServicio));

            sut.FiltroFamilia = "Eva Visnú";

            // Act
            sut.BuscarContextualCommand.Execute("crema");

            // Assert
            var lista = sut.ProductosResultadoBusqueda.Lista.Cast<ProductoModel>().ToList();
            Assert.AreEqual(2, lista.Count);
            CollectionAssert.AreEquivalent(new[] { "P1", "P3" }, lista.Select(p => p.Producto).ToList());
        }

        [TestMethod]
        public void OnBuscarContextual_ConFiltroSubgrupo_FiltraResultadosPorSubgrupo()
        {
            // Arrange
            var sut = CrearViewModelConContextualesMockeados(out _, out var servicio);
            ICollection<ProductoModel> resultadoServicio = new List<ProductoModel>
            {
                CrearProductoConStock("P1", "Eva Visnú", "Cremas"),
                CrearProductoConStock("P2", "Lisap", "Cremas"),
                CrearProductoConStock("P3", "Eva Visnú", "Otros")
            };
            A.CallTo(() => servicio.BuscarProductosContextual("crema", false))
                .Returns(Task.FromResult(resultadoServicio));

            sut.FiltroSubgrupo = "cremas"; // case insensitive

            // Act
            sut.BuscarContextualCommand.Execute("crema");

            // Assert
            var lista = sut.ProductosResultadoBusqueda.Lista.Cast<ProductoModel>().ToList();
            Assert.AreEqual(2, lista.Count);
            CollectionAssert.AreEquivalent(new[] { "P1", "P2" }, lista.Select(p => p.Producto).ToList());
        }

        [TestMethod]
        public void OnBuscarContextual_SinFiltros_DevuelveTodosLosResultados()
        {
            // Arrange
            var sut = CrearViewModelConContextualesMockeados(out _, out var servicio);
            ICollection<ProductoModel> resultadoServicio = new List<ProductoModel>
            {
                CrearProductoConStock("P1", "Eva Visnú", "Cremas"),
                CrearProductoConStock("P2", "Lisap", "Cremas")
            };
            A.CallTo(() => servicio.BuscarProductosContextual("crema", false))
                .Returns(Task.FromResult(resultadoServicio));

            // Act
            sut.BuscarContextualCommand.Execute("crema");

            // Assert
            Assert.AreEqual(2, sut.ProductosResultadoBusqueda.Lista.Count);
        }

        // ----- Helpers -----

        // NestoAPI#421: "exclusivo profesional" es un dato de la ficha del producto, no una
        // deducción de sus categorías. La casilla de la pestaña Tienda lee y escribe ese dato.

        [TestMethod]
        public void ProductoViewModel_AlCargarElProducto_LaCasillaRefleja_LoQueDiceLaFicha()
        {
            var sut = CrearViewModelConContextualesMockeados(out _, out var servicio);
            A.CallTo(() => servicio.LeerProducto("41269")).Returns(new ProductoModel
            {
                Producto = "41269",
                ExclusivoProfesional = true
            });

            sut.ReferenciaBuscar = "41269";

            Assert.IsTrue(sut.ExclusivoProfesional);
        }

        [TestMethod]
        public void ProductoViewModel_UnProductoDeUnSubgrupoEP_NoSeMarcaSolo()
        {
            // La razón de ser de #421: los subgrupos "Exclusivo Profesional" (COS/EPC, APA/EXP...)
            // son categorías navegables normales. Un producto suyo se vende al público mientras
            // nadie marque la casilla a mano.
            var sut = CrearViewModelConContextualesMockeados(out _, out var servicio);
            A.CallTo(() => servicio.LeerProducto("41269")).Returns(new ProductoModel
            {
                Producto = "41269",
                Grupo = "APA",
                Subgrupo = "Aparatología Exclusiva Profesional",
                ExclusivoProfesional = false
            });

            sut.ReferenciaBuscar = "41269";

            Assert.IsFalse(sut.ExclusivoProfesional);
        }

        [TestMethod]
        public void ProductoViewModel_AlGuardar_MandaLaMarcaAlServicioYActualizaLaFicha()
        {
            var sut = CrearViewModelConContextualesMockeados(out _, out var servicio);
            A.CallTo(() => servicio.LeerProducto("41269")).Returns(new ProductoModel
            {
                Producto = "41269",
                ExclusivoProfesional = false
            });
            sut.ReferenciaBuscar = "41269";

            sut.ExclusivoProfesional = true;
            sut.GuardarExclusivoProfesionalCommand.Execute();

            A.CallTo(() => servicio.GuardarExclusivoProfesional("41269", true)).MustHaveHappenedOnceExactly();
            Assert.IsTrue(sut.ProductoActual.ExclusivoProfesional);
        }

        [TestMethod]
        public void ProductoViewModel_AlDesmarcar_TambienSeGuarda()
        {
            // Desmarcar tiene que llegar al servicio igual que marcar: si no, un producto marcado
            // por error se queda sin venderse al público para siempre.
            var sut = CrearViewModelConContextualesMockeados(out _, out var servicio);
            A.CallTo(() => servicio.LeerProducto("41269")).Returns(new ProductoModel
            {
                Producto = "41269",
                ExclusivoProfesional = true
            });
            sut.ReferenciaBuscar = "41269";

            sut.ExclusivoProfesional = false;
            sut.GuardarExclusivoProfesionalCommand.Execute();

            A.CallTo(() => servicio.GuardarExclusivoProfesional("41269", false)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void ProductoViewModel_CargarLaFicha_NoGuardaNada()
        {
            // La casilla se guarda con su botón. Si el simple hecho de abrir una ficha guardara,
            // cualquier consulta escribiría en la ficha y republicaría el producto a la tienda.
            var sut = CrearViewModelConContextualesMockeados(out _, out var servicio);
            A.CallTo(() => servicio.LeerProducto("41269")).Returns(new ProductoModel
            {
                Producto = "41269",
                ExclusivoProfesional = true
            });

            sut.ReferenciaBuscar = "41269";

            A.CallTo(() => servicio.GuardarExclusivoProfesional(A<string>._, A<bool>._)).MustNotHaveHappened();
        }

        // Nesto#456 / NestoAPI#414: categorías secundarias en la pestaña Web. Quienes las mantienen
        // son Laura y Enrique, no informática, así que se ven con el código delante y el orden de
        // la lista es el que viaja a la tienda.

        private static ProductoViewModel CrearViewModelConCategorias(out IProductoService servicio)
        {
            // El fake va a una local y se copia al out al final: C# no deja usar un parámetro out
            // dentro de las lambdas de FakeItEasy.
            var sut = CrearViewModelConContextualesMockeados(out _, out IProductoService fake);
            A.CallTo(() => fake.LeerSubgruposProducto()).Returns(new List<SubgrupoProductoModel>
            {
                new SubgrupoProductoModel { Grupo = "COS", Subgrupo = "CRE", Nombre = "Cremas" },
                new SubgrupoProductoModel { Grupo = "COS", Subgrupo = "OFE", Nombre = "Ofertas Estética" },
                new SubgrupoProductoModel { Grupo = "COS", Subgrupo = "EPC", Nombre = "Corporales Exclusivos Profesional" },
                new SubgrupoProductoModel { Grupo = "APA", Subgrupo = "EXP", Nombre = "Aparatología Exclusiva Profesional" }
            });
            A.CallTo(() => fake.LeerProducto("41269")).Returns(new ProductoModel
            {
                Producto = "41269",
                Grupo = "COS",
                Subgrupo = "Cremas"        // OJO: la ficha trae la DESCRIPCIÓN, no el código
            });
            servicio = fake;
            return sut;
        }

        [TestMethod]
        public void ProductoViewModel_AlCargar_LasCategoriasSecundariasLleganEnSuOrden()
        {
            var sut = CrearViewModelConCategorias(out var servicio);
            A.CallTo(() => servicio.LeerCategoriasSecundarias("41269")).Returns(new List<CategoriaSecundariaModel>
            {
                new CategoriaSecundariaModel { Grupo = "COS", Subgrupo = "OFE", DescripcionSubgrupo = "Ofertas Estética" },
                new CategoriaSecundariaModel { Grupo = "APA", Subgrupo = "EXP", DescripcionSubgrupo = "Aparatología Exclusiva Profesional" }
            });

            sut.ReferenciaBuscar = "41269";

            Assert.AreEqual(2, sut.CategoriasSecundarias.Count);
            Assert.AreEqual("COS/OFE — Ofertas Estética", sut.CategoriasSecundarias[0].Descripcion);
            Assert.AreEqual("APA/EXP — Aparatología Exclusiva Profesional", sut.CategoriasSecundarias[1].Descripcion);
        }

        [TestMethod]
        public void ProductoViewModel_LaCategoriaPrincipalSeVeConSuCodigo()
        {
            // La ficha solo trae la descripción del subgrupo; el código se resuelve del catálogo
            // para que la principal se lea igual que las secundarias.
            var sut = CrearViewModelConCategorias(out _);

            sut.ReferenciaBuscar = "41269";

            Assert.AreEqual("COS/CRE — Cremas", sut.CategoriaPrincipalTexto);
        }

        [TestMethod]
        public void ProductoViewModel_ElSubgrupoPrincipal_NoSePuedeAnnadirComoSecundario()
        {
            var sut = CrearViewModelConCategorias(out _);
            sut.ReferenciaBuscar = "41269";

            sut.GrupoWebSeleccionado = "COS";

            CollectionAssert.AreEquivalent(
                new[] { "COS/EPC — Corporales Exclusivos Profesional", "COS/OFE — Ofertas Estética" },
                sut.SubgruposDelGrupoWeb.Select(sg => sg.Descripcion).ToArray());
        }

        [TestMethod]
        public void ProductoViewModel_AnnadirCategoria_LaPoneAlFinal()
        {
            var sut = CrearViewModelConCategorias(out var servicio);
            A.CallTo(() => servicio.LeerCategoriasSecundarias("41269")).Returns(new List<CategoriaSecundariaModel>
            {
                new CategoriaSecundariaModel { Grupo = "COS", Subgrupo = "OFE", DescripcionSubgrupo = "Ofertas Estética" }
            });
            sut.ReferenciaBuscar = "41269";

            sut.GrupoWebSeleccionado = "APA";
            sut.SubgrupoWebSeleccionado = sut.SubgruposDelGrupoWeb.Single();
            sut.AnnadirCategoriaSecundariaCommand.Execute();

            Assert.AreEqual(2, sut.CategoriasSecundarias.Count);
            Assert.AreEqual("APA/EXP — Aparatología Exclusiva Profesional", sut.CategoriasSecundarias[1].Descripcion);
        }

        [TestMethod]
        public void ProductoViewModel_AnnadirLaMismaCategoriaDosVeces_NoLaDuplica()
        {
            // El API rechaza los duplicados, pero avisar aquí ahorra el viaje y el error feo.
            var sut = CrearViewModelConCategorias(out var servicio);
            A.CallTo(() => servicio.LeerCategoriasSecundarias("41269")).Returns(new List<CategoriaSecundariaModel>
            {
                new CategoriaSecundariaModel { Grupo = "APA", Subgrupo = "EXP", DescripcionSubgrupo = "Aparatología Exclusiva Profesional" }
            });
            sut.ReferenciaBuscar = "41269";

            sut.GrupoWebSeleccionado = "APA";
            sut.SubgrupoWebSeleccionado = sut.SubgruposDelGrupoWeb.Single();
            sut.AnnadirCategoriaSecundariaCommand.Execute();

            Assert.AreEqual(1, sut.CategoriasSecundarias.Count);
        }

        [TestMethod]
        public void ProductoViewModel_SubirYBajar_CambianElOrdenQueViajaALaWeb()
        {
            var sut = CrearViewModelConCategorias(out var servicio);
            A.CallTo(() => servicio.LeerCategoriasSecundarias("41269")).Returns(new List<CategoriaSecundariaModel>
            {
                new CategoriaSecundariaModel { Grupo = "COS", Subgrupo = "OFE", DescripcionSubgrupo = "Ofertas Estética" },
                new CategoriaSecundariaModel { Grupo = "APA", Subgrupo = "EXP", DescripcionSubgrupo = "Aparatología Exclusiva Profesional" }
            });
            sut.ReferenciaBuscar = "41269";

            sut.CategoriaSecundariaSeleccionada = sut.CategoriasSecundarias[1];
            sut.SubirCategoriaSecundariaCommand.Execute();

            Assert.AreEqual("APA", sut.CategoriasSecundarias[0].Grupo);
            Assert.AreSame(sut.CategoriasSecundarias[0], sut.CategoriaSecundariaSeleccionada, "No se pierde la selección al mover");

            sut.BajarCategoriaSecundariaCommand.Execute();

            Assert.AreEqual("COS", sut.CategoriasSecundarias[0].Grupo);
        }

        [TestMethod]
        public void ProductoViewModel_GuardarCategorias_MandaLaListaCompletaEnOrden()
        {
            var sut = CrearViewModelConCategorias(out var servicio);
            A.CallTo(() => servicio.LeerCategoriasSecundarias("41269")).Returns(new List<CategoriaSecundariaModel>
            {
                new CategoriaSecundariaModel { Grupo = "COS", Subgrupo = "OFE", DescripcionSubgrupo = "Ofertas Estética" },
                new CategoriaSecundariaModel { Grupo = "APA", Subgrupo = "EXP", DescripcionSubgrupo = "Aparatología Exclusiva Profesional" }
            });
            List<CategoriaSecundariaModel>? enviadas = null;
            A.CallTo(() => servicio.GuardarCategoriasSecundarias("41269", A<List<CategoriaSecundariaModel>>._))
                .Invokes((string _, List<CategoriaSecundariaModel> lista) => enviadas = lista);
            sut.ReferenciaBuscar = "41269";

            sut.CategoriaSecundariaSeleccionada = sut.CategoriasSecundarias[1];
            sut.SubirCategoriaSecundariaCommand.Execute();
            sut.GuardarCategoriasSecundariasCommand.Execute();

            Assert.IsNotNull(enviadas);
            CollectionAssert.AreEqual(new[] { "APA", "COS" }, enviadas!.Select(c => c.Grupo).ToArray());
        }

        [TestMethod]
        public void ProductoViewModel_QuitarTodas_GuardaListaVacia()
        {
            // Quitar todas es una operación legítima: la web retira las secundarias del producto.
            var sut = CrearViewModelConCategorias(out var servicio);
            A.CallTo(() => servicio.LeerCategoriasSecundarias("41269")).Returns(new List<CategoriaSecundariaModel>
            {
                new CategoriaSecundariaModel { Grupo = "COS", Subgrupo = "OFE", DescripcionSubgrupo = "Ofertas Estética" }
            });
            List<CategoriaSecundariaModel>? enviadas = null;
            A.CallTo(() => servicio.GuardarCategoriasSecundarias("41269", A<List<CategoriaSecundariaModel>>._))
                .Invokes((string _, List<CategoriaSecundariaModel> lista) => enviadas = lista);
            sut.ReferenciaBuscar = "41269";

            sut.CategoriaSecundariaSeleccionada = sut.CategoriasSecundarias[0];
            sut.QuitarCategoriaSecundariaCommand.Execute();
            sut.GuardarCategoriasSecundariasCommand.Execute();

            Assert.IsNotNull(enviadas);
            Assert.AreEqual(0, enviadas!.Count);
        }

        [TestMethod]
        public void ProductoViewModel_SiFallaLaCargaDeCategorias_LaFichaSeAbreIgual()
        {
            // La pestaña Web no puede tumbar la ficha: la mayoría entra aquí a mirar stock.
            var sut = CrearViewModelConCategorias(out var servicio);
            A.CallTo(() => servicio.LeerCategoriasSecundarias("41269")).Throws(new Exception("la API está caída"));

            sut.ReferenciaBuscar = "41269";

            Assert.IsNotNull(sut.ProductoActual);
            Assert.AreEqual("41269", sut.ProductoActual.Producto);
        }

        private static ProductoViewModel CrearViewModelConContextualesMockeados(
            out IConfiguracion configuracion,
            out IProductoService servicio)
        {
            var regionManager = A.Fake<IRegionManager>();
            configuracion = A.Fake<IConfiguracion>();
            servicio = A.Fake<IProductoService>();
            var eventAggregator = A.Fake<IEventAggregator>();
            var dialogService = A.Fake<IDialogService>();
            var servicioAutenticacion = A.Fake<IServicioAutenticacion>();
            return new ProductoViewModel(regionManager, configuracion, servicio, eventAggregator, dialogService, servicioAutenticacion);
        }

        private static ProductoModel CrearProductoConStock(string id, string familia, string subgrupo)
        {
            return new ProductoModel
            {
                Producto = id,
                Nombre = $"Producto {id}",
                Familia = familia,
                Subgrupo = subgrupo,
                Stocks = new List<ProductoModel.StockProducto>
                {
                    new ProductoModel.StockProducto { Stock = 10, CantidadDisponible = 10 }
                }
            };
        }
    }
}
