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
    /// NestoAPI#762 (sugerencia 909, Carlos 30/10/26): al recibir una reposición, nada de cantidades a mano. Con SoloEscaneando
    /// «Leído» es de solo lectura, la referencia tecleada solo elige la línea y lo que llega sin etiqueta se apunta unidad a
    /// unidad con «Sin etiqueta» y su motivo (la API avisa al origen y a Compras); «−1» quita lo leído por error.
    /// </summary>
    [TestClass]
    public class RecibirReposicionSoloEscaneandoTests
    {
        private static readonly Guid ID_COMPARTIDO = new("5b1d0c1e-51c2-4a51-9b11-762000000001");

        private IServicioRecepcionReposiciones _servicio = null!;
        private ISonidosLector _sonidos = null!;
        private RecibirReposicionViewModel _vm = null!;
        private List<LecturaEnCursoRecepcion> _apuntes = null!;
        private List<PeticionSinEtiquetaRecepcion> _sinEtiqueta = null!;
        private bool _soloEscaneando;
        private bool _lecturasEnServidor;

        private RecepcionReposicion Reposicion() => new()
        {
            Tipo = "REPO",
            Documento = "81200",
            Titulo = "Reposición 81200",
            PuedeTerminar = true,
            SeTerminaDesdeAqui = true,
            SoloEscaneando = _soloEscaneando,
            Lineas = new List<LineaRecepcionReposicion>
            {
                new() { Producto = "17404", Descripcion = "Rollo papel camilla", CodigosBarras = new List<string> { "2900000174047" }, SinCodigo = true, Cantidad = 3 },
                new() { Producto = "40057", Descripcion = "Banda", CodigoBarras = "8422", Cantidad = 1 }
            }
        };

        private LecturasRecepcionReposicion LoQueHayEnElServidor() => new()
        {
            IdRecepcion = ID_COMPARTIDO,
            Lecturas = _apuntes.Where(a => a.Producto != null)
                .GroupBy(a => a.Producto)
                .Select(g => new LecturaRecepcionReposicion { Producto = g.Key, Cantidad = g.Sum(a => a.Cantidad) })
                .ToList()
        };

        [TestInitialize]
        public void Preparar()
        {
            _apuntes = new List<LecturaEnCursoRecepcion>();
            _sinEtiqueta = new List<PeticionSinEtiquetaRecepcion>();
            _soloEscaneando = true;
            _lecturasEnServidor = true;
            _servicio = A.Fake<IServicioRecepcionReposiciones>();
            _sonidos = A.Fake<ISonidosLector>();
            var configuracion = A.Fake<IConfiguracion>();
            var dialogos = A.Fake<IServicioDialogos>();
            A.CallTo(() => configuracion.leerParametro(A<string>._, Parametros.Claves.AlmacenPedidoVta)).Returns("REI");
            A.CallTo(() => _servicio.LeerPendientes(A<string>._, "REI")).Returns(new List<RecepcionPendiente>
            {
                new() { Tipo = "REPO", Documento = "81200", Titulo = "Reposición 81200", Lineas = 2, Unidades = 4 }
            });
            A.CallTo(() => _servicio.LeerRecepcion(A<string>._, "REI", "81200")).ReturnsLazily(() => Task.FromResult(Reposicion()));
            A.CallTo(() => _servicio.LeerLecturas(A<string>._, "REI", "81200"))
                .ReturnsLazily(() => Task.FromResult(_lecturasEnServidor ? LoQueHayEnElServidor() : null!));
            A.CallTo(() => _servicio.GuardarLecturas(A<string>._, "REI", "81200", A<List<LecturaEnCursoRecepcion>>._, A<string>._))
                .ReturnsLazily((string _, string _, string _, List<LecturaEnCursoRecepcion> lecturas, string _) =>
                {
                    _apuntes.AddRange(lecturas);
                    return Task.FromResult(LoQueHayEnElServidor());
                });
            A.CallTo(() => _servicio.ApuntarSinEtiqueta(A<string>._, "REI", "81200", A<PeticionSinEtiquetaRecepcion>._))
                .ReturnsLazily((string _, string _, string _, PeticionSinEtiquetaRecepcion p) =>
                {
                    _sinEtiqueta.Add(p);
                    return Task.FromResult(new ResultadoSinEtiquetaRecepcion
                    {
                        Mensaje = $"Apuntada 1 unidad sin etiqueta de {p.Producto}: se ha avisado a Algete y a Compras."
                    });
                });
            _vm = new RecibirReposicionViewModel(_servicio, dialogos, configuracion, _sonidos, A.Fake<IServicioNavegacion>(), _ => { });
        }

        private LineaRecibirReposicion Linea(string producto) => _vm.Lineas.Single(l => l.Producto == producto);

        private async Task LeerAsync(string codigo)
        {
            _vm.Lectura = codigo;
            _vm.LeerLecturaCommand.Execute(null);
            await _vm.LecturaProductoEnCurso;
            await _vm.EnvioEnCurso;
        }

        private async Task SinEtiquetaAsync(string producto, string motivo, string? comentario = null)
        {
            _vm.SinEtiquetaCommand.Execute(Linea(producto));
            _vm.MotivoSinEtiqueta = motivo;
            _vm.ComentarioSinEtiqueta = comentario;
            await _vm.ApuntarSinEtiquetaCommand.ExecuteAsync(null);
            await _vm.EnvioEnCurso;
        }

        [TestMethod]
        public async Task ConSoloEscaneando_LeidoEsDeSoloLectura()
        {
            await _vm.CargarAsync();

            Assert.IsTrue(_vm.SoloEscaneando);
            Assert.IsTrue(_vm.LeidoSoloLectura);
        }

        [TestMethod]
        public async Task SinSoloEscaneando_ComoHoy()
        {
            _soloEscaneando = false;

            await _vm.CargarAsync();

            Assert.IsFalse(_vm.SoloEscaneando);
            Assert.IsFalse(_vm.LeidoSoloLectura, "Sin SoloEscaneando (o con una API anterior) «Leído» se sigue tecleando");
            Assert.IsFalse(_vm.SinEtiquetaCommand.CanExecute(Linea("17404")));
            await LeerAsync("17404");
            Assert.AreEqual(1, Linea("17404").Leido, "Sin SoloEscaneando la referencia tecleada cuenta, como hoy");
        }

        [TestMethod]
        public void SoloEscaneando_SiNoViene_EsFalse()
        {
            RecepcionReposicion recepcion = Newtonsoft.Json.JsonConvert.DeserializeObject<RecepcionReposicion>("{\"Tipo\":\"REPO\",\"Documento\":\"1\"}")!;

            Assert.IsFalse(recepcion.SoloEscaneando);
        }

        [TestMethod]
        public async Task ElCodigoInternoLeido_Cuenta()
        {
            await _vm.CargarAsync();

            await LeerAsync("2900000174047");

            Assert.AreEqual(1, Linea("17404").Leido);
            A.CallTo(() => _sonidos.Correcto()).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task LaReferenciaTecleada_EligeLaLineaPeroNoCuenta()
        {
            await _vm.CargarAsync();

            await LeerAsync("17404");

            Assert.AreEqual(0, Linea("17404").Leido);
            Assert.AreSame(Linea("17404"), _vm.LineaElegida);
            Assert.AreEqual(RecibirReposicionViewModel.TextoReferenciaNoCuenta("17404"), _vm.Mensaje);
            Assert.AreEqual(0, _apuntes.Count);
            A.CallTo(() => _sonidos.Error()).MustHaveHappened();
        }

        [TestMethod]
        public async Task ElProductoElegidoEnLaLista_EligeLaLineaPeroNoCuenta()
        {
            await _vm.CargarAsync();

            _vm.LeerProductoElegido("40057", "Banda");
            await _vm.EnvioEnCurso;

            Assert.AreEqual(0, Linea("40057").Leido);
            Assert.AreSame(Linea("40057"), _vm.LineaElegida);
        }

        [TestMethod]
        public async Task UnaReferenciaQueNoViene_NoSeApunta()
        {
            A.CallTo(() => _servicio.BuscarProductosPorCodigo(A<string>._, "55555"))
                .Returns(new List<ProductoCodigoRecepcion> { new() { Producto = "55555", Nombre = "Otra cosa" } });
            await _vm.CargarAsync();

            await LeerAsync("55555");

            Assert.IsFalse(_vm.Lineas.Any(l => l.Producto == "55555"), "Teclear la referencia de lo que no venía no cuenta");
            Assert.AreEqual(RecibirReposicionViewModel.TextoNoVieneSoloEscaneando("55555"), _vm.Mensaje);
        }

        [TestMethod]
        public async Task SinEtiqueta_TrasEl200_CuentaUnaUnidadComoUnaLecturaYDiceElMensajeDeLaApi()
        {
            await _vm.CargarAsync();

            await SinEtiquetaAsync("17404", PeticionSinEtiquetaRecepcion.MOTIVO_NO_LLEVA);

            PeticionSinEtiquetaRecepcion peticion = _sinEtiqueta.Single();
            Assert.AreEqual("17404", peticion.Producto);
            Assert.AreEqual("NoLleva", peticion.Motivo);
            Assert.IsNull(peticion.Comentario);
            Assert.AreEqual(1, peticion.Unidades);
            Assert.AreEqual(Environment.MachineName, peticion.Dispositivo);
            Assert.AreEqual(1, Linea("17404").Leido);
            LecturaEnCursoRecepcion apunte = _apuntes.Single();
            Assert.AreEqual("17404", apunte.Producto);
            Assert.AreEqual(1, apunte.Cantidad, "Va a lo leído en curso del servidor, como una lectura del lector");
            Assert.AreEqual("Apuntada 1 unidad sin etiqueta de 17404: se ha avisado a Algete y a Compras.", _vm.Mensaje);
            Assert.IsFalse(_vm.PreguntandoSinEtiqueta);
        }

        [TestMethod]
        public async Task SinEtiqueta_ConUnaApiSinLecturasCompartidas_CuentaEnLaPantalla()
        {
            _lecturasEnServidor = false;
            await _vm.CargarAsync();

            await SinEtiquetaAsync("40057", PeticionSinEtiquetaRecepcion.MOTIVO_NO_SE_LEE);

            Assert.AreEqual("NoSeLee", _sinEtiqueta.Single().Motivo);
            Assert.AreEqual(1, Linea("40057").Leido);
        }

        [TestMethod]
        public async Task SinEtiqueta_ConOtro_ExigeElComentarioYLoManda()
        {
            await _vm.CargarAsync();
            _vm.SinEtiquetaCommand.Execute(Linea("17404"));

            _vm.MotivoOtro = true;
            Assert.IsFalse(_vm.ApuntarSinEtiquetaCommand.CanExecute(null), "Con «Otro» el comentario es obligatorio");
            _vm.ComentarioSinEtiqueta = "  Caja rota  ";
            Assert.IsTrue(_vm.ApuntarSinEtiquetaCommand.CanExecute(null));
            await _vm.ApuntarSinEtiquetaCommand.ExecuteAsync(null);
            await _vm.EnvioEnCurso;

            Assert.AreEqual("Otro", _sinEtiqueta.Single().Motivo);
            Assert.AreEqual("Caja rota", _sinEtiqueta.Single().Comentario);
            Assert.AreEqual(1, Linea("17404").Leido);
        }

        [TestMethod]
        public async Task SinEtiqueta_SinMotivo_NoSePuedeApuntar()
        {
            await _vm.CargarAsync();

            _vm.SinEtiquetaCommand.Execute(Linea("17404"));

            Assert.IsTrue(_vm.PreguntandoSinEtiqueta);
            Assert.IsFalse(_vm.ApuntarSinEtiquetaCommand.CanExecute(null));
        }

        [TestMethod]
        public async Task SinEtiqueta_LaApiDice400_NoCuentaYDaSuTexto()
        {
            A.CallTo(() => _servicio.ApuntarSinEtiqueta(A<string>._, A<string>._, A<string>._, A<PeticionSinEtiquetaRecepcion>._))
                .ThrowsAsync(new RecepcionReposicionException("El producto 17404 no viene en la reposición 81200."));
            await _vm.CargarAsync();

            await SinEtiquetaAsync("17404", PeticionSinEtiquetaRecepcion.MOTIVO_NO_LLEVA);

            Assert.AreEqual(0, Linea("17404").Leido);
            Assert.AreEqual(0, _apuntes.Count);
            Assert.AreEqual("El producto 17404 no viene en la reposición 81200.", _vm.Mensaje);
            Assert.IsTrue(_vm.PreguntandoSinEtiqueta, "La pregunta se queda para volver a intentarlo");
        }

        [TestMethod]
        public async Task SinEtiqueta_SinRed_NoCuentaYDiceQueLoIntenteDeNuevo()
        {
            A.CallTo(() => _servicio.ApuntarSinEtiqueta(A<string>._, A<string>._, A<string>._, A<PeticionSinEtiquetaRecepcion>._))
                .ThrowsAsync(new HttpRequestException("sin red"));
            await _vm.CargarAsync();

            await SinEtiquetaAsync("17404", PeticionSinEtiquetaRecepcion.MOTIVO_NO_LLEVA);

            Assert.AreEqual(0, Linea("17404").Leido);
            Assert.AreEqual(RecibirReposicionViewModel.TEXTO_SIN_ETIQUETA_SIN_CONEXION, _vm.Mensaje);
        }

        [TestMethod]
        public async Task SinEtiqueta_CadaVezUnaUnidad()
        {
            await _vm.CargarAsync();

            await SinEtiquetaAsync("17404", PeticionSinEtiquetaRecepcion.MOTIVO_NO_LLEVA);
            await SinEtiquetaAsync("17404", PeticionSinEtiquetaRecepcion.MOTIVO_NO_LLEVA);

            Assert.AreEqual(2, _sinEtiqueta.Count);
            Assert.AreEqual(2, Linea("17404").Leido);
        }

        [TestMethod]
        public async Task SinEtiqueta_EnLoQueNoVenia_NoSePuede()
        {
            A.CallTo(() => _servicio.BuscarProductosPorCodigo(A<string>._, "8437000000001"))
                .Returns(new List<ProductoCodigoRecepcion> { new() { Producto = "55555", Nombre = "Otra cosa" } });
            await _vm.CargarAsync();
            await LeerAsync("8437000000001");

            Assert.IsFalse(_vm.SinEtiquetaCommand.CanExecute(Linea("55555")), "La API no lo admite: no viene en la reposición");
        }

        [TestMethod]
        public async Task QuitarUno_QuitaUnaUnidadLeidaTambienEnElServidor()
        {
            await _vm.CargarAsync();
            await LeerAsync("8422");
            Assert.IsTrue(_vm.QuitarUnoCommand.CanExecute(Linea("40057")));

            _vm.QuitarUnoCommand.Execute(Linea("40057"));
            await _vm.EnvioEnCurso;

            Assert.AreEqual(0, Linea("40057").Leido);
            Assert.AreEqual(0, _apuntes.Where(a => a.Producto == "40057").Sum(a => a.Cantidad));
            Assert.AreEqual(-1, _apuntes.Last().Cantidad);
            Assert.IsFalse(_vm.QuitarUnoCommand.CanExecute(Linea("40057")), "Sin nada leído no hay qué quitar");
        }

        [TestMethod]
        public async Task QuitarUno_SinSoloEscaneando_NoSeOfrece()
        {
            _soloEscaneando = false;
            await _vm.CargarAsync();
            await LeerAsync("8422");

            Assert.IsFalse(_vm.QuitarUnoCommand.CanExecute(Linea("40057")), "Sin SoloEscaneando se corrige tecleando en «Leído»");
        }
    }
}
