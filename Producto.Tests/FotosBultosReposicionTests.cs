using FakeItEasy;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Models;
using Nesto.Infrastructure.Services;
using Nesto.Modules.Producto;
using Nesto.Modules.Producto.ViewModels;

namespace Producto.Tests
{
    /// <summary>NestoAPI#906: «Ver fotos de los bultos» en Diferencias de reposición y Recibir reposición.</summary>
    [TestClass]
    public class FotosBultosReposicionTests
    {
        private IServicioFotosBultosReposicion _servicio = null!;
        private List<string> _abiertos = null!;

        [TestInitialize]
        public void Inicializar()
        {
            _servicio = A.Fake<IServicioFotosBultosReposicion>();
            _abiertos = new List<string>();
            A.CallTo(() => _servicio.EnlaceFoto(A<string>._, A<int>._, A<int>._))
                .ReturnsLazily((string e, int t, int b) => Task.FromResult($"https://blob.example/{t}-{b}.jpg"));
        }

        private VisorFotosBultosReposicion Visor() => new VisorFotosBultosReposicion(_servicio, url => _abiertos.Add(url));

        [TestMethod]
        public async Task SinFotos_NoSaleElBoton()
        {
            A.CallTo(() => _servicio.BultosConFoto("1", 81463)).Returns(Task.FromResult(new List<int>()));
            var visor = Visor();

            await visor.ComprobarAsync("1", 81463);

            Assert.IsFalse(visor.HayFotos);
            Assert.IsFalse(visor.VerCommand.CanExecute(null));
        }

        [TestMethod]
        public async Task ConFotos_AbreLaDeCadaBultoEnElNavegador()
        {
            A.CallTo(() => _servicio.BultosConFoto("1", 81463)).Returns(Task.FromResult(new List<int> { 1, 3 }));
            var visor = Visor();

            await visor.ComprobarAsync("1", "81463");
            Assert.IsTrue(visor.HayFotos);
            await visor.VerAsync();

            CollectionAssert.AreEqual(new[] { "https://blob.example/81463-1.jpg", "https://blob.example/81463-3.jpg" }, _abiertos);
            Assert.AreEqual("Abiertas las fotos de los bultos 1 y 3 de la reposición 81463.", visor.Mensaje);
        }

        [TestMethod]
        public async Task UnaFotoQueNoSePuede_SeDiceYLasDemasSeAbren()
        {
            A.CallTo(() => _servicio.BultosConFoto("1", 81463)).Returns(Task.FromResult(new List<int> { 1, 2 }));
            A.CallTo(() => _servicio.EnlaceFoto("1", 81463, 2)).ThrowsAsync(new InvalidOperationException("No hay foto de ese bulto"));
            A.CallTo(() => _servicio.EnlaceFoto("1", 81463, 1)).Returns(Task.FromResult("file:///c:/malo.exe"));
            var visor = Visor();
            await visor.ComprobarAsync("1", 81463);

            await visor.VerAsync();

            Assert.AreEqual(0, _abiertos.Count, "Un enlace que no es web no se abre");
            Assert.AreEqual("No se ha podido abrir ninguna foto de la reposición 81463. No se ha podido: bulto 1: el servidor no ha dado un enlace válido; bulto 2: No hay foto de ese bulto.", visor.Mensaje);
        }

        [TestMethod]
        public async Task UnaComprobacionVieja_NoPisaLaNueva()
        {
            var lenta = new TaskCompletionSource<List<int>>();
            A.CallTo(() => _servicio.BultosConFoto("1", 81000)).Returns(lenta.Task);
            A.CallTo(() => _servicio.BultosConFoto("1", 81463)).Returns(Task.FromResult(new List<int>()));
            var visor = Visor();

            Task vieja = visor.ComprobarAsync("1", 81000);
            await visor.ComprobarAsync("1", 81463);
            lenta.SetResult(new List<int> { 1 });
            await vieja;

            Assert.IsFalse(visor.HayFotos);
        }

        [TestMethod]
        public async Task SinNumeroDeReposicionOSinServicio_NoPregunta()
        {
            var visor = Visor();
            await visor.ComprobarAsync("1", "COMP-12");
            Assert.IsFalse(visor.HayFotos);
            A.CallTo(() => _servicio.BultosConFoto(A<string>._, A<int>._)).MustNotHaveHappened();

            var sinServicio = new VisorFotosBultosReposicion(null);
            await sinServicio.ComprobarAsync("1", 81463);
            Assert.IsFalse(sinServicio.HayFotos);
        }

        [TestMethod]
        public void Urls()
        {
            Assert.AreEqual("Reposiciones/81463/Seguimiento?empresa=1", ServicioFotosBultosReposicion.UrlSeguimiento(null, 81463));
            Assert.AreEqual("Reposiciones/81463/Bultos/2/Foto?empresa=3&soloEnlace=true", ServicioFotosBultosReposicion.UrlFoto("3 ", 81463, 2));
        }

        [TestMethod]
        public void Diferencias_AlElegirUnaDiferencia_MiraSiSusBultosTienenFoto()
        {
            A.CallTo(() => _servicio.BultosConFoto("1", 81463)).Returns(Task.FromResult(new List<int> { 2 }));
            var vm = new DiferenciasReposicionViewModel(A.Fake<IServicioDiferenciasReposicion>(), A.Fake<IConfiguracion>(), A.Fake<IServicioDialogos>(),
                A.Fake<IServicioNavegacion>(), _servicio);

            vm.Seleccionada = new FilaDiferenciaReposicion(new DiferenciaReposicion { Id = 1, Empresa = "1", Traspaso = 81463, Producto = "17404" });

            Assert.IsTrue(vm.FotosBultos.HayFotos);
            vm.Seleccionada = null;
            Assert.IsFalse(vm.FotosBultos.HayFotos);
        }
    }
}
