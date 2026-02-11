using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Events;
using Nesto.Infrastructure.Shared;
using Nesto.Modulos.PedidoVenta;
using Nesto.Modulos.PlantillaVenta;
using Prism.Events;
using Prism.Regions;
using Prism.Services.Dialogs;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Unity;

namespace PlantillaVentaTests
{
    /// <summary>
    /// Tests para el sistema de Ganavisiones - FASES 7 y 8
    /// Issue #94
    /// </summary>
    [TestClass]
    public class GanavisionesTests
    {
        private PlantillaVentaViewModel CrearViewModel(bool conProductosBonificables = true)
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

            var vm = new PlantillaVentaViewModel(container, regionManager, configuracion, servicio, eventAggregator, dialogService, pedidoVentaService, servicioBorradores);
            vm.ListaFiltrableProductos.ListaOriginal = new ObservableCollection<IFiltrableItem>();

            // Configurar productos bonificables usando reflexión (campo privado _productosBonificablesIds)
            if (conProductosBonificables)
            {
                var productosBonificablesIds = new HashSet<string> { "PROD_BONIFICABLE_1", "PROD_BONIFICABLE_2" };
                var field = typeof(PlantillaVentaViewModel).GetField("_productosBonificablesIds",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                field?.SetValue(vm, productosBonificablesIds);
            }

            return vm;
        }

        #region BaseImponibleBonificable Tests

        [TestMethod]
        public void BaseImponibleBonificable_SoloSumaProductosCOS()
        {
            // Arrange
            var vm = CrearViewModel();
            vm.ListaFiltrableProductos.ListaOriginal.Add(new LineaPlantillaVenta
            {
                grupo = "COS",
                cantidad = 1,
                precio = 100M,
                descuento = 0M
            });

            // Act
            decimal baseImponible = vm.BaseImponibleBonificable;

            // Assert
            Assert.AreEqual(100M, baseImponible);
        }

        [TestMethod]
        public void BaseImponibleBonificable_SoloSumaProductosACC()
        {
            // Arrange
            var vm = CrearViewModel();
            vm.ListaFiltrableProductos.ListaOriginal.Add(new LineaPlantillaVenta
            {
                grupo = "ACC",
                cantidad = 2,
                precio = 50M,
                descuento = 0M
            });

            // Act
            decimal baseImponible = vm.BaseImponibleBonificable;

            // Assert
            Assert.AreEqual(100M, baseImponible);
        }

        [TestMethod]
        public void BaseImponibleBonificable_SoloSumaProductosPEL()
        {
            // Arrange
            var vm = CrearViewModel();
            vm.ListaFiltrableProductos.ListaOriginal.Add(new LineaPlantillaVenta
            {
                grupo = "PEL",
                cantidad = 1,
                precio = 75M,
                descuento = 0M
            });

            // Act
            decimal baseImponible = vm.BaseImponibleBonificable;

            // Assert
            Assert.AreEqual(75M, baseImponible);
        }

        [TestMethod]
        public void BaseImponibleBonificable_IgnoraGrupoAPA()
        {
            // Arrange
            var vm = CrearViewModel();
            vm.ListaFiltrableProductos.ListaOriginal.Add(new LineaPlantillaVenta
            {
                grupo = "APA", // Aparatos no son bonificables
                cantidad = 1,
                precio = 500M,
                descuento = 0M
            });

            // Act
            decimal baseImponible = vm.BaseImponibleBonificable;

            // Assert
            Assert.AreEqual(0M, baseImponible);
        }

        [TestMethod]
        public void BaseImponibleBonificable_IgnoraGrupoOtros()
        {
            // Arrange
            var vm = CrearViewModel();
            vm.ListaFiltrableProductos.ListaOriginal.Add(new LineaPlantillaVenta
            {
                grupo = "OTR",
                cantidad = 1,
                precio = 200M,
                descuento = 0M
            });

            // Act
            decimal baseImponible = vm.BaseImponibleBonificable;

            // Assert
            Assert.AreEqual(0M, baseImponible);
        }

