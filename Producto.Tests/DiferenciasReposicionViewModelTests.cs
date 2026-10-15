using FakeItEasy;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Models;
using Nesto.Infrastructure.Services;
using Nesto.Infrastructure.Shared;
using Nesto.Modules.Producto.ViewModels;

namespace Producto.Tests
{
    /// <summary>
    /// Nesto#526 (NestoAPI#634): la ventana «Diferencias de reposición». Lista las pendientes (por defecto) o resueltas del
    /// almacén de origen, y resuelve con los motivos que da la API. Los errores de la API (400, 403, 409) se enseñan tal cual.
    /// </summary>
    [TestClass]
    public class DiferenciasReposicionViewModelTests
    {
        private IServicioDiferenciasReposicion _servicio = null!;
        private IConfiguracion _configuracion = null!;
        private IServicioDialogos _dialogos = null!;
        private List<(string Estado, string? Almacen)> _lecturas = null!;
        private List<(int Id, ResolverDiferenciaReposicion Peticion)> _resueltas = null!;

        private static DiferenciaReposicion Falta(int id = 7) => new()
        {
            Id = id, Empresa = "1", Traspaso = 80915, Origen = "ALG", Destino = "ALC", Producto = "38631", Descripcion = "Champú",
            Enviado = 1, Leido = 0, Diferencia = -1, Tipo = "Falta", QuePaso = "La tienda recibió 0 de 1",
            QueComprobar = "Compruebe que la unidad sigue en Algete", HuecoSalida = "005/005/004", Huecos = "005/005/004, 007/001/002",
            FechaRecepcion = new DateTime(2026, 10, 8, 12, 7, 0), UsuarioRecepcion = "Paloma", Preparador = "Andre",
            Ajuste = "Hecho", NumOrdenAjuste = 123, Estado = "Pendiente",
            ResolucionesPosibles = new List<string> { "Confirmada", "NoEstaba", "ErrorRecepcion" }, OrigenConControlUbicaciones = true
        };

        private static DiferenciaReposicion Sobra(int id = 8) => new()
        {
            Id = id, Empresa = "1", Traspaso = 80906, Origen = "ALG", Destino = "ALC", Producto = "39001",
            Enviado = 6, Leido = 7, Diferencia = 1, Tipo = "Sobra", Estado = "Pendiente", Ajuste = "Apagado",
            ResolucionesPosibles = new List<string> { "Confirmada", "Sobraba", "ErrorRecepcion" }
        };

        private static DiferenciaReposicion Resuelta(DiferenciaReposicion d, string resolucion)
        {
            d.Estado = "Resuelta";
            d.Resolucion = resolucion;
            d.UsuarioResolucion = "Carlos";
            d.FechaResolucion = new DateTime(2026, 10, 15, 9, 30, 0);
            d.ResolucionesPosibles = new List<string>();
            return d;
        }

        [TestInitialize]
        public void Inicializar()
        {
            _servicio = A.Fake<IServicioDiferenciasReposicion>();
            _configuracion = A.Fake<IConfiguracion>();
            _dialogos = A.Fake<IServicioDialogos>();
            _lecturas = new();
            _resueltas = new();
            A.CallTo(() => _servicio.Leer(A<string>._, A<string>._, A<string>._))
                .ReturnsLazily((string _, string estado, string almacen) =>
                {
                    _lecturas.Add((estado, almacen));
                    var lista = estado == "Resuelta"
                        ? new List<DiferenciaReposicion> { Resuelta(Falta(5), "Confirmada") }
                        : new List<DiferenciaReposicion> { Falta(), Sobra() };
                    return Task.FromResult(lista);
                });
            A.CallTo(() => _servicio.Resolver(A<string>._, A<int>._, A<ResolverDiferenciaReposicion>._))
                .ReturnsLazily((string _, int id, ResolverDiferenciaReposicion p) =>
                {
                    _resueltas.Add((id, p));
                    return Task.FromResult(new ResultadoResolverDiferenciaReposicion
                    {
                        Diferencia = Resuelta(id == 7 ? Falta() : Sobra(), p.Resolucion),
                        Mensaje = "Se ha dado de baja 1 ud. de 38631 en ALG."
                    });
                });
            A.CallTo(() => _dialogos.ShowConfirmationAsync(A<string>._, A<string>._)).Returns(true);
        }

