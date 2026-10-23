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
    /// Ariadna#51 (corte 2, reposición 81119): lo leído al recibir una reposición ya no vive solo en la pantalla de Nesto. Cada
    /// lectura se guarda en el servidor (…/Lecturas), compartida con Ariadna y con otros Nesto; al abrirla se pinta lo ya leído
    /// y se termina con el IdRecepcion compartido. Con una API anterior (404), como antes.
    /// </summary>
    [TestClass]
    public class RecibirReposicionLecturasCompartidasTests
    {
        private static readonly Guid ID_COMPARTIDO = new("5b1d0c1e-51c2-4a51-9b11-811190000001");
        private const string GRUPO = "recepcion:REPO:81119:REI";

        private IServicioRecepcionReposiciones _servicio = null!;
        private IServicioDialogos _dialogos = null!;
        private IConfiguracion _configuracion = null!;
        private ISonidosLector _sonidos = null!;
        private IGruposAlmacenTiempoReal _grupos = null!;
        private RecibirReposicionViewModel _vm = null!;
        private TerminarRecepcionReposicion? _terminada;

        /// <summary>El servidor falso: los apuntes recibidos (de todos los equipos), que se suman.</summary>
        private List<LecturaEnCursoRecepcion> _apuntes = null!;
        private List<EquipoRecepcionReposicion> _equipos = null!;
        private List<List<LecturaEnCursoRecepcion>> _envios = null!;
        private bool _fallaElPost;
        private bool _apiAnterior;

        private static RecepcionReposicion Reposicion() => new()
        {
            Tipo = "REPO",
            Documento = "81119",
            Titulo = "Reposición 81119",
            PuedeTerminar = true,
            SeTerminaDesdeAqui = true,
            AdmiteRecibirEnParte = true,
            Lineas = new List<LineaRecepcionReposicion>
            {
                new() { Producto = "17404", Descripcion = "Cera", CodigoBarras = "8411", Cantidad = 2 },
                new() { Producto = "40057", Descripcion = "Banda", CodigoBarras = "8422", Cantidad = 1 },
                new() { Producto = "99001", Descripcion = "Sin código", SinCodigo = true, Cantidad = 3 }
            }
        };

        private LecturasRecepcionReposicion LoQueHayEnElServidor() => new()
        {
            IdRecepcion = ID_COMPARTIDO,
            Lecturas = _apuntes.Where(a => a.Producto != null)
                .GroupBy(a => a.Producto)
                .Select(g => new LecturaRecepcionReposicion { Producto = g.Key, Cantidad = g.Sum(a => a.Cantidad) })
                .ToList(),
            CodigosBulto = _apuntes.Where(a => a.CodigoBulto != null).Select(a => a.CodigoBulto!).Distinct().ToList(),
            Equipos = _equipos.ToList()
        };

        /// <summary>Lo que ha leído otro equipo (Ariadna) directamente en el servidor.</summary>
        private void OtroEquipoLee(string producto, int cantidad)
            => _apuntes.Add(new LecturaEnCursoRecepcion { IdCliente = Guid.NewGuid(), Producto = producto, Cantidad = cantidad, Dispositivo = "Ariadna" });

        [TestInitialize]
        public void Preparar()
        {
            _apuntes = new List<LecturaEnCursoRecepcion>();
            _equipos = new List<EquipoRecepcionReposicion>();
            _envios = new List<List<LecturaEnCursoRecepcion>>();
            _fallaElPost = false;
            _apiAnterior = false;
            _terminada = null;
            _servicio = A.Fake<IServicioRecepcionReposiciones>();
            _dialogos = A.Fake<IServicioDialogos>();
            _configuracion = A.Fake<IConfiguracion>();
            _sonidos = A.Fake<ISonidosLector>();
            _grupos = A.Fake<IGruposAlmacenTiempoReal>();
            A.CallTo(() => _grupos.IdConexion).Returns("conexion-nesto");
            A.CallTo(() => _configuracion.usuario).Returns("NUEVAVISION\\Paloma");
            A.CallTo(() => _configuracion.leerParametro(A<string>._, Parametros.Claves.AlmacenPedidoVta)).Returns("REI");
            A.CallTo(() => _servicio.LeerPendientes(A<string>._, "REI")).Returns(new List<RecepcionPendiente>
            {
                new() { Tipo = "REPO", Documento = "81119", Titulo = "Reposición 81119", Lineas = 3, Unidades = 6 }
            });
            A.CallTo(() => _servicio.LeerRecepcion(A<string>._, "REI", "81119")).Returns(Reposicion());
            A.CallTo(() => _servicio.LeerLecturas(A<string>._, "REI", "81119"))
                .ReturnsLazily(() => Task.FromResult(_apiAnterior ? null! : LoQueHayEnElServidor()));
            A.CallTo(() => _servicio.GuardarLecturas(A<string>._, "REI", "81119", A<List<LecturaEnCursoRecepcion>>._, A<string>._))
                .ReturnsLazily((string _, string _, string _, List<LecturaEnCursoRecepcion> lecturas, string _) =>
                {
                    if (_fallaElPost)
                    {
                        return Task.FromException<LecturasRecepcionReposicion>(new HttpRequestException("sin red"));
                    }
                    _envios.Add(lecturas);
                    // Un reenvío con el mismo IdCliente no suma dos veces
                    _apuntes.AddRange(lecturas.Where(l => _apuntes.All(a => a.IdCliente != l.IdCliente)));
                    return Task.FromResult(LoQueHayEnElServidor());
                });
            A.CallTo(() => _servicio.Terminar(A<string>._, "REI", "81119", A<TerminarRecepcionReposicion>._))
                .ReturnsLazily((string _, string _, string documento, TerminarRecepcionReposicion t) =>
                {
                    _terminada = t;
                    return Task.FromResult(new ResultadoRecepcionReposicion { Documento = documento });
                });
            A.CallTo(() => _dialogos.ShowConfirmationAsync(A<string>._, A<string>._)).Returns(true);
            _vm = new RecibirReposicionViewModel(_servicio, _dialogos, _configuracion, _sonidos, A.Fake<IServicioNavegacion>(), _ => { },
                null, _grupos);
        }

        private LineaRecibirReposicion Linea(string producto) => _vm.Lineas.Single(l => l.Producto == producto);

        private async Task LeerAsync(string codigo)
        {
            _vm.Lectura = codigo;
            _vm.LeerLecturaCommand.Execute(null);
            await _vm.LecturaProductoEnCurso;
            await _vm.LecturaEtiquetaEnCurso;
            await _vm.EnvioEnCurso;
        }

        [TestMethod]
        public async Task Abrir_PintaLoYaLeidoEnElServidorYQuienMasEstaRecibiendo()
        {
            // Paloma lo leyó en Nesto antes del corte; Andre ha seguido en Ariadna
            OtroEquipoLee("17404", 2);
            OtroEquipoLee("55555", 1);
            _equipos.Add(new EquipoRecepcionReposicion { Usuario = "Andre", Dispositivo = "Ariadna", Ultima = DateTime.Now });
            _equipos.Add(new EquipoRecepcionReposicion { Usuario = "Paloma", Dispositivo = Environment.MachineName, Ultima = DateTime.Now });

            await _vm.CargarAsync();

            Assert.IsTrue(_vm.LecturasCompartidas);
            Assert.AreEqual(2, Linea("17404").Leido);
            Assert.AreEqual(0, Linea("40057").Leido);
            Assert.IsTrue(Linea("55555").NoVenia, "Lo que no venía y ha leído otro equipo se apunta aparte");
            Assert.AreEqual(1, Linea("55555").Leido);
            Assert.AreEqual("También está recibiendo: Andre (Ariadna)", _vm.TextoOtrosEquipos, "Este equipo no sale");
            Assert.AreEqual(0, _envios.Count, "Pintar lo del servidor no es leer");
            A.CallTo(() => _grupos.EntrarEnGrupo(GRUPO)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task Leer_MandaElApunteAlServidorYPintaLoQueDevuelve()
        {
            await _vm.CargarAsync();
            OtroEquipoLee("40057", 1); // mientras tanto, en Ariadna

            await LeerAsync("8411");

            Assert.AreEqual(1, _envios.Count);
            LecturaEnCursoRecepcion apunte = _envios[0].Single();
            Assert.AreEqual("17404", apunte.Producto);
            Assert.AreEqual(1, apunte.Cantidad);
            Assert.AreNotEqual(Guid.Empty, apunte.IdCliente);
            Assert.AreEqual(Environment.MachineName, apunte.Dispositivo);
            A.CallTo(() => _servicio.GuardarLecturas(A<string>._, "REI", "81119", A<List<LecturaEnCursoRecepcion>>._, "conexion-nesto"))
                .MustHaveHappenedOnceExactly();
            Assert.AreEqual(1, Linea("17404").Leido);
            Assert.AreEqual(1, Linea("40057").Leido, "La respuesta trae también lo leído en Ariadna");
            A.CallTo(() => _sonidos.Correcto()).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task Leer_CadaLecturaLlevaSuIdCliente()
        {
            await _vm.CargarAsync();

            await LeerAsync("8411");
            await LeerAsync("8411");

            Assert.AreEqual(2, _envios.SelectMany(e => e).Select(a => a.IdCliente).Distinct().Count());
            Assert.AreEqual(2, Linea("17404").Leido);
        }

        [TestMethod]
        public async Task TeclearEnLeido_MandaLaDiferencia_TambienParaQuitar()
        {
            await _vm.CargarAsync();

            Linea("99001").Leido = 3;
            await _vm.EnvioEnCurso;
            Linea("99001").Leido = 1;
            await _vm.EnvioEnCurso;

            CollectionAssert.AreEqual(new[] { 3, -2 }, _envios.SelectMany(e => e).Select(a => a.Cantidad).ToArray());
            Assert.AreEqual(1, Linea("99001").Leido);
        }

        [TestMethod]
        public async Task Leer_SiElPostFalla_LaLecturaNoCuentaYSeAvisa()
        {
            await _vm.CargarAsync();
            _fallaElPost = true;

            await LeerAsync("8411");

            Assert.AreEqual(0, Linea("17404").Leido, "Lo que no ha llegado al servidor no cuenta");
            Assert.AreEqual(RecibirReposicionViewModel.TEXTO_LECTURA_NO_GUARDADA, _vm.Mensaje);
            Assert.AreEqual("No se ha podido guardar la lectura: compruebe la conexión.", _vm.Mensaje);
            A.CallTo(() => _sonidos.Error()).MustHaveHappened();
        }

        [TestMethod]
        public async Task Leer_LoQueNoVeniaYFallaElPost_NoSeQuedaApuntado()
        {
            A.CallTo(() => _servicio.BuscarProductosPorCodigo(A<string>._, "55555"))
                .Returns(new List<ProductoCodigoRecepcion> { new() { Producto = "55555", Nombre = "Crema de manos" } });
            await _vm.CargarAsync();
            _fallaElPost = true;

            await LeerAsync("55555");

            Assert.IsFalse(_vm.Lineas.Any(l => l.Producto == "55555"));
            Assert.AreEqual(RecibirReposicionViewModel.TEXTO_LECTURA_NO_GUARDADA, _vm.Mensaje);
        }

        [TestMethod]
        public async Task AvisoDeSignalR_DelGrupoDeLaReposicion_RecargaLoLeido()
        {
            await _vm.CargarAsync();
            OtroEquipoLee("40057", 1);
            _equipos.Add(new EquipoRecepcionReposicion { Usuario = "Andre", Dispositivo = "Ariadna" });

            _grupos.GrupoHaCambiado += Raise.FreeForm<EventHandler<string>>.With(_grupos, "Recepcion:REPO:81119:REI");
            await _vm.RecargaEnCurso;

            Assert.AreEqual(1, Linea("40057").Leido);
            Assert.AreEqual("También está recibiendo: Andre (Ariadna)", _vm.TextoOtrosEquipos);
            A.CallTo(() => _servicio.LeerLecturas(A<string>._, "REI", "81119")).MustHaveHappenedTwiceExactly();
        }

        [TestMethod]
        public async Task AvisoDeSignalR_DeOtroGrupo_NoRecarga()
        {
            await _vm.CargarAsync();

            _grupos.GrupoHaCambiado += Raise.FreeForm<EventHandler<string>>.With(_grupos, "recepcion:REPO:80000:REI");
            await _vm.RecargaEnCurso;

            A.CallTo(() => _servicio.LeerLecturas(A<string>._, A<string>._, A<string>._)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task Terminar_UsaElIdRecepcionCompartidoYLoLeidoEnElServidor()
        {
            await _vm.CargarAsync();
            await LeerAsync("8411");
            OtroEquipoLee("17404", 1); // Andre lee la otra en Ariadna y no ha llegado el aviso

            await _vm.TerminarCommand.ExecuteAsync(null);

            Assert.IsNotNull(_terminada);
            Assert.AreEqual(ID_COMPARTIDO, _terminada!.IdRecepcion);
            Assert.AreEqual(2, _terminada.Lecturas.Single(l => l.Producto == "17404").Cantidad, "Se termina con lo del servidor, de todos");
            A.CallTo(() => _grupos.SalirDeGrupo(GRUPO)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task Terminar_SiFallaLaApi_DiceQueLoLeidoSigueEnElServidor()
        {
            // Reposición 81119: el «Terminar» falló por un corte de la API y se perdió todo lo leído
            A.CallTo(() => _servicio.Terminar(A<string>._, "REI", "81119", A<TerminarRecepcionReposicion>._))
                .ThrowsAsync(new HttpRequestException("corte"));
            await _vm.CargarAsync();
            await LeerAsync("8411");

            await _vm.TerminarCommand.ExecuteAsync(null);

            StringAssert.Contains(_vm.Mensaje, "No se puede conectar con el servidor.");
            StringAssert.Contains(_vm.Mensaje, "Lo leído sigue guardado en el servidor: basta con volver a pulsar «Terminar»");
            Assert.AreEqual(1, Linea("17404").Leido, "Sigue a la vista");
            Assert.AreEqual(1, _apuntes.Count, "Y en el servidor");
            A.CallTo(() => _grupos.SalirDeGrupo(A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task Terminar_SiNoSePuedeComprobarLoLeido_NoTerminaYLoDice()
        {
            await _vm.CargarAsync();
            await LeerAsync("8411");
            A.CallTo(() => _servicio.LeerLecturas(A<string>._, "REI", "81119")).ThrowsAsync(new HttpRequestException("corte"));

            await _vm.TerminarCommand.ExecuteAsync(null);

            Assert.IsNull(_terminada);
            StringAssert.Contains(_vm.Mensaje, "Lo leído sigue guardado en el servidor");
        }

        [TestMethod]
        public async Task ApiAnterior_SinLecturas_ComoAntesSoloEnLaPantalla()
        {
            _apiAnterior = true;
            await _vm.CargarAsync();

            await LeerAsync("8411");
            await _vm.TerminarCommand.ExecuteAsync(null);

            Assert.IsFalse(_vm.LecturasCompartidas);
            A.CallTo(() => _servicio.GuardarLecturas(A<string>._, A<string>._, A<string>._, A<List<LecturaEnCursoRecepcion>>._, A<string>._))
                .MustNotHaveHappened();
            A.CallTo(() => _grupos.EntrarEnGrupo(A<string>._)).MustNotHaveHappened();
            Assert.IsNotNull(_terminada);
            Assert.AreNotEqual(Guid.Empty, _terminada!.IdRecepcion);
            Assert.AreNotEqual(ID_COMPARTIDO, _terminada.IdRecepcion);
            Assert.AreEqual(1, _terminada.Lecturas.Single().Cantidad);
        }

        [TestMethod]
        public async Task CerrarLaPestana_SaleDelGrupo()
        {
            await _vm.CargarAsync();

            Assert.IsTrue(_vm.ConfirmTabClose());

            A.CallTo(() => _grupos.SalirDeGrupo(GRUPO)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void TextoEquipos_QuitaElDominioYEsteEquipo()
        {
            string? texto = _vm.TextoEquipos(new[]
            {
                new EquipoRecepcionReposicion { Usuario = "NUEVAVISION\\Paloma", Dispositivo = Environment.MachineName },
                new EquipoRecepcionReposicion { Usuario = "NUEVAVISION\\Paloma", Dispositivo = "Ariadna" },
                new EquipoRecepcionReposicion { Usuario = "Andre", Dispositivo = "Ariadna" }
            });

            Assert.AreEqual("También está recibiendo: Paloma (Ariadna), Andre (Ariadna)", texto);
            Assert.IsNull(_vm.TextoEquipos(new[] { new EquipoRecepcionReposicion { Usuario = "Paloma", Dispositivo = Environment.MachineName } }));
        }
    }
}
