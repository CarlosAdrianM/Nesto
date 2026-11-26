using FakeItEasy;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Models;
using Nesto.Infrastructure.Services;
using Nesto.Modules.Producto.ViewModels;

namespace Producto.Tests
{
    /// <summary>
    /// Sugerencia 1429: «Devolver exceso a Algete». Calcular es el ensayo (no crea nada); crear pregunta con las unidades y las
    /// referencias y lo hace de verdad; los errores de la API (400, 403, 409) se enseñan con su mensaje.
    /// </summary>
    [TestClass]
    public class ExcesoReposicionDialogViewModelTests
    {
        private IServicioEnvioReposiciones _servicio = null!;
        private IServicioDialogos _dialogos = null!;
        private ExcesoReposicionDialogViewModel _vm = null!;
        private readonly List<(PeticionExcesoReposicion Peticion, bool Ensayo)> _llamadas = new();

        private static ResultadoExcesoReposicion Calculo(bool ensayo) => new()
        {
            Ensayo = ensayo,
            Origen = "ALC",
            Destino = "ALG",
            Lineas = new List<LineaExcesoReposicion>
            {
                new() { Producto = "40001", Nombre = "ESMALTE ROJO", Stock = 12, Pendiente = 1, StockMaximo = 4, Exceso = 8 },
                new() { Producto = "40002", Nombre = "ESMALTE AZUL", Stock = 3, SinMaximo = true, Exceso = 3 }
            },
            NumeroLineas = 2,
            TotalUnidades = 11,
            LineasSinMaximo = 1,
            Mensaje = "Se mandarían 11 unidades de 2 referencias de Alcobendas a Algete.",
            Reposicion = ensayo ? null : new ReposicionEnPreparacion()
        };

        [TestInitialize]
        public void Preparar()
        {
            _servicio = A.Fake<IServicioEnvioReposiciones>();
            _dialogos = A.Fake<IServicioDialogos>();
            _llamadas.Clear();
            A.CallTo(() => _servicio.CalcularExceso(A<PeticionExcesoReposicion>._, A<bool>._))
                .ReturnsLazily((PeticionExcesoReposicion p, bool ensayo) =>
                {
                    _llamadas.Add((p, ensayo));
                    return Task.FromResult(Calculo(ensayo));
                });
            A.CallTo(() => _dialogos.ShowConfirmationAnswer(A<string>._, A<string>._, A<bool>._)).Returns(true);
            _vm = new ExcesoReposicionDialogViewModel(_servicio, _dialogos);
            _vm.OnDialogOpened(new ParametrosDialogo
            {
                { ExcesoReposicionDialogViewModel.PARAMETRO_EMPRESA, "1" },
                { ExcesoReposicionDialogViewModel.PARAMETRO_ORIGEN, "ALC" },
                { ExcesoReposicionDialogViewModel.PARAMETRO_DESTINO, "ALG" }
            });
        }

        [TestMethod]
        public void SinFiltro_NoSePuedeCalcular()
        {
            Assert.IsFalse(_vm.CalcularCommand.CanExecute(null));
            _vm.Grupo = "  ";
            Assert.IsFalse(_vm.CalcularCommand.CanExecute(null));
            _vm.Subgrupo = "ESM";
            Assert.IsTrue(_vm.CalcularCommand.CanExecute(null));
        }

        [TestMethod]
        public async Task Calcular_EsElEnsayo_YEnsenaLasLineasYLosTotales()
        {
            _vm.Familia = " Faby ";

            await _vm.CalcularAsync();

            Assert.AreEqual(1, _llamadas.Count);
            Assert.IsTrue(_llamadas[0].Ensayo);
            Assert.AreEqual("Faby", _llamadas[0].Peticion.Familia);
            Assert.AreEqual("ALC", _llamadas[0].Peticion.Origen);
            Assert.AreEqual("ALG", _llamadas[0].Peticion.Destino);
            Assert.IsNull(_llamadas[0].Peticion.Grupo);
            Assert.AreEqual(2, _vm.Lineas.Count);
            Assert.AreEqual(11, _vm.TotalUnidades);
            Assert.AreEqual(1, _vm.LineasSinMaximo);
            StringAssert.Contains(_vm.Mensaje, "11 unidades");
            Assert.IsTrue(_vm.CrearCommand.CanExecute(null));
        }