        private DiferenciasReposicionViewModel Vm(string? grupo = null)
        {
            if (grupo != null)
            {
                A.CallTo(() => _configuracion.UsuarioEnGrupo(grupo)).Returns(true);
            }
            return new DiferenciasReposicionViewModel(_servicio, _configuracion, _dialogos);
        }

        [TestMethod]
        public async Task Cargar_PorDefectoLasPendientesDeTodosLosAlmacenesQuePuedeVer()
        {
            DiferenciasReposicionViewModel vm = Vm(Constantes.GruposSeguridad.ALMACEN);

            await vm.CargarAsync();

            CollectionAssert.AreEqual(new[] { ("Pendiente", (string?)null) }, _lecturas);
            Assert.AreEqual(2, vm.Diferencias.Count);
            Assert.AreEqual("2 pendientes de comprobar", vm.Resumen);
            Assert.IsTrue(vm.Cargada);
            Assert.IsNull(vm.Mensaje);
            FilaDiferenciaReposicion falta = vm.Diferencias[0];
            Assert.AreEqual("ALG → ALC", falta.Ruta);
            Assert.AreEqual("−1", falta.TextoDiferencia);
            Assert.AreEqual("+1", vm.Diferencias[1].TextoDiferencia);
            Assert.AreEqual("Ajustado (orden 123)", falta.TextoAjuste);
            Assert.AreEqual("Sin ajustar (el ajuste automático estaba apagado)", vm.Diferencias[1].TextoAjuste);
            Assert.AreEqual("005/005/004, 007/001/002", falta.Huecos);
            Assert.AreEqual("La tienda recibió 0 de 1", falta.QuePaso);
            CollectionAssert.AreEqual(new[] { "Comprobada, cuadra", "No estaba (merma o inventario)", "Error al recibir (se corrige)" },
                falta.Opciones.Select(o => o.Texto).ToArray());
        }

        [TestMethod]
        public void AlLlegar_LeeLaLista()
        {
            DiferenciasReposicionViewModel vm = Vm(Constantes.GruposSeguridad.ALMACEN);

            vm.AlLlegar(new ParametrosNavegacion());

            Assert.AreEqual(1, _lecturas.Count);
            Assert.AreEqual(2, vm.Diferencias.Count);
        }

        [TestMethod]
        public void CambiarElFiltro_VuelveALeerConEseEstadoYAlmacen()
        {
            DiferenciasReposicionViewModel vm = Vm(Constantes.GruposSeguridad.DIRECCION);

            vm.Estado = DiferenciaReposicion.ESTADO_RESUELTA;
            vm.Almacen = Constantes.Almacenes.ALMACEN_ALGETE;

            CollectionAssert.AreEqual(new[] { ("Resuelta", (string?)null), ("Resuelta", (string?)"ALG") }, _lecturas);
            Assert.AreEqual(1, vm.Diferencias.Count);
            Assert.IsFalse(vm.Diferencias[0].PuedeResolverse);
            Assert.AreEqual("Comprobada, cuadra · Carlos, 15/10/2026 09:30", vm.Diferencias[0].Resuelta);
            Assert.AreEqual("1 resuelta", vm.Resumen);
        }

        [TestMethod]
        public void Almacenes_AlmacenYDireccionEligen_LosDemasVenElSuyo()
        {
            DiferenciasReposicionViewModel almacen = Vm(Constantes.GruposSeguridad.ALMACEN);
            Assert.IsTrue(almacen.PuedeElegirAlmacen);
            CollectionAssert.AreEqual(new[] { null, "ALG", "REI", "ALC" }, almacen.Almacenes.Select(a => a.Valor).ToArray());

            var tienda = new DiferenciasReposicionViewModel(A.Fake<IServicioDiferenciasReposicion>(), A.Fake<IConfiguracion>(), _dialogos);
            Assert.IsFalse(tienda.PuedeElegirAlmacen);
            Assert.AreEqual(1, tienda.Almacenes.Count);
            Assert.IsNull(tienda.Almacenes[0].Valor, "Sin almacén: la API da las de su AlmacénPedidoVta");
        }

