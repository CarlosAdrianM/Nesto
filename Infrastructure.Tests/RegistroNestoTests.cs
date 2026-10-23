using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Composicion;
using Nesto.Infrastructure.Contracts;
using System;
using System.Windows.Controls;

namespace Nesto.Infrastructure.Tests
{
    /// <summary>
    /// Nesto#490 (4C.5): el contrato de <see cref="IRegistroNesto"/>/<see cref="IResolutorNesto"/>. Hasta el tramo 4 se
    /// comprobaba con los dos contenedores (Prism/Unity y Microsoft.Extensions.DependencyInjection) para que resolvieran
    /// igual; desde el tramo 4 (sin Prism ni Unity) solo queda el de MS.DI, <see cref="ContenedorMicrosoft"/>, y el
    /// contrato sigue siendo el mismo (incluido lo que hacía Unity: el último registro gana y los concretos sin registrar
    /// se construyen).
    /// </summary>
    public abstract class RegistroNestoContratoTests
    {
        public interface IServicio
        {
        }

        public class Servicio : IServicio
        {
        }

        public class OtroServicio : IServicio
        {
        }

        public class Concreto
        {
            public Concreto(IServicio servicio)
            {
                Servicio = servicio;
            }

            public IServicio Servicio { get; }
        }

        public class VistaDePrueba
        {
        }

        public class DialogoDePrueba : IDialogoNesto
        {
            public string Title => "Prueba";

            public event Action<ResultadoDialogo> RequestClose { add { } remove { } }

            public bool CanCloseDialog() => true;

            public void OnDialogClosed()
            {
            }

            public void OnDialogOpened(ParametrosDialogo parameters)
            {
            }
        }

        /// <summary>Registra con <paramref name="registrar"/> y devuelve el resolutor del contenedor ya montado.</summary>
        protected abstract IResolutorNesto Montar(Action<IRegistroNesto> registrar);

        [TestMethod]
        public void Transitorio_UnaInstanciaNuevaCadaVez()
        {
            IResolutorNesto resolutor = Montar(r => r.Transitorio<IServicio, Servicio>());

            IServicio primera = resolutor.Resolver<IServicio>();
            IServicio segunda = resolutor.Resolver<IServicio>();

            Assert.IsInstanceOfType(primera, typeof(Servicio));
            Assert.AreNotSame(primera, segunda);
        }

        [TestMethod]
        public void Transitorio_PorTipos_IgualQueElGenerico()
        {
            IResolutorNesto resolutor = Montar(r => r.Transitorio(typeof(IServicio), typeof(Servicio)));

            Assert.IsInstanceOfType(resolutor.Resolver(typeof(IServicio)), typeof(Servicio));
            Assert.AreNotSame(resolutor.Resolver(typeof(IServicio)), resolutor.Resolver(typeof(IServicio)));
        }

        [TestMethod]
        public void TransitorioConcreto_SeConstruyeConSusDependencias()
        {
            IResolutorNesto resolutor = Montar(r =>
            {
                r.Transitorio<IServicio, Servicio>();
                r.Transitorio<Concreto>();
            });

            Concreto concreto = resolutor.Resolver<Concreto>();

            Assert.IsInstanceOfType(concreto.Servicio, typeof(Servicio));
            Assert.AreNotSame(concreto, resolutor.Resolver<Concreto>());
        }

        [TestMethod]
        public void ConcretoSinRegistrar_SeConstruyeIgual_ComoHaciaUnity()
        {
            IResolutorNesto resolutor = Montar(r => r.Transitorio<IServicio, Servicio>());

            Concreto concreto = resolutor.Resolver<Concreto>();

            Assert.IsInstanceOfType(concreto.Servicio, typeof(Servicio));
        }

        [TestMethod]
        public void Singleton_SiempreLaMismaInstancia()
        {
            IResolutorNesto resolutor = Montar(r => r.Singleton<IServicio, Servicio>());

            IServicio primera = resolutor.Resolver<IServicio>();

            Assert.IsInstanceOfType(primera, typeof(Servicio));
            Assert.AreSame(primera, resolutor.Resolver<IServicio>());
            Assert.AreSame(primera, resolutor.Resolver(typeof(IServicio)));
        }

        [TestMethod]
        public void SingletonConFabrica_SeLlamaUnaVez_YRecibeElResolutor()
        {
            int llamadas = 0;
            IResolutorNesto resolutor = Montar(r =>
            {
                r.Transitorio<IServicio, Servicio>();
                r.Singleton(resolutorDeLaFabrica =>
                {
                    llamadas++;
                    return new Concreto(resolutorDeLaFabrica.Resolver<IServicio>());
                });
            });

            Concreto primera = resolutor.Resolver<Concreto>();
            Concreto segunda = resolutor.Resolver<Concreto>();

            Assert.AreSame(primera, segunda);
            Assert.AreEqual(1, llamadas);
            Assert.IsInstanceOfType(primera.Servicio, typeof(Servicio), "La fábrica saca sus dependencias del mismo contenedor");
        }

        [TestMethod]
        public void SingletonConFabricaPorTipo_IgualQueElGenerico()
        {
            var unico = new Servicio();
            IResolutorNesto resolutor = Montar(r => r.Singleton(typeof(IServicio), _ => unico));

            Assert.AreSame(unico, resolutor.Resolver<IServicio>());
            Assert.AreSame(unico, resolutor.Resolver<IServicio>());
        }