        [TestMethod]
        public async Task SinCalcular_O_TrasCambiarElFiltro_NoSePuedeCrear()
        {
            _vm.Familia = "Faby";
            Assert.IsFalse(_vm.CrearCommand.CanExecute(null));
            await _vm.CalcularAsync();

            _vm.Familia = "Otra";

            Assert.IsFalse(_vm.CrearCommand.CanExecute(null));
            Assert.AreEqual(0, _vm.Lineas.Count);
        }

        [TestMethod]
        public async Task Crear_PreguntaConUnidadesYReferencias_YLoHaceDeVerdad()
        {
            ResultadoDialogo? cerrado = null;
            _vm.RequestClose += r => cerrado = r;
            _vm.Familia = "Faby";
            await _vm.CalcularAsync();

            await _vm.CrearAsync();

            A.CallTo(() => _dialogos.ShowConfirmationAnswer(ExcesoReposicionDialogViewModel.TITULO_CONFIRMAR,
                A<string>.That.Matches(m => m.Contains("de ALC a ALG") && m.Contains("11 unidades") && m.Contains("2 referencias")), true))
                .MustHaveHappenedOnceExactly();
            Assert.IsFalse(_llamadas[1].Ensayo);
            Assert.AreEqual(ResultadoBoton.OK, cerrado!.Result);
            StringAssert.Contains(cerrado.Parameters.GetValue<string>(ExcesoReposicionDialogViewModel.PARAMETRO_MENSAJE), "11 unidades de 2 referencias");
        }

        [TestMethod]
        public async Task Crear_SiNoConfirma_NoCreaNada()
        {
            A.CallTo(() => _dialogos.ShowConfirmationAnswer(A<string>._, A<string>._, A<bool>._)).Returns(false);
            _vm.Familia = "Faby";
            await _vm.CalcularAsync();

            await _vm.CrearAsync();

            Assert.AreEqual(1, _llamadas.Count, "solo el ensayo");
        }

        [TestMethod]
        public async Task Crear_Con409_EnsenaElMotivoDeLaApiYNoCierra()
        {
            bool cerrado = false;
            _vm.RequestClose += _ => cerrado = true;
            _vm.Familia = "Faby";
            await _vm.CalcularAsync();
            const string motivo = "ALC ya tiene una reposición en preparación: termínela antes de crear otra.";
            A.CallTo(() => _servicio.CalcularExceso(A<PeticionExcesoReposicion>._, false)).Throws(new EnvioReposicionException(motivo, 409));

            await _vm.CrearAsync();

            Assert.AreEqual(motivo, _vm.Error);
            Assert.IsTrue(_vm.HayError);
            Assert.IsFalse(cerrado);
        }

        [TestMethod]
        public async Task Calcular_Con400_EnsenaElMotivoDeLaApi()
        {
            const string motivo = "Salen 812 productos: acote el filtro.";
            A.CallTo(() => _servicio.CalcularExceso(A<PeticionExcesoReposicion>._, true)).Throws(new EnvioReposicionException(motivo, 400));
            _vm.Familia = "F";

            await _vm.CalcularAsync();

            Assert.AreEqual(motivo, _vm.Error);
            Assert.IsFalse(_vm.CrearCommand.CanExecute(null));
        }

        [TestMethod]
        public void Url_DelEnsayoYDeVerdad()
        {
            Assert.AreEqual("Reposiciones/Exceso?ensayo=true", ServicioEnvioReposiciones.UrlExceso(true));
            Assert.AreEqual("Reposiciones/Exceso?ensayo=false", ServicioEnvioReposiciones.UrlExceso(false));
        }
    }
}
