using ControlesUsuario.Services;
using FakeItEasy;
using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.Cajas.Interfaces;
using Nesto.Modulos.Cajas.Models;
using Nesto.Modulos.Cajas.Services;
using Nesto.Modulos.Cajas.ViewModels;
using System.Data;
using System.Net;

namespace CajasTests
{
    /// <summary>Proyectos (gemela de NestoAPI#901): el cálculo del reparto y las reglas de pantalla (puros).</summary>
    [TestClass]
    public class CalculoRepartoApunteTests
    {
        [TestMethod]
        public void PartesIguales_LaUltimaLlevaElCentimoDeAjuste()
        {
            CollectionAssert.AreEqual(new[] { 33.33M, 33.33M, 33.34M }, CalculoRepartoApunte.PartesIguales(100M, 3));
            CollectionAssert.AreEqual(new[] { 0.02M, 0.03M }, CalculoRepartoApunte.PartesIguales(0.05M, 2));
            CollectionAssert.AreEqual(new[] { 60M }, CalculoRepartoApunte.PartesIguales(60M, 1));
            Assert.AreEqual(0, CalculoRepartoApunte.PartesIguales(60M, 0).Count);
        }

        [TestMethod]
        public void PartesIguales_SumanJustoElImporte()
        {
            foreach (int n in new[] { 2, 3, 6, 7, 11 })
            {
                Assert.AreEqual(1234.57M, CalculoRepartoApunte.PartesIguales(1234.57M, n).Sum(), $"en {n} partes");
            }
        }

        [TestMethod]
        public void Pendiente_ImporteMenosLoRepartido_SinContarFilasVacias()
        {
            var partes = new[] { new ParteReparto(1, 60M), new ParteReparto(null, null), new ParteReparto(2, 15.5M) };
            Assert.AreEqual(24.5M, CalculoRepartoApunte.Pendiente(100M, partes));
            Assert.AreEqual(-5M, CalculoRepartoApunte.Pendiente(100M, new[] { new ParteReparto(1, 105M) }));
        }

        [TestMethod]
        public void Motivos_RepartoBuenoOVacio_NoHayNinguno()
        {
            Assert.AreEqual(0, CalculoRepartoApunte.Motivos(100M, new[] { new ParteReparto(1, 60M), new ParteReparto(2, 40M) }).Count);
            Assert.AreEqual(0, CalculoRepartoApunte.Motivos(100M, new ParteReparto[0]).Count, "Sin filas = quitar el reparto");
            Assert.AreEqual(0, CalculoRepartoApunte.Motivos(100M, new[] { new ParteReparto(null, null) }).Count, "Una fila en blanco no cuenta");
        }

        [TestMethod]
        public void Motivos_UnCentimoDeMas_SeAdmite_DosNo()
        {
            Assert.AreEqual(0, CalculoRepartoApunte.Motivos(100M, new[] { new ParteReparto(1, 50.01M), new ParteReparto(2, 50M) }).Count);
            List<string> motivos = CalculoRepartoApunte.Motivos(100M, new[] { new ParteReparto(1, 50.02M), new ParteReparto(2, 50M) });
            Assert.AreEqual(1, motivos.Count);
            StringAssert.Contains(motivos[0], "pasa del importe del apunte (100,00 €)");
        }

        [TestMethod]
        public void Motivos_SinProyecto_ImporteNoPositivo_Repetido_TresDecimales()
        {
            List<string> motivos = CalculoRepartoApunte.Motivos(100M, new[]
            {
                new ParteReparto(null, 10M),
                new ParteReparto(1, 0M),
                new ParteReparto(2, 5M),
                new ParteReparto(2, 1.005M)
            });
            Assert.IsTrue(motivos.Any(m => m.Contains("fila 1 no tiene proyecto")));
            Assert.IsTrue(motivos.Any(m => m.Contains("fila 2 tiene que ser mayor que cero")));
            Assert.IsTrue(motivos.Any(m => m.Contains("varias filas")));
            Assert.IsTrue(motivos.Any(m => m.Contains("fila 4 tiene más de dos decimales")));
        }

        [TestMethod]
        public void ParaEnviar_SoloFilasCompletas_ComentarioLimpio()
        {
            List<ImputacionPedida> enviar = CalculoRepartoApunte.ParaEnviar(new[]
            {
                new ParteReparto(1, 60M, "  material  "),
                new ParteReparto(null, null, null),
                new ParteReparto(2, 40M, " ")
            });
            Assert.AreEqual(2, enviar.Count);
            Assert.AreEqual("material", enviar[0].Comentario);
            Assert.IsNull(enviar[1].Comentario);
        }

