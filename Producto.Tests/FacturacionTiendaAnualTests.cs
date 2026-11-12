using FakeItEasy;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Modules.Producto;
using Nesto.Modules.Producto.ViewModels;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

namespace Producto.Tests
{
    /// <summary>
    /// Nesto#559 (sugerencia 792 de Paloma, NestoAPI#719): «Facturado en tienda» con el acumulado del año y el comparativo con los
    /// años anteriores. Se ven en pantalla (GET api/Informes/FacturacionTienda?tipo=…) y su Excel (…/Excel, mismos parámetros) se
    /// guarda donde diga el usuario; el correo sigue siendo solo de la semana.
    /// </summary>
    [TestClass]
    public class FacturacionTiendaAnualTests
    {
        private static readonly DateTime MARTES = new DateTime(2026, 10, 27, 10, 0, 0);
        private IServicioFacturacionTienda _servicio = null!;
        private IConfiguracion _configuracion = null!;
        private IServicioDialogos _dialogos = null!;
        private IGuardadorFicheros _guardador = null!;

        [TestInitialize]
        public void Inicializar()
        {
            _servicio = A.Fake<IServicioFacturacionTienda>();
            _configuracion = A.Fake<IConfiguracion>();
            _dialogos = A.Fake<IServicioDialogos>();
            _guardador = A.Fake<IGuardadorFicheros>();
            A.CallTo(() => _servicio.LeerAnual(A<string>._, A<TipoInformeFacturacionTienda>._, A<DateTime>._, A<int>._))
                .ReturnsLazily((string tienda, TipoInformeFacturacionTienda tipo, DateTime hasta, int anios) =>
                    Task.FromResult(Informe(tipo, hasta, tipo == TipoInformeFacturacionTienda.Comparativo ? anios : 0)));
        }

        private FacturacionTiendaViewModel Vm(params string[] grupos)
        {
            foreach (string grupo in grupos)
            {
                A.CallTo(() => _configuracion.UsuarioEnGrupo(grupo)).Returns(true);
            }
            return new FacturacionTiendaViewModel(_servicio, _configuracion, _dialogos, () => MARTES, _guardador);
        }

        private static FacturacionTiendaAnual Informe(TipoInformeFacturacionTienda tipo, DateTime hasta, int anios)
        {
            List<int> anteriores = Enumerable.Range(1, anios).Select(i => hasta.Year - i).ToList();
            var meses = new List<MesFacturacionTienda>();
            decimal acumulado = 0;
            for (int mes = 1; mes <= hasta.Month; mes++)
            {
                acumulado += 100m * mes;
                DateTime fin = mes == hasta.Month ? hasta : new DateTime(hasta.Year, mes, 1).AddMonths(1).AddDays(-1);
                meses.Add(new MesFacturacionTienda
                {
                    Mes = mes,
                    NombreMes = new DateTime(2000, mes, 1).ToString("MMMM", new System.Globalization.CultureInfo("es-ES")),
                    Desde = new DateTime(hasta.Year, mes, 1),
                    Hasta = fin,
                    NumeroFacturas = mes,
                    BaseImponible = 100m * mes,
                    BaseImponibleCursos = 10m,
                    Acumulado = acumulado,
                    Anteriores = anteriores.Select(a => new ComparativoMesFacturacionTienda
                    {
                        Anio = a,
                        BaseImponible = 50m * mes,
                        VariacionImporte = 50m * mes,
                        VariacionPorcentaje = 100m
                    }).ToList()
                });
            }
            return new FacturacionTiendaAnual
            {
                Tipo = tipo.ToString(),
                Tienda = "REI",
                Anio = hasta.Year,
                Desde = new DateTime(hasta.Year, 1, 1),
                Hasta = hasta,
                AniosAnteriores = anteriores,
                Meses = meses,
                Total = new MesFacturacionTienda
                {
                    Mes = 0,
                    NombreMes = "Total",
                    NumeroFacturas = meses.Sum(m => m.NumeroFacturas),
                    BaseImponible = acumulado,
                    Acumulado = acumulado,
                    Anteriores = anteriores.Select(a => new ComparativoMesFacturacionTienda { Anio = a, BaseImponible = 0, VariacionImporte = acumulado, VariacionPorcentaje = null }).ToList()
                }
            };
        }

