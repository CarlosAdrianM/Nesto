using ControlesUsuario.Services;
using FakeItEasy;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Modules.Producto;
using Nesto.Modules.Producto.ViewModels;
using Newtonsoft.Json.Linq;

namespace Producto.Tests
{
    /// <summary>
    /// Sugerencia 1277 (Carlos 20/11/26): la ventana «Envíos internos» entre sedes: validaciones del alta, alta con etiquetas, lista de
    /// vivos (o también terminados), reimprimir, anular con 409 EN_FURGONETA y un servidor que aún no lo tiene.
    /// </summary>
    [TestClass]
    public class EnviosInternosViewModelTests
    {
        private IServicioEnviosInternos _servicio = null!;
        private IConfiguracion _configuracion = null!;
        private IServicioDialogos _dialogos = null!;

        [TestInitialize]
        public void Inicializar()
        {
            _servicio = A.Fake<IServicioEnviosInternos>();
            _configuracion = A.Fake<IConfiguracion>();
            _dialogos = A.Fake<IServicioDialogos>();
            A.CallTo(() => _configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ALMACEN)).Returns(true);
            A.CallTo(() => _servicio.Leer(A<int>._, A<bool>._)).ReturnsLazily(() => Task.FromResult(new List<EnvioInterno> { Vivo(12), Vivo(13) }));
        }

        private EnviosInternosViewModel Crear() => new EnviosInternosViewModel(_servicio, _configuracion, _dialogos);

        private static EnvioInterno Vivo(int id) => new EnvioInterno
        {
            Id = id,
            Origen = "ALG",
            Destino = "ALC",
            Descripcion = "Papel higiénico y café",
            Bultos = 2,
            Estado = EnvioInterno.ESTADO_EN_CAMINO,
            UsuarioAlta = "carlos",
            FechaAlta = new DateTime(2026, 11, 20, 10, 15, 0),
            BultosDetalle = new List<BultoEnvioInterno>
            {
                new BultoEnvioInterno { Codigo = $"EI{id}-1", Situacion = "En la furgoneta de Miguel" },
                new BultoEnvioInterno { Codigo = $"EI{id}-2", Situacion = "En Algete, sin recoger" }
            }
        };

        private static EnviosInternosViewModel Rellenar(EnviosInternosViewModel vm, string? origen = "ALG", string? destino = "ALC",
            string? descripcion = "Papel higiénico", string bultos = "2")
        {
            vm.Origen = origen;
            vm.Destino = destino;
            vm.Descripcion = descripcion;
            vm.BultosTexto = bultos;
            return vm;
        }

        [TestMethod]
        public void MotivoAltaNoValida_CadaRegla()
        {
            Assert.AreEqual(EnviosInternosViewModel.MENSAJE_FALTA_ORIGEN, EnviosInternosViewModel.MotivoAltaNoValida(null, "ALC", "x", "1", out _));
            Assert.AreEqual(EnviosInternosViewModel.MENSAJE_FALTA_ORIGEN, EnviosInternosViewModel.MotivoAltaNoValida("MAD", "ALC", "x", "1", out _));
            Assert.AreEqual(EnviosInternosViewModel.MENSAJE_FALTA_DESTINO, EnviosInternosViewModel.MotivoAltaNoValida("ALG", "", "x", "1", out _));
            Assert.AreEqual(EnviosInternosViewModel.MENSAJE_MISMA_SEDE, EnviosInternosViewModel.MotivoAltaNoValida("REI", "rei", "x", "1", out _));
            Assert.AreEqual(EnviosInternosViewModel.MENSAJE_FALTA_DESCRIPCION, EnviosInternosViewModel.MotivoAltaNoValida("ALG", "REI", "  ", "1", out _));
            Assert.AreEqual(EnviosInternosViewModel.MENSAJE_BULTOS, EnviosInternosViewModel.MotivoAltaNoValida("ALG", "REI", "x", "0", out _));
            Assert.AreEqual(EnviosInternosViewModel.MENSAJE_BULTOS, EnviosInternosViewModel.MotivoAltaNoValida("ALG", "REI", "x", "51", out _));
            Assert.AreEqual(EnviosInternosViewModel.MENSAJE_BULTOS, EnviosInternosViewModel.MotivoAltaNoValida("ALG", "REI", "x", "dos", out _));
            Assert.IsNull(EnviosInternosViewModel.MotivoAltaNoValida("ALG", "REI", "x", "50", out int bultos));
            Assert.AreEqual(50, bultos);
        }

        [TestMethod]
        public async Task Crear_NoValido_NoLlama()
        {
            var vm = Rellenar(Crear(), destino: "ALG");

            await vm.CrearAsync();

            A.CallTo(() => _servicio.Crear(A<NuevoEnvioInterno>._)).MustNotHaveHappened();
            Assert.AreEqual(EnviosInternosViewModel.MENSAJE_MISMA_SEDE, vm.Mensaje);
        }

