using FakeItEasy;
using Nesto.Modules.Producto;
using Nesto.Modules.Producto.ViewModels;

namespace Producto.Tests
{
    /// <summary>NestoAPI#913 (corte 1): las pestañas «Historia del aparato» y «Descuadres» de la ventana de Alquileres.</summary>
    [TestClass]
    public class AparatosViewModelsTests
    {
        private IServicioAparatos _servicio = null!;

        [TestInitialize]
        public void Inicializar()
        {
            _servicio = A.Fake<IServicioAparatos>();
        }

        private static HistoriaAparato Historia() => new HistoriaAparato
        {
            Producto = "26781",
            Nombre = "LÁSER SHE ",
            NumeroSerie = "SHE-2",
            Situacion = "Vendido",
            TextoSituacion = "Vendido al cliente 22709 (NOMBRE) el 17/11/2026, pedido 930489.",
            Registro = new RegistroAparato { Numero = 1934, SituacionSegunRegistro = "EnStock" },
            OtrosRegistros = new List<int> { 1940, 1951 },
            Movimientos = new List<MovimientoAparato>
            {
                new MovimientoAparato { Fecha = new DateTime(2026, 3, 2), Tipo = "Compra", Documento = "Pedido de compra 220931", Tercero = "P123", NombreTercero = "PROVEEDOR" },
                new MovimientoAparato { Fecha = new DateTime(2026, 11, 17), Tipo = "Venta", Documento = "Pedido 930489", Tercero = "22709" }
            }
        };

        [TestMethod]
        public async Task Historia_EnsenaLaSituacionCalculadaYLosMovimientos()
        {
            A.CallTo(() => _servicio.LeerHistoria("26781", "SHE-2")).Returns(Task.FromResult(Historia()));
            var vm = new HistoriaAparatoViewModel(_servicio);

            await vm.CargarAsync(" 26781", "SHE-2 ");

            Assert.AreEqual("Vendido", vm.Situacion);
            StringAssert.StartsWith(vm.TextoSituacion, "Vendido al cliente 22709");
            Assert.AreEqual("Registro 1934: según el registro, en stock. La serie está también en los registros 1940 y 1951.", vm.TextoRegistro);
            Assert.AreEqual(2, vm.Movimientos.Count);
            Assert.AreEqual("Compra", vm.Movimientos[0].Tipo);
            Assert.AreEqual("P123 PROVEEDOR", vm.Movimientos[0].Tercero);
            Assert.IsNull(vm.Aviso);
        }