        // ---------------------------------------------------------------- tipos y parámetros

        [TestMethod]
        public void AlAbrir_EsLaSemana_ComoHastaAhora()
        {
            var vm = Vm(Constantes.GruposSeguridad.TIENDAS);

            Assert.AreEqual(TipoInformeFacturacionTienda.Semana, vm.TipoInforme);
            CollectionAssert.AreEqual(new[] { TipoInformeFacturacionTienda.Semana, TipoInformeFacturacionTienda.Acumulado, TipoInformeFacturacionTienda.Comparativo },
                vm.Tipos.Select(t => t.Tipo).ToArray());
            Assert.IsTrue(vm.MandarCommand.CanExecute(null));
            Assert.IsFalse(vm.VerCommand.CanExecute(null), "La semana no se ve en pantalla: se manda por correo");
            Assert.AreEqual(2, vm.Anios);
        }

        [TestMethod]
        public void ElegirAcumulado_HastaHoy_SinCorreoYConVer()
        {
            var vm = Vm(Constantes.GruposSeguridad.TIENDAS);

            vm.Tipo = vm.Tipos.Single(t => t.Tipo == TipoInformeFacturacionTienda.Acumulado);

            Assert.AreEqual(MARTES.Date, vm.Hasta);
            Assert.IsTrue(vm.EsAnual);
            Assert.IsFalse(vm.EsComparativo);
            Assert.IsFalse(vm.MandarCommand.CanExecute(null), "El correo es solo de la semana");
            Assert.IsTrue(vm.VerCommand.CanExecute(null));
            Assert.IsTrue(vm.GuardarExcelCommand.CanExecute(null));
        }

        [TestMethod]
        public void VolverASemana_PoneLaSemanaAnterior()
        {
            var vm = Vm(Constantes.GruposSeguridad.TIENDAS);
            vm.Tipo = vm.Tipos.Single(t => t.Tipo == TipoInformeFacturacionTienda.Comparativo);

            vm.Tipo = vm.Tipos.Single(t => t.Tipo == TipoInformeFacturacionTienda.Semana);

            Assert.AreEqual(new DateTime(2026, 10, 19), vm.Desde);
            Assert.AreEqual(new DateTime(2026, 10, 25), vm.Hasta);
        }

        [TestMethod]
        public async Task Ver_Acumulado_PideTipoHastaYTienda_YEnsenaLosMesesYElTotal()
        {
            A.CallTo(() => _configuracion.leerParametro(A<string>._, A<string>._)).Returns(Task.FromResult("REI"));
            var vm = Vm(Constantes.GruposSeguridad.DIRECCION);
            await vm.LeerTiendaDelUsuarioAsync();
            vm.Tipo = vm.Tipos.Single(t => t.Tipo == TipoInformeFacturacionTienda.Acumulado);
            vm.Hasta = new DateTime(2026, 3, 15);

            await vm.VerAsync();

            A.CallTo(() => _servicio.LeerAnual("REI", TipoInformeFacturacionTienda.Acumulado, new DateTime(2026, 3, 15), 2)).MustHaveHappenedOnceExactly();
            Assert.AreEqual(4, vm.Filas.Count, "Enero, febrero, marzo y el total");
            Assert.AreEqual("marzo (hasta el 15)", vm.Filas[2].Mes, "El mes a medias, como en el Excel");
            Assert.AreEqual("enero", vm.Filas[0].Mes);
            Assert.IsTrue(vm.Filas[3].EsTotal);
            Assert.AreEqual("Total", vm.Filas[3].Mes);
            Assert.AreEqual(600m, vm.Filas[3].Acumulado);
            Assert.AreEqual(0, vm.AniosAnteriores.Count);
            Assert.IsTrue(vm.HayResultado);
            StringAssert.Contains(vm.Resumen, "REI");
            StringAssert.Contains(vm.Resumen, "600");
            Assert.IsFalse(vm.EstaOcupado);
        }