        [TestMethod]
        public void TextoPendiente_PendienteEnteroOPasado()
        {
            Assert.AreEqual("Pendiente: 40,00 € de 100,00 € (60,00 % repartido).", CalculoRepartoApunte.TextoPendiente(100M, new[] { new ParteReparto(1, 60M) }));
            Assert.AreEqual("Repartido entero (100,00 €).", CalculoRepartoApunte.TextoPendiente(100M, new[] { new ParteReparto(1, 100M) }));
            StringAssert.StartsWith(CalculoRepartoApunte.TextoPendiente(100M, new[] { new ParteReparto(1, 101M) }), "Se pasa en 1,00 €");
        }

        [TestMethod]
        public void MotivosProyecto_FaltasDeUsted()
        {
            List<string> motivos = ReglasPantallaProyectos.MotivosProyectoNoValido(" ", null, null, new DateTime(2026, 5, 1), new DateTime(2026, 4, 1), -1M, null);
            Assert.AreEqual(5, motivos.Count);
            Assert.AreEqual(0, ReglasPantallaProyectos.MotivosProyectoNoValido("IMPE0210-2026-1", "Curso", "CursoSEPE", null, null, null, null).Count);
        }

        [TestMethod]
        public void NombreTipo_LosDeLaApiEnCristiano()
        {
            Assert.AreEqual("Curso del SEPE", ReglasPantallaProyectos.NombreTipo("CursoSEPE"));
            Assert.AreEqual("Campaña", ReglasPantallaProyectos.NombreTipo("campana"));
            Assert.AreEqual("Raro", ReglasPantallaProyectos.NombreTipo("Raro"));
        }

        [TestMethod]
        public void TablaComparativa_UnaColumnaPorProyectoConSuCodigoEnCaption()
        {
            var comparativa = new ComparativaProyectosModel
            {
                Proyectos = new List<ProyectoModel> { new ProyectoModel { Id = 1, Codigo = "IMPE0210-2025.1" }, new ProyectoModel { Id = 2, Codigo = "IMPE0210-2026/1" } },
                Ingresos = new List<FilaComparativaProyectosModel> { new FilaComparativaProyectosModel { Cuenta = "70500000", NombreCuenta = "Cursos", Importes = new List<decimal> { 1000M, 1200M } } },
                Gastos = new List<FilaComparativaProyectosModel> { new FilaComparativaProyectosModel { Cuenta = "62300000", NombreCuenta = "Profesores", Importes = new List<decimal> { 400M, 500M } } },
                TotalesIngresos = new List<decimal> { 1000M, 1200M },
                TotalesGastos = new List<decimal> { 400M, 500M },
                Resultados = new List<decimal> { 600M, 700M },
                Margenes = new List<decimal?> { 60M, null }
            };

            DataTable tabla = ReglasPantallaProyectos.TablaComparativa(comparativa);

            Assert.AreEqual(3, tabla.Columns.Count);
            Assert.AreEqual("P0", tabla.Columns[1].ColumnName);
            Assert.AreEqual("IMPE0210-2025.1", tabla.Columns[1].Caption);
            Assert.AreEqual("IMPE0210-2026/1", tabla.Columns[2].Caption);
            DataRow resultado = tabla.Rows.Cast<DataRow>().Single(r => (string)r[0] == "Resultado");
            Assert.AreEqual("700,00 €", resultado[2]);
            DataRow margen = tabla.Rows.Cast<DataRow>().Single(r => (string)r[0] == "Margen");
            Assert.AreEqual("60,00 %", margen[1]);
            Assert.AreEqual("—", margen[2]);
            Assert.IsTrue(tabla.Rows.Cast<DataRow>().Any(r => (string)r[0] == "70500000 Cursos"));
        }
    }

    /// <summary>Proyectos: las URL y los errores de la API.</summary>
    [TestClass]
    public class ProyectosServiceTests
    {
        [TestMethod]
        public void UrlApuntes_ConTodosLosFiltros()
        {
            string url = ProyectosService.UrlApuntes(new FiltroApuntesProyectos
            {
                Empresa = "1",
                Desde = new DateTime(2026, 1, 1),
                Hasta = new DateTime(2026, 11, 25),
                Cuenta = "623,705",
                Proveedor = "1234",
                Documento = "F/26",
                Texto = "aula",
                SoloPendientes = false,
                Pagina = 2
            });
            Assert.AreEqual("Proyectos/ApuntesPorImputar?empresa=1&desde=2026-01-01&hasta=2026-11-25&cuenta=623%2C705&proveedor=1234&documento=F%2F26&texto=aula&soloPendientes=false&pagina=2", url);
        }

