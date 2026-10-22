using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Events;
using Nesto.Infrastructure.Models;
using Nesto.Infrastructure.Services;
using Nesto.Modulos.Rapports;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RapportsTests
{
    /// <summary>
    /// Nesto#544: la cinta «Puede ofrecer» de la plantilla, en el rapport. Se pregunta con el cliente y el contacto del rapport
    /// y sin carrito, solo en un rapport nuevo; «✗ No le interesa» y «🚫 Quitar» mandan la respuesta igual que en la plantilla;
    /// sin sugerencias (o con la API caída) no se ve. «✓ Añadir» (corte 2) pregunta por el mensajero si hay una plantilla
    /// abierta del cliente y, si no, abre una nueva con los parámetros; «Anadido» lo apunta la plantilla.
    /// </summary>
    [TestClass]
    public class SugerenciasProductosRapportTests
    {
        private static readonly Guid SESION = new Guid("0f6c9a43-8f2e-4a55-b3d5-2b6c1c3e9a11");

        private static (RapportViewModel vm, IServicioSugerenciasProductos sugerencias, IConfiguracion configuracion, IServicioDialogos dialogos) CrearViewModel()
        {
            var (vm, sugerencias, configuracion, dialogos, _, _) = CrearViewModelConNavegacion();
            return (vm, sugerencias, configuracion, dialogos);
        }

        private static (RapportViewModel vm, IServicioSugerenciasProductos sugerencias, IConfiguracion configuracion, IServicioDialogos dialogos,
            IServicioNavegacion navegacion, IMessenger messenger) CrearViewModelConNavegacion()
        {
            IConfiguracion configuracion = A.Fake<IConfiguracion>();
            A.CallTo(() => configuracion.leerParametro(A<string>._, A<string>._)).Returns(Task.FromResult<string>(null));
            A.CallTo(() => configuracion.leerParametro("1", "Vendedor")).Returns(Task.FromResult("NV"));
            IServicioDialogos dialogos = A.Fake<IServicioDialogos>();
            IServicioSugerenciasProductos sugerencias = A.Fake<IServicioSugerenciasProductos>();
            IServicioNavegacion navegacion = A.Fake<IServicioNavegacion>();
            var messenger = new WeakReferenceMessenger();
            var vm = new RapportViewModel(configuracion, A.Fake<IRapportService>(), dialogos, messenger, sugerencias, navegacion);
            return (vm, sugerencias, configuracion, dialogos, navegacion, messenger);
        }

        private static SeguimientoClienteDTO RapportNuevo(string cliente = "15191", string contacto = "2") =>
            new SeguimientoClienteDTO { Id = 0, Empresa = "1  ", Cliente = cliente + "     ", Contacto = contacto + "  " };

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
        public async Task RapportNuevo_PideConElClienteYElContacto_SinCarrito_YEnsenaLasFichas()
        {
            var (vm, sugerencias, _, _) = CrearViewModel();
            vm.VendedorUsuario = "JE";
            CalcularSugerenciasProductosDTO enviada = null;
            A.CallTo(() => sugerencias.Calcular(A<CalcularSugerenciasProductosDTO>._))
                .Invokes((CalcularSugerenciasProductosDTO p) => enviada = p)
                .Returns(Task.FromResult(Respuesta(Ficha(11, "39000", 2), Ficha(10, "38000", 1), Ficha(12, "40000", 3))));
            vm.rapport = RapportNuevo();

            await vm.CintaSugerencias.RefrescarAsync();

            Assert.IsNotNull(enviada);
            Assert.AreEqual("1", enviada.Empresa);
            Assert.AreEqual("15191", enviada.Cliente);
            Assert.AreEqual("2", enviada.Contacto, "El contacto del rapport, no el de la ficha");
            Assert.AreEqual("JE", enviada.Vendedor);
            Assert.AreEqual(CalcularSugerenciasProductosDTO.APLICACION_NESTO, enviada.Aplicacion);
            Assert.IsNull(enviada.IdSesion, "La primera vez la sesión la crea la API");
            Assert.AreEqual(0, enviada.Productos.Count, "En el rapport no hay carrito");
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
            vm.rapport = RapportNuevo();

            await vm.CintaSugerencias.RefrescarAsync();

            Assert.AreEqual("NV", enviada.Vendedor);
        }

        [TestMethod]
        public async Task RapportYaGuardado_NoPide_NiSeVe()
        {
            var (vm, sugerencias, _, _) = CrearViewModel();
            SeguimientoClienteDTO guardado = RapportNuevo();
            guardado.Id = 7;
            vm.rapport = guardado;

            await vm.CintaSugerencias.RefrescarAsync();

            A.CallTo(() => sugerencias.Calcular(A<CalcularSugerenciasProductosDTO>._)).MustNotHaveHappened();
            Assert.IsFalse(vm.CintaSugerencias.HaySugerencias);
        }

        [TestMethod]
        public async Task SinCliente_NoPide()
        {
            var (vm, sugerencias, _, _) = CrearViewModel();
            vm.rapport = new SeguimientoClienteDTO();

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
            vm.rapport = RapportNuevo();
            await vm.CintaSugerencias.RefrescarAsync();
            Assert.AreEqual(SESION, vm.CintaSugerencias.IdSesion);

            vm.rapport.Cliente = "34867"; // lo cambia el SelectorCliente (binding TwoWay)

            Assert.IsFalse(vm.CintaSugerencias.HaySugerencias, "Lo de antes era para otro cliente");
            Assert.IsNull(vm.CintaSugerencias.IdSesion);
            await vm.CintaSugerencias.RefrescarAsync();
            Assert.AreEqual(2, enviadas.Count);
            Assert.AreEqual("34867", enviadas[1].Cliente);
            Assert.IsNull(enviadas[1].IdSesion);
        }

        [TestMethod]
        public async Task AlCambiarDeContacto_SeVuelveAPreguntarConLaMismaSesion()
        {
            var (vm, sugerencias, _, _) = CrearViewModel();
            var enviadas = new List<CalcularSugerenciasProductosDTO>();
            A.CallTo(() => sugerencias.Calcular(A<CalcularSugerenciasProductosDTO>._))
                .Invokes((CalcularSugerenciasProductosDTO p) => enviadas.Add(p))
                .Returns(Task.FromResult(Respuesta(Ficha(10, "38000", 1))));
            vm.rapport = RapportNuevo();
            await vm.CintaSugerencias.RefrescarAsync();

            vm.rapport.Contacto = "5";
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
            vm.rapport = RapportNuevo();
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
            vm.rapport = RapportNuevo();
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
            vm.rapport = RapportNuevo();

            await vm.CintaSugerencias.RefrescarAsync();

            Assert.IsFalse(vm.CintaSugerencias.HaySugerencias);
        }

        [TestMethod]
        public async Task ApiCaida_NoSeVe_NiHayErrores()
        {
            var (vm, sugerencias, _, dialogos) = CrearViewModel();
            A.CallTo(() => sugerencias.Calcular(A<CalcularSugerenciasProductosDTO>._)).Throws(new InvalidOperationException("Sin conexión"));
            vm.rapport = RapportNuevo();

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

        private static async Task<(RapportViewModel vm, IServicioSugerenciasProductos sugerencias, IServicioNavegacion navegacion, IMessenger messenger)> RapportConFichas()
        {
            var (vm, sugerencias, _, _, navegacion, messenger) = CrearViewModelConNavegacion();
            A.CallTo(() => sugerencias.Calcular(A<CalcularSugerenciasProductosDTO>._))
                .Returns(Task.FromResult(Respuesta(Ficha(10, "38000", 1), Ficha(11, "39000", 2))));
            vm.rapport = RapportNuevo();
            await vm.CintaSugerencias.RefrescarAsync();
            return (vm, sugerencias, navegacion, messenger);
        }

        [TestMethod]
        public async Task Anadir_SaleEnElRapport()
        {
            var (vm, _, _, _) = await RapportConFichas();

            Assert.IsTrue(vm.CintaSugerencias.PuedeAnadir);
            Assert.IsTrue(vm.CintaSugerencias.AnadirCommand.CanExecute(vm.CintaSugerencias.Sugerencias[0]));
        }

        [TestMethod]
        public async Task Anadir_SinPlantillaAbierta_AbreUnaNuevaConElClienteElContactoElProductoLaSugerenciaYLaSesion()
        {
            var (vm, sugerencias, navegacion, _) = await RapportConFichas();
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
            var (vm, sugerencias, navegacion, messenger) = await RapportConFichas();
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
            var (vm, _, navegacion, messenger) = await RapportConFichas();
            var plantilla = new PlantillaAbierta(messenger, entra: false);

            await vm.CintaSugerencias.AnadirAsync(vm.CintaSugerencias.Sugerencias[0]);

            Assert.AreEqual(2, vm.CintaSugerencias.Sugerencias.Count);
            A.CallTo(() => navegacion.RequestNavigate(A<string>._, A<string>._, A<ParametrosNavegacion>._)).MustNotHaveHappened();
            GC.KeepAlive(plantilla);
        }

        #endregion
    }
}
