using FakeItEasy;
using Nesto.Infrastructure.Models;
using Nesto.Infrastructure.Services;
using Nesto.Modules.Producto.ViewModels;

namespace Producto.Tests
{
    /// <summary>
    /// NestoAPI#577 (09/10/26): el calendario de reposiciones se mantiene desde Nesto en vez de con scripts. Lo ve
    /// cualquiera; lo cambian quienes la API deja. Se mandan solo las filas cambiadas o nuevas y los errores de la API
    /// (400 con el motivo, 403) se enseñan tal cual.
    /// </summary>
    [TestClass]
    public class CalendarioReposicionesViewModelTests
    {
        private IServicioCalendarioReposiciones _servicio = null!;
        private CalendarioReposicionesViewModel _vm = null!;
        private List<GuardarCalendarioReposiciones> _guardadas = null!;

        private static List<FilaCalendarioReposicion> Calendario() => new()
        {
            new() { Id = 1, Empresa = "1", Origen = "REI", Destino = "ALG", DiaSemana = 1, HoraCierre = new TimeSpan(9, 0, 0),
                HoraLlegadaHabitual = new TimeSpan(13, 30, 0), Activo = true, Usuario = "Carlos", FechaModificacion = new DateTime(2026, 10, 9, 8, 0, 0) },
            new() { Id = 2, Empresa = "1", Origen = "REI", Destino = "ALG", DiaSemana = 3, HoraCierre = new TimeSpan(9, 0, 0),
                HoraLlegadaHabitual = new TimeSpan(13, 30, 0), Activo = true },
            new() { Id = 9, Empresa = "1", Origen = "ALG", Destino = "REI", DiaSemana = 1, HoraCierre = new TimeSpan(13, 0, 0),
                HoraLlegadaHabitual = new TimeSpan(11, 0, 0), LaborablesAntelacionCierre = 1, Activo = true }
        };

        [TestInitialize]
        public void Inicializar()
        {
            _servicio = A.Fake<IServicioCalendarioReposiciones>();
            _guardadas = new List<GuardarCalendarioReposiciones>();
            A.CallTo(() => _servicio.LeerCalendario(A<string>._)).ReturnsLazily(() => Task.FromResult(Calendario()));
            A.CallTo(() => _servicio.PuedeEditar()).Returns(true);
            A.CallTo(() => _servicio.Guardar(A<GuardarCalendarioReposiciones>._))
                .ReturnsLazily((GuardarCalendarioReposiciones p) =>
                {
                    _guardadas.Add(p);
                    return Task.FromResult(Calendario());
                });
            _vm = new CalendarioReposicionesViewModel(_servicio);
        }

        private FilaCalendarioEditable Fila(int id) => _vm.Filas.Single(f => f.Id == id);

        [TestMethod]
        public async Task Cargar_ConPermiso_EditableConLasFilasOrdenadasYSinCambios()
        {
            await _vm.CargarAsync();

            Assert.IsTrue(_vm.PuedeEditar);
            Assert.IsFalse(_vm.SoloLectura);
            CollectionAssert.AreEqual(new int?[] { 9, 1, 2 }, _vm.Filas.Select(f => f.Id).ToArray(), "Por origen, destino y día");
            Assert.AreEqual("09:00", Fila(1).HoraCierre);
            Assert.AreEqual("13:30", Fila(1).HoraLlegada);
            Assert.AreEqual("REI → ALG", Fila(1).Ruta);
            Assert.AreEqual((byte)1, Fila(9).Antelacion);
            Assert.IsFalse(_vm.HayCambios);
            Assert.IsFalse(_vm.GuardarCommand.CanExecute(null), "Sin cambios no hay nada que guardar");
            Assert.IsTrue(_vm.AnadirCommand.CanExecute(null));
            Assert.IsNull(_vm.Mensaje);
        }

        [TestMethod]
        public async Task Cargar_SinPermiso_SoloLectura()
        {
            A.CallTo(() => _servicio.PuedeEditar()).Returns(false);

            await _vm.CargarAsync();

            Assert.IsFalse(_vm.PuedeEditar);
            Assert.IsTrue(_vm.SoloLectura);
            Assert.AreEqual(3, _vm.Filas.Count, "Lo ve igual");
            Fila(1).HoraCierre = "10:00";
            Assert.IsFalse(_vm.GuardarCommand.CanExecute(null));
            Assert.IsFalse(_vm.AnadirCommand.CanExecute(null));
            StringAssert.Contains(_vm.Mensaje, "Solo lectura");
        }