        [TestMethod]
        public void UrlComparativa_PorIdsOPorProducto()
        {
            Assert.AreEqual("Proyectos/Comparativa?empresa=1&ids=1,3", ProyectosService.UrlComparativa(new[] { 1, 3, 1 }, "IMPE0210", "1"));
            Assert.AreEqual("Proyectos/Comparativa?empresa=1&producto=IMPE0210", ProyectosService.UrlComparativa(null, " IMPE0210 ", "1"));
        }

        [TestMethod]
        public void UrlListar_SoloLoQueViene()
        {
            Assert.AreEqual("Proyectos?empresa=1&soloAbiertos=true", ProyectosService.UrlListar("1", true, null, " "));
            Assert.AreEqual("Proyectos?empresa=1&soloAbiertos=false&tipo=Feria&filtro=BF%202026", ProyectosService.UrlListar("1", false, "Feria", "BF 2026"));
        }

        [TestMethod]
        public void Error_SinTabla_DiceQueNoEstaActivadoYLoDeLaApi()
        {
            ProyectosApiException ex = ProyectosService.Error(HttpStatusCode.ServiceUnavailable,
                "{\"Message\":\"Todavía no existen las tablas de proyectos.\",\"Codigo\":\"SIN_TABLA\"}", "contexto");
            Assert.AreEqual(ProyectosService.CODIGO_SIN_TABLA, ex.Codigo);
            StringAssert.StartsWith(ex.Message, "Los proyectos todavía no están activados en el servidor.");
            StringAssert.Contains(ex.Message, "Todavía no existen las tablas de proyectos.");
        }

        [TestMethod]
        public void Error_ConCodigo_ElMensajeDeLaApiTalCual()
        {
            ProyectosApiException ex = ProyectosService.Error(HttpStatusCode.Conflict,
                "{\"Message\":\"El apunte 123 ha cambiado.\",\"Codigo\":\"APUNTE_CAMBIADO\"}", "contexto");
            Assert.AreEqual("APUNTE_CAMBIADO", ex.Codigo);
            Assert.AreEqual("El apunte 123 ha cambiado.", ex.Message);
            Assert.AreEqual(HttpStatusCode.Conflict, ex.Estado);
        }

        [TestMethod]
        public void Error_SinCuerpo_PorElEstado()
        {
            Assert.AreEqual("Solo Administración, Dirección o Informática pueden ver y cambiar los proyectos.",
                ProyectosService.Error(HttpStatusCode.Forbidden, string.Empty, "contexto").Message);
            Assert.AreEqual("contexto", ProyectosService.Error(HttpStatusCode.InternalServerError, "<html>", "contexto").Message);
        }
    }

    /// <summary>Proyectos: la ventana (servicio falso).</summary>
    [TestClass]
    public class ProyectosViewModelTests
    {
        private static readonly DateTime Hoy = new DateTime(2026, 11, 25);

        private IProyectosService _servicio = null!;
        private IServicioDialogos _dialogos = null!;
        private IGuardadorFicherosDescarga _guardador = null!;
        private List<string> _abiertos = null!;
        private List<ProyectoModel> _proyectos = null!;

        [TestInitialize]
        public void Inicializar()
        {
            _servicio = A.Fake<IProyectosService>();
            _dialogos = A.Fake<IServicioDialogos>();
            _guardador = A.Fake<IGuardadorFicherosDescarga>();
            _abiertos = new List<string>();
            _proyectos = new List<ProyectoModel>
            {
                new ProyectoModel { Id = 1, Codigo = "IMPE0210-2026-1", Nombre = "Depilación", Tipo = "CursoSEPE", Estado = "Abierto", Producto = "IMPE0210", Resultado = 100M },
                new ProyectoModel { Id = 2, Codigo = "FERIA-2026", Nombre = "Feria", Tipo = "Feria", Estado = "Abierto" },
                new ProyectoModel { Id = 3, Codigo = "BF2025", Nombre = "Black Friday 2025", Tipo = "Campana", Estado = "Cerrado" }
            };
            A.CallTo(() => _servicio.Listar(A<string>._, A<bool>._, A<string?>._, A<string?>._))
                .ReturnsLazily((string e, bool soloAbiertos, string? t, string? f) =>
                    Task.FromResult(_proyectos.Where(p => !soloAbiertos || !p.EstaCerrado).ToList()));
        }

        private ProyectosViewModel CrearViewModel() => new ProyectosViewModel(_servicio, _dialogos, _guardador, () => Hoy, r => _abiertos.Add(r));

        private static ApunteProyectoModel Apunte(int numero, decimal importe, params ImputacionApunteModel[] imputaciones) => new ApunteProyectoModel
        {
            Empresa = "1",
            NumeroOrden = numero,
            Fecha = new DateTime(2026, 3, 1),
            Cuenta = "62300000",
            ImporteApunte = importe,
            Naturaleza = "Gasto",
            Imputado = imputaciones.Sum(i => i.Importe),
            Pendiente = importe - imputaciones.Sum(i => i.Importe),
            Imputaciones = imputaciones.ToList()
        };