        [TestMethod]
        public void BaseImponibleBonificable_SumaMultiplesGruposBonificables()
        {
            // Arrange
            var vm = CrearViewModel();
            vm.ListaFiltrableProductos.ListaOriginal.Add(new LineaPlantillaVenta
            {
                grupo = "COS",
                cantidad = 1,
                precio = 30M,
                descuento = 0M
            });
            vm.ListaFiltrableProductos.ListaOriginal.Add(new LineaPlantillaVenta
            {
                grupo = "ACC",
                cantidad = 1,
                precio = 40M,
                descuento = 0M
            });
            vm.ListaFiltrableProductos.ListaOriginal.Add(new LineaPlantillaVenta
            {
                grupo = "PEL",
                cantidad = 1,
                precio = 30M,
                descuento = 0M
            });

            // Act
            decimal baseImponible = vm.BaseImponibleBonificable;

            // Assert
            Assert.AreEqual(100M, baseImponible); // 30 + 40 + 30
        }

        [TestMethod]
        public void BaseImponibleBonificable_ExcluyeNoBonificables()
        {
            // Arrange
            var vm = CrearViewModel();
            vm.ListaFiltrableProductos.ListaOriginal.Add(new LineaPlantillaVenta
            {
                grupo = "COS",
                cantidad = 1,
                precio = 50M,
                descuento = 0M
            });
            vm.ListaFiltrableProductos.ListaOriginal.Add(new LineaPlantillaVenta
            {
                grupo = "APA", // No bonificable
                cantidad = 1,
                precio = 500M,
                descuento = 0M
            });

            // Act
            decimal baseImponible = vm.BaseImponibleBonificable;

            // Assert
            Assert.AreEqual(50M, baseImponible); // Solo el COS
        }

        [TestMethod]
        public void BaseImponibleBonificable_AplicaDescuentoCorrectamente()
        {
            // Arrange
            var vm = CrearViewModel();
            vm.ListaFiltrableProductos.ListaOriginal.Add(new LineaPlantillaVenta
            {
                grupo = "COS",
                cantidad = 1,
                precio = 100M,
                descuento = 0.10M // 10% descuento
            });

            // Act
            decimal baseImponible = vm.BaseImponibleBonificable;

            // Assert
            Assert.AreEqual(90M, baseImponible); // 100 - 10
        }

        [TestMethod]
        public void BaseImponibleBonificable_GrupoConEspacios_FuncionaCorrectamente()
        {
            // Arrange
            var vm = CrearViewModel();
            vm.ListaFiltrableProductos.ListaOriginal.Add(new LineaPlantillaVenta
            {
                grupo = " COS ", // Con espacios
                cantidad = 1,
                precio = 100M,
                descuento = 0M
            });

            // Act
            decimal baseImponible = vm.BaseImponibleBonificable;

            // Assert
            Assert.AreEqual(100M, baseImponible);
        }

        [TestMethod]
        public void BaseImponibleBonificable_GrupoMinusculas_FuncionaCorrectamente()
        {
            // Arrange
            var vm = CrearViewModel();
            vm.ListaFiltrableProductos.ListaOriginal.Add(new LineaPlantillaVenta
            {
                grupo = "cos", // Minúsculas
                cantidad = 1,
                precio = 100M,
                descuento = 0M
            });

            // Act
            decimal baseImponible = vm.BaseImponibleBonificable;

            // Assert
            Assert.AreEqual(100M, baseImponible);
        }

        [TestMethod]
        public void BaseImponibleBonificable_ListaVacia_DevuelveCero()
        {
            // Arrange
            var vm = CrearViewModel();
            // Lista vacía

            // Act
            decimal baseImponible = vm.BaseImponibleBonificable;

            // Assert
            Assert.AreEqual(0M, baseImponible);
        }

        #endregion

        #region GanavisionesDisponibles Tests

        [TestMethod]
        public void GanavisionesDisponibles_DiezEuros_DevuelveUno()
        {
            // Arrange
            var vm = CrearViewModel();
            vm.ListaFiltrableProductos.ListaOriginal.Add(new LineaPlantillaVenta
            {
                grupo = "COS",
                cantidad = 1,
                precio = 10M,
                descuento = 0M
            });

            // Act
            int ganavisiones = vm.GanavisionesDisponibles;

            // Assert
            Assert.AreEqual(1, ganavisiones);
        }

