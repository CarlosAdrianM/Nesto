using FakeItEasy;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Models;
using Nesto.Infrastructure.Services;
using Nesto.Infrastructure.Shared;
using Nesto.Modules.Producto;
using Nesto.Modules.Producto.ViewModels;

namespace Producto.Tests
{
    /// <summary>
    /// Nesto#590 (NestoAPI#772): la recogida de la reposición se pone al día sola por SignalR (grupo «Reposiciones:ALC» del
    /// AlmacenHub, sin sondeo) y el «Seguimiento» de una reposición enseña su historia por bulto.
    /// </summary>
    [TestClass]
    public class EnviarReposicionRecogidaYSeguimientoTests
    {
        private IServicioEnvioReposiciones _servicio = null!;
        private IServicioDialogos _dialogos = null!;
        private IGruposAlmacenTiempoReal _grupos = null!;
        private EnviarReposicionViewModel _vm = null!;

        private static EstadoReposicionPuntual Estado(int recogidos, int total) => new()
        {
            Origen = "ALC",
            Destino = "ALG",
            Situacion = EstadoReposicionPuntual.ABIERTA,
            SePuede = true,
            BultosRecogidos = recogidos,
            BultosTotal = total,
            RepartidorRecogida = recogidos > 0 ? "Andre" : null
        };

        [TestInitialize]
        public void Preparar()
        {
            _servicio = A.Fake<IServicioEnvioReposiciones>();
            _dialogos = A.Fake<IServicioDialogos>();
            _grupos = A.Fake<IGruposAlmacenTiempoReal>();
            IConfiguracion configuracion = A.Fake<IConfiguracion>();
            A.CallTo(() => configuracion.leerParametro(A<string>._, Parametros.Claves.AlmacenPedidoVta)).Returns("alc ");
            A.CallTo(() => _servicio.LeerEnPreparacion(A<string>._, "ALC")).Returns(Task.FromResult<ReposicionEnPreparacion>(null!));
            A.CallTo(() => _servicio.PuedeRellenarManual()).Returns(true);
            A.CallTo(() => _servicio.LeerEstadoPuntual(A<string>._, A<string>._)).ReturnsNextFromSequence(Estado(0, 7), Estado(6, 7));
            _vm = new EnviarReposicionViewModel(_servicio, _dialogos, configuracion, A.Fake<ISonidosLector>(), A.Fake<IServicioNavegacion>(),
                _ => { }, null, _grupos);
        }

        [TestMethod]
        public void GrupoReposiciones_TieneLaFormaDeLaApi()
        {
            Assert.AreEqual("Reposiciones:REI", GruposAlmacen.Reposiciones(" rei "));
        }

        [TestMethod]
        public async Task AlCargar_EntraEnElGrupoDeSuTienda()
        {
            await _vm.CargarAsync();

            A.CallTo(() => _grupos.EntrarEnGrupo("Reposiciones:ALC")).MustHaveHappenedOnceExactly();
            Assert.AreEqual("Sin recoger", _vm.EstadoRecogida);
        }

