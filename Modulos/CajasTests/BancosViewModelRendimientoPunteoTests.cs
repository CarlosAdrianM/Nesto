using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;
using CommunityToolkit.Mvvm.Input;
using FakeItEasy;
using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.Cajas.Interfaces;
using Nesto.Modulos.Cajas.Models;
using Nesto.Modulos.Cajas.ViewModels;
using Nesto.Modulos.PedidoCompra;
using Prism.Services.Dialogs;
using Unity;

namespace CajasTests
{
    /// <summary>
    /// Nesto#498 (25/09/26): tras «Puntear seleccionados» el mensaje salía enseguida, pero el botón
    /// tardaba varios segundos en volver. Medido: al restaurar la selección siguiente, el botón
    /// «Contabilizar apunte» evaluaba sus reglas (CanExecute) EN EL HILO DE LA UI, y varias reglas
    /// hacen llamadas HTTP síncronas (ReglaPagoProveedor, ReglaGastoPeriodico...). Con la vista
    /// real (DataGrid + SelectionChanged) eran 4 evaluaciones por punteo.
    ///
    /// Estos tests montan el ViewModel con listas realistas y una «vista simulada» que hace lo que
    /// hace el DataGrid: quita de la selección lo que el filtro oculta, avisa de SelectionChanged y
    /// el botón pregunta CanExecute al recibir CanExecuteChanged.
    /// </summary>
    [TestClass]
    public class BancosViewModelRendimientoPunteoTests
    {
        private const int APUNTES_BANCO = 2000;
        private const int APUNTES_CONTABILIDAD = 5000;
        private const int RETARDO_API_MS = 300; // LeerCuentasPorConcepto medido en prod: ~330 ms solo de SQL

        private sealed class Escenario
        {
            public BancosViewModel Sut;
            public int LlamadasApi;
            public ApunteBancarioWrapper BancoPunteado;
            public ContabilidadWrapper ContabilidadPunteada;
            public ApunteBancarioWrapper BancoSiguiente;
            public ContabilidadWrapper ContabilidadSiguiente;
        }

        private static ApunteBancarioDTO ReciboDomiciliado(int id, decimal importe) => new()
        {
            Id = id,
            ConceptoComun = "03",
            ConceptoPropio = "038",
            ImporteMovimiento = importe,
            FechaOperacion = new System.DateTime(2026, 9, 1).AddMinutes(id),
            RegistrosConcepto =
            [
                new RegistroComplementarioConcepto { Concepto = "CORE ENDESA ENERGIA" },
                new RegistroComplementarioConcepto { Concepto = "ES12000B12345678REF" },
                new RegistroComplementarioConcepto { Concepto = "FACTURA LUZ" }
            ]
        };

        private static Escenario Montar()
        {
            var escenario = new Escenario();
            var bancosService = A.Fake<IBancosService>();
            var contabilidadService = A.Fake<IContabilidadService>();
            // Las reglas que consultan la API tardan lo que tarda la API
            A.CallTo(() => contabilidadService.LeerCuentasPorConcepto(A<string>._, A<string>._, A<System.DateTime>._, A<System.DateTime>._))
                .ReturnsLazily(async () => { Interlocked.Increment(ref escenario.LlamadasApi); await Task.Delay(RETARDO_API_MS); return new List<ContabilidadDTO>(); });
            A.CallTo(() => bancosService.LeerProveedorPorNif(A<string>._))
                .ReturnsLazily(async () => { Interlocked.Increment(ref escenario.LlamadasApi); await Task.Delay(RETARDO_API_MS); return string.Empty; });
            A.CallTo(() => bancosService.CrearPunteo(A<int?>._, A<int?>._, A<decimal>._, A<string>._, A<int?>._)).Returns(1);

            var sut = new BancosViewModel(bancosService, contabilidadService, A.Fake<IConfiguracion>(), A.Fake<IDialogService>(),
                A.Fake<IPedidoCompraService>(), A.Fake<IUnityContainer>(), A.Fake<IRecursosHumanosService>());
            var banco = A.Fake<IBancoConciliacion>();
            A.CallTo(() => banco.Banco).Returns(new BancoDTO { Codigo = "TEST01" });
            sut.ListaBancos = [banco];
            sut.BancoSeleccionado = banco;

            sut.ApuntesBanco = new ObservableCollection<ApunteBancarioWrapper>(
                Enumerable.Range(1, APUNTES_BANCO).Select(i => new ApunteBancarioWrapper(ReciboDomiciliado(i, -10 - i))));
            sut.ApuntesContabilidad = new ObservableCollection<ContabilidadWrapper>(
                Enumerable.Range(1, APUNTES_CONTABILIDAD).Select(i => new ContabilidadWrapper(new ContabilidadDTO { Id = 100000 + i, Haber = 10 + i, Concepto = "Recibo " + i })));
            sut.ApuntesBancoCollectionView = CollectionViewSource.GetDefaultView(sut.ApuntesBanco);
            sut.ApuntesContabilidadCollectionView = CollectionViewSource.GetDefaultView(sut.ApuntesContabilidad);
            sut.MostrarSinPuntear = true; // aplica el filtro inicial, como al cargar

            SimularVista(sut);

            // El usuario selecciona la pareja que cuadra (posición 100 en las dos listas)
            escenario.BancoPunteado = sut.ApuntesBanco[99];
            escenario.ContabilidadPunteada = sut.ApuntesContabilidad.Single(c => c.Importe == escenario.BancoPunteado.ImporteMovimiento);
            escenario.BancoSiguiente = sut.ApuntesBanco[100];
            escenario.Sut = sut;
            sut.ApunteBancoSeleccionado = escenario.BancoPunteado;
            sut.ApunteContabilidadSeleccionado = escenario.ContabilidadPunteada;
            var visiblesContabilidad = sut.ApuntesContabilidadCollectionView.Cast<ContabilidadWrapper>().ToList();
            escenario.ContabilidadSiguiente = visiblesContabilidad[visiblesContabilidad.IndexOf(escenario.ContabilidadPunteada) + 1];
            EsperarEvaluacionReglas(sut);
            Interlocked.Exchange(ref escenario.LlamadasApi, 0);
            return escenario;
        }