        [TestMethod]
        public void GanavisionesDisponibles_NueveEuros_DevuelveCero()
        {
            // Arrange
            var vm = CrearViewModel();
            vm.ListaFiltrableProductos.ListaOriginal.Add(new LineaPlantillaVenta
            {
                grupo = "COS",
                cantidad = 1,
                precio = 9.99M,
                descuento = 0M
            });

            // Act
            int ganavisiones = vm.GanavisionesDisponibles;

            // Assert
            Assert.AreEqual(0, ganavisiones);
        }

        [TestMethod]
        public void GanavisionesDisponibles_CienEuros_DevuelveDiez()
        {
            // Arrange
            var vm = CrearViewModel();
            vm.ListaFiltrableProductos.ListaOriginal.Add(new LineaPlantillaVenta
            {
                grupo = "COS",
                cantidad = 1,
                precio = 100M,
                descuento = 0M
            });

            // Act
            int ganavisiones = vm.GanavisionesDisponibles;

            // Assert
            Assert.AreEqual(10, ganavisiones);
        }

        [TestMethod]
        public void GanavisionesDisponibles_TruncaDecimales()
        {
            // Arrange
            var vm = CrearViewModel();
            vm.ListaFiltrableProductos.ListaOriginal.Add(new LineaPlantillaVenta
            {
                grupo = "COS",
                cantidad = 1,
                precio = 25M, // 2.5 Ganavisiones -> trunca a 2
                descuento = 0M
            });

            // Act
            int ganavisiones = vm.GanavisionesDisponibles;

            // Assert
            Assert.AreEqual(2, ganavisiones);
        }

        #endregion

        #region HayGanavisionesDisponibles Tests

        [TestMethod]
        public void HayGanavisionesDisponibles_ConGanavisiones_DevuelveTrue()
        {
            // Arrange
            var vm = CrearViewModel();
            vm.ListaFiltrableProductos.ListaOriginal.Add(new LineaPlantillaVenta
            {
                grupo = "COS",
                cantidad = 1,
                precio = 10M,
                descuento = 0M
            });

            // Act
            bool hayGanavisiones = vm.HayGanavisionesDisponibles;

            // Assert
            Assert.IsTrue(hayGanavisiones);
        }

        [TestMethod]
        public void HayGanavisionesDisponibles_SinGanavisiones_DevuelveFalse()
        {
            // Arrange
            var vm = CrearViewModel();
            vm.ListaFiltrableProductos.ListaOriginal.Add(new LineaPlantillaVenta
            {
                grupo = "COS",
                cantidad = 1,
                precio = 5M, // Solo 5€, no llega a 1 Ganavision
                descuento = 0M
            });

            // Act
            bool hayGanavisiones = vm.HayGanavisionesDisponibles;

            // Assert
            Assert.IsFalse(hayGanavisiones);
        }

        [TestMethod]
        public void HayGanavisionesDisponibles_ListaVacia_DevuelveFalse()
        {
            // Arrange
            var vm = CrearViewModel();

            // Act
            bool hayGanavisiones = vm.HayGanavisionesDisponibles;

            // Assert
            Assert.IsFalse(hayGanavisiones);
        }

        [TestMethod]
        public void HayGanavisionesDisponibles_SinProductosBonificablesEnTabla_DevuelveFalse()
        {
            // Arrange: Hay puntos disponibles pero no hay productos en la tabla Ganavisiones
            var vm = CrearViewModel(conProductosBonificables: false);
            vm.ListaFiltrableProductos.ListaOriginal.Add(new LineaPlantillaVenta
            {
                grupo = "COS",
                cantidad = 1,
                precio = 100M, // 10 Ganavisiones disponibles
                descuento = 0M
            });

            // Act
            bool hayGanavisiones = vm.HayGanavisionesDisponibles;

            // Assert: False porque no hay productos bonificables en la tabla
            Assert.IsFalse(hayGanavisiones);
        }

