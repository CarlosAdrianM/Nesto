using ControlesUsuario.Globos;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Globos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace ControlesUsuario.Tests
{
    /// <summary>Nesto#592 (NestoAPI#774): la clave de pantalla de cada vista y el registro de anclas.</summary>
    [TestClass]
    public class GlobosTests
    {
        [DataTestMethod]
        [DataRow("EnviarReposicionView", null, "EnviarReposicion")]
        [DataRow("MainWindow", null, "Principal")]
        [DataRow("CampanaNotificaciones", null, null)]
        [DataRow("View", null, null)]
        [DataRow("SelectorCliente", "PedidoVenta", "PedidoVenta")]
        [DataRow(null, null, null)]
        public void ClavePantalla(string tipo, string puesta, string esperada)
        {
            Assert.AreEqual(esperada, PantallasNesto.Clave(tipo, puesta));
        }

        [TestMethod]
        public void Ancla_SeRegistraPorClave_YSinCargarNoEstaALaVista()
        {
            Exception error = null;
            var hilo = new Thread(() =>
            {
                try
                {
                    var texto = new TextBlock();
                    Globo.SetAncla(texto, " Prueba.Control ");

                    // Registrada, pero no está cargada en ninguna ventana: no cuenta como visible (el globo saldría centrado)
                    Assert.IsNull(RegistroAnclas.Visible("Prueba.Control"));
                    Assert.AreEqual(" Prueba.Control ", Globo.GetAncla(texto));
                    Globo.SetAncla(texto, null);
                    Assert.IsNull(RegistroAnclas.Visible("Prueba.Control"));
                    Assert.IsNull(RegistroAnclas.Visible("No.Existe"));
                }
                catch (Exception ex)
                {
                    error = ex;
                }
            });
            hilo.SetApartmentState(ApartmentState.STA);
            hilo.Start();
            hilo.Join();
            if (error != null)
            {
                throw error;
            }
        }

        // ---- El globo que espera a su control (la casilla del último paso del asistente de la plantilla de venta) ----

        [TestMethod]
        public void EnEspera_ElControlQueCuelgaDeLaVistaSinVerse_YNoElDeOtraVistaNiElQueNoExiste()
        {
            EjecutarEnSTA(() =>
            {
                var texto = new TextBlock();
                Globo.SetAncla(texto, "PruebaEspera.Control");
                var panel = new StackPanel();
                _ = panel.Children.Add(texto);
                var vista = new UserControl { Content = panel };
                var otraVista = new UserControl();

                // Sin cargar no se ve, pero EXISTE en su vista: su globo espera (no saldría centrado)
                Assert.IsNull(RegistroAnclas.Visible("PruebaEspera.Control"));
                Assert.AreSame(texto, RegistroAnclas.EnEspera(" PruebaEspera.Control ", vista).Single());
                Assert.IsTrue(RegistroAnclas.EstaDentro(texto, vista));
                // En otra vista, o una clave que nadie lleva: no espera (centrado, como siempre)
                Assert.AreEqual(0, RegistroAnclas.EnEspera("PruebaEspera.Control", otraVista).Count);
                Assert.AreEqual(0, RegistroAnclas.EnEspera("PruebaEspera.NoExiste", vista).Count);
                Assert.AreEqual(0, RegistroAnclas.EnEspera("PruebaEspera.Control", null).Count);
                Assert.AreEqual(0, RegistroAnclas.EnEspera(null, vista).Count);
            });
        }

        /// <summary>
        /// LA CLAVE de la casilla de «Finalizar»: en el Wizard de Xceed el control de un paso que no es el activo ya existe y
        /// está registrado (cuelga de la vista por el árbol lógico), pero no se ve; al entrar en su paso, se ve.
        /// </summary>
        [TestMethod]
        public void AsistenteXceed_ElControlDeOtroPasoEstaEnEspera_YAlLlegarASuPasoAvisaUnaVez()
        {
            EjecutarEnSTA(() =>
            {
                var asistente = new AsistentePrueba("PruebaAsistente.Casilla");
                var vista = new UserControl { Content = asistente.Wizard };
                Window ventana = Ventana(vista);
                ventana.Show();
                try
                {
                    Bombear();
                    int avisos = 0;
                    var espera = new EsperaVista(vista, _ => avisos++);

                    Assert.IsNull(RegistroAnclas.Visible("PruebaAsistente.Casilla"), "en el primer paso la casilla no se ve");
                    Assert.AreSame(asistente.Casilla, RegistroAnclas.EnEspera("PruebaAsistente.Casilla", vista).Single(),
                        "pero existe y está registrada dentro de la vista");
                    Assert.IsTrue(espera.Esperar("PruebaAsistente.Casilla"));
                    Assert.IsTrue(espera.Esperar("PruebaAsistente.Casilla"), "preguntar dos veces no engancha dos veces");
                    Assert.AreEqual(1, espera.Escuchando);
                    Assert.IsFalse(espera.Esperar("PruebaAsistente.NoExiste"));
                    Assert.AreEqual(0, avisos);

                    asistente.Wizard.CurrentPage = asistente.Ultimo;
                    Bombear();

                    Assert.AreSame(asistente.Casilla, RegistroAnclas.Visible("PruebaAsistente.Casilla"), "al llegar al paso, se ve");
                    Assert.AreEqual(1, avisos, "avisa una sola vez");
                    Assert.AreEqual(0, espera.Escuchando, "y deja de escuchar");
                    Assert.AreEqual(0, RegistroAnclas.EnEspera("PruebaAsistente.Casilla", vista).Count);
                    Assert.IsFalse(espera.Esperar("PruebaAsistente.Casilla"), "ya se ve: no espera");

                    // Ir y volver sin estar esperando no avisa
                    asistente.Wizard.CurrentPage = asistente.Primero;
                    Bombear();
                    asistente.Wizard.CurrentPage = asistente.Ultimo;
                    Bombear();
                    Assert.AreEqual(1, avisos);
                }
                finally
                {
                    ventana.Close();
                }
            });
        }

        [TestMethod]
        public void EsperaVista_AlDescargarseLaVista_NoQuedanManejadoresColgando()
        {
            EjecutarEnSTA(() =>
            {
                var asistente = new AsistentePrueba("PruebaDescarga.Casilla");
                var vista = new UserControl { Content = asistente.Wizard };
                var marco = new ContentControl { Content = vista };
                Window ventana = Ventana(marco);
                ventana.Show();
                try
                {
                    Bombear();
                    int avisos = 0;
                    var espera = new EsperaVista(vista, _ => avisos++);
                    Assert.IsTrue(espera.Esperar("PruebaDescarga.Casilla"));
                    Assert.AreEqual(1, espera.Escuchando);

                    // Se cierra la pestaña (la vista sale del árbol)
                    marco.Content = null;
                    Bombear();
                    Assert.AreEqual(0, espera.Escuchando);
                    // Con la vista descargada dice que espera (no hay que enseñarlo centrado) pero no engancha nada
                    Assert.IsTrue(espera.Esperar("PruebaDescarga.Casilla"));
                    Assert.AreEqual(0, espera.Escuchando);

                    // Vuelve la vista y se llega al paso: nadie escuchaba, nadie avisa
                    marco.Content = vista;
                    asistente.Wizard.CurrentPage = asistente.Ultimo;
                    Bombear();
                    Assert.IsNotNull(RegistroAnclas.Visible("PruebaDescarga.Casilla"));
                    Assert.AreEqual(0, avisos);
                }
                finally
                {
                    ventana.Close();
                }
            });
        }

        /// <summary>
        /// De punta a punta con el presentador (que es estático: se inicia una sola vez en todo el ensamblado de tests, con un
        /// servicio que solo tiene globos para la pantalla de esta prueba).
        /// </summary>
        [TestMethod]
        public void Presentador_ElGloboDeOtroPasoNoSaleAlAbrir_SaleAlLlegar_YVuelveAEsperarSiSeRetrocede()
        {
            EjecutarEnSTA(() =>
            {
                var cliente = new ClienteGlobosFalso();
                var servicio = new ServicioGlobos(cliente, () => "prueba");
                PresentadorGlobos.Iniciar(servicio);
                var vista = new AsistenteGlobosPruebaView();
                Window ventana = Ventana(vista);
                ventana.Show();
                try
                {
                    Bombear();
                    Assert.AreEqual(1, cliente.Leidas, "al cargarse la vista se piden sus globos");
                    Assert.IsFalse(servicio.HayEnPantalla, "la casilla está en otro paso: el globo no sale (tampoco centrado)");

                    vista.Asistente.Wizard.CurrentPage = vista.Asistente.Ultimo;
                    Bombear();
                    Assert.IsTrue(servicio.HayEnPantalla, "al llegar al paso, sale");
                    Assert.AreEqual(0, cliente.Vistos.Count);

                    // Se retrocede sin contestar: no se queda flotando ni se da por visto; vuelve a esperar a su control
                    vista.Asistente.Wizard.CurrentPage = vista.Asistente.Primero;
                    Bombear();
                    Assert.IsFalse(servicio.HayEnPantalla);
                    vista.Asistente.Wizard.CurrentPage = vista.Asistente.Ultimo;
                    Bombear();
                    Assert.IsTrue(servicio.HayEnPantalla, "y vuelve a salir al volver al paso");

                    // Se cierra la pantalla sin contestar: queda pendiente, como siempre
                    ventana.Content = null;
                    Bombear();
                    Assert.IsFalse(servicio.HayEnPantalla);
                    Assert.AreEqual(0, cliente.Vistos.Count);
                    Assert.AreEqual(1, cliente.Leidas, "sin volver a llamar a la API");
                }
                finally
                {
                    ventana.Close();
                }
            });
        }

        /// <summary>Un asistente de dos pasos con una casilla anclada en el último, como el de la plantilla de venta.</summary>
        private sealed class AsistentePrueba
        {
            public readonly Xceed.Wpf.Toolkit.Wizard Wizard = new Xceed.Wpf.Toolkit.Wizard();
            public readonly Xceed.Wpf.Toolkit.WizardPage Primero = new Xceed.Wpf.Toolkit.WizardPage { Title = "Primero", Content = new TextBlock { Text = "Primer paso" } };
            public readonly Xceed.Wpf.Toolkit.WizardPage Ultimo = new Xceed.Wpf.Toolkit.WizardPage { Title = "Finalizar" };
            public readonly CheckBox Casilla = new CheckBox { Content = "Casilla del último paso" };

            public AsistentePrueba(string ancla)
            {
                Globo.SetAncla(Casilla, ancla);
                var panel = new StackPanel();
                _ = panel.Children.Add(new TextBlock { Text = "Último paso" });
                _ = panel.Children.Add(Casilla);
                Ultimo.Content = panel;
                _ = Wizard.Items.Add(Primero);
                _ = Wizard.Items.Add(Ultimo);
                Wizard.CurrentPage = Primero;
            }
        }

        // El nombre acaba en «View»: su pantalla es «AsistenteGlobosPrueba»
        private sealed class AsistenteGlobosPruebaView : UserControl
        {
            public const string ANCLA = "AsistenteGlobosPrueba.Casilla";
            public readonly AsistentePrueba Asistente = new AsistentePrueba(ANCLA);

            public AsistenteGlobosPruebaView()
            {
                Content = Asistente.Wizard;
            }
        }

        private sealed class ClienteGlobosFalso : IClienteGlobos
        {
            public int Leidas;
            public readonly List<int> Vistos = new List<int>();

            public Task<List<DatosGlobo>> LeerPendientesAsync(string pantalla, CancellationToken cancelacion = default)
            {
                var globos = new List<DatosGlobo>();
                if (pantalla == "AsistenteGlobosPrueba")
                {
                    Leidas++;
                    globos.Add(new DatosGlobo { Id = 1, Pantalla = pantalla, Ancla = AsistenteGlobosPruebaView.ANCLA, Titulo = "Nuevo", Texto = "Esta casilla es nueva." });
                }
                return Task.FromResult(globos);
            }

            public Task MarcarVistoAsync(int id, CancellationToken cancelacion = default)
            {
                Vistos.Add(id);
                return Task.CompletedTask;
            }
        }

        private static Window Ventana(object contenido) => new Window
        {
            Content = contenido,
            Left = -10000,
            Top = -10000,
            Width = 600,
            Height = 400,
            ShowInTaskbar = false,
            ShowActivated = false,
            WindowStartupLocation = WindowStartupLocation.Manual
        };

        // Deja que el Dispatcher procese todo lo pendiente, hasta lo de menor prioridad (el presentador espera a ApplicationIdle)
        private static void Bombear()
        {
            for (int i = 0; i < 3; i++)
            {
                var marco = new DispatcherFrame();
                _ = Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.SystemIdle, new Action(() => marco.Continue = false));
                Dispatcher.PushFrame(marco);
            }
        }

        private static void EjecutarEnSTA(Action accion)
        {
            Exception error = null;
            var hilo = new Thread(() =>
            {
                try
                {
                    // Como el hilo de la interfaz de la aplicación: lo que sigue a un await vuelve a este hilo también cuando el
                    // evento salta desde el código de la prueba (cambiar de paso) y no desde dentro del Dispatcher
                    SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
                    accion();
                }
                catch (Exception ex)
                {
                    error = ex;
                }
                finally
                {
                    Dispatcher.CurrentDispatcher.InvokeShutdown();
                }
            });
            hilo.SetApartmentState(ApartmentState.STA);
            hilo.Start();
            hilo.Join();
            if (error != null)
            {
                throw new AssertFailedException(error.Message, error);
            }
        }
    }
}