        [TestMethod]
        public async Task Ver_Comparativo_PideLosAnios_YCadaFilaLlevaSusAnteriores()
        {
            var vm = Vm(Constantes.GruposSeguridad.TIENDAS);
            vm.Tipo = vm.Tipos.Single(t => t.Tipo == TipoInformeFacturacionTienda.Comparativo);
            vm.Anios = 3;

            await vm.VerAsync();

            A.CallTo(() => _servicio.LeerAnual(null, TipoInformeFacturacionTienda.Comparativo, MARTES.Date, 3)).MustHaveHappenedOnceExactly();
            CollectionAssert.AreEqual(new[] { 2025, 2024, 2023 }, vm.AniosAnteriores.ToArray());
            Assert.AreEqual(11, vm.Filas.Count, "De enero a octubre y el total");
            Assert.IsTrue(vm.Filas.All(f => f.Anteriores.Select(a => a.Anio).SequenceEqual(new[] { 2025, 2024, 2023 })));
            Assert.AreEqual(500m, vm.Filas[9].Anteriores[0].BaseImponible);
            Assert.IsNull(vm.Filas[10].Anteriores[0].VariacionPorcentaje, "Sin base del año anterior no hay porcentaje");
        }

        [TestMethod]
        public async Task CambiarLosParametros_BorraLoQueSeVeia()
        {
            var vm = Vm(Constantes.GruposSeguridad.TIENDAS);
            vm.Tipo = vm.Tipos.Single(t => t.Tipo == TipoInformeFacturacionTienda.Comparativo);
            await vm.VerAsync();
            Assert.IsTrue(vm.HayResultado);

            vm.Anios = 1;

            Assert.IsFalse(vm.HayResultado, "Lo de la tabla no es ya lo que dice la pantalla");
            Assert.AreEqual(0, vm.AniosAnteriores.Count);
        }

        [TestMethod]
        public async Task Ver_LaApiDice400_EnsenaSuMensajeTalCual()
        {
            A.CallTo(() => _servicio.LeerAnual(A<string>._, A<TipoInformeFacturacionTienda>._, A<DateTime>._, A<int>._))
                .ThrowsAsync(new FacturacionTiendaException("Los años a comparar van de 1 a 10.", 400));
            var vm = Vm(Constantes.GruposSeguridad.TIENDAS);
            vm.Tipo = vm.Tipos.Single(t => t.Tipo == TipoInformeFacturacionTienda.Comparativo);

            await vm.VerAsync();

            Assert.AreEqual("Los años a comparar van de 1 a 10.", vm.Mensaje);
            A.CallTo(() => _dialogos.ShowError("Los años a comparar van de 1 a 10.")).MustHaveHappenedOnceExactly();
            Assert.IsFalse(vm.HayResultado);
            Assert.IsFalse(vm.EstaOcupado);
        }

        [TestMethod]
        public async Task Ver_LaApiDice403_EnsenaSuMensajeTalCual()
        {
            A.CallTo(() => _servicio.LeerAnual(A<string>._, A<TipoInformeFacturacionTienda>._, A<DateTime>._, A<int>._))
                .ThrowsAsync(new FacturacionTiendaException("Cada tienda solo puede ver lo facturado en su almacén.", 403));
            var vm = Vm(Constantes.GruposSeguridad.TIENDAS);
            vm.Tipo = vm.Tipos.Single(t => t.Tipo == TipoInformeFacturacionTienda.Acumulado);

            await vm.VerAsync();

            Assert.AreEqual("Cada tienda solo puede ver lo facturado en su almacén.", vm.Mensaje);
        }

        // ---------------------------------------------------------------- el Excel

        [TestMethod]
        public async Task GuardarExcel_Comparativo_PideLosMismosParametros_YLoGuardaDondeDiga()
        {
            byte[] contenido = { 1, 2, 3 };
            A.CallTo(() => _servicio.DescargarExcel(A<string>._, A<TipoInformeFacturacionTienda>._, A<DateTime>._, A<DateTime>._, A<int>._))
                .Returns(Task.FromResult(new ExcelFacturacionTienda { Contenido = contenido, NombreFichero = "FacturacionTienda_REI_Comparativo_20261027.xlsx" }));
            A.CallTo(() => _guardador.ElegirDondeGuardar(A<string>._, A<string>._, A<string>._, A<string>._)).Returns(@"C:\Temp\x.xlsx");
            var vm = Vm(Constantes.GruposSeguridad.TIENDAS);
            vm.Tipo = vm.Tipos.Single(t => t.Tipo == TipoInformeFacturacionTienda.Comparativo);
            vm.Anios = 4;

            await vm.GuardarExcelAsync();

            A.CallTo(() => _servicio.DescargarExcel(null, TipoInformeFacturacionTienda.Comparativo, A<DateTime>._, MARTES.Date, 4)).MustHaveHappenedOnceExactly();
            A.CallTo(() => _guardador.ElegirDondeGuardar(A<string>._, "FacturacionTienda_REI_Comparativo_20261027.xlsx", A<string>._, ".xlsx")).MustHaveHappenedOnceExactly();
            A.CallTo(() => _guardador.Guardar(@"C:\Temp\x.xlsx", contenido)).MustHaveHappenedOnceExactly();
            StringAssert.Contains(vm.Mensaje, @"C:\Temp\x.xlsx");
        }