        [TestMethod]
        public async Task Guardar_MandaSoloLaFilaCambiadaConLoEditado()
        {
            await _vm.CargarAsync();
            Fila(1).HoraCierre = "9:30";
            Fila(1).HoraLlegada = "1400";
            Fila(2).Activo = false;

            Assert.IsTrue(_vm.HayCambios);
            Assert.IsTrue(_vm.GuardarCommand.CanExecute(null));
            await _vm.GuardarCommand.ExecuteAsync(null);

            GuardarCalendarioReposiciones peticion = _guardadas.Single();
            Assert.AreEqual("1", peticion.Empresa);
            CollectionAssert.AreEqual(new int?[] { 1, 2 }, peticion.Filas.Select(f => f.Id).ToArray(), "Solo las cambiadas; la 9 no");
            FilaCalendarioReposicion lunes = peticion.Filas[0];
            Assert.AreEqual("REI", lunes.Origen);
            Assert.AreEqual("ALG", lunes.Destino);
            Assert.AreEqual((byte)1, lunes.DiaSemana);
            Assert.AreEqual(new TimeSpan(9, 30, 0), lunes.HoraCierre);
            Assert.AreEqual(new TimeSpan(14, 0, 0), lunes.HoraLlegadaHabitual);
            Assert.IsTrue(lunes.Activo);
            Assert.IsFalse(peticion.Filas[1].Activo, "Desactivar es mandar Activo = false, no borrar");
            Assert.IsFalse(_vm.HayCambios, "Queda lo que devuelve la API");
            Assert.AreEqual("Guardado (2 filas).", _vm.Mensaje);
        }

        [TestMethod]
        public async Task Guardar_CambiarAntelacionYDia_LoManda()
        {
            await _vm.CargarAsync();
            Fila(9).Antelacion = 2;
            Fila(9).DiaSemana = 2;

            await _vm.GuardarCommand.ExecuteAsync(null);

            FilaCalendarioReposicion fila = _guardadas.Single().Filas.Single();
            Assert.AreEqual((byte)2, fila.LaborablesAntelacionCierre);
            Assert.AreEqual((byte)2, fila.DiaSemana);
        }

        [TestMethod]
        public async Task Guardar_ErrorDeLaApi_SeEnsenaTalCualYSeConservaLoEditado()
        {
            A.CallTo(() => _servicio.Guardar(A<GuardarCalendarioReposiciones>._))
                .ThrowsAsync(new CalendarioReposicionesException("La hora de llegada no puede ser anterior a la hora de cierre (la reposición llega el mismo día).", 400));
            await _vm.CargarAsync();
            Fila(1).HoraCierre = "14:00";

            await _vm.GuardarCommand.ExecuteAsync(null);

            Assert.AreEqual("La hora de llegada no puede ser anterior a la hora de cierre (la reposición llega el mismo día).", _vm.Mensaje);
            Assert.AreEqual("14:00", Fila(1).HoraCierre, "Lo tecleado no se pierde");
            Assert.IsTrue(_vm.HayCambios);
            Assert.IsFalse(_vm.EstaOcupado);
        }

        [TestMethod]
        public async Task Guardar_SinPermisoEnLaApi_EnsenaEl403()
        {
            A.CallTo(() => _servicio.Guardar(A<GuardarCalendarioReposiciones>._))
                .ThrowsAsync(new CalendarioReposicionesException("El calendario de reposiciones solo lo pueden cambiar las personas autorizadas a rellenar reposiciones a mano.", 403));
            await _vm.CargarAsync();
            Fila(1).HoraCierre = "10:00";

            await _vm.GuardarCommand.ExecuteAsync(null);

            StringAssert.Contains(_vm.Mensaje, "personas autorizadas");
        }

        [TestMethod]
        public async Task Guardar_HoraQueNoSeEntiende_NoLlamaALaApiYLoDice()
        {
            await _vm.CargarAsync();
            Fila(1).HoraCierre = "nueve";

            await _vm.GuardarCommand.ExecuteAsync(null);

            A.CallTo(() => _servicio.Guardar(A<GuardarCalendarioReposiciones>._)).MustNotHaveHappened();
            StringAssert.Contains(_vm.Mensaje, "«nueve»");
            StringAssert.Contains(_vm.Mensaje, "REI → ALG (lunes)");
        }

