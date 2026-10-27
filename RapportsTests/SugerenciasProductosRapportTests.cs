using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Events;
using Nesto.Infrastructure.Models;
using Nesto.Infrastructure.Services;
using Nesto.Infrastructure.Shared;
using Nesto.Modulos.Rapports;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace RapportsTests
{
    /// <summary>
    /// Nesto#544 y Nesto#557 (Sancho, comentario 325 de la 761): la cinta «Puede ofrecer» de la plantilla vive en la LISTA de
    /// rapports, con el cliente y el contacto del selector (lo que se consulta antes o durante la conversación), y ya no en la
    /// ficha de crear el rapport. Se pregunta sin carrito en cuanto cambia el cliente; «✗ No le interesa» y «🚫 Quitar» mandan
    /// la respuesta igual que en la plantilla (alimentan el modelo); sin sugerencias (o con la API caída) no se ve. «✓ Añadir»
    /// (corte 2) pregunta por el mensajero si hay una plantilla abierta del cliente y, si no, abre una nueva con los parámetros;
    /// «Anadido» lo apunta la plantilla.
    /// </summary>
    [TestClass]
    public class SugerenciasProductosRapportTests
    {
        private static readonly Guid SESION = new Guid("0f6c9a43-8f2e-4a55-b3d5-2b6c1c3e9a11");

        private static (ListaRapportsViewModel vm, IServicioSugerenciasProductos sugerencias, IConfiguracion configuracion, IServicioDialogos dialogos) CrearViewModel()
        {
            var (vm, sugerencias, configuracion, dialogos, _, _) = CrearViewModelConNavegacion();
            return (vm, sugerencias, configuracion, dialogos);
        }

        private static (ListaRapportsViewModel vm, IServicioSugerenciasProductos sugerencias, IConfiguracion configuracion, IServicioDialogos dialogos,
            IServicioNavegacion navegacion, IMessenger messenger) CrearViewModelConNavegacion()
        {
            IConfiguracion configuracion = A.Fake<IConfiguracion>();
            A.CallTo(() => configuracion.leerParametro(A<string>._, A<string>._)).Returns(Task.FromResult<string>(null));
            A.CallTo(() => configuracion.leerParametro("1", "Vendedor")).Returns(Task.FromResult("NV"));
            IServicioDialogos dialogos = A.Fake<IServicioDialogos>();
            IServicioSugerenciasProductos sugerencias = A.Fake<IServicioSugerenciasProductos>();
            IServicioNavegacion navegacion = A.Fake<IServicioNavegacion>();
            IRapportService servicio = A.Fake<IRapportService>();
            A.CallTo(() => servicio.cargarListaRapports(A<string>._, A<string>._, A<string>._))
                .Returns(Task.FromResult(new ObservableCollection<SeguimientoClienteDTO>()));
            var messenger = new WeakReferenceMessenger();
            var vm = new ListaRapportsViewModel(navegacion, configuracion, servicio, dialogos, messenger, sugerencias);
            return (vm, sugerencias, configuracion, dialogos, navegacion, messenger);
        }

        /// <summary>Lo que hace el SelectorCliente de la lista al elegir un cliente (con el relleno de la BD).</summary>
        private static void ElegirCliente(ListaRapportsViewModel vm, string cliente = "15191", string contacto = "2")
        {
            vm.clienteSeleccionado = cliente + "     ";
            vm.contactoSeleccionado = contacto + "  ";
        }

        private static SugerenciaProductoDTO Ficha(int id, string producto, int posicion) =>
            new SugerenciaProductoDTO
            {
                IdSugerencia = id,
                Producto = producto,
                Nombre = "PRODUCTO " + producto,
                Motivo = MotivosSugerenciaProducto.LE_TOCA,
                Texto = "Lo compra cada ~45 días; el último, hace 52.",
                Posicion = posicion
            };

        private static SugerenciasProductosDTO Respuesta(params SugerenciaProductoDTO[] fichas) =>
            new SugerenciasProductosDTO { IdSesion = SESION, Sugerencias = fichas.ToList() };

        [TestMethod]
        public void Nesto557_LaFichaDelRapportYaNoTieneCinta()
        {
            // Carlos (23/10/26): no tiene sentido tenerla en los dos sitios. Cuando se crea el rapport la conversación ya ha terminado.
            Assert.IsNull(typeof(RapportViewModel).GetProperty("CintaSugerencias"), "La cinta «Puede ofrecer» se quitó de RapportViewModel (Nesto#557)");
            Assert.IsNotNull(typeof(ListaRapportsViewModel).GetProperty("CintaSugerencias"), "Vive en la lista de rapports del cliente");
        }

        [TestMethod]
        public async Task AlElegirCliente_PideConElClienteYElContacto_SinCarrito_YEnsenaLasFichas()
        {
            var (vm, sugerencias, _, _) = CrearViewModel();
            vm.vendedor = "JE";
            CalcularSugerenciasProductosDTO enviada = null;
            A.CallTo(() => sugerencias.Calcular(A<CalcularSugerenciasProductosDTO>._))
                .Invokes((CalcularSugerenciasProductosDTO p) => enviada = p)
                .Returns(Task.FromResult(Respuesta(Ficha(11, "39000", 2), Ficha(10, "38000", 1), Ficha(12, "40000", 3))));
            ElegirCliente(vm);

            await vm.CintaSugerencias.RefrescarAsync();

            Assert.IsNotNull(enviada);
            Assert.AreEqual("1", enviada.Empresa);
            Assert.AreEqual("15191", enviada.Cliente);
            Assert.AreEqual("2", enviada.Contacto, "El contacto del selector");
            Assert.AreEqual("JE", enviada.Vendedor);
            Assert.AreEqual(CalcularSugerenciasProductosDTO.APLICACION_NESTO, enviada.Aplicacion);
            Assert.IsNull(enviada.IdSesion, "La primera vez la sesión la crea la API");
            Assert.AreEqual(0, enviada.Productos.Count, "En los rapports no hay carrito");
            Assert.IsTrue(vm.CintaSugerencias.HaySugerencias);
            CollectionAssert.AreEqual(new[] { "38000", "39000", "40000" }, vm.CintaSugerencias.Sugerencias.Select(s => s.Producto).ToArray());
            Assert.AreEqual(SESION, vm.CintaSugerencias.IdSesion);
        }

        [TestMethod]
        public async Task SinVendedorLeidoTodavia_UsaElParametroVendedor()
        {
            var (vm, sugerencias, _, _) = CrearViewModel();
            CalcularSugerenciasProductosDTO enviada = null;
            A.CallTo(() => sugerencias.Calcular(A<CalcularSugerenciasProductosDTO>._))
                .Invokes((CalcularSugerenciasProductosDTO p) => enviada = p)
                .Returns(Task.FromResult(Respuesta(Ficha(10, "38000", 1))));
            ElegirCliente(vm);

            await vm.CintaSugerencias.RefrescarAsync();

            Assert.AreEqual("NV", enviada.Vendedor);
        }

        [TestMethod]
        public async Task SinCliente_NoPide()
        {
            var (vm, sugerencias, _, _) = CrearViewModel();

            await vm.CintaSugerencias.RefrescarAsync();

            A.CallTo(() => sugerencias.Calcular(A<CalcularSugerenciasProductosDTO>._)).MustNotHaveHappened();
            Assert.IsFalse(vm.CintaSugerencias.HaySugerencias);
        }

        [TestMethod]
        public async Task AlElegirOtroCliente_SesionNueva_YSePideConElNuevo()
        {
            var (vm, sugerencias, _, _) = CrearViewModel();
            var enviadas = new List<CalcularSugerenciasProductosDTO>();
            A.CallTo(() => sugerencias.Calcular(A<CalcularSugerenciasProductosDTO>._))
                .Invokes((CalcularSugerenciasProductosDTO p) => enviadas.Add(p))
                .Returns(Task.FromResult(Respuesta(Ficha(10, "38000", 1))));
            ElegirCliente(vm);
            await vm.CintaSugerencias.RefrescarAsync();
            Assert.AreEqual(SESION, vm.CintaSugerencias.IdSesion);

            vm.clienteSeleccionado = "34867"; // lo cambia el SelectorCliente de la lista (binding TwoWay)

            Assert.IsFalse(vm.CintaSugerencias.HaySugerencias, "Lo de antes era para otro cliente");
            Assert.IsNull(vm.CintaSugerencias.IdSesion);
            await vm.CintaSugerencias.RefrescarAsync();
            Assert.AreEqual(2, enviadas.Count);
            Assert.AreEqual("34867", enviadas[1].Cliente);
            Assert.IsNull(enviadas[1].IdSesion);
        }

        [TestMethod]
        public async Task AlBorrarElCliente_LaCintaSeVacia_YNoPide()
        {
            var (vm, sugerencias, _, _) = CrearViewModel();
            A.CallTo(() => sugerencias.Calcular(A<CalcularSugerenciasProductosDTO>._))
                .Returns(Task.FromResult(Respuesta(Ficha(10, "38000", 1))));
            A.CallTo(() => vm.configuracion.usuario).Returns("carlos");
            ElegirCliente(vm);
            await vm.CintaSugerencias.RefrescarAsync();
            Assert.IsTrue(vm.CintaSugerencias.HaySugerencias);

            vm.clienteSeleccionado = null; // Nesto#551: vuelve la lista del día

            Assert.IsFalse(vm.CintaSugerencias.HaySugerencias);
            await vm.CintaSugerencias.RefrescarAsync();
            A.CallTo(() => sugerencias.Calcular(A<CalcularSugerenciasProductosDTO>._)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task AlCambiarDeContacto_SeVuelveAPreguntarConLaMismaSesion()
        {
            var (vm, sugerencias, _, _) = CrearViewModel();
            var enviadas = new List<CalcularSugerenciasProductosDTO>();
            A.CallTo(() => sugerencias.Calcular(A<CalcularSugerenciasProductosDTO>._))
                .Invokes((CalcularSugerenciasProductosDTO p) => enviadas.Add(p))
                .Returns(Task.FromResult(Respuesta(Ficha(10, "38000", 1))));
            ElegirCliente(vm);
            await vm.CintaSugerencias.RefrescarAsync();

            vm.contactoSeleccionado = "5";
            await vm.CintaSugerencias.RefrescarAsync();

            Assert.AreEqual(2, enviadas.Count);
            Assert.AreEqual("5", enviadas[1].Contacto);
            Assert.AreEqual(SESION, enviadas[1].IdSesion);
        }

        [TestMethod]
        public async Task NoLeInteresa_MandaLaRespuestaSinCarrito_YLaSustitutaOcupaSuHueco()
        {
            var (vm, sugerencias, _, _) = CrearViewModel();
            A.CallTo(() => sugerencias.Calcular(A<CalcularSugerenciasProductosDTO>._))
                .Returns(Task.FromResult(Respuesta(Ficha(10, "38000", 1), Ficha(11, "39000", 2))));
            List<ProductoCarritoSugerenciaDTO> carritoEnviado = null;
            A.CallTo(() => sugerencias.Responder(10, RespuestasSugerenciaProducto.NO_LE_INTERESA, A<List<ProductoCarritoSugerenciaDTO>>._))
                .Invokes((int id, string r, List<ProductoCarritoSugerenciaDTO> productos) => carritoEnviado = productos)
                .Returns(Task.FromResult(new ResultadoRespuestaSugerenciaProductoDTO { IdSugerencia = 10, Sustituta = Ficha(13, "41000", 1) }));
            ElegirCliente(vm);
            await vm.CintaSugerencias.RefrescarAsync();

            Assert.IsTrue(vm.CintaSugerencias.RechazarCommand.CanExecute(vm.CintaSugerencias.Sugerencias[0]));
            await vm.CintaSugerencias.RechazarAsync(vm.CintaSugerencias.Sugerencias[0]);

            A.CallTo(() => sugerencias.Responder(10, RespuestasSugerenciaProducto.NO_LE_INTERESA, A<List<ProductoCarritoSugerenciaDTO>>._)).MustHaveHappenedOnceExactly();
            Assert.IsNotNull(carritoEnviado);
            Assert.AreEqual(0, carritoEnviado.Count);
            CollectionAssert.AreEqual(new[] { "41000", "39000" }, vm.CintaSugerencias.Sugerencias.Select(s => s.Producto).ToArray());
        }

        [TestMethod]
        public async Task DescartadoPorElVendedor_ConElBoton_MandaDescartadoVendedor()
        {
            var (vm, sugerencias, _, dialogos) = CrearViewModel();
            A.CallTo(() => sugerencias.Calcular(A<CalcularSugerenciasProductosDTO>._))
                .Returns(Task.FromResult(Respuesta(Ficha(10, "38000", 1), Ficha(11, "39000", 2))));
            A.CallTo(() => sugerencias.Responder(A<int>._, A<string>._, A<List<ProductoCarritoSugerenciaDTO>>._))
                .Returns(Task.FromResult<ResultadoRespuestaSugerenciaProductoDTO>(null));
            ElegirCliente(vm);
            await vm.CintaSugerencias.RefrescarAsync();

            vm.CintaSugerencias.QuitarCommand.Execute(vm.CintaSugerencias.Sugerencias[1]);
            await Task.Yield();

            A.CallTo(() => sugerencias.Responder(11, RespuestasSugerenciaProducto.DESCARTADO_VENDEDOR, A<List<ProductoCarritoSugerenciaDTO>>._)).MustHaveHappenedOnceExactly();
            CollectionAssert.AreEqual(new[] { "38000" }, vm.CintaSugerencias.Sugerencias.Select(s => s.Producto).ToArray());
            A.CallTo(dialogos).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task SinSugerencias_NoSeVe()
        {
            var (vm, sugerencias, _, _) = CrearViewModel();
            A.CallTo(() => sugerencias.Calcular(A<CalcularSugerenciasProductosDTO>._)).Returns(Task.FromResult(Respuesta()));
            ElegirCliente(vm);

            await vm.CintaSugerencias.RefrescarAsync();

            Assert.IsFalse(vm.CintaSugerencias.HaySugerencias);
        }

        [TestMethod]
        public async Task ApiCaida_NoSeVe_NiHayErrores()
        {
            var (vm, sugerencias, _, dialogos) = CrearViewModel();
            A.CallTo(() => sugerencias.Calcular(A<CalcularSugerenciasProductosDTO>._)).Throws(new InvalidOperationException("Sin conexión"));
            ElegirCliente(vm);

            await vm.CintaSugerencias.RefrescarAsync();

            Assert.IsFalse(vm.CintaSugerencias.HaySugerencias);
            A.CallTo(dialogos).MustNotHaveHappened();
        }

        #region «✓ Añadir» (corte 2)

        /// <summary>Lo que haría una plantilla abierta de ese cliente: contesta con lo que diga <c>entra</c>.</summary>
        private sealed class PlantillaAbierta
        {
            public AnadirProductoSugeridoMensaje Recibido;

            public PlantillaAbierta(IMessenger messenger, bool entra)
            {
                messenger.Register<PlantillaAbierta, AnadirProductoSugeridoMensaje>(this, (r, m) =>
                {
                    r.Recibido = m;
                    m.Reply(Task.FromResult(entra));
                });
            }
        }

        private static async Task<(ListaRapportsViewModel vm, IServicioSugerenciasProductos sugerencias, IServicioNavegacion navegacion, IMessenger messenger)> ListaConFichas()
        {
            var (vm, sugerencias, _, _, navegacion, messenger) = CrearViewModelConNavegacion();
            A.CallTo(() => sugerencias.Calcular(A<CalcularSugerenciasProductosDTO>._))
                .Returns(Task.FromResult(Respuesta(Ficha(10, "38000", 1), Ficha(11, "39000", 2))));
            ElegirCliente(vm);
            await vm.CintaSugerencias.RefrescarAsync();
            return (vm, sugerencias, navegacion, messenger);
        }

        [TestMethod]
        public async Task Anadir_SaleEnLaLista()
        {
            var (vm, _, _, _) = await ListaConFichas();

            Assert.IsTrue(vm.CintaSugerencias.PuedeAnadir);
            Assert.IsTrue(vm.CintaSugerencias.AnadirCommand.CanExecute(vm.CintaSugerencias.Sugerencias[0]));
        }

        [TestMethod]
        public async Task Anadir_SinPlantillaAbierta_AbreUnaNuevaConElClienteElContactoElProductoLaSugerenciaYLaSesion()
        {
            var (vm, sugerencias, navegacion, _) = await ListaConFichas();
            ParametrosNavegacion parametros = null;
            A.CallTo(() => navegacion.RequestNavigate("MainRegion", "PlantillaVentaView", A<ParametrosNavegacion>._))
                .Invokes((string _, string _, ParametrosNavegacion p) => parametros = p);

            await vm.CintaSugerencias.AnadirAsync(vm.CintaSugerencias.Sugerencias[0]);

            Assert.IsNotNull(parametros, "Sin plantilla abierta, se abre una nueva");
            Assert.AreEqual("1", parametros.GetValue<object>(ProductoSugeridoParaPlantilla.PARAMETRO_EMPRESA));
            Assert.AreEqual("15191", parametros.GetValue<object>(ProductoSugeridoParaPlantilla.PARAMETRO_CLIENTE));
            Assert.AreEqual("2", parametros.GetValue<object>(ProductoSugeridoParaPlantilla.PARAMETRO_CONTACTO));
            Assert.AreEqual("38000", parametros.GetValue<object>(ProductoSugeridoParaPlantilla.PARAMETRO_PRODUCTO));
            Assert.AreEqual(10, parametros.GetValue<object>(ProductoSugeridoParaPlantilla.PARAMETRO_ID_SUGERENCIA));
            Assert.AreEqual(SESION, parametros.GetValue<object>(ProductoSugeridoParaPlantilla.PARAMETRO_ID_SESION));
            CollectionAssert.AreEqual(new[] { "39000" }, vm.CintaSugerencias.Sugerencias.Select(s => s.Producto).ToArray());
            A.CallTo(() => sugerencias.Responder(A<int>._, RespuestasSugerenciaProducto.ANADIDO, A<List<ProductoCarritoSugerenciaDTO>>._))
                .MustNotHaveHappened(); // lo apunta la plantilla, con su carrito
        }

        [TestMethod]
        public async Task Anadir_ConPlantillaAbiertaDelCliente_LoMeteElla_YNoSeAbreOtra()
        {
            var (vm, sugerencias, navegacion, messenger) = await ListaConFichas();
            var plantilla = new PlantillaAbierta(messenger, entra: true);

            await vm.CintaSugerencias.AnadirAsync(vm.CintaSugerencias.Sugerencias[1]);

            Assert.IsNotNull(plantilla.Recibido);
            ProductoSugeridoParaPlantilla producto = plantilla.Recibido.Producto;
            Assert.AreEqual("1", producto.Empresa);
            Assert.AreEqual("15191", producto.Cliente);
            Assert.AreEqual("2", producto.Contacto);
            Assert.AreEqual("39000", producto.Producto);
            Assert.AreEqual(11, producto.IdSugerencia);
            Assert.AreEqual(SESION, producto.IdSesion);
            A.CallTo(() => navegacion.RequestNavigate(A<string>._, A<string>._, A<ParametrosNavegacion>._)).MustNotHaveHappened();
            CollectionAssert.AreEqual(new[] { "38000" }, vm.CintaSugerencias.Sugerencias.Select(s => s.Producto).ToArray());
            A.CallTo(() => sugerencias.Responder(A<int>._, A<string>._, A<List<ProductoCarritoSugerenciaDTO>>._)).MustNotHaveHappened();
            GC.KeepAlive(plantilla);
        }

        [TestMethod]
        public async Task Anadir_SiLaPlantillaAbiertaNoPuedeMeterlo_LaFichaSeQueda()
        {
            var (vm, _, navegacion, messenger) = await ListaConFichas();
            var plantilla = new PlantillaAbierta(messenger, entra: false);

            await vm.CintaSugerencias.AnadirAsync(vm.CintaSugerencias.Sugerencias[0]);

            Assert.AreEqual(2, vm.CintaSugerencias.Sugerencias.Count);
            A.CallTo(() => navegacion.RequestNavigate(A<string>._, A<string>._, A<ParametrosNavegacion>._)).MustNotHaveHappened();
            GC.KeepAlive(plantilla);
        }

        #endregion

        #region Plegar (sugerencia 761)

        private static ListaRapportsViewModel CrearConConfiguracion(IConfiguracion configuracion) =>
            new ListaRapportsViewModel(A.Fake<IServicioNavegacion>(), configuracion, A.Fake<IRapportService>(), A.Fake<IServicioDialogos>(),
                new WeakReferenceMessenger(), A.Fake<IServicioSugerenciasProductos>());

        [TestMethod]
        public void Plegar_EnLaLista_SeGuardaConElParametroDelRapport_YNoTocaElDeLaPlantilla()
        {
            // Nesto#557: la cinta cambia de sitio pero el plegado sigue siendo el del rapport (SugerenciasRapportPlegadas)
            var (vm, _, configuracion, _) = CrearViewModel();
            Assert.IsTrue(vm.CintaSugerencias.Desplegadas, "Sin preferencia, desplegada");

            vm.CintaSugerencias.PlegarCommand.Execute(null);

            Assert.IsTrue(vm.CintaSugerencias.Plegadas);
            A.CallTo(() => configuracion.GuardarParametro("1", Parametros.Claves.SugerenciasRapportPlegadas, "1"))
                .MustHaveHappenedOnceExactly();
            A.CallTo(() => configuracion.GuardarParametro(A<string>._, Parametros.Claves.SugerenciasPlantillaPlegadas, A<string>._))
                .MustNotHaveHappened();
        }

        [TestMethod]
        public void AlAbrir_SeLeeElParametroDelRapport_NoElDeLaPlantilla()
        {
            IConfiguracion configuracion = A.Fake<IConfiguracion>();
            A.CallTo(() => configuracion.leerParametro(A<string>._, A<string>._)).Returns(Task.FromResult<string>(null));
            A.CallTo(() => configuracion.leerParametro("1", Parametros.Claves.SugerenciasPlantillaPlegadas)).Returns(Task.FromResult("0"));
            A.CallTo(() => configuracion.leerParametro("1", Parametros.Claves.SugerenciasRapportPlegadas)).Returns(Task.FromResult("1"));

            var vm = CrearConConfiguracion(configuracion);

            Assert.IsTrue(vm.CintaSugerencias.Plegadas, "En los rapports manda SugerenciasRapportPlegadas");
        }

        [TestMethod]
        public void AlAbrir_SinElParametroDelRapportEnLaBD_NoFalla_YSaleDesplegada()
        {
            // Si aún no se ha lanzado el script de la fila «(defecto)», la API da 404 y Configuracion.leerParametro lanza
            IConfiguracion configuracion = A.Fake<IConfiguracion>();
            A.CallTo(() => configuracion.leerParametro(A<string>._, A<string>._)).Returns(Task.FromResult<string>(null));
            A.CallTo(() => configuracion.leerParametro("1", Parametros.Claves.SugerenciasPlantillaPlegadas)).Returns(Task.FromResult("1"));
            A.CallTo(() => configuracion.leerParametro("1", Parametros.Claves.SugerenciasRapportPlegadas))
                .ThrowsAsync(new Exception("No se puede leer el parámetro"));

            var vm = CrearConConfiguracion(configuracion);

            Assert.IsTrue(vm.CintaSugerencias.Desplegadas, "Sin el parámetro, el valor por defecto: desplegada");
        }

        #endregion
    }
}