        [TestMethod]
        public async Task Resolver_SoloLasResolucionesQueDaLaApi()
        {
            DiferenciasReposicionViewModel vm = Vm(Constantes.GruposSeguridad.ALMACEN);
            await vm.CargarAsync();

            Assert.IsFalse(vm.ResolverCommand.CanExecute("Confirmada"), "Sin selección no se resuelve");
            vm.Seleccionada = vm.Diferencias[0];

            Assert.IsTrue(vm.ResolverCommand.CanExecute("Confirmada"));
            Assert.IsTrue(vm.ResolverCommand.CanExecute("NoEstaba"));
            Assert.IsFalse(vm.ResolverCommand.CanExecute("Sobraba"), "«Sobraba» solo si se recibió de más");
        }

        [TestMethod]
        public async Task Resolver_Confirmada_NoPreguntaMandaElComentarioYQuitaLaFila()
        {
            DiferenciasReposicionViewModel vm = Vm(Constantes.GruposSeguridad.ALMACEN);
            await vm.CargarAsync();
            vm.Seleccionada = vm.Diferencias[0];
            vm.Comentario = "  Está en su hueco  ";

            await vm.ResolverCommand.ExecuteAsync("Confirmada");

            A.CallTo(() => _dialogos.ShowConfirmationAsync(A<string>._, A<string>._)).MustNotHaveHappened();
            Assert.AreEqual(1, _resueltas.Count);
            Assert.AreEqual(7, _resueltas[0].Id);
            Assert.AreEqual("Confirmada", _resueltas[0].Peticion.Resolucion);
            Assert.AreEqual("Está en su hueco", _resueltas[0].Peticion.Comentario);
            Assert.AreEqual(1, vm.Diferencias.Count, "Viendo las pendientes, la resuelta sale de la lista");
            Assert.AreEqual(8, vm.Diferencias[0].Id);
            Assert.IsNull(vm.Seleccionada);
            Assert.IsNull(vm.Comentario);
            Assert.AreEqual("1 pendiente de comprobar", vm.Resumen);
            Assert.AreEqual("Se ha dado de baja 1 ud. de 38631 en ALG.", vm.Mensaje, "El mensaje de la API se enseña tal cual");
        }

        [TestMethod]
        public async Task Resolver_ViendoTodas_LaFilaSeQuedaComoResuelta()
        {
            DiferenciasReposicionViewModel vm = Vm(Constantes.GruposSeguridad.ALMACEN);
            vm.Estado = DiferenciaReposicion.ESTADO_TODAS;
            vm.Seleccionada = vm.Diferencias[0];

            await vm.ResolverCommand.ExecuteAsync("Confirmada");

            Assert.AreEqual(2, vm.Diferencias.Count);
            Assert.AreEqual(7, vm.Diferencias[0].Id);
            Assert.IsFalse(vm.Diferencias[0].EstaPendiente);
            Assert.AreEqual("1 pendientes y 1 resueltas", vm.Resumen);
        }

        [TestMethod]
        public async Task Resolver_NoEstaba_PreguntaYSiNoSeConfirmaNoLlamaALaApi()
        {
            A.CallTo(() => _dialogos.ShowConfirmationAsync(A<string>._, A<string>._)).Returns(false);
            DiferenciasReposicionViewModel vm = Vm(Constantes.GruposSeguridad.ALMACEN);
            await vm.CargarAsync();
            vm.Seleccionada = vm.Diferencias[0];

            await vm.ResolverCommand.ExecuteAsync("NoEstaba");

            A.CallTo(() => _dialogos.ShowConfirmationAsync("Diferencias de reposición", A<string>.That.Contains("Se dará de baja")))
                .MustHaveHappenedOnceExactly();
            Assert.AreEqual(0, _resueltas.Count);
            Assert.AreEqual(2, vm.Diferencias.Count);
        }