        [TestMethod]
        public async Task Anadir_DiaNuevo_ConLasHorasYLaAntelacionDeLaRutaYSeMandaSinId()
        {
            await _vm.CargarAsync();
            _vm.NuevoOrigen = "ALG";
            _vm.NuevoDestino = "REI";
            _vm.NuevoDia = 3;

            _vm.AnadirCommand.Execute(null);

            FilaCalendarioEditable nueva = _vm.Filas.Single(f => f.EsNueva);
            Assert.AreEqual("13:00", nueva.HoraCierre);
            Assert.AreEqual("11:00", nueva.HoraLlegada);
            Assert.AreEqual((byte)1, nueva.Antelacion);
            Assert.IsTrue(_vm.HayCambios);

            await _vm.GuardarCommand.ExecuteAsync(null);

            FilaCalendarioReposicion mandada = _guardadas.Single().Filas.Single();
            Assert.IsNull(mandada.Id);
            Assert.AreEqual("ALG", mandada.Origen);
            Assert.AreEqual("REI", mandada.Destino);
            Assert.AreEqual((byte)3, mandada.DiaSemana);
            Assert.IsTrue(mandada.Activo);
        }

        [TestMethod]
        public async Task Anadir_DiaQueYaTieneLaRuta_NoDuplicaYLoDice()
        {
            await _vm.CargarAsync();
            _vm.NuevoOrigen = "REI";
            _vm.NuevoDestino = "ALG";
            _vm.NuevoDia = 3;

            _vm.AnadirCommand.Execute(null);

            Assert.AreEqual(3, _vm.Filas.Count);
            StringAssert.Contains(_vm.Mensaje, "ya llega el miércoles");
        }

        [TestMethod]
        public async Task Anadir_MismoOrigenYDestino_NoAnade()
        {
            await _vm.CargarAsync();
            _vm.NuevoOrigen = "ALG";
            _vm.NuevoDestino = "ALG";

            _vm.AnadirCommand.Execute(null);

            Assert.AreEqual(3, _vm.Filas.Count);
            StringAssert.Contains(_vm.Mensaje, "distintos");
        }

        [TestMethod]
        public async Task Cargar_FallaLaApi_EnsenaElMensaje()
        {
            A.CallTo(() => _servicio.LeerCalendario(A<string>._)).ThrowsAsync(new CalendarioReposicionesException("El servidor ha contestado con el error 500.", 500));

            await _vm.CargarAsync();

            Assert.AreEqual("El servidor ha contestado con el error 500.", _vm.Mensaje);
            Assert.IsFalse(_vm.EstaOcupado);
        }

        [TestMethod]
        public void LeerHora_FormatosAdmitidos()
        {
            Assert.IsTrue(CalendarioReposicionesViewModel.LeerHora("9:30", out TimeSpan a));
            Assert.AreEqual(new TimeSpan(9, 30, 0), a);
            Assert.IsTrue(CalendarioReposicionesViewModel.LeerHora(" 09.30 ", out TimeSpan b));
            Assert.AreEqual(new TimeSpan(9, 30, 0), b);
            Assert.IsTrue(CalendarioReposicionesViewModel.LeerHora("0930", out TimeSpan c));
            Assert.AreEqual(new TimeSpan(9, 30, 0), c);
            Assert.IsFalse(CalendarioReposicionesViewModel.LeerHora("24:00", out _));
            Assert.IsFalse(CalendarioReposicionesViewModel.LeerHora("9:3", out _));
            Assert.IsFalse(CalendarioReposicionesViewModel.LeerHora("", out _));
        }

        #region Excepciones por fecha (NestoAPI#867)

