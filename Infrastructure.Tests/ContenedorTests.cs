using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Composicion;
using Nesto.Infrastructure.Contracts;
using System;

namespace Nesto.Infrastructure.Tests
{
    /// <summary>
    /// Nesto#490 (4C.5, tramo 2): <see cref="Contenedor.Actual"/> sustituye a <c>ContainerLocator.Container</c> de Prism.
    /// Desde el tramo 4 lo inicializa el arranque (Application.OnStartup) con el resolutor del contenedor de
    /// Microsoft.Extensions.DependencyInjection (<see cref="ContenedorMicrosoft"/>); los tests lo sustituyen.
    /// </summary>
    [TestClass]
    public class ContenedorTests
    {
        public interface IServicio
        {
        }

        public class Servicio : IServicio
        {
        }

        [TestInitialize]
        public void Inicializar() => Contenedor.Restablecer();

        [TestCleanup]
        public void Limpiar()
        {
            Contenedor.Restablecer();
        }

        [TestMethod]
        public void SinInicializar_EsNull_ComoEraContainerLocator()
        {
            // El diseñador de XAML y los tests cuentan con esto: los selectores y behaviors miran si es null
            Assert.IsNull(Contenedor.Actual);
        }

        [TestMethod]
        public void InicializadoComoEnElArranque_ResuelveLoMismoQueElContenedor()
        {
            var contenedor = new ContenedorMicrosoft();
            contenedor.Singleton<IServicio, Servicio>();
            ResolutorMicrosoft resolutor = contenedor.Construir();

            // Lo mismo que hace Application.OnStartup (Nesto#490, 4C.5, tramo 4)
            Contenedor.Establecer(resolutor);

            IServicio desdeNesto = Contenedor.Actual.Resolver<IServicio>();
            Assert.IsInstanceOfType(desdeNesto, typeof(Servicio));
            Assert.AreSame(resolutor.Resolver<IServicio>(), desdeNesto, "Tiene que ser el mismo contenedor que el de la aplicación");
            Assert.AreSame(desdeNesto, Contenedor.Actual.Resolver(typeof(IServicio)));
        }

        [TestMethod]
        public void InicializadoComoEnElArranque_ConcretoSinRegistrar_SeConstruyeComoConContainerLocator()
        {
            Contenedor.Establecer(new ContenedorMicrosoft().Construir());

            // Los selectores (SelectorClienteViewModel, SelectorProveedorViewModel...) se sacaban así de ContainerLocator;
            // ResolutorMicrosoft construye, como Unity, la clase concreta sin registrar
            Assert.IsInstanceOfType(Contenedor.Actual.Resolver<Servicio>(), typeof(Servicio));
        }

        [TestMethod]
        public void Establecer_ConNull_Lanza()
        {
            _ = Assert.ThrowsException<ArgumentNullException>(() => Contenedor.Establecer(null));
        }

        [TestMethod]
        public void Sustituir_PoneElFalso_YAlLiberarDejaElAnterior()
        {
            IResolutorNesto deLaAplicacion = A.Fake<IResolutorNesto>();
            Contenedor.Establecer(deLaAplicacion);
            IResolutorNesto falso = A.Fake<IResolutorNesto>();
            var servicio = new Servicio();
            _ = A.CallTo(() => falso.Resolver<IServicio>()).Returns(servicio);

            using (Contenedor.Sustituir(falso))
            {
                Assert.AreSame(falso, Contenedor.Actual);
                Assert.AreSame(servicio, Contenedor.Actual.Resolver<IServicio>());
            }

            Assert.AreSame(deLaAplicacion, Contenedor.Actual);
        }

        [TestMethod]
        public void Sustituir_ConNull_SimulaQueNoHayContenedor()
        {
            Contenedor.Establecer(A.Fake<IResolutorNesto>());

            using (Contenedor.Sustituir(null))
            {
                Assert.IsNull(Contenedor.Actual);
            }

            Assert.IsNotNull(Contenedor.Actual);
        }

        [TestMethod]
        public void Sustituir_LiberarDosVeces_NoPisaLoQueSeHayaPuestoDespues()
        {
            IDisposable sustitucion = Contenedor.Sustituir(A.Fake<IResolutorNesto>());
            sustitucion.Dispose();
            IResolutorNesto posterior = A.Fake<IResolutorNesto>();
            Contenedor.Establecer(posterior);

            sustitucion.Dispose();

            Assert.AreSame(posterior, Contenedor.Actual);
        }
    }
}
