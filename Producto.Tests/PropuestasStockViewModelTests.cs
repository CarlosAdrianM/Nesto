using FakeItEasy;
using Nesto.Modules.Producto;
using Nesto.Modules.Producto.Models;
using Nesto.Modules.Producto.ViewModels;

namespace Producto.Tests
{
    /// <summary>
    /// Nesto#529 (gemela de NestoAPI#631): Compras decide cada propuesta mensual de stock. La ventana lee el resumen y la
    /// lista con los filtros (por defecto, Pendiente del último mes), manda la decisión a la API y enseña su Mensaje y sus
    /// Cambios, o el Message de los 400/403/404/409.
    /// </summary>
    [TestClass]
    public class PropuestasStockViewModelTests
    {
        private static readonly DateTime Hoy = new DateTime(2026, 10, 15);

        private IServicioPropuestasStock _servicio = null!;
        private PropuestasStockViewModel _vm = null!;
        private List<(int Id, DecisionPropuestaStockPeticion Peticion)> _decisiones = null!;
        private List<(string? Mes, string? Estado)> _lecturas = null!;
        private List<FiltroPropuestasStock> _filtros = null!;
        private List<FiltroPropuestasStock> _filtrosResumen = null!;

        private static PropuestaStockModel Ajuste(int id = 1) => new()
        {
            Id = id, Mes = new DateTime(2026, 10, 1), Producto = "17404", Nombre = "Crema", Almacen = "ALG", Fase = "B",
            Accion = PropuestasStockConstantes.ACCION_AJUSTAR, StockMaximoActual = 555, StockMinimoActual = 295,
            StockMaximoPropuesto = 364, StockMinimoPropuesto = 220, ExcesoValorado = 1200m, Estado = "Pendiente",
            Explicacion = "Máximo 364 = venta de referencia 52/mes × 7 meses de cobertura. Mínimo 220 = ..."
        };

        private static PropuestaStockModel Liquidar(int id = 2) => new()
        {
            Id = id, Mes = new DateTime(2026, 10, 1), Producto = "20001", Nombre = "Aparato", Almacen = "ALG",
            Accion = PropuestasStockConstantes.ACCION_LIQUIDAR, Estado = "Pendiente", Explicacion = "Sin venta en 24 meses"
        };

        [TestInitialize]
        public void Inicializar()
        {
            _servicio = A.Fake<IServicioPropuestasStock>();
            _decisiones = new();
            _lecturas = new();
            _filtros = new();
            _filtrosResumen = new();
            A.CallTo(() => _servicio.LeerOpcionesFiltro(A<string>._)).Returns(new OpcionesFiltroPropuestasModel
            {
                Familias = new List<string> { "Lisap", "Eva Visnu" },
                Grupos = new List<string> { "COS", "APA" },
                Acciones = new List<string> { PropuestasStockConstantes.ACCION_AJUSTAR, PropuestasStockConstantes.ACCION_LIQUIDAR }
            });
            A.CallTo(() => _servicio.LeerMotivosSilencio()).Returns(new List<string> { "Nuevo", "Estacional", "Aparato", "Campaña" });
            A.CallTo(() => _servicio.LeerResumen(A<string>._, A<FiltroPropuestasStock>._))
                .ReturnsLazily((string mes, FiltroPropuestasStock filtro) =>
                {
                    _filtrosResumen.Add(filtro);
                    return Task.FromResult(new List<ResumenEstadoPropuestasModel>
                    {
                        new() { Mes = new DateTime(2026, 10, 1), Estado = "Aplicada", Propuestas = 3, ExcesoValorado = 100m },
                        new() { Mes = new DateTime(2026, 10, 1), Estado = "Pendiente", Propuestas = 2, ExcesoValorado = 1200m }
                    });
                });
            A.CallTo(() => _servicio.LeerPropuestas(A<string>._, A<string>._, A<int>._, A<FiltroPropuestasStock>._))
                .ReturnsLazily((string mes, string estado, int top, FiltroPropuestasStock filtro) =>
                {
                    _lecturas.Add((mes, estado));
                    _filtros.Add(filtro);
                    return Task.FromResult(new List<PropuestaStockModel> { Ajuste(), Liquidar() });
                });
            A.CallTo(() => _servicio.Decidir(A<int>._, A<DecisionPropuestaStockPeticion>._))
                .ReturnsLazily((int id, DecisionPropuestaStockPeticion p) =>
                {
                    _decisiones.Add((id, p));
                    return Task.FromResult(new ResultadoDecisionPropuestaStock
                    {
                        Id = id, Decision = p.Decision, Mensaje = "Propuesta del producto 17404 aplicada: ALG: máximo 555 → 364, mínimo 295 → 220.",
                        Cambios = new List<string> { "ALG: máximo 555 → 364, mínimo 295 → 220" }
                    });
                });
            _vm = new PropuestasStockViewModel(_servicio, () => Hoy);
        }