        private static List<ExcepcionCalendarioReposicion> ExcepcionesApi() => new()
        {
            new() { Id = 7, Empresa = "1", Origen = "ALG", Destino = "REI", FechaLlegada = new DateTime(2026, 11, 20),
                Tipo = TiposExcepcionCalendario.SIN_REPOSICION, Motivo = "Inventario del sábado 21", Usuario = "Carlos",
                CierraEl = new DateTime(2026, 11, 19, 16, 0, 0) },
            new() { Id = 8, Empresa = "1", Origen = "REI", Destino = "ALG", FechaLlegada = new DateTime(2026, 11, 17),
                Tipo = TiposExcepcionCalendario.REPOSICION_EXTRA, HoraCierre = new TimeSpan(10, 0, 0), HoraLlegadaHabitual = new TimeSpan(13, 30, 0),
                Motivo = "Feria", Usuario = "Manuel" }
        };

        [TestMethod]
        public async Task Cargar_TraeLasExcepcionesPorFecha()
        {
            A.CallTo(() => _servicio.LeerExcepciones(A<string>._)).ReturnsLazily(() => Task.FromResult(ExcepcionesApi()));

            await _vm.CargarAsync();

            CollectionAssert.AreEqual(new int?[] { 8, 7 }, _vm.Excepciones.Select(e => e.Id).ToArray(), "Por fecha");
            ExcepcionCalendarioFila quitado = _vm.Excepciones.Single(e => e.Id == 7);
            Assert.AreEqual("viernes 20/11/2026", quitado.Fecha);
            Assert.AreEqual("ALG → REI", quitado.Ruta);
            Assert.AreEqual("Sin reposición", quitado.TipoTexto);
            Assert.AreEqual("jueves 19/11 16:00", quitado.CierraEl);
            Assert.AreEqual("Día extra (cierra a las 10:00, llega a las 13:30)", _vm.Excepciones.Single(e => e.Id == 8).TipoTexto);
            Assert.IsNull(_vm.MensajeExcepciones);
        }

        [TestMethod]
        public async Task Cargar_FallanLasExcepciones_ElCalendarioSeVeIgual()
        {
            A.CallTo(() => _servicio.LeerExcepciones(A<string>._))
                .ThrowsAsync(new CalendarioReposicionesException("Falta lanzar Scripts/Issue867_ReposicionesCalendarioExcepciones.sql", 503));

            await _vm.CargarAsync();

            Assert.AreEqual(3, _vm.Filas.Count);
            Assert.IsNull(_vm.Mensaje);
            StringAssert.Contains(_vm.MensajeExcepciones, "Issue867");
        }

        [TestMethod]
        public async Task QuitarDia_MandaLaRutaElDiaYElMotivo_YRecarga()
        {
            ExcepcionCalendarioReposicion? mandada = null;
            A.CallTo(() => _servicio.GuardarExcepcion(A<ExcepcionCalendarioReposicion>._))
                .ReturnsLazily((ExcepcionCalendarioReposicion e) =>
                {
                    mandada = e;
                    return Task.FromResult(e);
                });
            await _vm.CargarAsync();
            _vm.ExcepcionOrigen = "ALG";
            _vm.ExcepcionDestino = "REI";
            _vm.ExcepcionFecha = new DateTime(2026, 11, 20, 15, 0, 0);
            _vm.ExcepcionMotivo = " Inventario del sábado 21 ";
            _vm.ExcepcionHoraCierre = "09:00";

            await _vm.QuitarDiaCommand.ExecuteAsync(null);

            Assert.IsNotNull(mandada);
            Assert.AreEqual(TiposExcepcionCalendario.SIN_REPOSICION, mandada!.Tipo);
            Assert.AreEqual(new DateTime(2026, 11, 20), mandada.FechaLlegada);
            Assert.AreEqual("Inventario del sábado 21", mandada.Motivo);
            Assert.IsNull(mandada.HoraCierre, "Un día quitado no lleva horas");
            A.CallTo(() => _servicio.LeerExcepciones(A<string>._)).MustHaveHappenedTwiceExactly();
            StringAssert.Contains(_vm.MensajeExcepciones, "Quitada la reposición de ALG a REI que llega el viernes 20/11/2026");
            Assert.IsNull(_vm.ExcepcionMotivo, "Se limpia el motivo");
        }

