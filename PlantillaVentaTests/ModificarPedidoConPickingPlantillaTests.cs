using CommunityToolkit.Mvvm.Messaging;
using ControlesUsuario.Dialogs;
using ControlesUsuario.Models;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Models;
using Nesto.Infrastructure.Services;
using Nesto.Infrastructure.Shared;
using Nesto.Models;
using Nesto.Modulos.PedidoVenta;
using Nesto.Modulos.PlantillaVenta;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace PlantillaVentaTests
{
    /// <summary>
    /// Novedad 750 (Paloma, 22/10/26): modificar con la plantilla el pedido 929116, con todas sus líneas ya en
    /// picking, solo para AÑADIR productos, fallaba al guardar con «La línea del producto 33896 ya tiene picking
    /// en el almacén y no se puede quitar desde la plantilla». La plantilla perdía los ids de las líneas que nadie
    /// había tocado: (1) al restaurar el almacén del borrador, el setter de almacenSeleccionado sustituía TODAS las
    /// líneas por las copias que devuelve POST PlantillaVentas/PonerStock, cuyo modelo no lleva los ids (ni la
    /// oferta personalizada); (2) si el producto ya estaba en la plantilla del cliente, OnCargarBorrador copiaba
    /// cantidades y precios a esa línea, pero no los ids.
    /// </summary>
    [TestClass]
    public class ModificarPedidoConPickingPlantillaTests
    {
        private const int PEDIDO = 929116;
        private const string NUEVO = "41000";

        private IPlantillaVentaService _servicio;
        private IServicioDialogos _dialogos;
        private IServicioRegistroErrores _errores;

        /// <summary>El 929116 tal y como lo devuelve GET ParaPlantilla (datos reales): todo con picking 99922.</summary>
        internal static PedidoParaPlantillaModel Pedido929116() => new PedidoParaPlantillaModel
        {
            Empresa = "1",
            Cliente = "4730",
            Contacto = "0",
            NumeroPedido = PEDIDO,
            FormaPago = "RCB",
            PlazosPago = "CONTADO",
            Ruta = "00",
            FechaEntrega = DateTime.Today.AddDays(1),
            Almacen = "ALG",
            Lineas = new List<LineaParaPlantillaModel>
            {
                // Oferta 3+1 (NºOferta 169804)
                new LineaParaPlantillaModel { Producto = "33896", Texto = "REAFIRMANTE AGE STOP", Cantidad = 3, CantidadOferta = 1, Precio = 17.99M,
                    IdLineaPago = 329293900, IdLineaOferta = 329294000, PagoTienePicking = true, OfertaTienePicking = true },
                // Oferta 4+1 (NºOferta 169805)
                new LineaParaPlantillaModel { Producto = "45147", Texto = "CREMA VENUS", Cantidad = 4, CantidadOferta = 1, Precio = 18M,
                    IdLineaPago = 329294100, IdLineaOferta = 329294200, PagoTienePicking = true, OfertaTienePicking = true },
                new LineaParaPlantillaModel { Producto = "45929", Texto = "PACK 26 COFRE ROSA BULGARA", Cantidad = 2, Precio = 38M, AplicarDescuento = true,
                    IdLineaPago = 329294300, PagoTienePicking = true },
                new LineaParaPlantillaModel { Producto = "27593", Texto = "HIDRATANTE ROSA BULGARA", Cantidad = 1, Precio = 25.63M, AplicarDescuento = true,
                    IdLineaPago = 329294400, PagoTienePicking = true },
                // Muestra a base 0: el 100 % va en DescuentoProducto, la línea lleva Descuento 0
                new LineaParaPlantillaModel { Producto = "44206", Texto = "MUESTRA ARTICREM", Cantidad = 4, Precio = 0.25M, Descuento = 0M, AplicarDescuento = true,
                    IdLineaPago = 329294800, PagoTienePicking = true },
                new LineaParaPlantillaModel { Producto = "41589", Texto = "MUESTRA ACEITE CALENDULA", Cantidad = 4, Precio = 0.35M, Descuento = 0M, AplicarDescuento = true,
                    IdLineaPago = 329294900, PagoTienePicking = true }
            },
            Regalos = new List<RegaloParaPlantillaModel>()
        };

        /// <summary>La plantilla del cliente: ya ha comprado 33896 y la muestra 44206; 41000 es lo que se añade.</summary>
        private static ObservableCollection<LineaPlantillaVenta> PlantillaCliente() => new ObservableCollection<LineaPlantillaVenta>
        {
            new LineaPlantillaVenta { producto = "33896", texto = "REAFIRMANTE AGE STOP", precio = 17.99M, aplicarDescuento = true, cantidadVendida = 12, iva = "G21" },
            new LineaPlantillaVenta { producto = "44206", texto = "MUESTRA ARTICREM", precio = 0.25M, aplicarDescuento = true, cantidadVendida = 20, subGrupo = "MMP", iva = "G21" },
            new LineaPlantillaVenta { producto = NUEVO, texto = "CREMA NUEVA", precio = 12M, aplicarDescuento = true, cantidadVendida = 3, iva = "G21" }
        };

        /// <summary>
        /// Como la API de verdad: POST PlantillaVentas/PonerStock deserializa las líneas en SU modelo (sin ids, sin
        /// oferta personalizada, sin descuentoProducto) y devuelve copias nuevas con el stock puesto.
        /// </summary>
        private static ObservableCollection<LineaPlantillaVenta> PonerStocksComoLaApi(IEnumerable<LineaPlantillaVenta> lineas) =>
            new ObservableCollection<LineaPlantillaVenta>(lineas.Select(l => new LineaPlantillaVenta
            {
                producto = l.producto,
                texto = l.texto,
                cantidad = l.cantidad,
                cantidadOferta = l.cantidadOferta,
                subGrupo = l.subGrupo,
                cantidadVendida = l.cantidadVendida,
                iva = l.iva,
                precio = l.precio,
                descuento = l.descuento,
                aplicarDescuento = l.aplicarDescuento,
                stock = 50,
                cantidadDisponible = 40,
                StockDisponibleTodosLosAlmacenes = 40,
                stocks = new List<StockAlmacenDTO> { new StockAlmacenDTO { almacen = "ALG", stock = 50, cantidadDisponible = 40 } }
            }).ToList());

        [TestInitialize]
        public void Setup()
        {
            _servicio = A.Fake<IPlantillaVentaService>();
            _dialogos = A.Fake<IServicioDialogos>();
            _errores = A.Fake<IServicioRegistroErrores>();
            A.CallTo(() => _servicio.CargarClientesVendedor(A<string>._, A<string>._, A<bool>._))
                .ReturnsLazily((string filtro, string vendedor, bool todos) => Task.FromResult<ICollection<ClienteJson>>(new List<ClienteJson> { ClienteJson(filtro) }));
            A.CallTo(() => _servicio.CargarPedidoParaPlantilla("1", PEDIDO)).ReturnsLazily(() => Task.FromResult(Pedido929116()));
            A.CallTo(() => _servicio.CargarListaPendientes(A<string>._, A<string>._)).Returns(Task.FromResult(new List<PedidoPendientePlantilla>()));
            A.CallTo(() => _servicio.CargarProductosPlantilla(A<ClienteJson>._)).ReturnsLazily(() => Task.FromResult(PlantillaCliente()));
            A.CallTo(() => _servicio.PonerStocks(A<ObservableCollection<LineaPlantillaVenta>>._, A<string>._, A<List<string>>._))
                .ReturnsLazily((ObservableCollection<LineaPlantillaVenta> lineas, string almacen, List<string> almacenes) => Task.FromResult(PonerStocksComoLaApi(lineas)));
        }

        private static ClienteJson ClienteJson(string cliente) =>
            new ClienteJson { empresa = "1", cliente = cliente.Trim(), contacto = "0", iva = "G21", cifNif = "12345678A", nombre = "CLIENTE " + cliente.Trim(), ruta = "00" };

        private PlantillaVentaViewModel CrearViewModel()
        {
            IConfiguracion configuracion = A.Fake<IConfiguracion>();
            A.CallTo(() => configuracion.servidorAPI).Returns("http://127.0.0.1:9/api/");
            A.CallTo(() => configuracion.LeerParametroSync(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.AlmacenRuta)).Returns("ALG");
            IPedidoVentaService servicioPedidos = A.Fake<IPedidoVentaService>();
            var vm = new PlantillaVentaViewModel(A.Fake<IAbridorPedidos>(), A.Fake<IServicioNavegacion>(), configuracion, _servicio,
                new WeakReferenceMessenger(), _dialogos, servicioPedidos, new BorradorPlantillaVentaService(configuracion),
                A.Fake<IServicioAutenticacion>());
            vm.ListaFiltrableProductos.ListaOriginal = new ObservableCollection<IFiltrableItem>();
            vm.PaginasWizard = new List<Xceed.Wpf.Toolkit.WizardPage>
            {
                new Xceed.Wpf.Toolkit.WizardPage { Name = "SeleccionCliente" },
                new Xceed.Wpf.Toolkit.WizardPage { Name = "SeleccionProductos" }
            };
            vm.ServicioChequesRegalo = A.Fake<IServicioChequesRegalo>();
            vm.ServicioCupones = A.Fake<IServicioCupones>();
            vm.ServicioRegistroErrores = _errores;
            return vm;
        }

        /// <summary>«Modificar con plantilla»: carga el pedido y espera a que OnCargarBorrador (Async Sub) termine.</summary>
        private static async Task CargarYEsperar(PlantillaVentaViewModel vm)
        {
            await vm.CargarPedidoParaModificarAsync("1", PEDIDO);
            await Task.Delay(2500);
        }

        private static LineaPlantillaVenta Linea(PlantillaVentaViewModel vm, string producto) =>
            vm.ListaFiltrableProductos.ListaOriginal.Cast<LineaPlantillaVenta>().Single(l => l.producto.Trim() == producto);

        [TestMethod]
        public void ModificarConPlantilla_AnadirUnProducto_LasLineasConPickingViajanIntactasConSuId() => EnStaConDispatcher(async () =>
        {
            var vm = CrearViewModel();
            await CargarYEsperar(vm);
            Assert.AreEqual(PEDIDO, vm.NumeroPedidoEnEdicion);

            // Paloma solo añade un producto nuevo
            Linea(vm, NUEVO).cantidad = 2;

            PedidoVentaDTO pedido = vm.PrepararPedido();

            string error = PlantillaVentaViewModel.ValidarLineasConPicking(Pedido929116(), pedido);
            Assert.IsNull(error, error);

            void ComprobarLinea(int id, string producto, int cantidad, decimal precio, decimal descuento)
            {
                LineaPedidoVentaDTO l = pedido.Lineas.SingleOrDefault(x => x.id == id);
                Assert.IsNotNull(l, $"La línea {id} ({producto}) ha perdido el id. Ids enviados: {string.Join(", ", pedido.Lineas.Select(x => x.id))}");
                Assert.AreEqual(producto, l.Producto.Trim());
                Assert.AreEqual(cantidad, l.Cantidad, $"cantidad de {id}");
                Assert.AreEqual(precio, l.PrecioUnitario, $"precio de {id}");
                Assert.AreEqual(descuento, l.DescuentoLinea, $"descuento de {id}");
            }
            ComprobarLinea(329293900, "33896", 3, 17.99M, 0M);
            ComprobarLinea(329294000, "33896", 1, 0M, 0M);   // la unidad de oferta (3+1), a base 0
            ComprobarLinea(329294100, "45147", 4, 18M, 0M);
            ComprobarLinea(329294200, "45147", 1, 0M, 0M);
            ComprobarLinea(329294300, "45929", 2, 38M, 0M);
            ComprobarLinea(329294400, "27593", 1, 25.63M, 0M);
            ComprobarLinea(329294800, "44206", 4, 0.25M, 0M); // muestra a base 0
            ComprobarLinea(329294900, "41589", 4, 0.35M, 0M);

            LineaPedidoVentaDTO nueva = pedido.Lineas.Single(x => x.Producto.Trim() == NUEVO);
            Assert.AreEqual(0, nueva.id, "La línea nueva va sin id: el PUT la crea");
            Assert.AreEqual(2, nueva.Cantidad);
            Assert.AreEqual(9, pedido.Lineas.Count(x => x.tipoLinea == 1));
            A.CallTo(() => _dialogos.ShowError(A<string>._)).MustNotHaveHappened();
        });

        [TestMethod]
        public void CambiarDeAlmacen_ConElPedidoCargado_NoPierdeLosIdsNiLaOfertaPersonalizada() => EnStaConDispatcher(async () =>
        {
            var vm = CrearViewModel();
            await CargarYEsperar(vm);
            LineaPlantillaVenta linea45147 = Linea(vm, "45147");

            // El setter vuelve a pedir los stocks (PonerStock): antes sustituía las líneas por las copias de la API
            vm.almacenSeleccionado = vm.listaAlmacenes.Single(a => a.Codigo == "ALG");
            await Task.Delay(500);

            Assert.AreSame(linea45147, Linea(vm, "45147"), "Se actualizan los stocks en sitio, sin cambiar las líneas");
            Assert.AreEqual(329294100, linea45147.idLineaPedido);
            Assert.AreEqual(329294200, linea45147.idLineaPedidoOferta);
            Assert.IsTrue(linea45147.stockActualizado);
            Assert.AreEqual(40, linea45147.cantidadDisponible);
            Assert.IsNull(PlantillaVentaViewModel.ValidarLineasConPicking(Pedido929116(), vm.PrepararPedido()));
        });

        [TestMethod]
        public void LineaConPicking_NoSeDejaCambiarEnLaPlantillaYSeAvisaAlMomento() => EnStaConDispatcher(async () =>
        {
            var vm = CrearViewModel();
            await CargarYEsperar(vm);
            LineaPlantillaVenta linea = Linea(vm, "33896");
            Assert.IsTrue(linea.tienePicking);

            linea.cantidad = 5;
            linea.cantidadOferta = 0;
            linea.precio = 10M;
            linea.descuento = 0.1M;

            Assert.AreEqual(3, linea.cantidad);
            Assert.AreEqual(1, linea.cantidadOferta);
            Assert.AreEqual(17.99M, linea.precio);
            Assert.AreEqual(0M, linea.descuento);
            A.CallTo(() => _dialogos.ShowNotification(A<string>.That.Contains("picking"))).MustHaveHappened();
            Assert.IsNull(PlantillaVentaViewModel.ValidarLineasConPicking(Pedido929116(), vm.PrepararPedido()));
        });

        [TestMethod]
        public void LineaNueva_SeEditaConNormalidad() => EnStaConDispatcher(async () =>
        {
            var vm = CrearViewModel();
            await CargarYEsperar(vm);
            LineaPlantillaVenta nueva = Linea(vm, NUEVO);

            nueva.cantidad = 3;
            nueva.cantidad = 1;

            Assert.IsFalse(nueva.tienePicking);
            Assert.AreEqual(1, nueva.cantidad);
        });

        [TestMethod]
        public void BloqueoPorPicking_SeRegistraEnElmahConPedidoProductoEIds() => EnStaConDispatcher(() =>
        {
            var vm = CrearViewModel();
            var pedido = new PedidoVentaDTO { numero = PEDIDO };
            pedido.Lineas.Add(new LineaPedidoVentaDTO { id = 329294000, Producto = "33896", Cantidad = 1, PrecioUnitario = 0M });
            pedido.Lineas.Add(new LineaPedidoVentaDTO { id = 0, Producto = NUEVO, Cantidad = 2, PrecioUnitario = 12M });
            string contexto = null;
            Exception excepcion = null;
            A.CallTo(() => _errores.RegistrarErrorAsync(A<Exception>._, A<string>._))
                .Invokes((Exception ex, string ctx) => { excepcion = ex; contexto = ctx; })
                .Returns(Task.CompletedTask);

            string error = vm.ComprobarLineasConPickingAntesDeGuardar(Pedido929116(), pedido);

            StringAssert.Contains(error, "33896");
            A.CallTo(() => _errores.RegistrarErrorAsync(A<Exception>._, A<string>._)).MustHaveHappenedOnceExactly();
            StringAssert.Contains(excepcion.Message, "33896");
            StringAssert.Contains(contexto, PEDIDO.ToString());
            StringAssert.Contains(contexto, "33896");
            StringAssert.Contains(contexto, "329293900"); // la línea que falta
            StringAssert.Contains(contexto, "329294000, 0"); // los ids que sí lleva el DTO
            return Task.CompletedTask;
        });

        [TestMethod]
        public void SinBloqueo_NoSeRegistraNada() => EnStaConDispatcher(() =>
        {
            var vm = CrearViewModel();
            var pedido = new PedidoVentaDTO { numero = PEDIDO };

            Assert.IsNull(vm.ComprobarLineasConPickingAntesDeGuardar(new PedidoParaPlantillaModel(), pedido));
            A.CallTo(() => _errores.RegistrarErrorAsync(A<Exception>._, A<string>._)).MustNotHaveHappened();
            return Task.CompletedTask;
        });

        private static void EnStaConDispatcher(Func<Task> prueba)
        {
            Exception error = null;
            var hilo = new System.Threading.Thread(() =>
            {
                System.Windows.Threading.Dispatcher dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
                System.Threading.SynchronizationContext.SetSynchronizationContext(new System.Windows.Threading.DispatcherSynchronizationContext(dispatcher));
                _ = dispatcher.InvokeAsync(async () =>
                {
                    try
                    {
                        await prueba();
                    }
                    catch (Exception ex)
                    {
                        error = ex;
                    }
                    finally
                    {
                        dispatcher.BeginInvokeShutdown(System.Windows.Threading.DispatcherPriority.Background);
                    }
                });
                System.Windows.Threading.Dispatcher.Run();
            });
            hilo.SetApartmentState(System.Threading.ApartmentState.STA);
            hilo.Start();
            if (!hilo.Join(TimeSpan.FromSeconds(60)))
            {
                Assert.Fail("La prueba no ha terminado");
            }
            if (error != null)
            {
                throw new AssertFailedException(error.ToString(), error);
            }
        }
    }
}
