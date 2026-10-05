using FakeItEasy;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Models;
using Nesto.Infrastructure.Services;
using Nesto.Infrastructure.Shared;
using Nesto.Modules.Producto.ViewModels;

namespace Producto.Tests
{
    /// <summary>
    /// Recibir en la tienda una reposición que sale de Algete (NestoAPI#553), con la misma API que Ariadna: se elige la
    /// reposición, se lee lo que llega, y al terminar entra LO LEÍDO; el servidor informa de las diferencias.
    /// </summary>
    [TestClass]
    public class RecibirReposicionViewModelTests
    {
        private IServicioRecepcionReposiciones _servicio = null!;
        private IServicioDialogos _dialogos = null!;
        private IConfiguracion _configuracion = null!;
        private RecibirReposicionViewModel _vm = null!;
        private TerminarRecepcionReposicion? _terminada;

        private static RecepcionReposicion Reposicion(bool puedeTerminar = true) => new()
        {
            Tipo = "REPO",
            Documento = "80878",
            Titulo = "Reposición 80878",
            PuedeTerminar = puedeTerminar,
            SeTerminaDesdeAqui = true,
            Lineas = new List<LineaRecepcionReposicion>
            {
                new() { Producto = "17404", Descripcion = "Cera", CodigoBarras = "8411", Cantidad = 2 },
                new() { Producto = "40057", Descripcion = "Banda", CodigoBarras = "8422", Cantidad = 1 },
                new() { Producto = "99001", Descripcion = "Sin código", SinCodigo = true, Cantidad = 3 }
            }
        };

        [TestInitialize]
        public void Preparar()
        {
            _servicio = A.Fake<IServicioRecepcionReposiciones>();
            _dialogos = A.Fake<IServicioDialogos>();
            _configuracion = A.Fake<IConfiguracion>();
            A.CallTo(() => _configuracion.leerParametro(A<string>._, Parametros.Claves.AlmacenPedidoVta)).Returns("ALC");
            A.CallTo(() => _servicio.LeerPendientes(A<string>._, "ALC")).Returns(new List<RecepcionPendiente>
            {
                new() { Tipo = "REPO", Documento = "80878", Titulo = "Reposición 80878", Lineas = 3, Unidades = 6 }
            });
            A.CallTo(() => _servicio.LeerRecepcion(A<string>._, "ALC", "80878")).Returns(Reposicion());
            A.CallTo(() => _servicio.Terminar(A<string>._, "ALC", "80878", A<TerminarRecepcionReposicion>._))
                .ReturnsLazily((string _, string _, string _, TerminarRecepcionReposicion t) =>
                {
                    _terminada = t;
                    return Task.FromResult(new ResultadoRecepcionReposicion { Documento = "80878" });
                });
            A.CallTo(() => _dialogos.ShowConfirmationAsync(A<string>._, A<string>._)).Returns(true);
            _vm = new RecibirReposicionViewModel(_servicio, _dialogos, _configuracion);
        }

        private async Task AbiertaAsync()
        {
            await _vm.CargarAsync();
            await _vm.ElegirCommand.ExecuteAsync(_vm.Pendientes[0]);
        }

        [TestMethod]
        public async Task Cargar_EnsenaLasReposicionesPendientesDelAlmacenDelUsuario()
        {
            await _vm.CargarAsync();

            Assert.AreEqual("ALC", _vm.Almacen);
            Assert.AreEqual(1, _vm.Pendientes.Count);
            Assert.AreEqual("80878", _vm.Pendientes[0].Documento);
        }

