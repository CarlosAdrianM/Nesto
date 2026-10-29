using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;

namespace ControlesUsuario.Tests
{
    /// <summary>Nesto (29/10/26): selector de correo reutilizable (combo editable con los correos del cliente).</summary>
    [TestClass]
    public class SelectorCorreoTests
    {
        // ---------------- CorreosElegibles (sin WPF) ----------------

        [DataTestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("   ")]
        [DataRow("ana@luna.es")]
        [DataRow(" ana.luna+cursos@sub.dominio.com ")]
        public void EsValido_VacioOConFormaDeCorreo_EsTrue(string correo)
        {
            Assert.IsTrue(CorreosElegibles.EsValido(correo));
        }

        [DataTestMethod]
        [DataRow("ana")]
        [DataRow("ana@luna")]
        [DataRow("ana@@luna.es")]
        [DataRow("ana luna@luna.es")]
        [DataRow("ana@luna.es; pepe@luna.es")]
        [DataRow("@luna.es")]
        public void EsValido_SinFormaDeCorreo_EsFalse(string correo)
        {
            Assert.IsFalse(CorreosElegibles.EsValido(correo));
        }

        [TestMethod]
        public void Unicos_QuitaVaciosRepetidosYLosQueNoSonCorreos_YSeparaLosQueVienenJuntos()
        {
            List<string> unicos = CorreosElegibles.Unicos(new[]
            {
                "ana@luna.es", null, "", "ANA@LUNA.ES", "pepe@sol.es; luis@mar.es", "no es un correo", " ana@luna.es "
            });

            CollectionAssert.AreEqual(new[] { "ana@luna.es", "pepe@sol.es", "luis@mar.es" }, unicos);
        }

        [TestMethod]
        public void Unicos_ConNull_DevuelveListaVacia()
        {
            Assert.AreEqual(0, CorreosElegibles.Unicos(null).Count);
        }

        [TestMethod]
        public void CorreoTrasCambiarLista_SinNadaEscrito_PoneElPrimero()
        {
            Assert.AreEqual("ana@luna.es", CorreosElegibles.CorreoTrasCambiarLista(null, null, new[] { "ana@luna.es", "pepe@sol.es" }));
        }

        [TestMethod]
        public void CorreoTrasCambiarLista_LoQuePusoElSelector_SeCambiaPorElPrimeroNuevo()
        {
            Assert.AreEqual("pepe@sol.es", CorreosElegibles.CorreoTrasCambiarLista("ana@luna.es", "ana@luna.es", new[] { "pepe@sol.es" }));
        }

        [TestMethod]
        public void CorreoTrasCambiarLista_LoQueEscribioElUsuario_SeRespeta()
        {
            Assert.AreEqual("mio@casa.es", CorreosElegibles.CorreoTrasCambiarLista("mio@casa.es", "ana@luna.es", new[] { "pepe@sol.es" }));
        }

        [TestMethod]
        public void CorreoTrasCambiarLista_ListaVaciaYNadaDelUsuario_SeQuedaVacio()
        {
            Assert.AreEqual(string.Empty, CorreosElegibles.CorreoTrasCambiarLista("ana@luna.es", "ana@luna.es", new List<string>()));
        }

        // ---------------- El control ----------------

        [TestMethod]
        public void Control_AlDarleCorreos_OfreceLosUnicosYPoneElPrimero()
        {
            EjecutarEnSTA(() =>
            {
                var selector = new SelectorCorreo { Correos = new[] { "ana@luna.es", "ANA@luna.es", "pepe@sol.es" } };

                CollectionAssert.AreEqual(new[] { "ana@luna.es", "pepe@sol.es" }, selector.CorreosOfrecidos.ToList());
                Assert.AreEqual("ana@luna.es", selector.Correo);
                Assert.IsTrue(selector.EsValido);
            });
        }

        [TestMethod]
        public void Control_SiElUsuarioEscribeOtro_NoSeLoPisaAlCambiarLosCorreos()
        {
            EjecutarEnSTA(() =>
            {
                var selector = new SelectorCorreo { Correos = new[] { "ana@luna.es" } };
                selector.Correo = "nuevo@cliente.es";

                selector.Correos = new[] { "pepe@sol.es" };

                Assert.AreEqual("nuevo@cliente.es", selector.Correo);
            });
        }

        [TestMethod]
        public void Control_ConColeccionObservable_SigueLosCambios()
        {
            EjecutarEnSTA(() =>
            {
                var correos = new ObservableCollection<string>();
                var selector = new SelectorCorreo { Correos = correos };

                correos.Add("ana@luna.es");
                correos.Add("pepe@sol.es");

                CollectionAssert.AreEqual(new[] { "ana@luna.es", "pepe@sol.es" }, selector.CorreosOfrecidos.ToList());
                Assert.AreEqual("ana@luna.es", selector.Correo);

                correos.Clear();
                correos.Add("luis@mar.es");

                Assert.AreEqual("luis@mar.es", selector.Correo, "El que puso el selector se cambia por el del cliente nuevo");
            });
        }

        [TestMethod]
        public void Control_CorreoSinFormato_NoEsValido()
        {
            EjecutarEnSTA(() =>
            {
                var selector = new SelectorCorreo { Correo = "ana@luna" };

                Assert.IsFalse(selector.EsValido);

                selector.Correo = "ana@luna.es";

                Assert.IsTrue(selector.EsValido);
            });
        }

        [TestMethod]
        public void Control_CorreosANullEnElTeardown_NoLanzaNiTocaElCorreo()
        {
            EjecutarEnSTA(() =>
            {
                var correos = new ObservableCollection<string> { "ana@luna.es" };
                var selector = new SelectorCorreo { Correos = correos };

                selector.Correos = null;
                correos.Add("pepe@sol.es"); // ya no se sigue: no debe tocar nada

                Assert.AreEqual(0, selector.CorreosOfrecidos.Count);
                Assert.AreEqual("ana@luna.es", selector.Correo);
            });
        }

        private static void EjecutarEnSTA(Action action)
        {
            Exception capturada = null;
            var hilo = new Thread(() =>
            {
                try { action(); }
                catch (Exception ex) { capturada = ex; }
            });
            hilo.SetApartmentState(ApartmentState.STA);
            hilo.Start();
            hilo.Join();
            if (capturada != null)
            {
                throw new AssertFailedException(capturada.Message, capturada);
            }
        }
    }
}
