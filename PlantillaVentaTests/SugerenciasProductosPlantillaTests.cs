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
    /// Nesto#537 (épica NestoAPI#669, API NestoAPI#670): la franja «Puede ofrecer» de la plantilla. La API decide qué se
    /// sugiere; aquí se prueba lo que es del cliente: cuándo se pregunta, «✓ Añadir», «✗ No le interesa» con su sustituta,
    /// que lo tecleado a mano cambie la ficha, el cambio de cliente, que sin API no haya panel y la sesión al grabar.
    /// </summary>
    [TestClass]
    public class SugerenciasProductosPlantillaTests
    {
        private static readonly Guid SESION = new Guid("6c1f8f0e-3c55-4a8e-9c1e-0b5d1a9a5370");

        private static (PlantillaVentaViewModel vm, IServicioSugerenciasProductos sugerencias, IPlantillaVentaService servicio, IServicioDialogos dialogos)
            CrearViewModel()
        {
            IConfiguracion configuracion = A.Fake<IConfiguracion>();
            // Una dirección que no contesta: el precio y el stock que se piden al actualizar la línea fallan dentro de su Try
            A.CallTo(() => configuracion.servidorAPI).Returns("http://127.0.0.1:9/api/");
            A.CallTo(() => configuracion.LeerParametroSync(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.AlmacenRuta)).Returns("ALG");
            A.CallTo(() => configuracion.leerParametro(A<string>._, Parametros.Claves.Vendedor)).Returns(Task.FromResult("ASH"));
            IServicioDialogos dialogos = A.Fake<IServicioDialogos>();
            IPlantillaVentaService servicio = A.Fake<IPlantillaVentaService>();
            var vm = new PlantillaVentaViewModel(A.Fake<IAbridorPedidos>(), A.Fake<IServicioNavegacion>(), configuracion, servicio,
                new WeakReferenceMessenger(), dialogos, A.Fake<IPedidoVentaService>(), A.Fake<IBorradorPlantillaVentaService>(),
                A.Fake<IServicioAutenticacion>());
            vm.ListaFiltrableProductos.ListaOriginal = new ObservableCollection<IFiltrableItem>();
            vm.ComprobadorSustitucion = null; // sin avisos de sustitución en estos tests
            vm._clienteSeleccionado = Cliente("15191");
            vm.Estado.Contacto = "0";

            IServicioSugerenciasProductos sugerencias = A.Fake<IServicioSugerenciasProductos>();
            vm.ServicioSugerenciasProductos = sugerencias;
            return (vm, sugerencias, servicio, dialogos);
        }

        private static ClienteJson Cliente(string cliente) =>
            new ClienteJson { empresa = "1", cliente = cliente, contacto = "0", iva = "G21", cifNif = "12345678A" };

        private static SugerenciaProductoDTO Ficha(int id, string producto, int posicion, string motivo = MotivosSugerenciaProducto.COMPRADO_JUNTO) =>
            new SugerenciaProductoDTO
            {
                IdSugerencia = id,
                Producto = producto,
                Nombre = "PRODUCTO " + producto,
                Motivo = motivo,
                Texto = "Lo compran con lo que lleva.",
                Posicion = posicion
            };

        private static SugerenciasProductosDTO Respuesta(params SugerenciaProductoDTO[] fichas) =>
            new SugerenciasProductosDTO { IdSesion = SESION, Sugerencias = fichas.ToList() };

        private static LineaPlantillaVenta Linea(PlantillaVentaViewModel vm, string producto, int cantidad)
        {
            var linea = new LineaPlantillaVenta { producto = producto, texto = "PRODUCTO " + producto, cantidad = cantidad, precio = 10M, cantidadVendida = 3 };
            vm.ListaFiltrableProductos.ListaOriginal.Add(linea);
            return linea;
        }

        #region Carga

        [TestMethod]
        public async Task Carga_PideConElClienteYElCarrito_YEnsenaLasFichasEnSuOrden()
        {
            var (vm, sugerencias, _, _) = CrearViewModel();
            Linea(vm, "17404", 2);
            CalcularSugerenciasProductosDTO enviada = null;
            A.CallTo(() => sugerencias.Calcular(A<CalcularSugerenciasProductosDTO>._))
                .Invokes((CalcularSugerenciasProductosDTO p) => enviada = p)
                .Returns(Task.FromResult(Respuesta(Ficha(12, "40000", 3), Ficha(10, "38000", 1), Ficha(11, "39000", 2))));

            await vm.RefrescarSugerenciasProductos();

            Assert.IsNotNull(enviada);
            Assert.AreEqual("1", enviada.Empresa);
            Assert.AreEqual("15191", enviada.Cliente);
            Assert.AreEqual("0", enviada.Contacto);
            Assert.AreEqual("ASH", enviada.Vendedor, "El vendedor del usuario: la API filtra por su grupo");
            Assert.AreEqual("Nesto", enviada.Aplicacion);
            Assert.IsNull(enviada.IdSesion, "La primera vez la sesión la crea la API");
            Assert.AreEqual("17404", enviada.Productos.Single().Producto);
            Assert.AreEqual(2, enviada.Productos.Single().Cantidad);

            Assert.IsTrue(vm.HaySugerenciasProductos);
            CollectionAssert.AreEqual(new[] { "38000", "39000", "40000" }, vm.SugerenciasProductos.Select(s => s.Producto).ToArray());
            Assert.AreEqual(SESION, vm.IdSesionSugerencias);
        }

        [TestMethod]
        public async Task Carga_ConElCarritoQuieto_NoVuelveAPreguntar_YLaSesionVaEnLaSiguiente()
        {
            var (vm, sugerencias, _, _) = CrearViewModel();
            LineaPlantillaVenta linea = Linea(vm, "17404", 2);
            var enviadas = new List<CalcularSugerenciasProductosDTO>();
            A.CallTo(() => sugerencias.Calcular(A<CalcularSugerenciasProductosDTO>._))
                .Invokes((CalcularSugerenciasProductosDTO p) => enviadas.Add(p))
                .Returns(Task.FromResult(Respuesta(Ficha(10, "38000", 1))));

            await vm.RefrescarSugerenciasProductos();
            linea.cantidad = 6; // cambiar cuántas no cambia qué se sugiere
            await vm.RefrescarSugerenciasProductos();
            Assert.AreEqual(1, enviadas.Count, "Sin cambiar qué productos hay, no se pregunta otra vez");

            Linea(vm, "17405", 1);
            await vm.RefrescarSugerenciasProductos();
            Assert.AreEqual(2, enviadas.Count);
            Assert.AreEqual(SESION, enviadas[1].IdSesion, "La misma sesión: la API mantiene las fichas en su hueco");
        }

        [TestMethod]
        public async Task Carga_NuncaMasDeCuatroFichas()
        {
            var (vm, sugerencias, _, _) = CrearViewModel();
            A.CallTo(() => sugerencias.Calcular(A<CalcularSugerenciasProductosDTO>._))
                .Returns(Task.FromResult(Respuesta(Ficha(1, "1", 1), Ficha(2, "2", 2), Ficha(3, "3", 3), Ficha(4, "4", 4), Ficha(5, "5", 5))));

            await vm.RefrescarSugerenciasProductos();

            Assert.AreEqual(4, vm.SugerenciasProductos.Count);
        }

        #endregion

        #region API caída

        [TestMethod]
        public async Task ApiCaida_NoHayPanel_NiDialogo()
        {
            var (vm, sugerencias, _, dialogos) = CrearViewModel();
            A.CallTo(() => sugerencias.Calcular(A<CalcularSugerenciasProductosDTO>._)).Returns(Task.FromResult<SugerenciasProductosDTO>(null));

            await vm.RefrescarSugerenciasProductos();

            Assert.IsFalse(vm.HaySugerenciasProductos);
            Assert.IsNull(vm.IdSesionSugerencias);
            A.CallTo(() => dialogos.ShowError(A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task ApiQueLanza_NoHayPanel_NiDialogo()
        {
            var (vm, sugerencias, _, dialogos) = CrearViewModel();
            A.CallTo(() => sugerencias.Calcular(A<CalcularSugerenciasProductosDTO>._)).Throws(new InvalidOperationException("Sin conexión"));

            await vm.RefrescarSugerenciasProductos();

            Assert.IsFalse(vm.HaySugerenciasProductos);
            A.CallTo(() => dialogos.ShowError(A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task ApiSinNadaQueOfrecer_NoHayPanel()
        {
            var (vm, sugerencias, _, _) = CrearViewModel();
            A.CallTo(() => sugerencias.Calcular(A<CalcularSugerenciasProductosDTO>._)).Returns(Task.FromResult(Respuesta()));

            await vm.RefrescarSugerenciasProductos();

            Assert.IsFalse(vm.HaySugerenciasProductos);
        }

        [TestMethod]
        public async Task ApiQueSeCaeDespues_ElPanelDesaparece()
        {
            var (vm, sugerencias, _, _) = CrearViewModel();
            A.CallTo(() => sugerencias.Calcular(A<CalcularSugerenciasProductosDTO>._))
                .ReturnsNextFromSequence(Task.FromResult(Respuesta(Ficha(10, "38000", 1))), Task.FromResult<SugerenciasProductosDTO>(null));

            await vm.RefrescarSugerenciasProductos();
            Assert.IsTrue(vm.HaySugerenciasProductos);
            Linea(vm, "17404", 1);
            await vm.RefrescarSugerenciasProductos();

            Assert.IsFalse(vm.HaySugerenciasProductos);
        }

        #endregion

        #region Aceptar

        [TestMethod]
        public async Task Anadir_LaMeteEnElPedidoConUnaUnidad_QuitaLaFicha_YApuntaAnadido()
        {
            var (vm, sugerencias, _, _) = CrearViewModel();
            LineaPlantillaVenta enPlantilla = Linea(vm, "38000", 0);
            A.CallTo(() => sugerencias.Calcular(A<CalcularSugerenciasProductosDTO>._))
                .Returns(Task.FromResult(Respuesta(Ficha(10, "38000", 1), Ficha(11, "39000", 2))));
            await vm.RefrescarSugerenciasProductos();
            List<ProductoCarritoSugerenciaDTO> carritoEnviado = null;
            A.CallTo(() => sugerencias.Responder(10, RespuestasSugerenciaProducto.ANADIDO, A<List<ProductoCarritoSugerenciaDTO>>._))
                .Invokes((int _, string _, List<ProductoCarritoSugerenciaDTO> c) => carritoEnviado = c)
                .Returns(Task.FromResult(new ResultadoRespuestaSugerenciaProductoDTO { IdSugerencia = 10, Respuesta = RespuestasSugerenciaProducto.ANADIDO }));

            await vm.AnadirSugerenciaProductoAsync(vm.SugerenciasProductos[0]);

            Assert.AreEqual(1, enPlantilla.cantidad, "Una unidad, editable como cualquier línea");
            Assert.IsTrue(vm.listaProductosPedido.Any(l => l.producto == "38000"));
            CollectionAssert.AreEqual(new[] { "39000" }, vm.SugerenciasProductos.Select(s => s.Producto).ToArray());
            A.CallTo(() => sugerencias.Responder(10, RespuestasSugerenciaProducto.ANADIDO, A<List<ProductoCarritoSugerenciaDTO>>._)).MustHaveHappenedOnceExactly();
            Assert.IsTrue(carritoEnviado.Any(p => p.Producto == "38000"), "Con el carrito de después de añadirla");
        }

        [TestMethod]
        public async Task Anadir_ProductoQueElClienteNoHaCompradoNunca_SeBuscaYEntra()
        {
            var (vm, sugerencias, servicio, _) = CrearViewModel();
            A.CallTo(() => sugerencias.Calcular(A<CalcularSugerenciasProductosDTO>._)).Returns(Task.FromResult(Respuesta(Ficha(10, "45685", 1))));
            A.CallTo(() => servicio.BuscarLineaProducto("1", "45685"))
                .Returns(Task.FromResult(new LineaPlantillaVenta { producto = "45685", texto = "PANTALON PRESOTERAPIA", precio = 12M }));
            await vm.RefrescarSugerenciasProductos();

            await vm.AnadirSugerenciaProductoAsync(vm.SugerenciasProductos[0]);

            LineaPlantillaVenta nueva = vm.ListaFiltrableProductos.ListaOriginal.OfType<LineaPlantillaVenta>().SingleOrDefault(l => l.producto == "45685");
            Assert.IsNotNull(nueva);
            Assert.AreEqual(1, nueva.cantidad);
            Assert.IsFalse(vm.HaySugerenciasProductos);
        }

        [TestMethod]
        public async Task Anadir_ProductoQueNoSeEncuentra_AvisaDeUsted_YNoApuntaNada()
        {
            var (vm, sugerencias, servicio, dialogos) = CrearViewModel();
            A.CallTo(() => sugerencias.Calcular(A<CalcularSugerenciasProductosDTO>._)).Returns(Task.FromResult(Respuesta(Ficha(10, "45685", 1))));
            A.CallTo(() => servicio.BuscarLineaProducto("1", "45685")).Returns(Task.FromResult<LineaPlantillaVenta>(null));
            await vm.RefrescarSugerenciasProductos();

            await vm.AnadirSugerenciaProductoAsync(vm.SugerenciasProductos[0]);

            A.CallTo(() => dialogos.ShowError(A<string>.That.Contains("Búsquelo a mano"))).MustHaveHappenedOnceExactly();
            A.CallTo(() => sugerencias.Responder(A<int>._, A<string>._, A<List<ProductoCarritoSugerenciaDTO>>._)).MustNotHaveHappened();
            Assert.AreEqual(1, vm.SugerenciasProductos.Count);
        }

        #endregion

        #region Rechazar

        [TestMethod]
        public async Task NoLeInteresa_ApuntaElRechazo_YLaSustitutaOcupaSuHueco()
        {
            var (vm, sugerencias, _, _) = CrearViewModel();
            A.CallTo(() => sugerencias.Calcular(A<CalcularSugerenciasProductosDTO>._))
                .Returns(Task.FromResult(Respuesta(Ficha(10, "38000", 1), Ficha(11, "39000", 2), Ficha(12, "40000", 3))));
            await vm.RefrescarSugerenciasProductos();
            SugerenciaProductoDTO sustituta = Ficha(13, "41000", 2, MotivosSugerenciaProducto.LE_TOCA);
            A.CallTo(() => sugerencias.Responder(11, RespuestasSugerenciaProducto.NO_LE_INTERESA, A<List<ProductoCarritoSugerenciaDTO>>._))
                .Returns(Task.FromResult(new ResultadoRespuestaSugerenciaProductoDTO
                {
                    IdSugerencia = 11,
                    Respuesta = RespuestasSugerenciaProducto.NO_LE_INTERESA,
                    Sustituta = sustituta
                }));

            await vm.RechazarSugerenciaProductoAsync(vm.SugerenciasProductos[1]);

            CollectionAssert.AreEqual(new[] { "38000", "41000", "40000" }, vm.SugerenciasProductos.Select(s => s.Producto).ToArray(),
                "La sustituta va en el mismo sitio; las demás no se mueven");
            A.CallTo(() => sugerencias.Responder(11, RespuestasSugerenciaProducto.NO_LE_INTERESA, A<List<ProductoCarritoSugerenciaDTO>>._)).MustHaveHappenedOnceExactly();
            Assert.AreEqual(0, vm.listaProductosPedido.Count, "Rechazar no mete nada en el pedido");
        }

        [TestMethod]
        public async Task NoLeInteresa_SinSustituta_LaFichaSeVa()
        {
            var (vm, sugerencias, _, _) = CrearViewModel();
            A.CallTo(() => sugerencias.Calcular(A<CalcularSugerenciasProductosDTO>._))
                .Returns(Task.FromResult(Respuesta(Ficha(10, "38000", 1), Ficha(11, "39000", 2))));
            await vm.RefrescarSugerenciasProductos();
            A.CallTo(() => sugerencias.Responder(10, A<string>._, A<List<ProductoCarritoSugerenciaDTO>>._))
                .Returns(Task.FromResult(new ResultadoRespuestaSugerenciaProductoDTO { IdSugerencia = 10, Respuesta = RespuestasSugerenciaProducto.NO_LE_INTERESA }));

            await vm.RechazarSugerenciaProductoAsync(vm.SugerenciasProductos[0]);

            CollectionAssert.AreEqual(new[] { "39000" }, vm.SugerenciasProductos.Select(s => s.Producto).ToArray());
        }

        [TestMethod]
        public async Task NoLeInteresa_ConLaApiCaida_LaFichaSeVaSinDialogo()
        {
            var (vm, sugerencias, _, dialogos) = CrearViewModel();
            A.CallTo(() => sugerencias.Calcular(A<CalcularSugerenciasProductosDTO>._)).Returns(Task.FromResult(Respuesta(Ficha(10, "38000", 1))));
            await vm.RefrescarSugerenciasProductos();
            A.CallTo(() => sugerencias.Responder(A<int>._, A<string>._, A<List<ProductoCarritoSugerenciaDTO>>._))
                .Returns(Task.FromResult<ResultadoRespuestaSugerenciaProductoDTO>(null));

            await vm.RechazarSugerenciaProductoAsync(vm.SugerenciasProductos[0]);

            Assert.IsFalse(vm.HaySugerenciasProductos);
            A.CallTo(() => dialogos.ShowError(A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task NoLeInteresa_NoVuelveAPreguntarPorqueElCarritoNoHaCambiado()
        {
            var (vm, sugerencias, _, _) = CrearViewModel();
            A.CallTo(() => sugerencias.Calcular(A<CalcularSugerenciasProductosDTO>._)).Returns(Task.FromResult(Respuesta(Ficha(10, "38000", 1))));
            await vm.RefrescarSugerenciasProductos();
            A.CallTo(() => sugerencias.Responder(A<int>._, A<string>._, A<List<ProductoCarritoSugerenciaDTO>>._))
                .Returns(Task.FromResult(new ResultadoRespuestaSugerenciaProductoDTO { Sustituta = Ficha(13, "41000", 1) }));

            await vm.RechazarSugerenciaProductoAsync(vm.SugerenciasProductos[0]);
            await vm.RefrescarSugerenciasProductos();

            A.CallTo(() => sugerencias.Calcular(A<CalcularSugerenciasProductosDTO>._)).MustHaveHappenedOnceExactly();
            Assert.AreEqual("41000", vm.SugerenciasProductos.Single().Producto);
        }

        #endregion

        #region Añadido a mano

        [TestMethod]
        public async Task AnadidoAMano_UnProductoSugerido_SeVuelveAPreguntarYSaleOtraEnSuLugar()
        {
            var (vm, sugerencias, _, _) = CrearViewModel();
            var enviadas = new List<CalcularSugerenciasProductosDTO>();
            A.CallTo(() => sugerencias.Calcular(A<CalcularSugerenciasProductosDTO>._))
                .ReturnsLazily((CalcularSugerenciasProductosDTO p) =>
                {
                    enviadas.Add(p);
                    return Task.FromResult(enviadas.Count == 1
                        ? Respuesta(Ficha(10, "38000", 1), Ficha(11, "39000", 2))
                        : Respuesta(Ficha(12, "40000", 1), Ficha(11, "39000", 2)));
                });
            await vm.RefrescarSugerenciasProductos();

            Linea(vm, "38000", 3); // el vendedor lo teclea sin pulsar «✓»
            await vm.RefrescarSugerenciasProductos();

            Assert.AreEqual(2, enviadas.Count, "Cambia el carrito: se vuelve a preguntar");
            Assert.IsTrue(enviadas[1].Productos.Any(p => p.Producto == "38000"), "La API ya lo ve en el carrito y lo excluye");
            CollectionAssert.AreEqual(new[] { "40000", "39000" }, vm.SugerenciasProductos.Select(s => s.Producto).ToArray());
            A.CallTo(() => sugerencias.Responder(A<int>._, A<string>._, A<List<ProductoCarritoSugerenciaDTO>>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public void AnadidoAMano_SiLaRespuestaTodaviaLoTrae_NoSeEnsena()
        {
            var (vm, _, _, _) = CrearViewModel();
            Linea(vm, "38000", 1);

            vm.AplicarSugerenciasProductos(new[] { Ficha(10, "38000", 1), Ficha(11, "39000", 2) });

            CollectionAssert.AreEqual(new[] { "39000" }, vm.SugerenciasProductos.Select(s => s.Producto).ToArray());
        }

        #endregion

        #region Cambio de cliente y de contacto

        [TestMethod]
        public async Task CambioDeCliente_VaciaElPanel_YEmpiezaSesionNueva()
        {
            var (vm, sugerencias, _, _) = CrearViewModel();
            vm.ReiniciarSugerenciasProductosSiCambiaElCliente(vm._clienteSeleccionado);
            var enviadas = new List<CalcularSugerenciasProductosDTO>();
            A.CallTo(() => sugerencias.Calcular(A<CalcularSugerenciasProductosDTO>._))
                .Invokes((CalcularSugerenciasProductosDTO p) => enviadas.Add(p))
                .Returns(Task.FromResult(Respuesta(Ficha(10, "38000", 1))));
            await vm.RefrescarSugerenciasProductos();
            Assert.AreEqual(SESION, vm.IdSesionSugerencias);

            ClienteJson otro = Cliente("34867");
            vm._clienteSeleccionado = otro;
            vm.ReiniciarSugerenciasProductosSiCambiaElCliente(otro);

            Assert.IsFalse(vm.HaySugerenciasProductos, "Lo de antes era para otro cliente");
            Assert.IsNull(vm.IdSesionSugerencias);
            await vm.RefrescarSugerenciasProductos();
            Assert.AreEqual(2, enviadas.Count);
            Assert.AreEqual("34867", enviadas[1].Cliente);
            Assert.IsNull(enviadas[1].IdSesion, "Sesión nueva para el cliente nuevo");
        }

        [TestMethod]
        public async Task MismoCliente_NoReiniciaNada()
        {
            var (vm, sugerencias, _, _) = CrearViewModel();
            vm.ReiniciarSugerenciasProductosSiCambiaElCliente(vm._clienteSeleccionado);
            A.CallTo(() => sugerencias.Calcular(A<CalcularSugerenciasProductosDTO>._)).Returns(Task.FromResult(Respuesta(Ficha(10, "38000", 1))));
            await vm.RefrescarSugerenciasProductos();

            vm.ReiniciarSugerenciasProductosSiCambiaElCliente(Cliente("15191"));

            Assert.IsTrue(vm.HaySugerenciasProductos);
            Assert.AreEqual(SESION, vm.IdSesionSugerencias);
        }

        [TestMethod]
        public async Task CambioDeContacto_SeVuelveAPreguntar()
        {
            var (vm, sugerencias, _, _) = CrearViewModel();
            var enviadas = new List<CalcularSugerenciasProductosDTO>();
            A.CallTo(() => sugerencias.Calcular(A<CalcularSugerenciasProductosDTO>._))
                .Invokes((CalcularSugerenciasProductosDTO p) => enviadas.Add(p))
                .Returns(Task.FromResult(Respuesta(Ficha(10, "38000", 1))));
            await vm.RefrescarSugerenciasProductos();

            vm.Estado.Contacto = "2";
            await vm.RefrescarSugerenciasProductos();

            Assert.AreEqual(2, enviadas.Count);
            Assert.AreEqual("2", enviadas[1].Contacto);
            Assert.AreEqual(SESION, enviadas[1].IdSesion, "Mismo cliente: misma sesión");
        }

        [TestMethod]
        public async Task RespuestaQueLlegaTrasCambiarDeCliente_SeDescarta()
        {
            var (vm, sugerencias, _, _) = CrearViewModel();
            var pendiente = new TaskCompletionSource<SugerenciasProductosDTO>();
            A.CallTo(() => sugerencias.Calcular(A<CalcularSugerenciasProductosDTO>._)).Returns(pendiente.Task);

            Task enCurso = vm.RefrescarSugerenciasProductos();
            vm._clienteSeleccionado = Cliente("34867");
            pendiente.SetResult(Respuesta(Ficha(10, "38000", 1)));
            await enCurso;

            Assert.IsFalse(vm.HaySugerenciasProductos);
            Assert.IsNull(vm.IdSesionSugerencias);
        }

        #endregion

        #region Al grabar

        [TestMethod]
        public async Task AlGrabar_ElPedidoLlevaLaSesion()
        {
            var (vm, sugerencias, _, _) = CrearViewModel();
            Linea(vm, "17404", 2);
            A.CallTo(() => sugerencias.Calcular(A<CalcularSugerenciasProductosDTO>._)).Returns(Task.FromResult(Respuesta(Ficha(10, "38000", 1))));
            await vm.RefrescarSugerenciasProductos();

            PedidoVentaDTO pedido = vm.PrepararPedido();

            Assert.AreEqual(SESION, pedido.IdSesionSugerencias);
        }

        [TestMethod]
        public void AlGrabar_SinSugerencias_LaSesionVaVacia()
        {
            var (vm, _, _, _) = CrearViewModel();
            Linea(vm, "17404", 2);

            PedidoVentaDTO pedido = vm.PrepararPedido();

            Assert.IsNull(pedido.IdSesionSugerencias);
        }

        #endregion

        #region Quitar (Nesto#537, segunda vuelta)

        [TestMethod]
        public async Task Quitar_ApuntaDescartadoVendedor_NoNoLeInteresa_YLaSustitutaOcupaSuHueco()
        {
            var (vm, sugerencias, _, dialogos) = CrearViewModel();
            A.CallTo(() => sugerencias.Calcular(A<CalcularSugerenciasProductosDTO>._))
                .Returns(Task.FromResult(Respuesta(Ficha(10, "38000", 1), Ficha(11, "39000", 2), Ficha(12, "40000", 3))));
            await vm.RefrescarSugerenciasProductos();
            A.CallTo(() => sugerencias.Responder(10, RespuestasSugerenciaProducto.DESCARTADO_VENDEDOR, A<List<ProductoCarritoSugerenciaDTO>>._))
                .Returns(Task.FromResult(new ResultadoRespuestaSugerenciaProductoDTO
                {
                    IdSugerencia = 10,
                    Respuesta = RespuestasSugerenciaProducto.DESCARTADO_VENDEDOR,
                    Sustituta = Ficha(13, "41000", 1, MotivosSugerenciaProducto.PARECIDOS)
                }));

            await vm.QuitarSugerenciaProductoAsync(vm.SugerenciasProductos[0]);

            CollectionAssert.AreEqual(new[] { "41000", "39000", "40000" }, vm.SugerenciasProductos.Select(s => s.Producto).ToArray());
            A.CallTo(() => sugerencias.Responder(10, RespuestasSugerenciaProducto.DESCARTADO_VENDEDOR, A<List<ProductoCarritoSugerenciaDTO>>._)).MustHaveHappenedOnceExactly();
            A.CallTo(() => sugerencias.Responder(A<int>._, RespuestasSugerenciaProducto.NO_LE_INTERESA, A<List<ProductoCarritoSugerenciaDTO>>._)).MustNotHaveHappened();
            Assert.AreEqual(0, vm.listaProductosPedido.Count, "Quitar no mete nada en el pedido");
            A.CallTo(dialogos).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task Quitar_ConElBotonYLaApiCaida_LaFichaSeVaSinDialogo()
        {
            var (vm, sugerencias, _, dialogos) = CrearViewModel();
            A.CallTo(() => sugerencias.Calcular(A<CalcularSugerenciasProductosDTO>._)).Returns(Task.FromResult(Respuesta(Ficha(10, "38000", 1), Ficha(11, "39000", 2))));
            await vm.RefrescarSugerenciasProductos();
            A.CallTo(() => sugerencias.Responder(A<int>._, A<string>._, A<List<ProductoCarritoSugerenciaDTO>>._))
                .Returns(Task.FromResult<ResultadoRespuestaSugerenciaProductoDTO>(null));

            Assert.IsTrue(vm.QuitarSugerenciaProductoCommand.CanExecute(vm.SugerenciasProductos[1]));
            vm.QuitarSugerenciaProductoCommand.Execute(vm.SugerenciasProductos[1]);
            await Task.Yield();

            CollectionAssert.AreEqual(new[] { "38000" }, vm.SugerenciasProductos.Select(s => s.Producto).ToArray());
            A.CallTo(() => sugerencias.Responder(11, RespuestasSugerenciaProducto.DESCARTADO_VENDEDOR, A<List<ProductoCarritoSugerenciaDTO>>._)).MustHaveHappenedOnceExactly();
            A.CallTo(dialogos).MustNotHaveHappened();
        }

        #endregion

        #region Tarjetas (Nesto#537, segunda vuelta)

        [TestMethod]
        public async Task Tarjetas_LlevanLaFotoElNombreConTamanoYLaFamiliaQueMandaLaApi()
        {
            var (vm, sugerencias, _, _) = CrearViewModel();
            SugerenciaProductoDTO conFoto = Ficha(10, "38000", 1);
            conFoto.Nombre = "Crema Reafirmante";
            conFoto.UrlFoto = "https://tienda/38000.jpg";
            conFoto.Tamano = 500;
            conFoto.UnidadMedida = "ml";
            conFoto.Familia = "Lisap Milano";
            conFoto.NombreConTamano = "Crema Reafirmante 500 ml";
            SugerenciaProductoDTO sinFoto = Ficha(11, "39000", 2, MotivosSugerenciaProducto.LE_TOCA);
            A.CallTo(() => sugerencias.Calcular(A<CalcularSugerenciasProductosDTO>._)).Returns(Task.FromResult(Respuesta(conFoto, sinFoto)));

            await vm.RefrescarSugerenciasProductos();

            SugerenciaProductoDTO primera = vm.SugerenciasProductos[0];
            Assert.IsTrue(primera.TieneFoto);
            Assert.AreEqual("https://tienda/38000.jpg", primera.UrlFoto);
            Assert.AreEqual("Crema Reafirmante 500 ml", primera.NombreMostrar);
            Assert.AreEqual("38000 · Lisap Milano", primera.ProductoYFamilia);
            SugerenciaProductoDTO segunda = vm.SugerenciasProductos[1];
            Assert.IsFalse(segunda.TieneFoto, "Sin foto, la tarjeta enseña el icono del motivo");
            Assert.AreEqual("🔁", segunda.Icono);
            Assert.AreEqual("PRODUCTO 39000", segunda.NombreMostrar);
        }

        #endregion

        #region Plegar

        [TestMethod]
        public void Plegar_SeGuardaParaElUsuario()
        {
            var (vm, _, _, _) = CrearViewModel();
            Assert.IsTrue(vm.SugerenciasProductosDesplegadas, "Sin preferencia, desplegada");

            vm.PlegarSugerenciasProductosCommand.Execute(null);

            Assert.IsTrue(vm.SugerenciasProductosPlegadas);
            Assert.IsFalse(vm.SugerenciasProductosDesplegadas);
            A.CallTo(() => vm.configuracion.GuardarParametro(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.SugerenciasPlantillaPlegadas, "1"))
                .MustHaveHappenedOnceExactly();
        }

        #endregion
    }
}