        [TestMethod]
        public async Task Resolver_400DeLaApi_EnsenaSuMensajeYDejaLaFila()
        {
            A.CallTo(() => _servicio.Resolver(A<string>._, A<int>._, A<ResolverDiferenciaReposicion>._))
                .ThrowsAsync(new DiferenciasReposicionException("«NoEstaba» solo vale si se recibió de menos.", 400));
            DiferenciasReposicionViewModel vm = Vm(Constantes.GruposSeguridad.ALMACEN);
            await vm.CargarAsync();
            vm.Seleccionada = vm.Diferencias[0];
            vm.Comentario = "Mirado";

            await vm.ResolverCommand.ExecuteAsync("NoEstaba");

            Assert.AreEqual("«NoEstaba» solo vale si se recibió de menos.", vm.Mensaje);
            Assert.AreEqual(2, vm.Diferencias.Count);
            Assert.AreSame(vm.Diferencias[0], vm.Seleccionada, "Sigue elegida para probar otra resolución");
            Assert.AreEqual("Mirado", vm.Comentario, "No se pierde lo escrito");
            Assert.AreEqual(1, _lecturas.Count, "Un 400 no vuelve a leer");
            Assert.IsFalse(vm.EstaOcupado);
        }

        [TestMethod]
        public async Task Resolver_409YaResuelta_EnsenaSuMensajeYVuelveALeer()
        {
            A.CallTo(() => _servicio.Resolver(A<string>._, A<int>._, A<ResolverDiferenciaReposicion>._))
                .ThrowsAsync(new DiferenciasReposicionException("La diferencia de 38631 de la reposición 80915 ya está resuelta (Carlos).", 409));
            DiferenciasReposicionViewModel vm = Vm(Constantes.GruposSeguridad.ALMACEN);
            await vm.CargarAsync();
            vm.Seleccionada = vm.Diferencias[0];

            await vm.ResolverCommand.ExecuteAsync("Confirmada");

            Assert.AreEqual("La diferencia de 38631 de la reposición 80915 ya está resuelta (Carlos).", vm.Mensaje);
            Assert.AreEqual(2, _lecturas.Count, "La ha resuelto otra persona: se vuelve a leer");
        }

        [TestMethod]
        public async Task Cargar_403DeLaApi_EnsenaSuMensaje()
        {
            A.CallTo(() => _servicio.Leer(A<string>._, A<string>._, A<string>._))
                .ThrowsAsync(new DiferenciasReposicionException("Solo puede ver las diferencias de reposición de su almacén.", 403));
            DiferenciasReposicionViewModel vm = Vm();

            await vm.CargarAsync();

            Assert.AreEqual("Solo puede ver las diferencias de reposición de su almacén.", vm.Mensaje);
            Assert.AreEqual(0, vm.Diferencias.Count);
            Assert.IsFalse(vm.EstaOcupado);
        }

        [TestMethod]
        public async Task Resolver_ComentarioDemasiadoLargo_NoLlamaALaApi()
        {
            DiferenciasReposicionViewModel vm = Vm(Constantes.GruposSeguridad.ALMACEN);
            await vm.CargarAsync();
            vm.Seleccionada = vm.Diferencias[0];
            vm.Comentario = new string('x', 501);

            await vm.ResolverCommand.ExecuteAsync("Confirmada");

            Assert.AreEqual(0, _resueltas.Count);
            StringAssert.Contains(vm.Mensaje, "500");
        }

        [TestMethod]
        public void LaVista_SeCreaConSuViewModelYFondoBlanco()
        {
            Exception? error = null;
            var hilo = new Thread(() =>
            {
                try
                {
                    DiferenciasReposicionViewModel vm = Vm(Constantes.GruposSeguridad.ALMACEN);
                    var vista = new Nesto.Modules.Producto.Views.DiferenciasReposicionView(vm);

                    Assert.AreSame(vm, vista.DataContext);
                    Assert.AreEqual(System.Windows.Media.Colors.White, ((System.Windows.Media.SolidColorBrush)vista.Background).Color);
                }
                catch (Exception ex)
                {
                    error = ex;
                }
            });
            hilo.SetApartmentState(ApartmentState.STA);
            hilo.Start();
            hilo.Join();
            if (error != null)
            {
                throw new AssertFailedException("La vista no se ha podido crear: " + error.Message, error);
            }
        }
    }
}