        [TestMethod]
        public void HayGanavisionesDisponibles_ConProductosBonificablesEnTabla_DevuelveTrue()
        {
            // Arrange: Hay puntos disponibles Y hay productos en la tabla Ganavisiones
            var vm = CrearViewModel(conProductosBonificables: true);
            vm.ListaFiltrableProductos.ListaOriginal.Add(new LineaPlantillaVenta
            {
                grupo = "COS",
                cantidad = 1,
                precio = 100M, // 10 Ganavisiones disponibles
                descuento = 0M
            });

            // Act
            bool hayGanavisiones = vm.HayGanavisionesDisponibles;

            // Assert: True porque hay puntos Y productos bonificables
            Assert.IsTrue(hayGanavisiones);
        }

        #endregion

        #region GanavisionesExcedidos Tests

        [TestMethod]
        public void GanavisionesExcedidos_SinRegalos_DevuelveFalse()
        {
            // Arrange
            var vm = CrearViewModel();
            vm.ListaFiltrableProductos.ListaOriginal.Add(new LineaPlantillaVenta
            {
                grupo = "COS",
                cantidad = 1,
                precio = 100M,
                descuento = 0M
            });
            // No hay regalos seleccionados

            // Act
            bool excedidos = vm.GanavisionesExcedidos;

            // Assert
            Assert.IsFalse(excedidos);
        }

        #endregion

        #region PuedePasarDePaginaRegalos Tests

        [TestMethod]
        public void PuedePasarDePaginaRegalos_SinExceder_DevuelveTrue()
        {
            // Arrange
            var vm = CrearViewModel();
            vm.ListaFiltrableProductos.ListaOriginal.Add(new LineaPlantillaVenta
            {
                grupo = "COS",
                cantidad = 1,
                precio = 100M,
                descuento = 0M
            });

            // Act
            bool puedePasar = vm.PuedePasarDePaginaRegalos;

            // Assert
            Assert.IsTrue(puedePasar);
        }

        #endregion

        #region FASE 8: ListaRegalosSeleccionados Tests

        [TestMethod]
        public void ListaRegalosSeleccionados_ConListaNull_DevuelveListaVacia()
        {
            // Arrange
            var vm = CrearViewModel();
            // ListaProductosBonificables no se ha inicializado (es null)

            // Act
            var regalos = vm.ListaRegalosSeleccionados;

            // Assert
            Assert.IsNotNull(regalos);
            Assert.AreEqual(0, regalos.Count);
        }

        [TestMethod]
        public void ListaRegalosSeleccionados_SoloDevuelveRegalosConCantidadMayorQueCero()
        {
            // Arrange
            var vm = CrearViewModel();
            vm.ListaProductosBonificables = new System.Collections.ObjectModel.ObservableCollection<LineaRegalo>
            {
                new LineaRegalo { producto = "PROD1", texto = "Producto 1", precio = 10M, cantidad = 0 },
                new LineaRegalo { producto = "PROD2", texto = "Producto 2", precio = 20M, cantidad = 2 },
                new LineaRegalo { producto = "PROD3", texto = "Producto 3", precio = 15M, cantidad = 1 }
            };

            // Act
            var regalos = vm.ListaRegalosSeleccionados;

            // Assert
            Assert.AreEqual(2, regalos.Count);
            Assert.IsTrue(regalos.Any(r => r.producto == "PROD2"));
            Assert.IsTrue(regalos.Any(r => r.producto == "PROD3"));
            Assert.IsFalse(regalos.Any(r => r.producto == "PROD1"));
        }

        [TestMethod]
        public void ListaRegalosSeleccionados_ListaVacia_DevuelveListaVacia()
        {
            // Arrange
            var vm = CrearViewModel();
            vm.ListaProductosBonificables = new System.Collections.ObjectModel.ObservableCollection<LineaRegalo>();

            // Act
            var regalos = vm.ListaRegalosSeleccionados;

            // Assert
            Assert.IsNotNull(regalos);
            Assert.AreEqual(0, regalos.Count);
        }

        #endregion

        #region FASE 8: Texto Bonificado Tests

        [TestMethod]
        public void TextoBonificado_SeAñadeSufijo()
        {
            // Este test documenta el comportamiento esperado de PrepararPedido
            // El texto del regalo debe terminar con " (BONIFICADO)"
            string textoOriginal = "Champú anticaspa";
            string textoEsperado = "Champú anticaspa (BONIFICADO)";

            string textoBonificado = textoOriginal + " (BONIFICADO)";

            Assert.AreEqual(textoEsperado, textoBonificado);
        }