        [TestMethod]
        public async Task GuardarExcel_Semana_LlevaDesdeYHasta()
        {
            A.CallTo(() => _servicio.DescargarExcel(A<string>._, A<TipoInformeFacturacionTienda>._, A<DateTime>._, A<DateTime>._, A<int>._))
                .Returns(Task.FromResult(new ExcelFacturacionTienda { Contenido = new byte[] { 1 } }));
            A.CallTo(() => _guardador.ElegirDondeGuardar(A<string>._, A<string>._, A<string>._, A<string>._)).Returns(@"C:\Temp\s.xlsx");
            var vm = Vm(Constantes.GruposSeguridad.TIENDAS);

            await vm.GuardarExcelAsync();

            A.CallTo(() => _servicio.DescargarExcel(null, TipoInformeFacturacionTienda.Semana, new DateTime(2026, 10, 19), new DateTime(2026, 10, 25), A<int>._))
                .MustHaveHappenedOnceExactly();
            A.CallTo(() => _guardador.ElegirDondeGuardar(A<string>._, "FacturacionTienda_Tienda_Semana_20261025.xlsx", A<string>._, A<string>._))
                .MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task GuardarExcel_SiCancela_NoGuardaNada()
        {
            A.CallTo(() => _servicio.DescargarExcel(A<string>._, A<TipoInformeFacturacionTienda>._, A<DateTime>._, A<DateTime>._, A<int>._))
                .Returns(Task.FromResult(new ExcelFacturacionTienda { Contenido = new byte[] { 1 } }));
            A.CallTo(() => _guardador.ElegirDondeGuardar(A<string>._, A<string>._, A<string>._, A<string>._)).Returns(null!);
            var vm = Vm(Constantes.GruposSeguridad.TIENDAS);

            await vm.GuardarExcelAsync();

            A.CallTo(() => _guardador.Guardar(A<string>._, A<byte[]>._)).MustNotHaveHappened();
            Assert.IsNull(vm.Mensaje);
            Assert.IsFalse(vm.EstaOcupado);
        }

        [TestMethod]
        public async Task GuardarExcel_LaApiDiceQueNo_EnsenaSuMensajeYNoPreguntaDonde()
        {
            A.CallTo(() => _servicio.DescargarExcel(A<string>._, A<TipoInformeFacturacionTienda>._, A<DateTime>._, A<DateTime>._, A<int>._))
                .ThrowsAsync(new FacturacionTiendaException("«XYZ» no es una tienda: tiene que ser REI o ALC.", 400));
            var vm = Vm(Constantes.GruposSeguridad.TIENDAS);

            await vm.GuardarExcelAsync();

            Assert.AreEqual("«XYZ» no es una tienda: tiene que ser REI o ALC.", vm.Mensaje);
            A.CallTo(() => _guardador.ElegirDondeGuardar(A<string>._, A<string>._, A<string>._, A<string>._)).MustNotHaveHappened();
        }

        // ---------------------------------------------------------------- el servicio

        private sealed class HandlerFalso : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
            public List<string> Urls { get; } = new();

            public HandlerFalso(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Urls.Add(request.Method.Method + " " + request.RequestUri!.PathAndQuery);
                return Task.FromResult(_responder(request));
            }
        }

        private static ServicioFacturacionTienda Servicio(HandlerFalso handler)
        {
            var factoria = A.Fake<IClienteApiFactory>();
            A.CallTo(() => factoria.Crear()).ReturnsLazily(() =>
                new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("http://api.local/api/") });
            return new ServicioFacturacionTienda(factoria);
        }

