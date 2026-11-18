using ControlesUsuario.Converters;
using ControlesUsuario.Services;
using ControlesUsuario.ViewModels;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace ControlesUsuario.Tests
{
    /// <summary>
    /// Carlos 18/11/26 (continuación de la sugerencia 1331): cada usuario elige el tamaño de letra del nombre y la dirección del
    /// cliente en el selector (parámetro TamanoLetraSelectorCliente: Normal, Grande, MuyGrande). Normal = como siempre (10).
    /// </summary>
    [TestClass]
    public class SelectorClienteTamanoLetraTests
    {
        [TestInitialize]
        public void Inicializar() => PreferenciaTamanoLetraSelectorCliente.ReiniciarParaPruebas();

        [TestCleanup]
        public void Limpiar() => PreferenciaTamanoLetraSelectorCliente.ReiniciarParaPruebas();

        // ---------- La regla (pura) ----------

        [DataTestMethod]
        [DataRow("Normal", TamanoLetraSelectorCliente.Normal)]
        [DataRow("Grande", TamanoLetraSelectorCliente.Grande)]
        [DataRow("MuyGrande", TamanoLetraSelectorCliente.MuyGrande)]
        [DataRow(" muygrande ", TamanoLetraSelectorCliente.MuyGrande)]
        [DataRow("Muy grande", TamanoLetraSelectorCliente.MuyGrande)]
        [DataRow("GRANDE", TamanoLetraSelectorCliente.Grande)]
        [DataRow(null, TamanoLetraSelectorCliente.Normal)]
        [DataRow("", TamanoLetraSelectorCliente.Normal)]
        [DataRow("14", TamanoLetraSelectorCliente.Normal)]
        [DataRow("Enorme", TamanoLetraSelectorCliente.Normal)]
        public void Regla_DesdeParametro(string valor, TamanoLetraSelectorCliente esperado)
        {
            Assert.AreEqual(esperado, ReglaTamanoLetraSelectorCliente.DesdeParametro(valor));
        }

        [TestMethod]
        public void Regla_AParametro_VaYVuelve()
        {
            foreach (TamanoLetraSelectorCliente tamano in ReglaTamanoLetraSelectorCliente.Todos)
            {
                Assert.AreEqual(tamano, ReglaTamanoLetraSelectorCliente.DesdeParametro(ReglaTamanoLetraSelectorCliente.AParametro(tamano)));
            }
            Assert.AreEqual("Normal", ReglaTamanoLetraSelectorCliente.AParametro(TamanoLetraSelectorCliente.Normal));
            Assert.AreEqual("Grande", ReglaTamanoLetraSelectorCliente.AParametro(TamanoLetraSelectorCliente.Grande));
            Assert.AreEqual("MuyGrande", ReglaTamanoLetraSelectorCliente.AParametro(TamanoLetraSelectorCliente.MuyGrande));
        }

        [TestMethod]
        public void Regla_Puntos_NormalEsElDeSiempreYCrecen()
        {
            Assert.AreEqual(10d, ReglaTamanoLetraSelectorCliente.Puntos(TamanoLetraSelectorCliente.Normal));
            Assert.AreEqual(13d, ReglaTamanoLetraSelectorCliente.Puntos(TamanoLetraSelectorCliente.Grande));
            Assert.AreEqual(16d, ReglaTamanoLetraSelectorCliente.Puntos(TamanoLetraSelectorCliente.MuyGrande));
        }

        [TestMethod]
        public void Regla_Descripciones_ConEspacio()
        {
            Assert.AreEqual("Muy grande", ReglaTamanoLetraSelectorCliente.Descripcion(TamanoLetraSelectorCliente.MuyGrande));
            Assert.AreEqual(3, ReglaTamanoLetraSelectorCliente.Todos.Count);
        }

        // ---------- La preferencia de la sesión ----------

        [TestMethod]
        public async Task Preferencia_CargarAsync_LeeElParametroDeLaEmpresaPorDefecto()
        {
            var cfg = A.Fake<IConfiguracion>();
            A.CallTo(() => cfg.leerParametro("1", Parametros.Claves.TamanoLetraSelectorCliente)).Returns(Task.FromResult("MuyGrande"));
            int avisos = 0;
            PreferenciaTamanoLetraSelectorCliente.Cambiado += (s, e) => avisos++;

            await PreferenciaTamanoLetraSelectorCliente.CargarAsync(cfg);

            Assert.AreEqual(TamanoLetraSelectorCliente.MuyGrande, PreferenciaTamanoLetraSelectorCliente.Actual);
            Assert.AreEqual(1, avisos);
        }

        [TestMethod]
        public async Task Preferencia_CargarAsync_SinFilaONoSeEntiende_Normal_YNoLanza()
        {
            var cfg = A.Fake<IConfiguracion>();
            A.CallTo(() => cfg.leerParametro(A<string>._, A<string>._)).ThrowsAsync(new Exception("No se puede leer el parámetro"));
            await PreferenciaTamanoLetraSelectorCliente.CargarAsync(cfg);
            Assert.AreEqual(TamanoLetraSelectorCliente.Normal, PreferenciaTamanoLetraSelectorCliente.Actual);

            A.CallTo(() => cfg.leerParametro(A<string>._, A<string>._)).Returns(Task.FromResult("Gigante"));
            await PreferenciaTamanoLetraSelectorCliente.CargarAsync(cfg);
            Assert.AreEqual(TamanoLetraSelectorCliente.Normal, PreferenciaTamanoLetraSelectorCliente.Actual);

            await PreferenciaTamanoLetraSelectorCliente.CargarAsync(null);
            Assert.AreEqual(TamanoLetraSelectorCliente.Normal, PreferenciaTamanoLetraSelectorCliente.Actual);
        }

        [TestMethod]
        public void Preferencia_Establecer_ElMismoValor_NoAvisa()
        {
            int avisos = 0;
            PreferenciaTamanoLetraSelectorCliente.Cambiado += (s, e) => avisos++;
            PreferenciaTamanoLetraSelectorCliente.Establecer(TamanoLetraSelectorCliente.Normal);
            Assert.AreEqual(0, avisos);
            PreferenciaTamanoLetraSelectorCliente.Establecer(TamanoLetraSelectorCliente.Grande);
            Assert.AreEqual(1, avisos);
        }

        // ---------- El ViewModel (la ruedita) ----------

        [TestMethod]
        public void ViewModel_CambiarElTamano_LoAplicaALaSesionYLoGuardaEnSuParametro()
        {
            var cfg = A.Fake<IConfiguracion>();
            var vm = new SelectorClienteViewModel(cfg, A.Fake<ISelectorClienteService>());
            Assert.AreEqual(TamanoLetraSelectorCliente.Normal, vm.TamanoNombreYDireccionUsuario);

            vm.TamanoNombreYDireccionUsuario = TamanoLetraSelectorCliente.Grande;

            Assert.AreEqual(TamanoLetraSelectorCliente.Grande, PreferenciaTamanoLetraSelectorCliente.Actual);
            A.CallTo(() => cfg.GuardarParametro("1", Parametros.Claves.TamanoLetraSelectorCliente, "Grande")).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void ViewModel_ElMismoTamano_NoGuardaNada()
        {
            var cfg = A.Fake<IConfiguracion>();
            var vm = new SelectorClienteViewModel(cfg, A.Fake<ISelectorClienteService>());
            vm.TamanoNombreYDireccionUsuario = TamanoLetraSelectorCliente.Normal;
            A.CallTo(() => cfg.GuardarParametro(A<string>._, A<string>._, A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public void ViewModel_SiNoSePuedeGuardar_NoLanza_YLaSesionLoTieneIgual()
        {
            var cfg = A.Fake<IConfiguracion>();
            A.CallTo(() => cfg.GuardarParametro(A<string>._, A<string>._, A<string>._)).ThrowsAsync(new Exception("sin red"));
            var vm = new SelectorClienteViewModel(cfg, A.Fake<ISelectorClienteService>());
            vm.TamanoNombreYDireccionUsuario = TamanoLetraSelectorCliente.MuyGrande;
            Assert.AreEqual(TamanoLetraSelectorCliente.MuyGrande, PreferenciaTamanoLetraSelectorCliente.Actual);
        }

        [TestMethod]
        public void IgualAConverter_ParaLosRadioButton()
        {
            var conv = new IgualAConverter();
            Assert.AreEqual(true, conv.Convert(TamanoLetraSelectorCliente.Grande, typeof(bool?), TamanoLetraSelectorCliente.Grande, null));
            Assert.AreEqual(false, conv.Convert(TamanoLetraSelectorCliente.Normal, typeof(bool?), TamanoLetraSelectorCliente.Grande, null));
            Assert.AreEqual(TamanoLetraSelectorCliente.Grande, conv.ConvertBack(true, typeof(TamanoLetraSelectorCliente), TamanoLetraSelectorCliente.Grande, null));
            Assert.AreSame(Binding.DoNothing, conv.ConvertBack(false, typeof(TamanoLetraSelectorCliente), TamanoLetraSelectorCliente.Grande, null));
        }

        // ---------- El control (la DependencyProperty) ----------

        [TestMethod]
        public void DP_PorDefectoNull_EsDecirElDelUsuario()
        {
            Assert.IsNull(SelectorCliente.TamanoNombreYDireccionProperty.DefaultMetadata.DefaultValue);
        }

        [TestMethod]
        public void Control_SinTocarNada_SeVeComoSiempre_A10()
        {
            EnSta(() =>
            {
                var sut = CrearSelector();
                Assert.AreEqual(TamanoLetraSelectorCliente.Normal, sut.TamanoNombreYDireccionEfectivo);
                Assert.AreEqual(10d, sut.txtNombreCliente.FontSize);
                Assert.AreEqual(10d, sut.txtDireccionCliente.FontSize);
                Assert.AreEqual(TextTrimming.CharacterEllipsis, sut.txtNombreCliente.TextTrimming);
                Assert.AreEqual(TextWrapping.NoWrap, sut.txtDireccionCliente.TextWrapping);
            });
        }

        [TestMethod]
        public void Control_ConLaPreferenciaDelUsuario_NaceConEseTamano()
        {
            EnSta(() =>
            {
                PreferenciaTamanoLetraSelectorCliente.Establecer(TamanoLetraSelectorCliente.MuyGrande);
                var sut = CrearSelector();
                Assert.AreEqual(16d, sut.txtNombreCliente.FontSize);
                Assert.AreEqual(16d, sut.txtDireccionCliente.FontSize);
            });
        }

        [TestMethod]
        public void Control_LaPantallaFijaSuTamano_ManaSobreElDelUsuario()
        {
            EnSta(() =>
            {
                PreferenciaTamanoLetraSelectorCliente.Establecer(TamanoLetraSelectorCliente.MuyGrande);
                var sut = CrearSelector();
                sut.TamanoNombreYDireccion = TamanoLetraSelectorCliente.Grande;
                Assert.AreEqual(13d, sut.txtNombreCliente.FontSize);

                sut.TamanoNombreYDireccion = null;
                Assert.AreEqual(16d, sut.txtNombreCliente.FontSize);
            });
        }

        [TestMethod]
        public void Control_Cargado_CambiaAlMomentoSiCambiaLaPreferencia_YDescargadoDejaDeEscuchar()
        {
            EnSta(() =>
            {
                var sut = CrearSelector();
                sut.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));

                PreferenciaTamanoLetraSelectorCliente.Establecer(TamanoLetraSelectorCliente.Grande);
                Assert.AreEqual(13d, sut.txtNombreCliente.FontSize);
                Assert.AreEqual(13d, sut.txtDireccionCliente.FontSize);

                sut.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
                PreferenciaTamanoLetraSelectorCliente.Establecer(TamanoLetraSelectorCliente.MuyGrande);
                Assert.AreEqual(13d, sut.txtNombreCliente.FontSize, "Descargado no escucha");

                // Al volver a cargarse se pone al día
                sut.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
                Assert.AreEqual(16d, sut.txtNombreCliente.FontSize);
                sut.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
            });
        }

        private static SelectorCliente CrearSelector()
        {
            var vm = new SelectorClienteViewModel(A.Fake<IConfiguracion>(), A.Fake<ISelectorClienteService>());
            var sut = new SelectorCliente(vm, A.Fake<IServicioNavegacion>());
            sut.Measure(new Size(400, 400));
            sut.Arrange(new Rect(0, 0, 400, 400));
            return sut;
        }

        private static void EnSta(Action prueba)
        {
            Exception error = null;
            var hilo = new Thread(() =>
            {
                try
                {
                    prueba();
                }
                catch (Exception ex)
                {
                    error = ex;
                }
            });
            hilo.SetApartmentState(ApartmentState.STA);
            hilo.Start();
            if (!hilo.Join(TimeSpan.FromSeconds(30)))
            {
                Assert.Fail("La prueba no ha terminado");
            }
            if (error != null)
            {
                throw new AssertFailedException(error.ToString(), error);
            }
        }
    }
}