        [TestMethod]
        public void Instancia_SiempreEsa()
        {
            var instancia = new Servicio();
            IResolutorNesto resolutor = Montar(r => r.Instancia<IServicio>(instancia));

            Assert.AreSame(instancia, resolutor.Resolver<IServicio>());
            Assert.AreSame(instancia, resolutor.Resolver(typeof(IServicio)));
        }

        [TestMethod]
        public void ElUltimoRegistroGana_ComoEnUnity()
        {
            IResolutorNesto resolutor = Montar(r =>
            {
                r.Transitorio<IServicio, Servicio>();
                r.Singleton<IServicio, OtroServicio>();
            });

            Assert.IsInstanceOfType(resolutor.Resolver<IServicio>(), typeof(OtroServicio));
        }

        [TestMethod]
        public void Vista_PorSuNombre_NuevaCadaVez()
        {
            IResolutorNesto resolutor = Montar(r => r.Vista<VistaDePrueba>("VistaDePrueba"));

            object primera = resolutor.ResolverVista("VistaDePrueba");

            Assert.IsInstanceOfType(primera, typeof(VistaDePrueba));
            Assert.AreNotSame(primera, resolutor.ResolverVista("VistaDePrueba"));
            Assert.IsTrue(resolutor.TieneVista("VistaDePrueba"));
            Assert.IsFalse(resolutor.TieneVista("OtraVista"));
            Assert.IsFalse(resolutor.TieneVista(null));
        }

        [TestMethod]
        public void Vista_ConOtroNombre_SoloSeEncuentraConEse()
        {
            IResolutorNesto resolutor = Montar(r => r.Vista("OtroNombre", typeof(VistaDePrueba)));

            Assert.IsInstanceOfType(resolutor.ResolverVista("OtroNombre"), typeof(VistaDePrueba));
            Assert.IsFalse(resolutor.TieneVista("VistaDePrueba"));
        }

        [TestMethod]
        public void Dialogo_PorSuNombre_ElMismoRegistro()
        {
            var dialogo = new DialogoRegistrado("Prueba", typeof(Grid), typeof(DialogoDePrueba));
            IResolutorNesto resolutor = Montar(r => r.Dialogo("Prueba", dialogo));

            Assert.AreSame(dialogo, resolutor.Dialogo("Prueba"));
            Assert.IsNull(resolutor.Dialogo("NoExiste"));
            Assert.IsNull(resolutor.Dialogo(null));
            Assert.IsFalse(resolutor.TieneVista("Prueba"), "Un diálogo no es una vista");
        }

        [TestMethod]
        public void SinNombre_LaVistaYElDialogoLanzan()
        {
            _ = Assert.ThrowsException<ArgumentException>(() => Montar(r => r.Vista<VistaDePrueba>(" ")));
            _ = Assert.ThrowsException<ArgumentException>(() => Montar(r => r.Dialogo(null, new DialogoRegistrado("Prueba", typeof(Grid), typeof(DialogoDePrueba)))));
        }
    }

    [TestClass]
    public class ContenedorMicrosoftTests : RegistroNestoContratoTests
    {
        protected override IResolutorNesto Montar(Action<IRegistroNesto> registrar)
        {
            var contenedor = new ContenedorMicrosoft();
            registrar(contenedor);
            return contenedor.Construir();
        }

        [TestMethod]
        public void LasVistasYLosDialogos_SonServiciosConClave()
        {
            var contenedor = new ContenedorMicrosoft();
            contenedor.Vista<VistaDePrueba>("VistaDePrueba");
            contenedor.Dialogo("Prueba", new DialogoRegistrado("Prueba", typeof(Grid), typeof(DialogoDePrueba)));

            IServiceProvider proveedor = contenedor.Construir().Proveedor;
            var claves = proveedor.GetRequiredService<IServiceProviderIsKeyedService>();

            Assert.IsTrue(claves.IsKeyedService(typeof(object), "VistaDePrueba"));
            Assert.IsTrue(claves.IsKeyedService(typeof(DialogoRegistrado), "Prueba"));
            Assert.IsNull(proveedor.GetService(typeof(VistaDePrueba)), "Como era con Prism: la vista solo se pide por su nombre");
        }

        [TestMethod]
        public void LaFabricaDelSingleton_RecibeUnResolutorSobreElMismoContenedor()
        {
            // Nesto#490 (4C.5, tramo 4): sustituye a la de RegistroPrism (allí la fábrica recibía un ResolutorPrism)
            var contenedor = new ContenedorMicrosoft();
            IResolutorNesto recibido = null;
            contenedor.Singleton<OtroServicio, OtroServicio>();
            contenedor.Singleton<IServicio>(r =>
            {
                recibido = r;
                return new Servicio();
            });
            ResolutorMicrosoft resolutor = contenedor.Construir();

            _ = resolutor.Resolver<IServicio>();

            Assert.IsInstanceOfType(recibido, typeof(ResolutorMicrosoft));
            Assert.AreSame(resolutor.Resolver<OtroServicio>(), recibido.Resolver<OtroServicio>(), "Los singletons son los del mismo contenedor");
        }

        [TestMethod]
        public void ConValidacion_UnaDependenciaQueFaltaLanzaAlConstruir()
        {
            var contenedor = new ContenedorMicrosoft();
            contenedor.Transitorio<Concreto>();

            _ = Assert.ThrowsException<AggregateException>(() => contenedor.Construir());
        }
    }
}
