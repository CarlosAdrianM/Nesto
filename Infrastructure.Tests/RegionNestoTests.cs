using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Navegacion;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace Nesto.Infrastructure.Tests
{
    /// <summary>
    /// Nesto#490 (4C.4, región propia): lo propio de <see cref="GestorRegiones"/> y <see cref="RegionNesto"/> que no se ve
    /// desde <see cref="IServicioNavegacion"/> (lo que sí se ve lo comprueban las pruebas del shell con las dos
    /// implementaciones, en <c>ShellRegionPrincipalTests</c>). Lanzan en los mismos casos que Prism.
    /// </summary>
    [TestClass]
    public class RegionNestoTests
    {
        private sealed class Activable : IConActivacion
        {
            public List<bool> Cambios { get; } = new List<bool>();
            private bool _isActive;
            public bool IsActive
            {
                get => _isActive;
                set
                {
                    _isActive = value;
                    Cambios.Add(value);
                }
            }
        }

        // La propia vista es la que pide el aviso, y no su DataContext
        private sealed class VistaActivable : Border, IConActivacion
        {
            public bool IsActive { get; set; }
        }

        private static void EnSta(Action accion)
        {
            Exception error = null;
            var hilo = new Thread(() =>
            {
                try { accion(); } catch (Exception ex) { error = ex; }
            });
            hilo.SetApartmentState(ApartmentState.STA);
            hilo.Start();
            hilo.Join();
            if (error != null)
            {
                throw new AssertFailedException(error.ToString(), error);
            }
        }

        [TestMethod]
        public void Registrar_DosRegionesConElMismoNombreEnUnAmbito_Lanza()
        {
            EnSta(() =>
            {
                var gestor = new GestorRegiones();
                gestor.Registrar("DetalleRegion", new ContentControl());

                Assert.ThrowsException<ArgumentException>(() => gestor.Registrar("DetalleRegion", new ContentControl()));
                // En otro ámbito sí puede llamarse igual
                Assert.IsNotNull(gestor.CrearAmbito().Registrar("DetalleRegion", new ContentControl()));
            });
        }

        [TestMethod]
        public void Registrar_SegunElControl_ElAdaptadorQueLeToca()
        {
            EnSta(() =>
            {
                var gestor = new GestorRegiones();

                Assert.IsInstanceOfType(gestor.Registrar("Pestanas", new TabControl()), typeof(RegionPestanas));
                Assert.IsInstanceOfType(gestor.Registrar("Contenido", new ContentControl()), typeof(RegionContenido));
                Assert.ThrowsException<NotSupportedException>(() => gestor.Registrar("Otra", new StackPanel()));
                Assert.IsFalse(gestor.Contiene("Otra"));
            });
        }

        [TestMethod]
        public void Anadir_LaMismaVistaDosVecesOConUnNombreRepetido_Lanza()
        {
            EnSta(() =>
            {
                RegionNesto region = new GestorRegiones().Registrar("MainRegion", new TabControl());
                var vista = new Border();
                region.Anadir(vista, "Clientes");

                Assert.ThrowsException<InvalidOperationException>(() => region.Anadir(vista));
                Assert.ThrowsException<InvalidOperationException>(() => region.Anadir(new Border(), "Clientes"));
                Assert.ThrowsException<ArgumentException>(() => region.Quitar(new Border()));
                Assert.ThrowsException<ArgumentException>(() => region.Activar(new Border()));
            });
        }

        [TestMethod]
        public void RegionContenido_LaPrimeraVistaSeActivaSolaYActivarOtraDesactivaLaAnterior()
        {
            EnSta(() =>
            {
                var control = new ContentControl();
                RegionNesto region = new GestorRegiones().Registrar("DetalleRegion", control);
                var primerContexto = new Activable();
                var primera = new Border { DataContext = primerContexto };
                var segunda = new Border();

                region.Anadir(primera);
                region.Anadir(segunda);
                Assert.AreSame(primera, control.Content);
                CollectionAssert.AreEqual(new object[] { primera }, region.VistasActivas.ToList());

                region.Activar(segunda);

                Assert.AreSame(segunda, control.Content);
                CollectionAssert.AreEqual(new object[] { segunda }, region.VistasActivas.ToList());
                CollectionAssert.AreEqual(new[] { true, false }, primerContexto.Cambios);

                region.Desactivar(segunda);
                Assert.IsNull(control.Content);
            });
        }

        [TestMethod]
        public void Quitar_DescuelgaElGestorDeLaRegionPeroNoElAmbitoPropio()
        {
            EnSta(() =>
            {
                var gestor = new GestorRegiones();
                RegionNesto region = gestor.Registrar("MainRegion", new TabControl());
                var normal = new Border();
                var conAmbito = new Border();
                GestorRegiones ambito = gestor.CrearAmbito();
                region.Anadir(normal);
                region.Anadir(conAmbito, null, ambito);
                Assert.AreSame(gestor, GestorRegiones.GetGestor(normal));
                Assert.AreSame(ambito, GestorRegiones.GetGestor(conAmbito));

                region.Quitar(normal);
                region.Quitar(conAmbito);

                Assert.IsNull(GestorRegiones.GetGestor(normal));
                Assert.AreSame(ambito, GestorRegiones.GetGestor(conAmbito));
            });
        }

        [TestMethod]
        public void LaVistaQueEsIConActivacion_RecibeElAvisoElla()
        {
            EnSta(() =>
            {
                var pestanas = new TabControl();
                RegionNesto region = new GestorRegiones().Registrar("MainRegion", pestanas);
                var vista = new VistaActivable();
                region.Anadir(vista);

                region.Activar(vista);
                Assert.IsTrue(vista.IsActive);
                Assert.AreSame(vista, pestanas.SelectedItem);

                region.Desactivar(vista);
                Assert.IsFalse(vista.IsActive);
                Assert.IsNull(pestanas.SelectedItem);
            });
        }

        [TestMethod]
        public void UnaNavegacionQueFalla_LlegaAlManejadorConLaRegionYElDestino()
        {
            EnSta(() =>
            {
                var gestor = new GestorRegiones();
                gestor.Registrar("MainRegion", new TabControl());
                (string region, string destino, Exception error) fallo = default;
                var navegacion = new ServicioNavegacionNesto(gestor, nombre => throw new InvalidOperationException("sin registrar"),
                    (region, destino, error) => fallo = (region, destino, error));

                navegacion.RequestNavigate("MainRegion", "VistaQueNoExiste");

                Assert.AreEqual("MainRegion", fallo.region);
                Assert.AreEqual("VistaQueNoExiste", fallo.destino);
                Assert.IsInstanceOfType(fallo.error, typeof(InvalidOperationException));
            });
        }
    }
}
