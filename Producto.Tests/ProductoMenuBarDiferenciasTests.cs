using FakeItEasy;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Models;
using Nesto.Infrastructure.Services;
using Nesto.Infrastructure.Shared;
using Nesto.Modules.Producto;

namespace Producto.Tests
{
    /// <summary>
    /// Nesto#526 (NestoAPI#634): el botón «Diferencias» del grupo Reposición abre la ventana y dice cuántas quedan por
    /// comprobar («Diferencias (2)»); el contador se pone al día al resolver una.
    /// </summary>
    [TestClass]
    public class ProductoMenuBarDiferenciasTests
    {
        private IServicioNavegacion _navegacion = null!;
        private IConfiguracion _configuracion = null!;
        private IServicioDiferenciasReposicion _diferencias = null!;
        private int _pendientes;

        [TestInitialize]
        public void Inicializar()
        {
            _navegacion = A.Fake<IServicioNavegacion>();
            _configuracion = A.Fake<IConfiguracion>();
            _diferencias = A.Fake<IServicioDiferenciasReposicion>();
            _pendientes = 2;
            A.CallTo(() => _diferencias.ContarPendientes(A<string>._, A<string>._))
                .ReturnsLazily(() => Task.FromResult(new ContadorDiferenciasReposicion { Pendientes = _pendientes }));
        }

        private ProductoMenuBarViewModel Menu(string grupo)
        {
            A.CallTo(() => _configuracion.UsuarioEnGrupo(grupo)).Returns(true);
            return new ProductoMenuBarViewModel(_navegacion, _configuracion, _diferencias);
        }

        [TestMethod]
        public void Abrir_AbreLaVentanaDeDiferencias()
        {
            ProductoMenuBarViewModel menu = Menu(Constantes.GruposSeguridad.TIENDAS);

            Assert.IsTrue(menu.AbrirDiferenciasReposicionCommand.CanExecute(null));
            menu.AbrirDiferenciasReposicionCommand.Execute(null);

            A.CallTo(() => _navegacion.RequestNavigate("MainRegion", "DiferenciasReposicionView")).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void ConPendientes_LaEtiquetaLasCuenta()
        {
            ProductoMenuBarViewModel menu = Menu(Constantes.GruposSeguridad.ALMACEN);

            Assert.AreEqual(2, menu.DiferenciasPendientes);
            Assert.AreEqual("Diferencias (2)", menu.EtiquetaDiferencias);
            Assert.AreEqual("2 diferencias de reposición pendientes de comprobar", menu.AyudaDiferencias);
            A.CallTo(() => _diferencias.ContarPendientes("1", null)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void AlResolverUna_SeVuelveACargarElContador()
        {
            ProductoMenuBarViewModel menu = Menu(Constantes.GruposSeguridad.DIRECCION);
            _pendientes = 0;

            _diferencias.DiferenciasCambiadas += Raise.WithEmpty();

            Assert.AreEqual(0, menu.DiferenciasPendientes);
            Assert.AreEqual("Diferencias", menu.EtiquetaDiferencias);
        }

        [TestMethod]
        public void SiElContadorFalla_SeQuedaSinNumeroYNoRevienta()
        {
            A.CallTo(() => _diferencias.ContarPendientes(A<string>._, A<string>._))
                .ThrowsAsync(new DiferenciasReposicionException("Sin permiso", 403));

            ProductoMenuBarViewModel menu = Menu(Constantes.GruposSeguridad.TIENDAS);

            Assert.AreEqual(0, menu.DiferenciasPendientes);
            Assert.AreEqual("Diferencias", menu.EtiquetaDiferencias);
        }

        [TestMethod]
        public void QuienNoRecibeReposiciones_NiVeElBotonNiPideElContador()
        {
            var menu = new ProductoMenuBarViewModel(_navegacion, _configuracion, _diferencias);

            Assert.IsFalse(menu.AbrirDiferenciasReposicionCommand.CanExecute(null));
            A.CallTo(() => _diferencias.ContarPendientes(A<string>._, A<string>._)).MustNotHaveHappened();
        }
    }
}