        [TestMethod]
        public async Task Cargar_PorDefectoPendientesDelUltimoMes_ConResumenYMotivosDeLaApi()
        {
            await _vm.CargarAsync();

            Assert.AreEqual(((string?)null, (string?)"Pendiente"), _lecturas.Single(), "Sin mes (el último guardado) y Pendiente");
            Assert.AreEqual(2, _vm.Propuestas.Count);
            Assert.AreEqual(1, _vm.PropuestaSeleccionada?.Id, "Queda seleccionada la primera");
            Assert.AreEqual(364, _vm.StockMaximo, "De entrada, el máximo propuesto");
            Assert.AreEqual(220, _vm.StockMinimo);
            Assert.AreEqual("Pendiente", _vm.Resumen[0].Estado, "El resumen en el orden de los estados");
            StringAssert.Contains(_vm.TextoResumen, "octubre 2026");
            StringAssert.Contains(_vm.TextoResumen, "Pendiente: 2");
            CollectionAssert.Contains(_vm.MotivosSilencio, "Campaña", "Los motivos de silencio vienen de la API");
            Assert.IsTrue(_vm.PuedeDecidir);
        }

        [TestMethod]
        public async Task CambiarFiltros_VuelveALeerConElMesYElEstado()
        {
            await _vm.CargarAsync();

            _vm.MesSeleccionado = _vm.Meses.First(m => m.Valor == "2026-09");
            await Task.Yield();
            _vm.EstadoSeleccionado = _vm.Estados.First(e => e.Valor == null);
            await Task.Yield();

            CollectionAssert.Contains(_lecturas, ("2026-09", "Pendiente"));
            Assert.AreEqual(("2026-09", (string?)null), _lecturas.Last(), "«Todas» va sin estado");
            A.CallTo(() => _servicio.LeerResumen("2026-09", A<FiltroPropuestasStock>._)).MustHaveHappened();
        }

        // Sugerencia 644 (Manuel): filtros por familia, grupo, acción y texto, y el contador refleja lo filtrado.

        [TestMethod]
        public async Task Cargar_RellenaLosFiltrosConTodasDelanteYSinFiltrar()
        {
            await _vm.CargarAsync();

            CollectionAssert.AreEqual(new string?[] { null, "Lisap", "Eva Visnu" }, _vm.Familias.Select(f => f.Valor).ToList());
            Assert.AreEqual("(todas)", _vm.Familias[0].Texto);
            CollectionAssert.AreEqual(new string?[] { null, "COS", "APA" }, _vm.Grupos.Select(g => g.Valor).ToList());
            CollectionAssert.AreEqual(new string?[] { null, PropuestasStockConstantes.ACCION_AJUSTAR, PropuestasStockConstantes.ACCION_LIQUIDAR },
                _vm.Acciones.Select(a => a.Valor).ToList());
            Assert.IsNull(_vm.FamiliaSeleccionada.Valor);
            Assert.IsTrue(_filtros.Single().EstaVacio, "De entrada, sin filtros");
            Assert.AreEqual(1, _lecturas.Count, "Rellenar los desplegables no vuelve a leer");
        }

        [TestMethod]
        public async Task CambiarFamiliaGrupoYAccion_VuelveALeerConElFiltroYElResumenFiltrado()
        {
            await _vm.CargarAsync();

            _vm.FamiliaSeleccionada = _vm.Familias.First(f => f.Valor == "Lisap");
            await Task.Yield();
            _vm.GrupoSeleccionado = _vm.Grupos.First(g => g.Valor == "COS");
            await Task.Yield();
            _vm.AccionSeleccionada = _vm.Acciones.First(a => a.Valor == PropuestasStockConstantes.ACCION_LIQUIDAR);
            await Task.Yield();

            FiltroPropuestasStock ultimo = _filtros.Last();
            Assert.AreEqual("Lisap", ultimo.Familia);
            Assert.AreEqual("COS", ultimo.Grupo);
            Assert.AreEqual(PropuestasStockConstantes.ACCION_LIQUIDAR, ultimo.Accion);
            Assert.AreEqual(4, _lecturas.Count, "Una lectura al cargar y otra por cada filtro");
            Assert.AreEqual("Lisap", _filtrosResumen.Last().Familia, "El resumen (el contador) va con los mismos filtros");
            Assert.AreEqual(PropuestasStockConstantes.ACCION_LIQUIDAR, _filtrosResumen.Last().Accion);
        }