        [TestMethod]
        public async Task AnadirDiaExtra_ConHorasEnBlanco_LasDeLaRuta()
        {
            ExcepcionCalendarioReposicion? mandada = null;
            A.CallTo(() => _servicio.GuardarExcepcion(A<ExcepcionCalendarioReposicion>._))
                .ReturnsLazily((ExcepcionCalendarioReposicion e) =>
                {
                    mandada = e;
                    return Task.FromResult(e);
                });
            await _vm.CargarAsync();
            _vm.ExcepcionOrigen = "REI";
            _vm.ExcepcionDestino = "ALG";
            _vm.ExcepcionFecha = new DateTime(2026, 11, 17);
            _vm.ExcepcionMotivo = "Feria";
            _vm.ExcepcionHoraLlegada = "12:00";

            await _vm.AnadirDiaExtraCommand.ExecuteAsync(null);

            Assert.AreEqual(TiposExcepcionCalendario.REPOSICION_EXTRA, mandada!.Tipo);
            Assert.IsNull(mandada.HoraCierre);
            Assert.AreEqual(new TimeSpan(12, 0, 0), mandada.HoraLlegadaHabitual);
            StringAssert.Contains(_vm.MensajeExcepciones, "Añadida una reposición extra");
        }

        [TestMethod]
        public async Task QuitarDia_SinMotivo_NoLlamaALaApi()
        {
            await _vm.CargarAsync();
            _vm.ExcepcionFecha = new DateTime(2026, 11, 20);
            _vm.ExcepcionMotivo = " ";

            await _vm.QuitarDiaCommand.ExecuteAsync(null);

            A.CallTo(() => _servicio.GuardarExcepcion(A<ExcepcionCalendarioReposicion>._)).MustNotHaveHappened();
            StringAssert.Contains(_vm.MensajeExcepciones, "Indique el motivo");
        }

        [TestMethod]
        public async Task QuitarDia_409DeLaApi_EnsenaSuMensajeYNoRecarga()
        {
            A.CallTo(() => _servicio.GuardarExcepcion(A<ExcepcionCalendarioReposicion>._))
                .ThrowsAsync(new CalendarioReposicionesException("ya no se puede cambiar", 409));
            await _vm.CargarAsync();
            _vm.ExcepcionFecha = new DateTime(2026, 11, 20);
            _vm.ExcepcionMotivo = "Inventario";

            await _vm.QuitarDiaCommand.ExecuteAsync(null);

            Assert.AreEqual("ya no se puede cambiar", _vm.MensajeExcepciones);
            A.CallTo(() => _servicio.LeerExcepciones(A<string>._)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task BorrarExcepcion_LaSeleccionada_YRecarga()
        {
            A.CallTo(() => _servicio.LeerExcepciones(A<string>._)).ReturnsLazily(() => Task.FromResult(ExcepcionesApi()));
            await _vm.CargarAsync();
            Assert.IsFalse(_vm.BorrarExcepcionCommand.CanExecute(null), "Sin seleccionar no se borra nada");

            _vm.ExcepcionSeleccionada = _vm.Excepciones.Single(e => e.Id == 7);
            await _vm.BorrarExcepcionCommand.ExecuteAsync(null);

            A.CallTo(() => _servicio.BorrarExcepcion(7)).MustHaveHappenedOnceExactly();
            StringAssert.Contains(_vm.MensajeExcepciones, "Borrada la excepción del viernes 20/11/2026");
        }

        [TestMethod]
        public async Task Excepciones_SinPermiso_NoSeTocan()
        {
            A.CallTo(() => _servicio.PuedeEditar()).Returns(false);
            A.CallTo(() => _servicio.LeerExcepciones(A<string>._)).ReturnsLazily(() => Task.FromResult(ExcepcionesApi()));

            await _vm.CargarAsync();
            _vm.ExcepcionSeleccionada = _vm.Excepciones.First();

            Assert.AreEqual(2, _vm.Excepciones.Count, "Las ve igual");
            Assert.IsFalse(_vm.QuitarDiaCommand.CanExecute(null));
            Assert.IsFalse(_vm.AnadirDiaExtraCommand.CanExecute(null));
            Assert.IsFalse(_vm.BorrarExcepcionCommand.CanExecute(null));
        }

        #endregion

        [TestMethod]
        public void Ayuda_DiceQueCambiarLaHoraDeHoyNoRepiteLaReposicion()
        {
            Assert.AreEqual("Cambiar la hora de cierre de hoy no repite la reposición de hoy; vale desde la siguiente.", _vm.Ayuda);
        }
    }
}
