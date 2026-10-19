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
    /// NestoAPI#673 (Novedades 598, Reina): «Dar de alta solo lo leído». La clienta que viene a recoger lo que pidió el mismo
    /// día que llega la reposición: entra ya lo leído (Parcial = true) y el resto de la reposición queda pendiente.
    /// </summary>
    [TestClass]
    public class RecibirReposicionPorPartesTests
    {
        private IServicioRecepcionReposiciones _servicio = null!;
        private IServicioDialogos _dialogos = null!;
        private RecibirReposicionViewModel _vm = null!;
        private readonly List<TerminarRecepcionReposicion> _enviadas = new();
        private string? _confirmacion;
        private ResultadoRecepcionReposicion _respuesta = null!;

        private static RecepcionReposicion Reposicion(bool admiteParcial = true, string? recibidoEnParte = null) => new()
        {
            Tipo = "REPO",
            Documento = "80878",
            Titulo = "Reposición 80878",
            PuedeTerminar = true,
            SeTerminaDesdeAqui = true,
            AdmiteRecibirEnParte = admiteParcial,
            TextoRecibidaEnParte = recibidoEnParte,
            Lineas = new List<LineaRecepcionReposicion>
            {
                new() { Producto = "17404", Descripcion = "Cera", CodigoBarras = "8411", Cantidad = 2 },
                new() { Producto = "40057", Descripcion = "Banda", CodigoBarras = "8422", Cantidad = 1 }
            }
        };

        [TestInitialize]
        public void Preparar()
        {
            _enviadas.Clear();
            _confirmacion = null;
            _servicio = A.Fake<IServicioRecepcionReposiciones>();
            _dialogos = A.Fake<IServicioDialogos>();
            var configuracion = A.Fake<IConfiguracion>();
            A.CallTo(() => configuracion.leerParametro(A<string>._, Parametros.Claves.AlmacenPedidoVta)).Returns("ALC");
            A.CallTo(() => _servicio.LeerPendientes(A<string>._, "ALC")).Returns(new List<RecepcionPendiente>
            {
                new() { Tipo = "REPO", Documento = "80878", Titulo = "Reposición 80878", Lineas = 2, Unidades = 3 }
            });
            A.CallTo(() => _servicio.LeerRecepcion(A<string>._, "ALC", "80878")).Returns(Reposicion());
            _respuesta = new ResultadoRecepcionReposicion
            {
                Documento = "80878", Parcial = true, LineasPendientes = 2, UnidadesPendientes = 2,
                Mensaje = "Recibido en parte: se ha dado de alta lo leído."
            };
            A.CallTo(() => _servicio.Terminar(A<string>._, "ALC", "80878", A<TerminarRecepcionReposicion>._))
                .ReturnsLazily((string _, string _, string _, TerminarRecepcionReposicion t) =>
                {
                    _enviadas.Add(t);
                    return Task.FromResult(_respuesta);
                });
            A.CallTo(() => _dialogos.ShowConfirmationAsync(A<string>._, A<string>._))
                .ReturnsLazily((string _, string texto) =>
                {
                    _confirmacion = texto;
                    return Task.FromResult(true);
                });
            _vm = new RecibirReposicionViewModel(_servicio, _dialogos, configuracion, A.Fake<ISonidosLector>(), A.Fake<IServicioNavegacion>(), _ => { });
        }

        private void Leer(string codigo)
        {
            _vm.Lectura = codigo;
            _vm.LeerLecturaCommand.Execute(null);
        }

        [TestMethod]
        public async Task ConUnaApiQueLoAdmite_SeOfreceElBoton()
        {
            await _vm.CargarAsync();

            Assert.IsTrue(_vm.PuedeRecibirEnParte);
            Assert.IsTrue(_vm.RecibirLoLeidoCommand.CanExecute(null));
        }

        [TestMethod]
        public async Task ConUnaApiAnterior_NoSeOfrece()
        {
            // Una API anterior no conoce Parcial y la terminaría entera, con lo no leído como falta
            A.CallTo(() => _servicio.LeerRecepcion(A<string>._, "ALC", "80878")).Returns(Reposicion(admiteParcial: false));

            await _vm.CargarAsync();

            Assert.IsFalse(_vm.PuedeRecibirEnParte);
            Assert.IsFalse(_vm.RecibirLoLeidoCommand.CanExecute(null));
        }

        [TestMethod]
        public async Task PideConfirmacionDeUstedYMandaParcialConLoLeido()
        {
            await _vm.CargarAsync();
            Leer("8411");

            await _vm.RecibirLoLeidoCommand.ExecuteAsync(null);

            Assert.AreEqual("Se darán de alta la unidad leída. El resto de la reposición queda pendiente para recibirlo después.", _confirmacion);
            TerminarRecepcionReposicion enviada = _enviadas.Single();
            Assert.IsTrue(enviada.Parcial);
            Assert.AreEqual("17404", enviada.Lecturas.Single().Producto);
            Assert.AreEqual(1, enviada.Lecturas.Single().Cantidad);
        }

        [TestMethod]
        public void TextoDeLaConfirmacion_ConVariasUnidades()
        {
            Assert.AreEqual("Se darán de alta las 3 unidades leídas. El resto de la reposición queda pendiente para recibirlo después.",
                RecibirReposicionViewModel.TextoConfirmarParcial(3));
        }

        [TestMethod]
        public async Task SiNoSeConfirma_NoMandaNada()
        {
            A.CallTo(() => _dialogos.ShowConfirmationAsync(A<string>._, A<string>._)).Returns(false);
            await _vm.CargarAsync();
            Leer("8411");

            await _vm.RecibirLoLeidoCommand.ExecuteAsync(null);

            Assert.AreEqual(0, _enviadas.Count);
            Assert.AreEqual(1, _vm.Lineas.Single(l => l.Producto == "17404").Leido, "Lo leído no se pierde");
        }

        [TestMethod]
        public async Task TrasLaParcial_LaReposicionSigueAbiertaConLoQueQuedaYOtroIdentificador()
        {
            await _vm.CargarAsync();
            Leer("8411");
            // Lo que contesta la API después: queda 1 de 17404 y 1 de 40057
            A.CallTo(() => _servicio.LeerRecepcion(A<string>._, "ALC", "80878")).Returns(new RecepcionReposicion
            {
                Tipo = "REPO", Documento = "80878", PuedeTerminar = true, SeTerminaDesdeAqui = true, AdmiteRecibirEnParte = true,
                TextoRecibidaEnParte = "Recibido en parte: 1 línea, 1 ud.",
                Lineas = new List<LineaRecepcionReposicion>
                {
                    new() { Producto = "17404", CodigoBarras = "8411", Cantidad = 1 },
                    new() { Producto = "40057", CodigoBarras = "8422", Cantidad = 1 }
                }
            });

            await _vm.RecibirLoLeidoCommand.ExecuteAsync(null);

            Assert.IsNotNull(_vm.Seleccionada, "Sigue abierta");
            Assert.AreEqual("80878", _vm.Seleccionada!.Documento);
            Assert.AreEqual(1, _vm.Lineas.Single(l => l.Producto == "17404").Enviado);
            Assert.IsTrue(_vm.Lineas.All(l => l.Leido == 0), "Lo leído ya ha entrado");
            Assert.AreEqual("Recibido en parte: 1 línea, 1 ud.", _vm.TextoRecibidaEnParte);
            StringAssert.Contains(_vm.Mensaje, "Recibido en parte");

            // La recepción que la termina va con otro IdRecepcion (si no, la API la daría por ya hecha)
            Leer("8411");
            Leer("8422");
            await _vm.TerminarCommand.ExecuteAsync(null);
            Assert.AreEqual(2, _enviadas.Count);
            Assert.IsFalse(_enviadas[1].Parcial);
            Assert.AreNotEqual(_enviadas[0].IdRecepcion, _enviadas[1].IdRecepcion);
        }

        [TestMethod]
        public async Task SiLoLeidoEraTodo_QuedaRecibidaComoAlTerminar()
        {
            _respuesta = new ResultadoRecepcionReposicion { Documento = "80878", Parcial = false, Mensaje = "Recepción terminada." };
            await _vm.CargarAsync();
            Leer("8411");
            Leer("8411");
            Leer("8422");

            await _vm.RecibirLoLeidoCommand.ExecuteAsync(null);

            Assert.IsNull(_vm.Seleccionada);
            Assert.AreEqual(0, _vm.Pendientes.Count);
            Assert.AreEqual("Recepción terminada.", _vm.Mensaje);
        }

        [TestMethod]
        public async Task ConAlgoQueNoVenia_LoDiceYNoMandaNada()
        {
            A.CallTo(() => _servicio.BuscarProductosPorCodigo(A<string>._, "55555"))
                .Returns(new List<ProductoCodigoRecepcion> { new() { Producto = "55555", Nombre = "Crema" } });
            await _vm.CargarAsync();
            Leer("8411");
            Leer("55555");
            await _vm.LecturaProductoEnCurso;

            await _vm.RecibirLoLeidoCommand.ExecuteAsync(null);

            Assert.AreEqual(0, _enviadas.Count);
            Assert.IsNull(_confirmacion);
            StringAssert.Contains(_vm.Mensaje, "55555 no venía");
        }

        [TestMethod]
        public async Task ConMasDeLoEnviado_LoDiceYNoMandaNada()
        {
            await _vm.CargarAsync();
            Leer("8422");
            Leer("8422");

            await _vm.RecibirLoLeidoCommand.ExecuteAsync(null);

            Assert.AreEqual(0, _enviadas.Count);
            StringAssert.Contains(_vm.Mensaje, "40057 (enviado 1, leído 2)");
        }

        [TestMethod]
        public async Task SinNadaLeido_LoDiceYNoPregunta()
        {
            await _vm.CargarAsync();

            await _vm.RecibirLoLeidoCommand.ExecuteAsync(null);

            Assert.IsNull(_confirmacion);
            Assert.AreEqual(0, _enviadas.Count);
            StringAssert.Contains(_vm.Mensaje, "No ha leído nada");
        }

        [TestMethod]
        public async Task SiLaApiLaRechaza_EnsenaElMotivoYLoLeidoSigue()
        {
            A.CallTo(() => _servicio.Terminar(A<string>._, "ALC", "80878", A<TerminarRecepcionReposicion>._))
                .Throws(new RecepcionReposicionException("Lea la etiqueta del bulto antes de recibir la reposición 80878."));
            await _vm.CargarAsync();
            Leer("8411");

            await _vm.RecibirLoLeidoCommand.ExecuteAsync(null);

            StringAssert.Contains(_vm.Mensaje, "Lea la etiqueta");
            Assert.AreEqual(1, _vm.Lineas.Single(l => l.Producto == "17404").Leido);
        }

        [TestMethod]
        public async Task LaListaSenalaLaQueLlevaMasDeDosDiasAMedias()
        {
            A.CallTo(() => _servicio.LeerPendientes(A<string>._, "ALC")).Returns(new List<RecepcionPendiente>
            {
                new() { Tipo = "REPO", Documento = "80878", Titulo = "Reposición 80878", Lineas = 2, Unidades = 3 },
                new()
                {
                    Tipo = "REPO", Documento = "80862", Titulo = "Reposición 80862", Lineas = 1, Unidades = 1, RecibidaEnParte = true,
                    TextoRecibidaEnParte = "Recibido en parte: 1 línea, 2 ud.", AvisarRecibidaEnParte = true,
                    AvisoRecibidaEnParte = "La reposición 80862 lleva más de 2 días recibida solo en parte: termine de recibirla."
                }
            });

            await _vm.CargarAsync();

            Assert.AreEqual("La reposición 80862 lleva más de 2 días recibida solo en parte: termine de recibirla.", _vm.Mensaje);
        }
    }
}