        [TestMethod]
        public async Task ContadorConFiltros_DiceLosFiltrosYCuentaLoQueDevuelveLaApi()
        {
            await _vm.CargarAsync();

            _vm.FamiliaSeleccionada = _vm.Familias.First(f => f.Valor == "Lisap");
            await Task.Yield();

            StringAssert.StartsWith(_vm.TextoResumen, "Propuestas de octubre 2026 (familia Lisap): 5. Pendiente: 2 (");
            StringAssert.Contains(_vm.TextoResumen, "· Aplicada: 3 (");
        }

        [TestMethod]
        public async Task Buscar_ElTextoVaConIntroOActualizarNoACadaTecla()
        {
            await _vm.CargarAsync();

            _vm.TextoBusqueda = "  crema ";
            Assert.AreEqual(1, _lecturas.Count, "Escribir no lee");

            await _vm.CargarCommand.ExecuteAsync(null);

            Assert.AreEqual("crema", _filtros.Last().Texto);
            Assert.AreEqual("crema", _filtrosResumen.Last().Texto);
            StringAssert.Contains(_vm.TextoResumen, "(«crema»)");
        }

        [TestMethod]
        public async Task QuitarFiltros_LosDejaEnTodasYLeeUnaSolaVez()
        {
            await _vm.CargarAsync();
            _vm.FamiliaSeleccionada = _vm.Familias.First(f => f.Valor == "Lisap");
            await Task.Yield();
            _vm.TextoBusqueda = "crema";
            int lecturas = _lecturas.Count;

            await _vm.LimpiarFiltrosCommand.ExecuteAsync(null);

            Assert.IsNull(_vm.FamiliaSeleccionada.Valor);
            Assert.IsNull(_vm.TextoBusqueda);
            Assert.AreEqual(lecturas + 1, _lecturas.Count, "Una sola lectura al quitar los filtros");
            Assert.IsTrue(_filtros.Last().EstaVacio);
            Assert.AreEqual("Pendiente", _lecturas.Last().Estado, "El estado no se toca");
        }

        [TestMethod]
        public async Task CambiarDeMes_RecargaLasOpcionesYMantieneLaFamiliaSiSigue()
        {
            await _vm.CargarAsync();
            _vm.FamiliaSeleccionada = _vm.Familias.First(f => f.Valor == "Lisap");
            await Task.Yield();
            _vm.AccionSeleccionada = _vm.Acciones.First(a => a.Valor == PropuestasStockConstantes.ACCION_LIQUIDAR);
            await Task.Yield();
            A.CallTo(() => _servicio.LeerOpcionesFiltro("2026-09")).Returns(new OpcionesFiltroPropuestasModel
            {
                Familias = new List<string> { "Lisap" },
                Acciones = new List<string> { PropuestasStockConstantes.ACCION_AJUSTAR }
            });

            _vm.MesSeleccionado = _vm.Meses.First(m => m.Valor == "2026-09");
            await Task.Yield();

            A.CallTo(() => _servicio.LeerOpcionesFiltro("2026-09")).MustHaveHappenedOnceExactly();
            Assert.AreEqual("Lisap", _vm.FamiliaSeleccionada.Valor, "Lisap sigue en septiembre");
            Assert.IsNull(_vm.AccionSeleccionada.Valor, "«Liquidar» no está en septiembre: vuelve a «(todas)»");
            Assert.AreEqual("Lisap", _filtros.Last().Familia);
            Assert.IsNull(_filtros.Last().Accion);
        }