        [TestMethod]
        public async Task Cargar_SinAlmacenDelUsuario_LoDiceYNoPideNada()
        {
            A.CallTo(() => _configuracion.leerParametro(A<string>._, Parametros.Claves.AlmacenPedidoVta)).Returns(" ");

            await _vm.CargarAsync();

            StringAssert.Contains(_vm.Mensaje, "almacén");
            A.CallTo(() => _servicio.LeerPendientes(A<string>._, A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task Elegir_EnsenaLasLineasConLoEnviado()
        {
            await AbiertaAsync();

            Assert.AreEqual(3, _vm.Lineas.Count);
            Assert.AreEqual(2, _vm.Lineas[0].Enviado);
            Assert.AreEqual(0, _vm.Lineas[0].Leido);
        }

        [TestMethod]
        public async Task Leer_UnCodigoDeBarras_SumaUnoEnSuLinea()
        {
            await AbiertaAsync();

            _vm.LeerCommand.Execute("8411");
            _vm.LeerCommand.Execute(" 17404 ");

            Assert.AreEqual(2, _vm.Lineas[0].Leido, "Vale el código de barras y la referencia");
        }

        [TestMethod]
        public async Task Leer_UnCodigoQueNoVenia_SeApuntaAparte()
        {
            await AbiertaAsync();

            _vm.LeerCommand.Execute("55555");

            LineaRecibirReposicion ajena = _vm.Lineas.Single(l => l.Producto == "55555");
            Assert.IsTrue(ajena.NoVenia);
            Assert.AreEqual(1, ajena.Leido);
            StringAssert.Contains(_vm.Mensaje, "no venía");
        }

        [TestMethod]
        public async Task Terminar_ConDiferencias_PideConfirmacionConElResumenYMandaLoLeido()
        {
            await AbiertaAsync();
            _vm.LeerCommand.Execute("8411");
            _vm.LeerCommand.Execute("8422");
            _vm.LeerCommand.Execute("8422");
            _vm.Lineas[2].Leido = 3;

            await _vm.TerminarCommand.ExecuteAsync(null);

            A.CallTo(() => _dialogos.ShowConfirmationAsync(A<string>._,
                A<string>.That.Matches(t => t.Contains("Faltan 1") && t.Contains("Sobran 1") && t.Contains("entra lo leído"))))
                .MustHaveHappenedOnceExactly();
            Assert.IsNotNull(_terminada);
            Assert.AreNotEqual(Guid.Empty, _terminada!.IdRecepcion);
            Assert.AreEqual(1, _terminada.Lecturas.Single(l => l.Producto == "17404").Cantidad);
            Assert.AreEqual(2, _terminada.Lecturas.Single(l => l.Producto == "40057").Cantidad);
            Assert.AreEqual(3, _terminada.Lecturas.Single(l => l.Producto == "99001").Cantidad);
        }

        [TestMethod]
        public async Task Terminar_SiNoSeConfirma_NoMandaNada()
        {
            await AbiertaAsync();
            A.CallTo(() => _dialogos.ShowConfirmationAsync(A<string>._, A<string>._)).Returns(false);

            await _vm.TerminarCommand.ExecuteAsync(null);

            Assert.IsNull(_terminada);
        }

        [TestMethod]
        public async Task Terminar_ReintentarUsaElMismoIdentificador()
        {
            await AbiertaAsync();
            var ids = new List<Guid>();
            A.CallTo(() => _servicio.Terminar(A<string>._, A<string>._, A<string>._, A<TerminarRecepcionReposicion>._))
                .ReturnsLazily((string _, string _, string _, TerminarRecepcionReposicion t) =>
                {
                    ids.Add(t.IdRecepcion);
                    return ids.Count == 1
                        ? Task.FromException<ResultadoRecepcionReposicion>(new HttpRequestException("sin red"))
                        : Task.FromResult(new ResultadoRecepcionReposicion { Documento = "80878" });
                });

            await _vm.TerminarCommand.ExecuteAsync(null);
            await _vm.TerminarCommand.ExecuteAsync(null);

            Assert.AreEqual(2, ids.Count);
            Assert.AreEqual(ids[0], ids[1], "Así un reenvío no recibe dos veces");
        }

        [TestMethod]
        public async Task Terminar_EnsenaLoQueContestaElServidorYLaQuitaDeLaLista()
        {
            await AbiertaAsync();
            A.CallTo(() => _servicio.Terminar(A<string>._, A<string>._, A<string>._, A<TerminarRecepcionReposicion>._))
                .Returns(new ResultadoRecepcionReposicion
                {
                    Documento = "80878",
                    AvisadoA = @"NUEVAVISION\Andre",
                    Diferencias = new List<DiferenciaRecepcionReposicion> { new() { Producto = "17404", Esperado = 2, Leido = 0 } }
                });

            await _vm.TerminarCommand.ExecuteAsync(null);

            StringAssert.Contains(_vm.Mensaje, "Andre");
            StringAssert.Contains(_vm.Mensaje, "17404");
            Assert.AreEqual(0, _vm.Pendientes.Count);
            Assert.IsNull(_vm.Seleccionada);
        }

        [TestMethod]
        public async Task Terminar_SinPermiso_EnsenaElMotivoDelServidor()
        {
            await AbiertaAsync();
            A.CallTo(() => _servicio.Terminar(A<string>._, A<string>._, A<string>._, A<TerminarRecepcionReposicion>._))
                .Throws(new RecepcionReposicionException("Solo la puede recibir la tienda de destino."));

            await _vm.TerminarCommand.ExecuteAsync(null);

            Assert.AreEqual("Solo la puede recibir la tienda de destino.", _vm.Mensaje);
            Assert.AreEqual(1, _vm.Pendientes.Count);
        }

        [TestMethod]
        public async Task Terminar_SiElUsuarioNoPuedeTerminarla_NoSePuede()
        {
            A.CallTo(() => _servicio.LeerRecepcion(A<string>._, "ALC", "80878")).Returns(Reposicion(puedeTerminar: false));
            await AbiertaAsync();

            Assert.IsFalse(_vm.TerminarCommand.CanExecute(null));
            StringAssert.Contains(_vm.Mensaje, "no puedes terminarla");
        }
    }
}
