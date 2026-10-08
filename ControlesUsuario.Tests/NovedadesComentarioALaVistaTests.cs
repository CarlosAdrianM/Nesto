using ControlesUsuario.Dialogs;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace ControlesUsuario.Tests
{
    /// <summary>
    /// Nesto#516: al abrir un aviso de la campana, el comentario (o la novedad) al que lleva queda a la vista
    /// y con el foco. La vista sigue al VM; la lógica de reintento y colocación está en
    /// <see cref="SeguimientoDestacado"/>.
    /// </summary>
    [TestClass]
    public class NovedadesComentarioALaVistaTests
    {
        #region La vista se engancha al VM (antes no: el AutoWire pone el DataContext dentro de InitializeComponent)

        [TestMethod]
        public void NovedadesDialog_ConElViewModelDelAutoWire_EscuchaSusCambios()
        {
            EjecutarEnSTA(() =>
            {
                var vista = new NovedadesDialog();

                Assert.IsInstanceOfType(vista.DataContext, typeof(NovedadesDialogViewModel),
                    "El AutoWire de Prism pone el VM durante InitializeComponent");
                Assert.IsTrue(EscuchaA(vista.DataContext, vista),
                    "Sin engancharse al DataContext que ya trae, la vista nunca se entera de NovedadDestacada");
            });
        }

        [TestMethod]
        public void NovedadesDialog_DesdeLaCampana_SigueElComentarioYNoLaNovedad()
        {
            EjecutarEnSTA(() =>
            {
                var servicio = A.Fake<INovedadesService>();
                A.CallTo(() => servicio.ObtenerNovedades(A<string>._)).Returns(Task.FromResult(new List<NovedadUsuario>()));
                A.CallTo(() => servicio.LeerComentarios(A<int>._)).Returns(Task.FromResult(new List<ComentarioNovedad>
                {
                    new ComentarioNovedad { Id = 100, Texto = "Mi pregunta", NombreVisible = "Alfredo" },
                    new ComentarioNovedad { Id = 101, Texto = "La respuesta", NombreVisible = "Carlos" }
                }));
                var vm = new NovedadesDialogViewModel(servicio, null, _ => false);
                var parametros = new DialogParameters();
                parametros.Add("novedades", new List<NovedadUsuario>
                {
                    new NovedadUsuario { Id = 2, Version = "1.10.30.0", Titulo = "x", Categoria = "Nuevo", VotosPositivos = 0, VotosNegativos = 0, NumeroComentarios = 2 }
                });
                vm.OnDialogOpened(parametros);
                var vista = new NovedadesDialog { DataContext = vm };

                vm.IrANovedadComentario(2, 101).GetAwaiter().GetResult();

                Assert.IsNotNull(vm.NovedadDestacada?.ComentarioDestacado, "el VM destaca el comentario");
                Assert.IsTrue(vista.Seguimiento.Activo, "sigue activo");
                Assert.IsTrue(vista.Seguimiento.EsComentario);
                Assert.AreSame(vm.NovedadDestacada.ComentarioDestacado, vista.Seguimiento.Objetivo);
            });
        }

        private static bool EscuchaA(object vm, object vista)
        {
            FieldInfo campo = null;
            for (Type t = vm.GetType(); t != null && campo == null; t = t.BaseType)
            {
                campo = t.GetField("PropertyChanged", BindingFlags.Instance | BindingFlags.NonPublic);
            }
            var manejador = campo?.GetValue(vm) as PropertyChangedEventHandler;
            return manejador != null && manejador.GetInvocationList().Any(d => d.Target == vista);
        }

        #endregion

        #region Colocación: centrada si cabe, arriba si no

        [TestMethod]
        public void CalcularDesplazamiento_ElementoQueCabe_LoCentra()
        {
            // Elemento de 100 a 400 px por debajo del borde visible, área de 500: se centra.
            double nuevo = SeguimientoDestacado.CalcularDesplazamiento(desplazamientoActual: 1000, yElemento: 400, altoElemento: 100, altoVisible: 500);

            Assert.AreEqual(1000 + 400 - 200, nuevo);
        }

        [TestMethod]
        public void CalcularDesplazamiento_ElementoMasAltoQueElArea_LoPoneArribaConMargen()
        {
            double nuevo = SeguimientoDestacado.CalcularDesplazamiento(desplazamientoActual: 1000, yElemento: 400, altoElemento: 800, altoVisible: 500);

            Assert.AreEqual(1000 + 400 - SeguimientoDestacado.MARGEN_SUPERIOR, nuevo);
        }

        [TestMethod]
        public void CalcularDesplazamiento_CercaDelPrincipio_NoBajaDeCero()
        {
            double nuevo = SeguimientoDestacado.CalcularDesplazamiento(desplazamientoActual: 0, yElemento: 50, altoElemento: 100, altoVisible: 500);

            Assert.AreEqual(0, nuevo);
        }

        #endregion

        #region Reintento y prioridad del comentario

        [TestMethod]
        public void Paso_ContenedorAunSinGenerar_ReintentaHastaElTopeYTermina()
        {
            var seguimiento = new SeguimientoDestacado();
            seguimiento.SeguirComentario("comentario");

            for (int i = 1; i < SeguimientoDestacado.MAX_INTENTOS; i++)
            {
                PasoSeguimiento paso = seguimiento.Paso(encontrado: false, yaColocado: false);
                Assert.IsFalse(paso.Colocar);
                Assert.IsFalse(paso.Terminar, $"intento {i}");
            }
            Assert.IsTrue(seguimiento.Paso(false, false).Terminar);
            Assert.IsFalse(seguimiento.Activo);
        }

        [TestMethod]
        public void Paso_ComentarioQueApareceTarde_LoColocaYLeDaElFocoUnaVez()
        {
            var seguimiento = new SeguimientoDestacado();
            seguimiento.SeguirComentario("comentario");
            seguimiento.Paso(false, false);
            seguimiento.Paso(false, false);

            PasoSeguimiento primero = seguimiento.Paso(encontrado: true, yaColocado: false);
            PasoSeguimiento segundo = seguimiento.Paso(encontrado: true, yaColocado: false);

            Assert.IsTrue(primero.Colocar);
            Assert.IsTrue(primero.DarFoco);
            Assert.IsTrue(segundo.Colocar, "Si la lista se movió (imágenes que cargan después), se vuelve a colocar");
            Assert.IsFalse(segundo.DarFoco);
        }

        [TestMethod]
        public void Paso_NovedadSinComentario_SeColocaPeroNoSeLlevaElFoco()
        {
            var seguimiento = new SeguimientoDestacado();
            seguimiento.SeguirNovedad("novedad");

            PasoSeguimiento paso = seguimiento.Paso(true, false);

            Assert.IsTrue(paso.Colocar);
            Assert.IsFalse(paso.DarFoco, "Desde el buscador el foco se queda donde estaba");
        }

        [TestMethod]
        public void Paso_ColocadoYQuieto_TerminaTrasVariasComprobaciones()
        {
            var seguimiento = new SeguimientoDestacado();
            seguimiento.SeguirComentario("comentario");
            seguimiento.Paso(true, false);

            for (int i = 1; i < SeguimientoDestacado.PASOS_QUIETO; i++)
            {
                Assert.IsFalse(seguimiento.Paso(true, true).Terminar);
            }
            Assert.IsTrue(seguimiento.Paso(true, true).Terminar);
        }

        [TestMethod]
        public void SeguirNovedad_ConUnComentarioEnCurso_NoLoPisa()
        {
            var seguimiento = new SeguimientoDestacado();
            seguimiento.SeguirComentario("comentario");

            seguimiento.SeguirNovedad("novedad");

            Assert.AreEqual("comentario", seguimiento.Objetivo);
            Assert.IsTrue(seguimiento.EsComentario);
        }

        [TestMethod]
        public void SeguirComentario_TrasLaNovedad_PasaAlComentario()
        {
            var seguimiento = new SeguimientoDestacado();
            seguimiento.SeguirNovedad("novedad");
            seguimiento.Paso(true, false);

            seguimiento.SeguirComentario("comentario");

            Assert.AreEqual("comentario", seguimiento.Objetivo);
            Assert.IsTrue(seguimiento.Paso(true, false).DarFoco);
        }

        [TestMethod]
        public void SeguirNovedad_DespuesDeTerminarElComentario_SigueLaNovedadNueva()
        {
            var seguimiento = new SeguimientoDestacado();
            seguimiento.SeguirComentario("comentario");
            seguimiento.Interrumpir();

            seguimiento.SeguirNovedad("otra");

            Assert.AreEqual("otra", seguimiento.Objetivo);
            Assert.IsTrue(seguimiento.Activo);
        }

        [TestMethod]
        public void Interrumpir_ElUsuarioMueveLaLista_NoSeLeVuelveALlevar()
        {
            var seguimiento = new SeguimientoDestacado();
            seguimiento.SeguirComentario("comentario");
            seguimiento.Paso(true, false);

            seguimiento.Interrumpir();

            Assert.IsTrue(seguimiento.Paso(true, false).Terminar);
            Assert.IsFalse(seguimiento.Paso(true, false).Colocar);
        }

        #endregion

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
