using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Composicion;
using Nesto.Infrastructure.Contracts;
using Prism.Ioc;
using Prism.Unity;
using System;
using System.Windows.Controls;

namespace Nesto.Infrastructure.Tests
{
    /// <summary>
    /// Nesto#490 (4C.5): lo mismo registrado con <see cref="IRegistroNesto"/> se tiene que resolver igual con los dos
    /// contenedores: el de Prism/Unity (<see cref="RegistroPrism"/>/<see cref="ResolutorPrism"/>, el de hoy) y el de
    /// Microsoft.Extensions.DependencyInjection (<see cref="ContenedorMicrosoft"/>, el del tramo 4). Las pruebas son las
    /// mismas para los dos; las de Prism comprueban además que queda el mismo registro de Unity que antes se hacía a mano.
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
    public class RegistroPrismTests : RegistroNestoContratoTests
    {
        protected override IResolutorNesto Montar(Action<IRegistroNesto> registrar)
        {
            var unity = new UnityContainerExtension();
            registrar(new RegistroPrism(unity));
            return new ResolutorPrism(unity);
        }

        [TestMethod]
        public void Vista_QuedaElMismoRegistroQueRegisterObjectConNombre()
        {
            var unity = new UnityContainerExtension();

            new RegistroPrism(unity).Vista<VistaDePrueba>("VistaDePrueba");

            Assert.IsTrue(unity.IsRegistered(typeof(object), "VistaDePrueba"));
            Assert.IsInstanceOfType(unity.Resolve(typeof(object), "VistaDePrueba"), typeof(VistaDePrueba));
            Assert.IsFalse(unity.IsRegistered(typeof(VistaDePrueba)), "Como Register<object, X>(nombre): solo por nombre");
        }

        [TestMethod]
        public void Dialogo_QuedaComoInstanciaConNombre()
        {
            var unity = new UnityContainerExtension();
            var dialogo = new DialogoRegistrado("Prueba", typeof(Grid), typeof(DialogoDePrueba));

            new RegistroPrism(unity).Dialogo("Prueba", dialogo);

            Assert.IsTrue(unity.IsRegistered(typeof(DialogoRegistrado), "Prueba"));
            Assert.AreSame(dialogo, unity.Resolve(typeof(DialogoRegistrado), "Prueba"));
        }

        [TestMethod]
        public void Singleton_Transitorio_EInstancia_QuedanRegistradosEnUnity()
        {
            var unity = new UnityContainerExtension();
            var registro = new RegistroPrism(unity);

            registro.Singleton<IServicio, Servicio>();
            registro.Transitorio<Concreto>();
            registro.Instancia<OtroServicio>(new OtroServicio());

            Assert.IsTrue(unity.IsRegistered(typeof(IServicio)));
            Assert.AreSame(unity.Resolve<IServicio>(), unity.Resolve<IServicio>());
            Assert.IsTrue(unity.IsRegistered(typeof(Concreto)));
            Assert.AreNotSame(unity.Resolve<Concreto>(), unity.Resolve<Concreto>());
            Assert.IsTrue(unity.IsRegistered(typeof(OtroServicio)));
        }

        [TestMethod]
        public void LaFabricaDelSingleton_RecibeUnResolutorSobreElContenedorDePrism()
        {
            var unity = new UnityContainerExtension();
            IResolutorNesto recibido = null;

            new RegistroPrism(unity).Singleton<IServicio>(r =>
            {
                recibido = r;
                return new Servicio();
            });
            _ = unity.Resolve<IServicio>();

            Assert.IsInstanceOfType(recibido, typeof(ResolutorPrism));
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
            Assert.IsNull(proveedor.GetService(typeof(VistaDePrueba)), "Como con Prism: la vista solo se pide por su nombre");
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
