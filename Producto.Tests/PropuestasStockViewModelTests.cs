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
            A.CallTo(() => _servicio.LeerMotivosSilencio()).Returns(new List<string> { "Nuevo", "Estacional", "Aparato", "Campaña" });
            A.CallTo(() => _servicio.LeerResumen(A<string>._)).Returns(new List<ResumenEstadoPropuestasModel>
            {
                new() { Mes = new DateTime(2026, 10, 1), Estado = "Aplicada", Propuestas = 3, ExcesoValorado = 100m },
                new() { Mes = new DateTime(2026, 10, 1), Estado = "Pendiente", Propuestas = 2, ExcesoValorado = 1200m }
            });
            A.CallTo(() => _servicio.LeerPropuestas(A<string>._, A<string>._, A<int>._))
                .ReturnsLazily((string mes, string estado, int top) =>
                {
                    _lecturas.Add((mes, estado));
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
            A.CallTo(() => _servicio.LeerResumen("2026-09")).MustHaveHappened();
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
            A.CallTo(() => _servicio.LeerPropuestas(A<string>._, A<string>._, A<int>._))
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
            A.CallTo(() => _servicio.LeerResumen(A<string>._))
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
            A.CallTo(() => _servicio.LeerPropuestas(A<string>._, A<string>._, A<int>._))
                .Returns(new List<PropuestaStockModel> { Liquidar() });

            await _vm.PosponerCommand.ExecuteAsync(null);

            Assert.AreEqual(2, _vm.PropuestaSeleccionada?.Id);
        }

        [TestMethod]
        public void AlLlegar_Carga()
        {
            _vm.AlLlegar(new Nesto.Infrastructure.Contracts.ParametrosNavegacion());

            A.CallTo(() => _servicio.LeerPropuestas(A<string>._, A<string>._, A<int>._)).MustHaveHappened();
        }
    }
}