        /// <summary>Lo que hacen los DataGrid de BancosView.xaml y el botón «Contabilizar apunte».</summary>
        private static void SimularVista(BancosViewModel sut)
        {
            var seleccionBanco = new List<object>();
            var seleccionContabilidad = new List<object>();

            // Botón: al recibir CanExecuteChanged, WPF pregunta CanExecute
            sut.ContabilizarApunteCommand.CanExecuteChanged += (s, e) => sut.ContabilizarApunteCommand.CanExecute(null);
            sut.PuntearApuntesCommand.CanExecuteChanged += (s, e) => sut.PuntearApuntesCommand.CanExecute(null);

            // SelectedItem="{Binding ...}" + EventTrigger SelectionChanged -> Seleccionar...Command(SelectedItems)
            sut.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(BancosViewModel.ApunteBancoSeleccionado))
                {
                    seleccionBanco.Clear();
                    if (sut.ApunteBancoSeleccionado != null) seleccionBanco.Add(sut.ApunteBancoSeleccionado);
                    sut.SeleccionarApuntesBancoCommand.Execute(new List<object>(seleccionBanco));
                }
                else if (e.PropertyName == nameof(BancosViewModel.ApunteContabilidadSeleccionado))
                {
                    seleccionContabilidad.Clear();
                    if (sut.ApunteContabilidadSeleccionado != null) seleccionContabilidad.Add(sut.ApunteContabilidadSeleccionado);
                    sut.SeleccionarApuntesContabilidadCommand.Execute(new List<object>(seleccionContabilidad));
                }
            };