        [TestMethod]
        public async Task Crear_Valido_MandaElAltaDiceLasEtiquetasYRelee()
        {
            var vm = Rellenar(Crear(), descripcion: "  Papel higiénico  ");
            NuevoEnvioInterno? mandado = null;
            A.CallTo(() => _servicio.Crear(A<NuevoEnvioInterno>._))
                .Invokes((NuevoEnvioInterno n) => mandado = n)
                .Returns(Task.FromResult(new EnvioInterno
                {
                    Id = 12, Origen = "ALG", Destino = "ALC", Bultos = 2, Estado = EnvioInterno.ESTADO_PENDIENTE,
                    Codigos = new List<string> { "EI12-1", "EI12-2" }, EtiquetasImpresas = true
                }));

            await vm.CrearAsync();

            Assert.IsNotNull(mandado);
            Assert.AreEqual("ALG", mandado!.Origen);
            Assert.AreEqual("ALC", mandado.Destino);
            Assert.AreEqual("Papel higiénico", mandado.Descripcion);
            Assert.AreEqual(2, mandado.Bultos);
            Assert.IsNull(mandado.Impresora);
            Assert.AreEqual("Envío interno 12 creado (Algete → Alcobendas, 2 bultos): etiquetas EI12-1, EI12-2 impresas.", vm.Mensaje);
            Assert.IsNull(vm.Descripcion, "listo para el siguiente");
            Assert.AreEqual(12, vm.Seleccionado!.Id);
            A.CallTo(() => _dialogos.ShowError(A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task Crear_SinImprimirLasEtiquetas_AvisaEnRojo()
        {
            var vm = Rellenar(Crear());
            A.CallTo(() => _servicio.Crear(A<NuevoEnvioInterno>._)).Returns(Task.FromResult(new EnvioInterno
            {
                Id = 14, Origen = "ALG", Destino = "ALC", Bultos = 2, Codigos = new List<string> { "EI14-1", "EI14-2" }, EtiquetasImpresas = false
            }));

            await vm.CrearAsync();

            StringAssert.Contains(vm.Mensaje, "NO se han impreso");
            A.CallTo(() => _dialogos.ShowError(A<string>.That.Contains("Reimprimir etiquetas"))).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task Crear_ServidorSinActualizar_LoDiceYDejaDeOfrecerlo()
        {
            var vm = Rellenar(Crear());
            A.CallTo(() => _servicio.Crear(A<NuevoEnvioInterno>._)).ThrowsAsync(ErroresApiReparto.Excepcion(503, "", ServicioEnviosInternos.MENSAJE_SIN_PERMISO));

            await vm.CrearAsync();

            A.CallTo(() => _dialogos.ShowError(ErroresApiReparto.MENSAJE_SERVIDOR_SIN_ACTUALIZAR)).MustHaveHappenedOnceExactly();
            Assert.IsFalse(vm.Disponible);
            Assert.IsFalse(vm.CrearCommand.CanExecute(null));
            Assert.AreEqual(ErroresApiReparto.MENSAJE_SERVIDOR_SIN_ACTUALIZAR, vm.Aviso);
        }

        [TestMethod]
        public async Task Crear_SinPermiso403_ElMensajeDeUsted()
        {
            var vm = Rellenar(Crear());
            A.CallTo(() => _servicio.Crear(A<NuevoEnvioInterno>._)).ThrowsAsync(ErroresApiReparto.Excepcion(403, "", ServicioEnviosInternos.MENSAJE_SIN_PERMISO));

            await vm.CrearAsync();

            A.CallTo(() => _dialogos.ShowError(ServicioEnviosInternos.MENSAJE_SIN_PERMISO)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task AlLlegar_LeeLosVivosDeSieteDias_YLaCasillaLeeTambienLosTerminados()
        {
            var vm = Crear();

            vm.AlLlegar(new ParametrosNavegacion());
            await vm.CargaEnCurso;

            A.CallTo(() => _servicio.Leer(7, true)).MustHaveHappenedOnceExactly();
            Assert.AreEqual(2, vm.Lista.Count);
            Assert.AreEqual("Algete → Alcobendas", vm.Lista[0].Trayecto);
            Assert.AreEqual("En camino", vm.Lista[0].EstadoTexto);
            Assert.AreEqual("EI12-1: En la furgoneta de Miguel" + Environment.NewLine + "EI12-2: En Algete, sin recoger", vm.Lista[0].DondeEstan);

            vm.VerTerminados = true;
            await vm.CargaEnCurso;

            A.CallTo(() => _servicio.Leer(7, false)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task Leer_ServidorSinActualizar_AvisoYListaVaciaSinDialogos()
        {
            A.CallTo(() => _servicio.Leer(A<int>._, A<bool>._)).ThrowsAsync(ErroresApiReparto.Excepcion(404, "{\"Message\":\"No HTTP resource was found that matches the request URI.\"}", null));
            var vm = Crear();

            await vm.CargarAsync();

            Assert.AreEqual(ErroresApiReparto.MENSAJE_SERVIDOR_SIN_ACTUALIZAR, vm.Aviso);
            Assert.AreEqual(0, vm.Lista.Count);
            Assert.IsFalse(vm.Disponible);
            A.CallTo(() => _dialogos.ShowError(A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task Anular_EnFurgoneta409_SuMensajeYRelee()
        {
            var vm = Crear();
            await vm.CargarAsync();
            A.CallTo(() => _dialogos.GetText(A<string>._, A<string>._)).Returns("Ya no hace falta");
            A.CallTo(() => _servicio.Anular(12, "Ya no hace falta")).ThrowsAsync(ErroresApiReparto.Excepcion(409,
                "{\"Message\":\"No se puede: el bulto EI12-1 lo lleva Miguel en la furgoneta.\",\"Codigo\":\"EN_FURGONETA\"}", null));

            await vm.AnularAsync();

            A.CallTo(() => _dialogos.ShowError("No se puede: el bulto EI12-1 lo lleva Miguel en la furgoneta.")).MustHaveHappenedOnceExactly();
            A.CallTo(() => _servicio.Leer(A<int>._, A<bool>._)).MustHaveHappenedTwiceExactly();
        }

        [TestMethod]
        public async Task Anular_SinMotivo_NoLlama_YConMotivoAnula()
        {
            var vm = Crear();
            await vm.CargarAsync();
            A.CallTo(() => _dialogos.GetText(A<string>._, A<string>._)).ReturnsNextFromSequence("  ", "Se manda en la reposición");

            await vm.AnularAsync();
            A.CallTo(() => _servicio.Anular(A<int>._, A<string>._)).MustNotHaveHappened();
            A.CallTo(() => _dialogos.ShowError(EnviosInternosViewModel.MENSAJE_FALTA_MOTIVO)).MustHaveHappenedOnceExactly();

            await vm.AnularAsync();
            A.CallTo(() => _servicio.Anular(12, "Se manda en la reposición")).MustHaveHappenedOnceExactly();
            Assert.AreEqual("Envío interno 12 anulado.", vm.Mensaje);
        }

        [TestMethod]
        public async Task Reimprimir_LlamaYDiceLosCodigos()
        {
            var vm = Crear();
            await vm.CargarAsync();
            A.CallTo(() => _servicio.ImprimirEtiquetas(12, null)).Returns(Task.FromResult(new EnvioInterno
            {
                Id = 12, Codigos = new List<string> { "EI12-1", "EI12-2" }, EtiquetasImpresas = true
            }));

            await vm.ReimprimirAsync();

            Assert.AreEqual("Etiquetas del envío interno 12 impresas (EI12-1, EI12-2).", vm.Mensaje);
        }

        [TestMethod]
        public void SinGrupo_NiLeeNiDejaHacerNada()
        {
            var vm = new EnviosInternosViewModel(_servicio, A.Fake<IConfiguracion>(), _dialogos);

            vm.AlLlegar(new ParametrosNavegacion());

            Assert.IsFalse(vm.PuedeUsar);
            Assert.AreEqual(EnviosInternosViewModel.MENSAJE_SIN_GRUPO, vm.Aviso);
            Assert.IsFalse(vm.CrearCommand.CanExecute(null));
            A.CallTo(() => _servicio.Leer(A<int>._, A<bool>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public void Servicio_RutasYCuerpos()
        {
            Assert.AreEqual("RepartoPropio/EnviosInternos?dias=7&soloVivos=true", ServicioEnviosInternos.UrlLeer(7, true));
            Assert.AreEqual("RepartoPropio/EnviosInternos/12/ImprimirEtiquetas", ServicioEnviosInternos.UrlImprimir(12));
            Assert.AreEqual("RepartoPropio/EnviosInternos/12?motivo=ya%20no", ServicioEnviosInternos.UrlAnular(12, " ya no "));
            JObject alta = JObject.Parse(ServicioEnviosInternos.CuerpoCrear(new NuevoEnvioInterno { Origen = "ALG", Destino = "REI", Descripcion = "Café", Bultos = 1 }));
            Assert.AreEqual("ALG", (string?)alta["Origen"]);
            Assert.AreEqual("REI", (string?)alta["Destino"]);
            Assert.AreEqual("Café", (string?)alta["Descripcion"]);
            Assert.AreEqual(1, (int)alta["Bultos"]!);
            Assert.IsNull(alta["Impresora"], "sin impresora no viaja");
        }

        [TestMethod]
        public void MenuProducto_SoloAlmacenDireccionEInformatica()
        {
            Assert.IsTrue(EnviosInternosViewModel.PuedeUsarEnviosInternos(_configuracion));
            var tienda = A.Fake<IConfiguracion>();
            A.CallTo(() => tienda.UsuarioEnGrupo(Constantes.GruposSeguridad.TIENDAS)).Returns(true);
            Assert.IsFalse(EnviosInternosViewModel.PuedeUsarEnviosInternos(tienda));
        }
    }
}