        [TestMethod]
        public async Task Historia_SinNumeroDeSerie_NoLlamaYLoDice()
        {
            var vm = new HistoriaAparatoViewModel(_servicio);

            await vm.CargarAsync("26781", "  ");

            Assert.AreEqual(HistoriaAparatoViewModel.MENSAJE_SIN_SERIE, vm.Aviso);
            A.CallTo(() => _servicio.LeerHistoria(A<string>._, A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task Historia_SiLaApiDa404_EnsenaSuMensaje()
        {
            A.CallTo(() => _servicio.LeerHistoria(A<string>._, A<string>._))
                .ThrowsAsync(new AparatosApiException("No hay rastro del 26781 con nº de serie X.", System.Net.HttpStatusCode.NotFound));
            var vm = new HistoriaAparatoViewModel(_servicio);

            await vm.CargarAsync("26781", "X");

            Assert.AreEqual("No se ha podido leer la historia del 26781 con nº de serie X: No hay rastro del 26781 con nº de serie X.", vm.Aviso);
            Assert.AreEqual(0, vm.Movimientos.Count);
        }

        [TestMethod]
        public async Task Historia_UnaRespuestaVieja_NoPisaLaNueva()
        {
            var lenta = new TaskCompletionSource<HistoriaAparato>();
            A.CallTo(() => _servicio.LeerHistoria("26781", "SHE-1")).Returns(lenta.Task);
            A.CallTo(() => _servicio.LeerHistoria("26781", "SHE-2")).Returns(Task.FromResult(Historia()));
            var vm = new HistoriaAparatoViewModel(_servicio);

            Task primera = vm.CargarAsync("26781", "SHE-1");
            await vm.CargarAsync("26781", "SHE-2");
            lenta.SetResult(new HistoriaAparato { Situacion = "EnStock", Movimientos = new List<MovimientoAparato>() });
            await primera;

            Assert.AreEqual("SHE-2", vm.NumeroSerie);
            Assert.AreEqual("Vendido", vm.Situacion);
        }

        private static InformeDescuadresAparatos Informe() => new InformeDescuadresAparatos
        {
            TotalDescuadres = 3,
            Descuadres = new List<DescuadreAparato>
            {
                new DescuadreAparato { Producto = "26781", Nombre = "LÁSER SHE", NumeroSerie = "SHE-2", Motivo = "RegistroEnStockPeroVendido",
                    TextoMotivo = "En stock según el registro, pero vendido", Motivos = new List<string> { "RegistroEnStockPeroVendido" },
                    SituacionSegunRegistro = "EnStock", SituacionCalculada = "Vendido", Registros = new List<int> { 1934 } },
                new DescuadreAparato { Producto = "26781", Nombre = "LÁSER SHE", NumeroSerie = "SHE-9", Motivo = "VendidoSinRegistro",
                    TextoMotivo = "Vendido sin registro", Motivos = new List<string> { "VendidoSinRegistro", "SerieEscritaDeDosFormas" },
                    SituacionCalculada = "Vendido" },
                new DescuadreAparato { Producto = "40010", Nombre = "RADIOFRECUENCIA", NumeroSerie = "RF-1", Motivo = "CompradoSinRegistro",
                    TextoMotivo = "Comprado sin registro", Motivos = new List<string> { "CompradoSinRegistro" }, SituacionCalculada = "EnStock" }
            },
            Productos = new List<ResumenProductoAparatos>
            {
                new ResumenProductoAparatos { Producto = "26781", StockContableTotal = 2, EnStockSegunRegistro = 3, EnStockSegunMovimientos = 2, Diferencia = 1 }
            }
        };

        [TestMethod]
        public async Task Descuadres_SeFiltranPorProductoYPorMotivo()
        {
            A.CallTo(() => _servicio.LeerDescuadres(false)).Returns(Task.FromResult(Informe()));
            var vm = new DescuadresAparatosViewModel(_servicio);

            await vm.CargarAsync();

            Assert.AreEqual(3, vm.Descuadres.Count);
            Assert.AreEqual(1, vm.Productos.Count);
            Assert.AreEqual("(Todos)", vm.MotivoElegido.Texto);
            Assert.AreEqual(4, vm.Motivos.Count, "(Todos) y los tres motivos principales");
            Assert.AreEqual("(sin registro)", vm.Descuadres[1].SituacionSegunRegistro);

            vm.FiltroProducto = "radio";
            Assert.AreEqual("40010", vm.Descuadres.Single().Producto);

            vm.FiltroProducto = null;
            vm.MotivoElegido = vm.Motivos.Single(m => m.Codigo == "VendidoSinRegistro");
            Assert.AreEqual("SHE-9", vm.Descuadres.Single().NumeroSerie);
            Assert.AreEqual("1 de 3 descuadres.", vm.Resumen);
        }

        [TestMethod]
        public void Descuadres_ElFiltroDeMotivoMiraTambienLosSecundarios()
        {
            var fila = new FilaDescuadreAparato(Informe().Descuadres[1]);

            Assert.IsTrue(DescuadresAparatosViewModel.PasaFiltro(fila, null, "SerieEscritaDeDosFormas"));
            Assert.IsFalse(DescuadresAparatosViewModel.PasaFiltro(fila, null, "CompradoSinRegistro"));
            Assert.IsTrue(DescuadresAparatosViewModel.PasaFiltro(fila, "she", null), "También por el nombre");
        }

        [TestMethod]
        public async Task Descuadres_LaCasillaDeLaListaDeAlquileres_VuelveALeer()
        {
            A.CallTo(() => _servicio.LeerDescuadres(A<bool>._)).Returns(Task.FromResult(Informe()));
            var vm = new DescuadresAparatosViewModel(_servicio);
            await vm.CargarAsync();

            vm.SoloListaAlquileres = true;

            A.CallTo(() => _servicio.LeerDescuadres(true)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task Descuadres_DobleClic_SoloVaAlRegistroSiLoTiene()
        {
            A.CallTo(() => _servicio.LeerDescuadres(false)).Returns(Task.FromResult(Informe()));
            var vm = new DescuadresAparatosViewModel(_servicio);
            await vm.CargarAsync();
            var pedidos = new List<string>();
            vm.IrAlRegistro = (producto, serie) => pedidos.Add($"{producto}|{serie}");

            Assert.IsFalse(vm.IrAlRegistroCommand.CanExecute(vm.Descuadres[1]), "Sin registro no hay adónde ir");
            vm.IrAlRegistroCommand.Execute(vm.Descuadres[0]);

            CollectionAssert.AreEqual(new[] { "26781|SHE-2" }, pedidos);
        }

        [TestMethod]
        public async Task Descuadres_SiLaApiFalla_SeDice()
        {
            A.CallTo(() => _servicio.LeerDescuadres(A<bool>._)).ThrowsAsync(new AparatosApiException("No tiene permiso para ver los aparatos."));
            var vm = new DescuadresAparatosViewModel(_servicio);

            await vm.CargarAsync();

            Assert.AreEqual("No se han podido leer los descuadres: No tiene permiso para ver los aparatos.", vm.Aviso);
            Assert.AreEqual(0, vm.Descuadres.Count);
        }

        [TestMethod]
        public void Urls_LlevanLosParametrosEscapados()
        {
            Assert.AreEqual("Aparatos/Historia?producto=26781&numeroSerie=SHE%20RFS%209", ServicioAparatos.UrlHistoria(" 26781 ", "SHE RFS 9"));
            Assert.AreEqual("Aparatos/Descuadres?soloListaAlquileres=true", ServicioAparatos.UrlDescuadres(true));
        }
    }
}