        [TestMethod]
        public void TextoBonificado_SeTruncaA50Caracteres()
        {
            // Este test documenta el comportamiento de truncado
            // Textos muy largos se truncan a 50 caracteres
            string textoOriginal = "Este es un nombre de producto muy largo que excede el límite";
            string textoBonificado = textoOriginal + " (BONIFICADO)";

            if (textoBonificado.Length > 50)
            {
                textoBonificado = textoBonificado.Substring(0, 50);
            }

            Assert.AreEqual(50, textoBonificado.Length);
            Assert.IsTrue(textoBonificado.StartsWith("Este es un nombre de producto muy largo que excede"));
        }

        #endregion

        #region FASE 9: ValidarServirJunto Tests

        [TestMethod]
        public void ValidarServirJuntoRequest_ConstruyeCorrectamente()
        {
            // Arrange & Act
            var request = new ValidarServirJuntoRequest
            {
                Almacen = "ALG",
                ProductosBonificados = new System.Collections.Generic.List<string> { "PROD1", "PROD2" }
            };

            // Assert
            Assert.AreEqual("ALG", request.Almacen);
            Assert.AreEqual(2, request.ProductosBonificados.Count);
            Assert.IsTrue(request.ProductosBonificados.Contains("PROD1"));
            Assert.IsTrue(request.ProductosBonificados.Contains("PROD2"));
        }

        [TestMethod]
        public void ValidarServirJuntoResponse_PuedeDesmarcar_True()
        {
            // Arrange & Act
            var response = new ValidarServirJuntoResponse
            {
                PuedeDesmarcar = true,
                ProductosProblematicos = new System.Collections.Generic.List<ProductoSinStockDTO>(),
                Mensaje = null
            };

            // Assert
            Assert.IsTrue(response.PuedeDesmarcar);
            Assert.AreEqual(0, response.ProductosProblematicos.Count);
            Assert.IsNull(response.Mensaje);
        }

        [TestMethod]
        public void ValidarServirJuntoResponse_NoPuedeDesmarcar_ConMensaje()
        {
            // Arrange & Act
            var response = new ValidarServirJuntoResponse
            {
                PuedeDesmarcar = false,
                ProductosProblematicos = new System.Collections.Generic.List<ProductoSinStockDTO>
                {
                    new ProductoSinStockDTO { ProductoId = "PROD1", ProductoNombre = "Producto 1", AlmacenConStock = "REI" }
                },
                Mensaje = "No se puede desmarcar porque PROD1 no tiene stock en ALG"
            };

            // Assert
            Assert.IsFalse(response.PuedeDesmarcar);
            Assert.AreEqual(1, response.ProductosProblematicos.Count);
            Assert.IsNotNull(response.Mensaje);
            Assert.IsTrue(response.Mensaje.Contains("PROD1"));
        }

        [TestMethod]
        public void ProductoSinStockDTO_AlmacenConStock_NullCuandoNoHayStock()
        {
            // Este test documenta que si el producto no tiene stock en ningún almacén,
            // AlmacenConStock será null
            var producto = new ProductoSinStockDTO
            {
                ProductoId = "PROD1",
                ProductoNombre = "Producto sin stock",
                AlmacenConStock = null
            };

            Assert.IsNull(producto.AlmacenConStock);
        }

        #endregion

        /*
         * TESTS E2E PENDIENTES (requieren integración completa):
         *
         * 1. PrepararPedido_SinRegalos_NoAñadeLineasBonificadas
         *    - Crear pedido sin pasar por SeleccionRegalos
         *    - Verificar que no hay líneas con DescuentoLinea = 1
         *
         * 2. PrepararPedido_ConRegalos_AñadeLineasConDescuento100
         *    - Crear pedido con regalos seleccionados
         *    - Verificar que las líneas de regalo tienen DescuentoLinea = 1
         *    - Verificar que las líneas tienen texto con "(BONIFICADO)"
         *
         * 3. ValidadorGanavisiones_Backend_ValidaCorrectamente
         *    - Crear pedido con regalos dentro del límite -> se crea OK
         *    - Crear pedido excediendo Ganavisiones -> validador rechaza
         */
    }
}