        private async Task<ProyectosViewModel> ConApuntes(params ApunteProyectoModel[] apuntes)
        {
            A.CallTo(() => _servicio.ApuntesPorImputar(A<FiltroApuntesProyectos>._))
                .Returns(Task.FromResult(new ApuntesPorImputarModel { Apuntes = apuntes.ToList() }));
            var vm = CrearViewModel();
            await vm.BuscarAsync();
            await vm.Apuntes.BuscarAsync();
            return vm;
        }

        [TestMethod]
        public async Task Buscar_ListaLosProyectosYPreparaLosAbiertosParaImputar()
        {
            var vm = CrearViewModel();
            vm.SoloAbiertos = false;
            await vm.BuscarAsync();

            Assert.AreEqual(3, vm.Proyectos.Count);
            CollectionAssert.AreEqual(new[] { 1, 2 }, vm.Apuntes.ProyectosAbiertos.Select(p => p.Id).ToList(), "Solo los abiertos para imputar");
            Assert.AreEqual(3, vm.Cuenta.Proyectos.Count, "La cuenta de explotación también de los cerrados");
            A.CallTo(() => _servicio.Listar("1", false, null, null)).MustHaveHappened();
        }

        [TestMethod]
        public void Apuntes_PorDefectoDelUnoDeEneroAHoyYSoloPendientes()
        {
            var vm = CrearViewModel();
            Assert.AreEqual(new DateTime(2026, 1, 1), vm.Apuntes.Desde);
            Assert.AreEqual(Hoy, vm.Apuntes.Hasta);
            Assert.IsTrue(vm.Apuntes.SoloPendientes);
        }

        [TestMethod]
        public async Task Nuevo_GuardaConElCodigoEnMayusculasYLoVacioANull()
        {
            A.CallTo(() => _servicio.Crear(A<ProyectoModel>._)).ReturnsLazily((ProyectoModel p) => Task.FromResult(new ProyectoModel { Id = 9, Codigo = p.Codigo }));
            var vm = CrearViewModel();
            vm.NuevoCommand.Execute(null);
            vm.Edicion!.Codigo = " impe0210-2026-2 ";
            vm.Edicion.Nombre = "Depilación 2";
            vm.Edicion.Producto = " ";

            await vm.GuardarAsync();

            A.CallTo(() => _servicio.Crear(A<ProyectoModel>.That.Matches(p =>
                p.Codigo == "IMPE0210-2026-2" && p.Empresa == "1" && p.Tipo == "CursoSEPE" && p.Producto == null))).MustHaveHappenedOnceExactly();
            Assert.IsNull(vm.Edicion, "Guardado, se cierra el panel");
            StringAssert.StartsWith(vm.Resumen, "Proyecto IMPE0210-2026-2 creado.");
        }

        [TestMethod]
        public async Task Guardar_ConFaltas_NoLlamaALaApiYLasEnsena()
        {
            var vm = CrearViewModel();
            vm.NuevoCommand.Execute(null);

            await vm.GuardarAsync();

            A.CallTo(() => _servicio.Crear(A<ProyectoModel>._)).MustNotHaveHappened();
            A.CallTo(() => _dialogos.ShowError(A<string>.That.Contains("Falta el código"))).MustHaveHappened();
            Assert.IsNotNull(vm.Edicion);
        }