            // Al refiltrar, el DataGrid quita de la selección las filas que desaparecen
            ((INotifyCollectionChanged)sut.ApuntesBancoCollectionView).CollectionChanged += (s, e) =>
            {
                if (sut.ApunteBancoSeleccionado != null && !sut.ApuntesBancoCollectionView.Contains(sut.ApunteBancoSeleccionado))
                {
                    sut.ApunteBancoSeleccionado = null;
                }
            };
            ((INotifyCollectionChanged)sut.ApuntesContabilidadCollectionView).CollectionChanged += (s, e) =>
            {
                if (sut.ApunteContabilidadSeleccionado != null && !sut.ApuntesContabilidadCollectionView.Contains(sut.ApunteContabilidadSeleccionado))
                {
                    sut.ApunteContabilidadSeleccionado = null;
                }
            };
        }

        private static void EsperarEvaluacionReglas(BancosViewModel sut)
        {
            Assert.IsTrue(sut.EvaluacionReglasContabilizacion.Wait(30000), "La evaluación de reglas no terminó");
        }

        private static long PuntearYMedirHastaBotonActivo(Escenario escenario)
        {
            var sut = escenario.Sut;
            var reloj = Stopwatch.StartNew();
            long msBotonActivo = -1;
            PropertyChangedEventHandler handler = (s, e) =>
            {
                if (e.PropertyName == nameof(BancosViewModel.IsBusyPunteando) && !sut.IsBusyPunteando && msBotonActivo < 0)
                {
                    msBotonActivo = reloj.ElapsedMilliseconds;
                }
            };
            sut.PropertyChanged += handler;
            sut.PuntearApuntesCommand.Execute(null);
            SpinWait.SpinUntil(() => msBotonActivo >= 0, 60000);
            sut.PropertyChanged -= handler;
            return msBotonActivo;
        }

        [TestMethod]
        public void PuntearApuntes_ConListasGrandes_ElBotonVuelveSinEsperarALasReglasDeContabilizacion()
        {
            Escenario escenario = Montar();

            long ms = PuntearYMedirHastaBotonActivo(escenario);
            int llamadasHastaBotonActivo = escenario.LlamadasApi;
            System.Console.WriteLine($"Punteo con {APUNTES_BANCO} apuntes de banco y {APUNTES_CONTABILIDAD} de contabilidad: " +
                $"{ms} ms hasta que el botón vuelve; {llamadasHastaBotonActivo} llamadas a la API de reglas ya lanzadas ({RETARDO_API_MS} ms cada una)");

            Assert.IsTrue(ms < RETARDO_API_MS, $"El botón tardó {ms} ms en volver: algo espera a la API en el hilo de la UI");

            // Las reglas se siguen evaluando (en segundo plano) para la selección nueva, y el botón
            // «Contabilizar apunte» refleja el resultado cuando llega.
            EsperarEvaluacionReglas(escenario.Sut);
            System.Console.WriteLine($"Llamadas a la API de reglas en segundo plano tras el punteo: {escenario.LlamadasApi}");
            Assert.IsTrue(escenario.LlamadasApi > 0, "Las reglas tienen que evaluarse para la selección nueva");
            Assert.IsTrue(escenario.LlamadasApi <= 4, $"Antes eran 8 llamadas (4 evaluaciones); ahora {escenario.LlamadasApi}");
            Assert.IsFalse(escenario.Sut.ContabilizarApunteCommand.CanExecute(null), "Ninguna regla admite el recibo: botón gris");
        }

        [TestMethod]
        public void SeleccionarApunte_ConReglasQueConsultanLaApi_NoCongelaLaVentana()
        {
            Escenario escenario = Montar();

            var reloj = Stopwatch.StartNew();
            escenario.Sut.ApunteBancoSeleccionado = escenario.BancoSiguiente; // clic en otra fila
            long ms = reloj.ElapsedMilliseconds;

            Assert.IsTrue(ms < RETARDO_API_MS, $"Seleccionar una fila tardó {ms} ms: las reglas corren en el hilo de la UI");
            EsperarEvaluacionReglas(escenario.Sut);
            Assert.IsTrue(escenario.LlamadasApi > 0, "Las reglas se evalúan igualmente, en segundo plano");
        }

        [TestMethod]
        public void PuntearApuntes_ConListasGrandes_SeleccionaLosSiguientesYOcultaLosPunteados()
        {
            Escenario escenario = Montar();

            PuntearYMedirHastaBotonActivo(escenario);
            EsperarEvaluacionReglas(escenario.Sut);

            var sut = escenario.Sut;
            Assert.AreEqual(EstadoPunteo.CompletamentePunteado, escenario.BancoPunteado.EstadoPunteo);
            Assert.AreEqual(EstadoPunteo.CompletamentePunteado, escenario.ContabilidadPunteada.EstadoPunteo);
            Assert.IsFalse(sut.ApuntesBancoCollectionView.Contains(escenario.BancoPunteado), "Lo punteado se oculta");
            Assert.IsFalse(sut.ApuntesContabilidadCollectionView.Contains(escenario.ContabilidadPunteada), "Lo punteado se oculta");
            Assert.AreEqual(APUNTES_BANCO - 1, sut.ContadorApuntesBanco);
            Assert.AreEqual(APUNTES_CONTABILIDAD - 1, sut.ContadorApuntesContabilidad);
            Assert.AreSame(escenario.BancoSiguiente, sut.ApunteBancoSeleccionado, "Queda seleccionado el siguiente del banco");
            Assert.AreSame(escenario.ContabilidadSiguiente, sut.ApunteContabilidadSeleccionado, "Queda seleccionado el siguiente de contabilidad");
            Assert.IsFalse(sut.IsBusyPunteando);
        }
    }
}