        [TestMethod]
        public async Task AlVolverACargar_NoEntraDosVeces()
        {
            await _vm.CargarAsync();
            await _vm.CargarAsync();

            A.CallTo(() => _grupos.EntrarEnGrupo(A<string>._)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task ConUnAvisoDeSuGrupo_ReleeElEstado()
        {
            await _vm.CargarAsync();

            _grupos.GrupoHaCambiado += Raise.FreeForm<EventHandler<string>>.With(_grupos, "reposiciones:alc");
            await _vm.RecargaEnCurso;

            A.CallTo(() => _servicio.LeerEstadoPuntual("1", "ALC")).MustHaveHappenedTwiceExactly();
            Assert.AreEqual("Recogidos 6 de 7 bultos por Andre", _vm.EstadoRecogida);
        }

        [TestMethod]
        public async Task ConUnAvisoDeOtroGrupo_NoRelee()
        {
            await _vm.CargarAsync();

            _grupos.GrupoHaCambiado += Raise.FreeForm<EventHandler<string>>.With(_grupos, "Reposiciones:REI");
            await _vm.RecargaEnCurso;

            A.CallTo(() => _servicio.LeerEstadoPuntual(A<string>._, A<string>._)).MustHaveHappenedOnceExactly();
            Assert.AreEqual("Sin recoger", _vm.EstadoRecogida);
        }

        [TestMethod]
        public async Task AlCerrarLaPestana_SaleDelGrupoYYaNoRelee()
        {
            await _vm.CargarAsync();

            Assert.IsTrue(_vm.ConfirmTabClose());
            _grupos.GrupoHaCambiado += Raise.FreeForm<EventHandler<string>>.With(_grupos, "Reposiciones:ALC");
            await _vm.RecargaEnCurso;

            A.CallTo(() => _grupos.SalirDeGrupo("Reposiciones:ALC")).MustHaveHappenedOnceExactly();
            A.CallTo(() => _servicio.LeerEstadoPuntual(A<string>._, A<string>._)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task SinTiendaValida_NoEntraEnNingunGrupo()
        {
            IConfiguracion algete = A.Fake<IConfiguracion>();
            A.CallTo(() => algete.leerParametro(A<string>._, Parametros.Claves.AlmacenPedidoVta)).Returns("ALG");
            var vm = new EnviarReposicionViewModel(_servicio, _dialogos, algete, A.Fake<ISonidosLector>(), A.Fake<IServicioNavegacion>(), _ => { }, null, _grupos);

            await vm.CargarAsync();

            A.CallTo(() => _grupos.EntrarEnGrupo(A<string>._)).MustNotHaveHappened();
        }

        // ---------------------------------------------------------------- seguimiento

        private static SeguimientoReposicion Seguimiento() => new()
        {
            Empresa = "1",
            Traspaso = 81010,
            Origen = "ALC",
            Destino = "ALG",
            Eventos = new List<EventoSeguimiento>
            {
                new() { FechaHora = new DateTime(2026, 10, 31, 9, 5, 0), Tipo = "Preparada", Usuario = "NUEVAVISION\\Paloma" },
                new() { FechaHora = new DateTime(2026, 10, 31, 8, 0, 0), Tipo = "Creada", Usuario = "Automatico" }
            },
            Bultos = new List<BultoSeguimiento>
            {
                new()
                {
                    Codigo = "R81010-2", Bulto = 2, Total = 2, Estado = "En la furgoneta de Andre",
                    Eventos = new List<EventoSeguimiento>
                    {
                        new() { FechaHora = new DateTime(2026, 10, 31, 12, 10, 0), Tipo = "Recogido", Usuario = "NUEVAVISION\\Andre", Origen = "Repartidor" }
                    }
                },
                new()
                {
                    Codigo = "R81010-1", Bulto = 1, Total = 2, Estado = "Recibido",
                    Eventos = new List<EventoSeguimiento>
                    {
                        new() { FechaHora = new DateTime(2026, 10, 31, 17, 30, 0), Tipo = "Recibido", Usuario = "Pedro", Detalle = "en Algete" },
                        new() { FechaHora = new DateTime(2026, 10, 31, 9, 6, 0), Tipo = "Etiquetado", Usuario = "Paloma" }
                    }
                }
            }
        };

        [TestMethod]
        public void TextoSeguimiento_CabeceraYCadaBultoEnOrden()
        {
            string texto = EnviarReposicionViewModel.TextoSeguimiento(Seguimiento());

            string[] lineas = texto.Replace("\r", string.Empty).Split('\n');
            Assert.AreEqual("Reposición 81010: ALC → ALG", lineas[0]);
            Assert.AreEqual("  31/10 8:00  Creada  Automatico", lineas[1]);
            Assert.AreEqual("  31/10 9:05  Preparada  Paloma", lineas[2]);
            StringAssert.Contains(texto, "Bulto 1 de 2 (R81010-1): Recibido");
            StringAssert.Contains(texto, "  31/10 17:30  Recibido  Pedro (en Algete)");
            Assert.IsTrue(texto.IndexOf("Bulto 1 de 2") < texto.IndexOf("Bulto 2 de 2"), "Por número de bulto");
            Assert.IsTrue(texto.IndexOf("Etiquetado") < texto.IndexOf("Recibido  Pedro"), "Los eventos, por hora");
            StringAssert.Contains(texto, "Bulto 2 de 2 (R81010-2): En la furgoneta de Andre");
            StringAssert.Contains(texto, "  31/10 12:10  Recogido  Andre");
        }

        [TestMethod]
        public async Task Seguimiento_DeUnaEnviada_LoEnsenaEnUnDialogo()
        {
            A.CallTo(() => _servicio.LeerSeguimiento("1", 81010)).Returns(Seguimiento());

            await _vm.SeguimientoEnviadaCommand.ExecuteAsync(new ReposicionEnviada { Traspaso = 81010 });

            A.CallTo(() => _dialogos.ShowNotification("Seguimiento de la reposición 81010",
                A<string>.That.StartsWith("Reposición 81010: ALC → ALG"))).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task Seguimiento_PorNumero_ConLaR_YSiNoSeSabeNada_LoDice()
        {
            A.CallTo(() => _servicio.LeerSeguimiento("1", 81999)).Returns(Task.FromResult<SeguimientoReposicion>(null!));
            await _vm.CargarAsync();
            _vm.NumeroReimprimirBultos = "R81999";

            Assert.IsTrue(_vm.SeguimientoPorNumeroCommand.CanExecute(null));
            await _vm.SeguimientoPorNumeroCommand.ExecuteAsync(null);

            A.CallTo(() => _dialogos.ShowNotification("Seguimiento de la reposición 81999", "No se sabe nada de la reposición 81999."))
                .MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task Seguimiento_SinConexion_LoDiceSinRomper()
        {
            A.CallTo(() => _servicio.LeerSeguimiento(A<string>._, A<int>._)).ThrowsAsync(new HttpRequestException("sin red"));

            await _vm.SeguimientoEnviadaCommand.ExecuteAsync(new ReposicionEnviada { Traspaso = 81010 });

            StringAssert.StartsWith(_vm.Mensaje, "No se puede conectar con el servidor");
        }
    }
}