        [TestMethod]
        public async Task OpcionesSoloSeLeenUnaVezPorMes()
        {
            await _vm.CargarAsync();
            await _vm.CargarCommand.ExecuteAsync(null);
            _vm.FamiliaSeleccionada = _vm.Familias.First(f => f.Valor == "Lisap");
            await Task.Yield();

            A.CallTo(() => _servicio.LeerOpcionesFiltro(A<string>._)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void ComponerResumen_ConFiltrosYSinPropuestas_LoDice()
        {
            Assert.AreEqual("No hay propuestas de ese mes con estos filtros (familia Lisap · acción Liquidar).",
                PropuestasStockViewModel.ComponerResumen(new List<ResumenEstadoPropuestasModel>(), "familia Lisap · acción Liquidar"));
        }

        [TestMethod]
        public void Filtro_ParametrosUrlYDescripcion()
        {
            var filtro = new FiltroPropuestasStock { Familia = "Eva Visnu", Accion = "Ajustar máx/mín", Texto = " 50% " };

            CollectionAssert.AreEqual(new[] { "familia=Eva%20Visnu", "accion=Ajustar%20m%C3%A1x%2Fm%C3%ADn", "texto=50%25" }, filtro.ParametrosUrl());
            Assert.AreEqual("familia Eva Visnu · acción Ajustar máx/mín · «50%»", filtro.Descripcion());
        }

        [TestMethod]
        public void NombreEstado_EnPotenciacion()
        {
            Assert.AreEqual("En potenciación", PropuestasStockViewModel.NombreEstado(PropuestasStockConstantes.ESTADO_EN_POTENCIACION));
            CollectionAssert.Contains(_vm.Estados.Select(e => e.Valor).ToList(), PropuestasStockConstantes.ESTADO_EN_POTENCIACION);
        }

        [TestMethod]
        public void Meses_ElUltimoCalculadoYLos24Anteriores()
        {
            Assert.IsNull(_vm.Meses[0].Valor);
            Assert.AreEqual("2026-10", _vm.Meses[1].Valor);
            Assert.AreEqual(25, _vm.Meses.Count);
        }

        [TestMethod]
        public async Task SinMotivosEnLaApi_UsaLosDeReserva()
        {
            A.CallTo(() => _servicio.LeerMotivosSilencio()).Returns(new List<string>());
            var vm = new PropuestasStockViewModel(_servicio, () => Hoy);

            await vm.CargarAsync();

            CollectionAssert.AreEqual(PropuestasStockConstantes.MOTIVOS_SILENCIO_RESERVA.ToList(), vm.MotivosSilencio);
        }

        [TestMethod]
        public async Task Aplicar_SinCambiar_MandaSoloLaDecisionYEnsenaMensajeYCambios()
        {
            await _vm.CargarAsync();

            await _vm.AplicarCommand.ExecuteAsync(null);

            var (id, peticion) = _decisiones.Single();
            Assert.AreEqual(1, id);
            Assert.AreEqual("Aplicar", peticion.Decision);
            Assert.IsNull(peticion.StockMaximo, "Sin cambios no se mandan: la API aplica los propuestos");
            Assert.IsNull(peticion.StockMinimo);
            StringAssert.Contains(_vm.Mensaje, "aplicada");
            CollectionAssert.AreEqual(new[] { "ALG: máximo 555 → 364, mínimo 295 → 220" }, _vm.UltimosCambios.ToList());
            Assert.AreEqual(2, _lecturas.Count, "Después de decidir se vuelve a leer");
        }

        [TestMethod]
        public async Task Aplicar_ConOtroMaximoSinMotivo_NoLlamaALaApiYPideElMotivo()
        {
            await _vm.CargarAsync();
            _vm.StockMaximo = 300;

            await _vm.AplicarCommand.ExecuteAsync(null);

            Assert.AreEqual(0, _decisiones.Count);
            StringAssert.Contains(_vm.Mensaje, "indique el motivo");
        }

        [TestMethod]
        public async Task Aplicar_ConOtroMaximoYMotivo_MandaLosValores()
        {
            await _vm.CargarAsync();
            _vm.StockMaximo = 300;
            _vm.StockMinimo = 200;
            _vm.Motivo = "  Viene campaña  ";

            await _vm.AplicarCommand.ExecuteAsync(null);

            var peticion = _decisiones.Single().Peticion;
            Assert.AreEqual(300, peticion.StockMaximo);
            Assert.AreEqual(200, peticion.StockMinimo);
            Assert.AreEqual("Viene campaña", peticion.Motivo);
        }

        [TestMethod]
        public async Task Aplicar_MinimoMayorQueMaximo_NoLlamaALaApi()
        {
            await _vm.CargarAsync();
            _vm.StockMaximo = 10;
            _vm.StockMinimo = 20;
            _vm.Motivo = "x";

            await _vm.AplicarCommand.ExecuteAsync(null);

            Assert.AreEqual(0, _decisiones.Count);
            StringAssert.Contains(_vm.Mensaje, "mínimo no puede ser mayor");
        }

        [TestMethod]
        public async Task Rechazar_SinMotivo_LoPide_ConMotivo_LoManda()
        {
            await _vm.CargarAsync();

            await _vm.RechazarCommand.ExecuteAsync(null);
            Assert.AreEqual(0, _decisiones.Count);
            StringAssert.Contains(_vm.Mensaje, "indique el motivo");

            _vm.Motivo = "Es de temporada";
            await _vm.RechazarCommand.ExecuteAsync(null);
            Assert.AreEqual("Rechazar", _decisiones.Single().Peticion.Decision);
            Assert.AreEqual("Es de temporada", _decisiones.Single().Peticion.Motivo);
        }

        [TestMethod]
        public async Task Posponer_MandaLaDecision()
        {
            await _vm.CargarAsync();

            await _vm.PosponerCommand.ExecuteAsync(null);

            Assert.AreEqual("Posponer", _decisiones.Single().Peticion.Decision);
            Assert.IsNull(_decisiones.Single().Peticion.StockMaximo);
        }

        [TestMethod]
        public async Task Silenciar_PideMotivoYFechaDentroDe24Meses()
        {
            await _vm.CargarAsync();

            await _vm.SilenciarCommand.ExecuteAsync(null);
            StringAssert.Contains(_vm.Mensaje, "motivo del silencio");

            _vm.MotivoSilencio = "Campaña";
            _vm.SilenciarHasta = Hoy.AddMonths(25);
            await _vm.SilenciarCommand.ExecuteAsync(null);
            StringAssert.Contains(_vm.Mensaje, "24 meses");

            _vm.SilenciarHasta = Hoy;
            await _vm.SilenciarCommand.ExecuteAsync(null);
            StringAssert.Contains(_vm.Mensaje, "posterior a hoy");
            Assert.AreEqual(0, _decisiones.Count);

            _vm.SilenciarHasta = Hoy.AddMonths(24);
            await _vm.SilenciarCommand.ExecuteAsync(null);
            var peticion = _decisiones.Single().Peticion;
            Assert.AreEqual("Silenciar", peticion.Decision);
            Assert.AreEqual("Campaña", peticion.MotivoSilencio);
            Assert.AreEqual(Hoy.AddMonths(24), peticion.SilenciarHasta);
        }

        [TestMethod]
        public async Task Silenciar_LaFechaPropuestaEsDentroDeTresMeses()
        {
            await _vm.CargarAsync();

            Assert.AreEqual(Hoy.AddMonths(3), _vm.SilenciarHasta);
        }

        [TestMethod]
        public async Task Liquidar_SoloEnLasDeLiquidar_ConCanal_AplicaConElCanal()
        {
            await _vm.CargarAsync();
            Assert.IsFalse(_vm.LiquidarCommand.CanExecute(null), "Una de ajustar no se liquida");
            Assert.IsTrue(_vm.AplicarCommand.CanExecute(null));

            _vm.PropuestaSeleccionada = _vm.Propuestas.Single(p => p.Id == 2);
            Assert.IsTrue(_vm.LiquidarCommand.CanExecute(null));
            Assert.IsFalse(_vm.AplicarCommand.CanExecute(null), "La de liquidar se acepta con «Liquidar» (con el canal)");

            await _vm.LiquidarCommand.ExecuteAsync(null);
            Assert.AreEqual(0, _decisiones.Count);
            StringAssert.Contains(_vm.Mensaje, "Outlet o Ganavisiones");

            _vm.CanalLiquidacion = "Ganavisiones";
            await _vm.LiquidarCommand.ExecuteAsync(null);
            var (id, peticion) = _decisiones.Single();
            Assert.AreEqual(2, id);
            Assert.AreEqual("Aplicar", peticion.Decision);
            Assert.AreEqual("Ganavisiones", peticion.CanalLiquidacion);
        }

        [TestMethod]
        public async Task NoSePuedenDecidir_LasAplicadasSuperadasYSinAccion()
        {
            var aplicada = Ajuste(5);
            aplicada.Estado = "Aplicada";
            var superada = Ajuste(6);
            superada.Estado = "Superada";
            var mantener = Ajuste(7);
            mantener.Accion = PropuestasStockConstantes.ACCION_MANTENER;
            A.CallTo(() => _servicio.LeerPropuestas(A<string>._, A<string>._, A<int>._, A<FiltroPropuestasStock>._))
                .Returns(new List<PropuestaStockModel> { aplicada, superada, mantener });
            await _vm.CargarAsync();

            foreach (PropuestaStockModel p in _vm.Propuestas)
            {
                _vm.PropuestaSeleccionada = p;
                Assert.IsFalse(_vm.PuedeDecidir, $"Propuesta {p.Id}");
                Assert.IsFalse(_vm.AplicarCommand.CanExecute(null));
                Assert.IsFalse(_vm.RechazarCommand.CanExecute(null));
            }
        }

        [DataTestMethod]
        [DataRow(400, "Si no aplica el máximo y el mínimo propuestos (364/220), indique el motivo.")]
        [DataRow(404, "No existe la propuesta 1.")]
        [DataRow(409, "Esta propuesta es de septiembre 2026 y ya hay una más reciente: decida sobre la última.")]
        public async Task Decidir_ErrorDeLaApi_EnsenaSuMessage(int codigo, string mensaje)
        {
            A.CallTo(() => _servicio.Decidir(A<int>._, A<DecisionPropuestaStockPeticion>._))
                .ThrowsAsync(new PropuestasStockException(mensaje, codigo));
            await _vm.CargarAsync();

            await _vm.PosponerCommand.ExecuteAsync(null);

            Assert.AreEqual(mensaje, _vm.Mensaje);
            Assert.AreEqual(0, _vm.UltimosCambios.Count);
            Assert.IsFalse(_vm.EstaOcupado);
            Assert.AreEqual(1, _lecturas.Count, "Si falla no se vuelve a leer");
        }

        [TestMethod]
        public async Task Cargar_403_DejaLaVentanaVaciaSinDecisionesYConElMensaje()
        {
            A.CallTo(() => _servicio.LeerResumen(A<string>._, A<FiltroPropuestasStock>._))
                .ThrowsAsync(new PropuestasStockException("Solo Compras y Dirección pueden ver las propuestas de stock.", 403));

            await _vm.CargarAsync();

            Assert.IsTrue(_vm.SinPermiso);
            Assert.AreEqual(0, _vm.Propuestas.Count);
            Assert.IsNull(_vm.PropuestaSeleccionada);
            Assert.IsFalse(_vm.PuedeDecidir);
            StringAssert.Contains(_vm.Mensaje, "Solo Compras y Dirección");
        }

        [TestMethod]
        public async Task Decidir_403_EnsenaElMensaje()
        {
            A.CallTo(() => _servicio.Decidir(A<int>._, A<DecisionPropuestaStockPeticion>._))
                .ThrowsAsync(new PropuestasStockException("Solo Compras y Dirección pueden decidir las propuestas de stock.", 403));
            await _vm.CargarAsync();

            await _vm.PosponerCommand.ExecuteAsync(null);

            Assert.AreEqual("Solo Compras y Dirección pueden decidir las propuestas de stock.", _vm.Mensaje);
        }

        [TestMethod]
        public async Task TrasDecidir_SiLaFilaYaNoSaleSeSeleccionaLaSiguiente()
        {
            await _vm.CargarAsync();
            A.CallTo(() => _servicio.LeerPropuestas(A<string>._, A<string>._, A<int>._, A<FiltroPropuestasStock>._))
                .Returns(new List<PropuestaStockModel> { Liquidar() });

            await _vm.PosponerCommand.ExecuteAsync(null);

            Assert.AreEqual(2, _vm.PropuestaSeleccionada?.Id);
        }

        // Nesto#535 (gemela de NestoAPI#655): «Potenciar ventas» con objetivo y fecha de revisión.

        private static PropuestaStockModel Propio(int id = 3, int? sugerido = 45) => new()
        {
            Id = id, Mes = new DateTime(2026, 10, 1), Producto = "40100", Nombre = "Sérum propio", Almacen = "ALG",
            Accion = "Potenciar ventas", Estado = "Pendiente", VentaMedia6Meses = 30m, ObjetivoVentaSugerido = sugerido
        };

        private async Task CargarConPropio(int? sugerido = 45)
        {
            A.CallTo(() => _servicio.LeerPropuestas(A<string>._, A<string>._, A<int>._, A<FiltroPropuestasStock>._))
                .Returns(new List<PropuestaStockModel> { Propio(sugerido: sugerido) });
            await _vm.CargarAsync();
        }

        [TestMethod]
        public async Task Potenciar_DeEntrada_ObjetivoSugeridoPorLaApiYRevisionDentroDeSeisMeses()
        {
            await CargarConPropio();

            Assert.AreEqual(45, _vm.ObjetivoVentaMensual);
            Assert.AreEqual(Hoy.AddMonths(6), _vm.PotenciarHasta);
            Assert.IsTrue(_vm.PotenciarCommand.CanExecute(null));
        }

        [TestMethod]
        public async Task Potenciar_SinObjetivoSugerido_QuedaVacioYEsObligatorio()
        {
            await CargarConPropio(sugerido: null);
            Assert.IsNull(_vm.ObjetivoVentaMensual);

            await _vm.PotenciarCommand.ExecuteAsync(null);
            Assert.AreEqual(0, _decisiones.Count);
            StringAssert.Contains(_vm.Mensaje, "indique el objetivo de venta");

            _vm.ObjetivoVentaMensual = 0;
            await _vm.PotenciarCommand.ExecuteAsync(null);
            Assert.AreEqual(0, _decisiones.Count, "El objetivo tiene que ser más de 0");
        }

        [TestMethod]
        public async Task Potenciar_FechaDeRevisionFuturaYComoMucho24Meses()
        {
            await CargarConPropio();

            _vm.PotenciarHasta = null;
            await _vm.PotenciarCommand.ExecuteAsync(null);
            StringAssert.Contains(_vm.Mensaje, "fecha de revisión");

            _vm.PotenciarHasta = Hoy;
            await _vm.PotenciarCommand.ExecuteAsync(null);
            StringAssert.Contains(_vm.Mensaje, "posterior a hoy");

            _vm.PotenciarHasta = Hoy.AddMonths(24).AddDays(1);
            await _vm.PotenciarCommand.ExecuteAsync(null);
            StringAssert.Contains(_vm.Mensaje, "24 meses");
            Assert.AreEqual(0, _decisiones.Count);
        }

        [TestMethod]
        public async Task Potenciar_MandaDecisionObjetivoFechaYMotivo_YTrasReleerSeVeEnPotenciacion()
        {
            await CargarConPropio();
            _vm.ObjetivoVentaMensual = 50;
            _vm.PotenciarHasta = new DateTime(2027, 4, 15, 13, 0, 0);
            _vm.Motivo = "  Campaña en salones  ";
            var enPotenciacion = Propio();
            enPotenciacion.Estado = PropuestasStockConstantes.ESTADO_EN_POTENCIACION;
            enPotenciacion.Decision = "Potenciar";
            enPotenciacion.ObjetivoVentaMensual = 50;
            enPotenciacion.PotenciarHasta = new DateTime(2027, 4, 15);
            enPotenciacion.PotenciacionPropuestaId = 3;
            enPotenciacion.PotenciacionObjetivo = 50;
            enPotenciacion.PotenciacionHasta = new DateTime(2027, 4, 15);
            enPotenciacion.PotenciacionVigente = true;
            A.CallTo(() => _servicio.LeerPropuestas(A<string>._, A<string>._, A<int>._, A<FiltroPropuestasStock>._))
                .Returns(new List<PropuestaStockModel> { enPotenciacion });

            await _vm.PotenciarCommand.ExecuteAsync(null);

            var (id, peticion) = _decisiones.Single();
            Assert.AreEqual(3, id);
            Assert.AreEqual(PropuestasStockConstantes.DECISION_POTENCIAR, peticion.Decision);
            Assert.AreEqual(50, peticion.ObjetivoVentaMensual);
            Assert.AreEqual(new DateTime(2027, 4, 15), peticion.PotenciarHasta, "Solo la fecha, sin hora");
            Assert.AreEqual("Campaña en salones", peticion.Motivo);
            Assert.IsNull(peticion.StockMaximo);
            Assert.IsNull(peticion.CanalLiquidacion);

            PropuestaStockModel fila = _vm.PropuestaSeleccionada!;
            Assert.AreEqual(3, fila.Id, "Sigue seleccionada la misma fila, ya releída");
            Assert.IsTrue(fila.EstaEnPotenciacion);
            Assert.AreEqual("50 uds/mes hasta 15/04/2027", fila.ObjetivoYFechaPotenciacion);
            Assert.AreEqual("En potenciación: objetivo 50 uds/mes hasta 15/04/2027.", fila.TextoPotenciacion, "Sin nota de la API, el objetivo y la fecha");
            StringAssert.Contains(fila.TextoDecision, "objetivo 50 uds/mes, revisión el 15/04/2027");
            Assert.IsFalse(_vm.EstaOcupado);
        }

        [TestMethod]
        public async Task Potenciar_ApiAnteriorSinLaDecision_MensajeClaroSinRomper()
        {
            A.CallTo(() => _servicio.Decidir(A<int>._, A<DecisionPropuestaStockPeticion>._))
                .ThrowsAsync(new PropuestasStockException("La decisión tiene que ser Aplicar, Rechazar, Posponer, Silenciar.", 400));
            await CargarConPropio();

            await _vm.PotenciarCommand.ExecuteAsync(null);

            Assert.AreEqual(PropuestasStockConstantes.MENSAJE_API_SIN_POTENCIAR, _vm.Mensaje);
            Assert.IsFalse(_vm.EstaOcupado);
            Assert.IsTrue(_vm.PotenciarCommand.CanExecute(null), "La pantalla sigue funcionando");
        }

        [TestMethod]
        public async Task Potenciar_ApiSinElScript_EnsenaSuMensajeTalCual()
        {
            const string sinScript = "Falta el script de «Potenciar ventas» (Scripts/Issue655_PotenciarVentas.sql): de momento solo se puede aplicar, rechazar, posponer o silenciar.";
            A.CallTo(() => _servicio.Decidir(A<int>._, A<DecisionPropuestaStockPeticion>._))
                .ThrowsAsync(new PropuestasStockException(sinScript, 404));
            await CargarConPropio();

            await _vm.PotenciarCommand.ExecuteAsync(null);

            Assert.AreEqual(sinScript, _vm.Mensaje);
        }

        [TestMethod]
        public void MensajeErrorPotenciar_OtrosErroresTalCual()
        {
            Assert.AreEqual("Para potenciar las ventas indique el objetivo de venta (unidades al mes, más de 0).",
                PropuestasStockViewModel.MensajeErrorPotenciar(new PropuestasStockException("Para potenciar las ventas indique el objetivo de venta (unidades al mes, más de 0).", 400)));
            Assert.AreEqual("La decisión tiene que ser Aplicar, Rechazar, Posponer, Silenciar, Potenciar.",
                PropuestasStockViewModel.MensajeErrorPotenciar(new PropuestasStockException("La decisión tiene que ser Aplicar, Rechazar, Posponer, Silenciar, Potenciar.", 400)));
        }

        [TestMethod]
        public async Task Potenciar_NoSePuedeEnLasQueNoSeDeciden_NiSinPermiso()
        {
            var aplicada = Propio();
            aplicada.Estado = "Aplicada";
            A.CallTo(() => _servicio.LeerPropuestas(A<string>._, A<string>._, A<int>._, A<FiltroPropuestasStock>._))
                .Returns(new List<PropuestaStockModel> { aplicada });
            await _vm.CargarAsync();
            Assert.IsFalse(_vm.PotenciarCommand.CanExecute(null));

            A.CallTo(() => _servicio.LeerResumen(A<string>._, A<FiltroPropuestasStock>._))
                .ThrowsAsync(new PropuestasStockException("Solo Compras y Dirección pueden ver las propuestas de stock.", 403));
            await _vm.CargarAsync();
            Assert.IsFalse(_vm.PotenciarCommand.CanExecute(null));
        }

        [TestMethod]
        public void SinPotenciacion_NoSeEnsenaObjetivoNiNota()
        {
            PropuestaStockModel fila = Ajuste();

            Assert.IsNull(fila.ObjetivoYFechaPotenciacion);
            Assert.IsNull(fila.TextoPotenciacion);
        }

        [TestMethod]
        public void AlLlegar_Carga()
        {
            _vm.AlLlegar(new Nesto.Infrastructure.Contracts.ParametrosNavegacion());

            A.CallTo(() => _servicio.LeerPropuestas(A<string>._, A<string>._, A<int>._, A<FiltroPropuestasStock>._)).MustHaveHappened();
        }
    }
}