        [TestMethod]
        public void Consulta_SegunElTipo()
        {
            DateTime desde = new DateTime(2026, 10, 19), hasta = new DateTime(2026, 10, 25);

            Assert.AreEqual("?tipo=Semana&tienda=ALC&desde=2026-10-19&hasta=2026-10-25",
                ServicioFacturacionTienda.Consulta("ALC ", TipoInformeFacturacionTienda.Semana, desde, hasta, 2));
            Assert.AreEqual("?tipo=Acumulado&hasta=2026-10-25",
                ServicioFacturacionTienda.Consulta(null, TipoInformeFacturacionTienda.Acumulado, desde, hasta, 2));
            Assert.AreEqual("?tipo=Comparativo&tienda=REI&hasta=2026-10-25&anios=3",
                ServicioFacturacionTienda.Consulta("REI", TipoInformeFacturacionTienda.Comparativo, desde, hasta, 3));
        }

        [TestMethod]
        public async Task Servicio_LeerAnual_GetYLeeElJson()
        {
            var handler = new HandlerFalso(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"Tipo\":\"Comparativo\",\"Tienda\":\"REI\",\"Anio\":2026,\"AniosAnteriores\":[2025],\"Meses\":[{\"Mes\":1,\"NombreMes\":\"Enero\",\"BaseImponible\":10.5,\"Anteriores\":[{\"Anio\":2025,\"BaseImponible\":5,\"VariacionImporte\":5.5,\"VariacionPorcentaje\":110.0}]}],\"Total\":{\"Mes\":0,\"NombreMes\":\"Total\",\"BaseImponible\":10.5}}",
                    Encoding.UTF8, "application/json")
            });

            FacturacionTiendaAnual informe = await Servicio(handler).LeerAnual("REI", TipoInformeFacturacionTienda.Comparativo, new DateTime(2026, 11, 12), 1);

            Assert.AreEqual("GET /api/Informes/FacturacionTienda?tipo=Comparativo&tienda=REI&hasta=2026-11-12&anios=1", handler.Urls.Single());
            Assert.AreEqual(2025, informe.AniosAnteriores.Single());
            Assert.AreEqual(110m, informe.Meses[0].Anteriores[0].VariacionPorcentaje);
            Assert.AreEqual("Total", informe.Total.NombreMes);
        }

        [TestMethod]
        public async Task Servicio_DescargarExcel_TraeLosBytesYElNombre()
        {
            var handler = new HandlerFalso(_ =>
            {
                var contenido = new ByteArrayContent(new byte[] { 80, 75 });
                contenido.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment") { FileName = "FacturacionTienda_ALC_Acumulado_20261112.xlsx" };
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = contenido };
            });

            ExcelFacturacionTienda excel = await Servicio(handler)
                .DescargarExcel("ALC", TipoInformeFacturacionTienda.Acumulado, new DateTime(2026, 1, 1), new DateTime(2026, 11, 12), 2);

            Assert.AreEqual("GET /api/Informes/FacturacionTienda/Excel?tipo=Acumulado&tienda=ALC&hasta=2026-11-12", handler.Urls.Single());
            CollectionAssert.AreEqual(new byte[] { 80, 75 }, excel.Contenido);
            Assert.AreEqual("FacturacionTienda_ALC_Acumulado_20261112.xlsx", excel.NombreFichero);
        }

        [TestMethod]
        public async Task Servicio_DescargarExcel_400EnTexto_LanzaConElMensajeDeLaApi()
        {
            var handler = new HandlerFalso(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("«Mensual» no es un tipo del informe: tiene que ser Semana, Acumulado o Comparativo.")
            });

            FacturacionTiendaException ex = await Assert.ThrowsExceptionAsync<FacturacionTiendaException>(() => Servicio(handler)
                .DescargarExcel(null, TipoInformeFacturacionTienda.Acumulado, DateTime.Today, DateTime.Today, 2));

            Assert.AreEqual(400, ex.Codigo);
            Assert.AreEqual("«Mensual» no es un tipo del informe: tiene que ser Semana, Acumulado o Comparativo.", ex.Message);
        }
    }
}