        [TestMethod]
        public async Task Editar_ModificaConSuId()
        {
            A.CallTo(() => _servicio.Modificar(A<int>._, A<ProyectoModel>._)).ReturnsLazily((int id, ProyectoModel p) => Task.FromResult(p));
            var vm = CrearViewModel();
            await vm.BuscarAsync();
            vm.ProyectoSeleccionado = vm.Proyectos[0];
            vm.EditarCommand.Execute(null);
            vm.Edicion!.Nombre = "Depilación (mañanas)";

            await vm.GuardarAsync();

            A.CallTo(() => _servicio.Modificar(1, A<ProyectoModel>.That.Matches(p => p.Nombre == "Depilación (mañanas)" && p.Codigo == "IMPE0210-2026-1")))
                .MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task Borrar_SinConfirmar_NoBorra_YSu409SeEnsenaTalCual()
        {
            var vm = CrearViewModel();
            await vm.BuscarAsync();
            vm.ProyectoSeleccionado = vm.Proyectos[0];

            await vm.BorrarAsync();
            A.CallTo(() => _servicio.Borrar(A<int>._)).MustNotHaveHappened();

            A.CallTo(() => _dialogos.ShowConfirmationAnswer(A<string>._, A<string>._, true)).Returns(true);
            A.CallTo(() => _servicio.Borrar(1)).ThrowsAsync(new ProyectosApiException("El proyecto IMPE0210-2026-1 tiene 3 imputaciones: quítelas antes de borrarlo, o ciérrelo.",
                "PROYECTO_CON_IMPUTACIONES", HttpStatusCode.Conflict));
            await vm.BorrarAsync();

            A.CallTo(() => _dialogos.ShowError("El proyecto IMPE0210-2026-1 tiene 3 imputaciones: quítelas antes de borrarlo, o ciérrelo.")).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task Reabrir_ElTexto403DeLaApiSeEnsena()
        {
            A.CallTo(() => _dialogos.ShowConfirmationAnswer(A<string>._, A<string>._, true)).Returns(true);
            A.CallTo(() => _servicio.Reabrir(3)).ThrowsAsync(new ProyectosApiException("Solo Dirección puede reabrir un proyecto cerrado.", "SIN_PERMISO", HttpStatusCode.Forbidden));
            var vm = CrearViewModel();
            vm.SoloAbiertos = false;
            await vm.BuscarAsync();
            vm.ProyectoSeleccionado = vm.Proyectos.Single(p => p.Id == 3);
            Assert.IsTrue(vm.ReabrirCommand.CanExecute(null));
            Assert.IsFalse(vm.CerrarCommand.CanExecute(null));
            Assert.IsFalse(vm.EditarCommand.CanExecute(null), "Cerrado no se edita");

            await vm.ReabrirAsync();

            A.CallTo(() => _dialogos.ShowError("Solo Dirección puede reabrir un proyecto cerrado.")).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task Cerrar_Confirmado_LlamaYRecarga()
        {
            A.CallTo(() => _dialogos.ShowConfirmationAnswer(A<string>._, A<string>._, true)).Returns(true);
            var vm = CrearViewModel();
            await vm.BuscarAsync();
            vm.ProyectoSeleccionado = vm.Proyectos[1];

            await vm.CerrarAsync();

            A.CallTo(() => _servicio.Cerrar(2)).MustHaveHappenedOnceExactly();
            StringAssert.StartsWith(vm.Resumen, "Proyecto FERIA-2026 cerrado.");
        }

        [TestMethod]
        public async Task VerCuenta_PasaALaPestanaYCargaSuCuenta()
        {
            A.CallTo(() => _servicio.CuentaExplotacion(1)).Returns(Task.FromResult(new CuentaExplotacionProyectoModel
            {
                Proyecto = _proyectos[0],
                TotalIngresos = 1000M,
                TotalGastos = 400M,
                Resultado = 600M,
                Margen = 60M,
                Presupuesto = new PresupuestoFrenteARealModel { PresupuestoIngresos = 900M, DesviacionIngresos = 100M }
            }));
            var vm = CrearViewModel();
            await vm.BuscarAsync();
            vm.ProyectoSeleccionado = vm.Proyectos[0];

            await vm.VerCuentaAsync();

            Assert.AreEqual(ProyectosViewModel.PESTANA_CUENTA, vm.PestanaSeleccionada);
            Assert.AreEqual(1, vm.Cuenta.ProyectoElegido?.Id);
            Assert.AreEqual("Ingresos 1.000,00 € · Gastos 400,00 € · Resultado 600,00 €", vm.Cuenta.TextoTotales);
            Assert.AreEqual("Margen sobre ingresos: 60,00 %", vm.Cuenta.TextoMargen);
            Assert.IsTrue(vm.Cuenta.HayPresupuesto);
            Assert.AreEqual("900,00 €", vm.Cuenta.LineasPresupuesto[0].Presupuesto);
            Assert.AreEqual("—", vm.Cuenta.LineasPresupuesto[1].Presupuesto);
        }

        [TestMethod]
        public async Task Excel_SeGuardaDondeDiceYSeAbre()
        {
            A.CallTo(() => _servicio.CuentaExplotacion(1)).Returns(Task.FromResult(new CuentaExplotacionProyectoModel { Proyecto = _proyectos[0] }));
            A.CallTo(() => _servicio.CuentaExplotacionExcel(1)).Returns(Task.FromResult(new FicheroProyecto { Nombre = "CuentaExplotacion_IMPE0210-2026-1.xlsx", Contenido = new byte[] { 1, 2 } }));
            A.CallTo(() => _guardador.ElegirDondeGuardar(A<string>._, "CuentaExplotacion_IMPE0210-2026-1.xlsx", A<string>._, A<string>._, ".xlsx")).Returns(@"C:\Descargas\c.xlsx");
            var vm = CrearViewModel();
            await vm.BuscarAsync();
            await vm.Cuenta.ElegirAsync(_proyectos[0]);

            await vm.Cuenta.ExcelAsync();

            A.CallTo(() => _guardador.Guardar(@"C:\Descargas\c.xlsx", A<byte[]>.That.Matches(b => b.Length == 2))).MustHaveHappenedOnceExactly();
            CollectionAssert.AreEqual(new[] { @"C:\Descargas\c.xlsx" }, _abiertos);
        }

        [TestMethod]
        public void NombreExcel_SinNombreDeLaApi_UnoValidoParaFichero()
        {
            Assert.AreEqual("Cuenta de explotación IMPE0210-2026_1.xlsx", CuentaExplotacionProyectoViewModel.NombreExcel(null, "IMPE0210-2026/1"));
        }

        [TestMethod]
        public async Task Comparar_SinProducto_LoDiceYNoLlama_ConProducto_TablaLadoALado()
        {
            A.CallTo(() => _servicio.CuentaExplotacion(A<int>._)).ReturnsLazily((int id) => Task.FromResult(new CuentaExplotacionProyectoModel { Proyecto = _proyectos.Single(p => p.Id == id) }));
            A.CallTo(() => _servicio.Comparativa(null, "IMPE0210", "1")).Returns(Task.FromResult(new ComparativaProyectosModel
            {
                Proyectos = new List<ProyectoModel> { _proyectos[0], new ProyectoModel { Id = 7, Codigo = "IMPE0210-2025-1" } },
                Resultados = new List<decimal> { 600M, 500M }
            }));
            var vm = CrearViewModel();
            await vm.BuscarAsync();

            await vm.Cuenta.ElegirAsync(_proyectos[1]);
            await vm.Cuenta.CompararAsync();
            A.CallTo(() => _servicio.Comparativa(A<IEnumerable<int>?>._, A<string?>._, A<string>._)).MustNotHaveHappened();
            A.CallTo(() => _dialogos.ShowNotification(A<string>._, A<string>.That.Contains("no tiene producto"))).MustHaveHappened();

            await vm.Cuenta.ElegirAsync(_proyectos[0]);
            await vm.Cuenta.CompararAsync();
            Assert.IsTrue(vm.Cuenta.HayComparativa);
            Assert.AreEqual("IMPE0210-2025-1", vm.Cuenta.Comparativa!.Table!.Columns[2].Caption);
        }

        [TestMethod]
        public async Task BuscarApuntes_MandaLosFiltrosLimpios()
        {
            var vm = CrearViewModel();
            vm.Apuntes.Cuenta = " 623 ";
            vm.Apuntes.Tercero = "";
            vm.Apuntes.SoloPendientes = false;

            await vm.Apuntes.BuscarAsync();

            A.CallTo(() => _servicio.ApuntesPorImputar(A<FiltroApuntesProyectos>.That.Matches(f =>
                f.Empresa == "1" && f.Cuenta == "623" && f.Proveedor == null && !f.SoloPendientes && f.Pagina == 0
                && f.Desde == new DateTime(2026, 1, 1) && f.Hasta == Hoy))).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task CargarMas_PideLaPaginaSiguienteYAnade()
        {
            A.CallTo(() => _servicio.ApuntesPorImputar(A<FiltroApuntesProyectos>.That.Matches(f => f.Pagina == 0)))
                .Returns(Task.FromResult(new ApuntesPorImputarModel { HayMas = true, Apuntes = new List<ApunteProyectoModel> { Apunte(1, 10M) } }));
            A.CallTo(() => _servicio.ApuntesPorImputar(A<FiltroApuntesProyectos>.That.Matches(f => f.Pagina == 1)))
                .Returns(Task.FromResult(new ApuntesPorImputarModel { HayMas = false, Apuntes = new List<ApunteProyectoModel> { Apunte(2, 20M) } }));
            var vm = CrearViewModel();
            await vm.Apuntes.BuscarAsync();
            Assert.IsTrue(vm.Apuntes.HayMas);
            StringAssert.Contains(vm.Apuntes.Resumen, "Cargar más");

            await vm.Apuntes.CargarMasAsync();

            Assert.AreEqual(2, vm.Apuntes.Apuntes.Count);
            Assert.IsFalse(vm.Apuntes.HayMas);
        }

        [TestMethod]
        public async Task ElegirUnApunte_CargaSuRepartoYElPendienteEnVivo()
        {
            var vm = await ConApuntes(Apunte(10, 100M, new ImputacionApunteModel { ProyectoId = 1, CodigoProyecto = "IMPE0210-2026-1", Importe = 60M, EstadoProyecto = "Abierto" }));

            vm.Apuntes.CambiarSeleccion(vm.Apuntes.Apuntes);

            Assert.AreEqual(1, vm.Apuntes.Filas.Count);
            Assert.AreEqual("Pendiente: 40,00 € de 100,00 € (60,00 % repartido).", vm.Apuntes.TextoPendiente);
            vm.Apuntes.Filas[0].Importe = 120M;
            StringAssert.StartsWith(vm.Apuntes.TextoPendiente, "Se pasa en 20,00 €");
            Assert.IsFalse(vm.Apuntes.GuardarRepartoCommand.CanExecute(null), "No deja pasar del importe del apunte");
        }

        [TestMethod]
        public async Task RepartirAPartesIguales_EntreLosElegidos()
        {
            var vm = await ConApuntes(Apunte(10, 100M));
            vm.Apuntes.CambiarSeleccion(vm.Apuntes.Apuntes);
            vm.Apuntes.Filas[0].ProyectoId = 1;
            vm.Apuntes.AnadirFilaCommand.Execute(null);
            vm.Apuntes.Filas[1].ProyectoId = 2;
            vm.Apuntes.AnadirFilaCommand.Execute(null);
            vm.Apuntes.Filas[2].ProyectoId = 3;

            vm.Apuntes.RepartirAPartesIguales();

            CollectionAssert.AreEqual(new decimal?[] { 33.33M, 33.33M, 33.34M }, vm.Apuntes.Filas.Select(f => f.Importe).ToList());
            Assert.AreEqual("Repartido entero (100,00 €).", vm.Apuntes.TextoPendiente);
        }

        [TestMethod]
        public async Task TodoAEsteProyecto_QuitaLasDemasPeroRespetaLasDeProyectosCerrados()
        {
            var vm = await ConApuntes(Apunte(10, 100M,
                new ImputacionApunteModel { ProyectoId = 3, CodigoProyecto = "BF2025", Importe = 30M, EstadoProyecto = "Cerrado" },
                new ImputacionApunteModel { ProyectoId = 1, CodigoProyecto = "IMPE0210-2026-1", Importe = 20M, EstadoProyecto = "Abierto" },
                new ImputacionApunteModel { ProyectoId = 2, CodigoProyecto = "FERIA-2026", Importe = 10M, EstadoProyecto = "Abierto" }));
            vm.Apuntes.CambiarSeleccion(vm.Apuntes.Apuntes);
            Assert.IsTrue(vm.Apuntes.OpcionesProyecto.Any(p => p.Id == 3), "El cerrado se enseña en el desplegable");

            vm.Apuntes.TodoAEsteProyecto(vm.Apuntes.Filas.Single(f => f.ProyectoId == 1));

            Assert.AreEqual(2, vm.Apuntes.Filas.Count);
            Assert.AreEqual(30M, vm.Apuntes.Filas.Single(f => f.ProyectoId == 3).Importe);
            Assert.AreEqual(70M, vm.Apuntes.Filas.Single(f => f.ProyectoId == 1).Importe);
        }

        [TestMethod]
        public async Task GuardarReparto_MandaElImporteLeidoYEnsenaLosAvisosSinBloquear()
        {
            var vm = await ConApuntes(Apunte(10, 100M));
            A.CallTo(() => _servicio.GuardarImputaciones(10, A<PeticionImputacionesApunte>._)).Returns(Task.FromResult(new ImputacionesApunteModel
            {
                Apunte = Apunte(10, 100M, new ImputacionApunteModel { ProyectoId = 1, CodigoProyecto = "IMPE0210-2026-1", Importe = 100M, EstadoProyecto = "Abierto" }),
                Cambiado = true,
                Avisos = new List<string> { "El apunte es del 01/03/2026, fuera de las fechas del proyecto IMPE0210-2026-1." }
            }));
            vm.Apuntes.CambiarSeleccion(vm.Apuntes.Apuntes);
            FilaApunteProyecto fila = vm.Apuntes.Apuntes[0];
            vm.Apuntes.Filas[0].ProyectoId = 1;
            vm.Apuntes.Filas[0].Importe = 100M;

            await vm.Apuntes.GuardarRepartoAsync();

            A.CallTo(() => _servicio.GuardarImputaciones(10, A<PeticionImputacionesApunte>.That.Matches(p =>
                p.Empresa == "1" && p.ImporteApunte == 100M && p.Imputaciones.Count == 1 && p.Imputaciones[0].ProyectoId == 1 && p.Imputaciones[0].Importe == 100M)))
                .MustHaveHappenedOnceExactly();
            Assert.AreSame(fila, vm.Apuntes.Apuntes[0], "Se actualiza en su sitio (no se pierde la selección)");
            Assert.AreEqual(0M, fila.Pendiente);
            Assert.AreEqual("IMPE0210-2026-1 (100,00 €)", fila.ProyectosParaMostrar);
            StringAssert.Contains(vm.Apuntes.AvisosReparto, "fuera de las fechas");
            A.CallTo(() => _dialogos.ShowError(A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task GuardarReparto_ApunteCambiado_LoRecargaYLoDice()
        {
            var vm = await ConApuntes(Apunte(10, 100M));
            A.CallTo(() => _servicio.GuardarImputaciones(10, A<PeticionImputacionesApunte>._))
                .ThrowsAsync(new ProyectosApiException("El apunte 10 ha cambiado desde que lo leyó.", ProyectosService.CODIGO_APUNTE_CAMBIADO, HttpStatusCode.Conflict));
            A.CallTo(() => _servicio.LeerImputaciones("1", 10)).Returns(Task.FromResult(new ImputacionesApunteModel { Apunte = Apunte(10, 80M) }));
            vm.Apuntes.CambiarSeleccion(vm.Apuntes.Apuntes);
            vm.Apuntes.Filas[0].ProyectoId = 1;
            vm.Apuntes.Filas[0].Importe = 100M;

            await vm.Apuntes.GuardarRepartoAsync();

            Assert.AreEqual(80M, vm.Apuntes.Apuntes[0].ImporteApunte);
            A.CallTo(() => _dialogos.ShowError(A<string>.That.Matches(m => m.Contains("ha cambiado") && m.Contains("revise el reparto")))).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task ImputarSeleccionados_UnPutPorApunteEnteroYResumenDeLoQueFalla()
        {
            var vm = await ConApuntes(Apunte(10, 100M), Apunte(11, 50M, new ImputacionApunteModel { ProyectoId = 3, Importe = 50M, EstadoProyecto = "Cerrado" }));
            A.CallTo(() => _dialogos.ShowConfirmationAnswer(A<string>._, A<string>._, true)).Returns(true);
            A.CallTo(() => _servicio.GuardarImputaciones(10, A<PeticionImputacionesApunte>._)).Returns(Task.FromResult(new ImputacionesApunteModel { Apunte = Apunte(10, 100M) }));
            A.CallTo(() => _servicio.GuardarImputaciones(11, A<PeticionImputacionesApunte>._))
                .ThrowsAsync(new ProyectosApiException("El proyecto BF2025 está cerrado: su imputación de 50,00 € no se puede quitar.", "PROYECTO_CERRADO", HttpStatusCode.Conflict));
            vm.Apuntes.CambiarSeleccion(vm.Apuntes.Apuntes);
            Assert.IsFalse(vm.Apuntes.HayApunteEnReparto, "Con varios no hay panel de reparto");
            vm.Apuntes.ProyectoParaSeleccionados = vm.Apuntes.ProyectosAbiertos.Single(p => p.Id == 2);

            await vm.Apuntes.ImputarSeleccionadosAsync();

            A.CallTo(() => _servicio.GuardarImputaciones(10, A<PeticionImputacionesApunte>.That.Matches(p =>
                p.ImporteApunte == 100M && p.Imputaciones.Single().ProyectoId == 2 && p.Imputaciones.Single().Importe == 100M))).MustHaveHappenedOnceExactly();
            A.CallTo(() => _dialogos.ShowNotification(A<string>._, A<string>.That.Matches(m =>
                m.StartsWith("1 apunte imputado a FERIA-2026.") && m.Contains("No se ha podido imputar 1:") && m.Contains("Apunte 11")))).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void PreguntaImputarSeleccionados_AvisaDeLosQueYaTienenReparto()
        {
            var filas = new List<FilaApunteProyecto>
            {
                new FilaApunteProyecto(Apunte(1, 100M)),
                new FilaApunteProyecto(Apunte(2, 50M, new ImputacionApunteModel { ProyectoId = 1, Importe = 10M }))
            };
            string pregunta = ApuntesProyectosViewModel.PreguntaImputarSeleccionados(filas, new ProyectoModel { Codigo = "FERIA-2026", Nombre = "Feria" });
            StringAssert.StartsWith(pregunta, "¿Imputar los 2 apuntes elegidos enteros (150,00 €) a FERIA-2026 · Feria?");
            StringAssert.Contains(pregunta, "1 ya tiene reparto: se sustituye por este.");
        }

        [TestMethod]
        public async Task ErrorSinTabla_SeEnsenaTalCual()
        {
            A.CallTo(() => _servicio.Listar(A<string>._, A<bool>._, A<string?>._, A<string?>._))
                .ThrowsAsync(ProyectosService.Error(HttpStatusCode.ServiceUnavailable, "{\"Message\":\"Todavía no existen las tablas.\",\"Codigo\":\"SIN_TABLA\"}", "x"));
            var vm = CrearViewModel();

            await vm.BuscarAsync();

            A.CallTo(() => _dialogos.ShowError(A<string>.That.StartsWith("Los proyectos todavía no están activados en el servidor."))).MustHaveHappenedOnceExactly();
        }
    }
}
