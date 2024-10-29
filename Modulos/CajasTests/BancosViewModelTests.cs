using ControlesUsuario.Dialogs;
using FakeItEasy;
using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.Cajas;
using Nesto.Modulos.Cajas.Models;
using Nesto.Modulos.Cajas.ViewModels;
using Nesto.Modulos.PedidoCompra;
using Prism.Commands;
using Prism.Events;
using Prism.Services.Dialogs;
using System.Collections.ObjectModel;
using Unity;

namespace CajasTests
{
    [TestClass]
    public class BancosViewModelTests
    {
        [TestMethod]
        public void BancosViewModel_DescuadreSaldoInicial_MuestraLaDiferenciaEntreElSaldoInicialDelBancoYElDeLaCaja()
        {
            var _bancosService = A.Fake<IBancosService>();
            var _contabilidadService = A.Fake<IContabilidadService>();
            var _configuracion = A.Fake<IConfiguracion>();
            var _dialogService = A.Fake<IDialogService>();
            var _pedidoCompraService = A.Fake<IPedidoCompraService>();
            var _container = A.Fake<IUnityContainer>();
            var _recursosHumanosService = A.Fake<IRecursosHumanosService>();
            var sut = new BancosViewModel(_bancosService, _contabilidadService, _configuracion, _dialogService, _pedidoCompraService, _container, _recursosHumanosService);
            
            sut.ContenidoCuaderno43 = new ContenidoCuaderno43()
            {
                Cabecera = new RegistroCabeceraCuenta
                {
                    ClaveDebeOHaber = "2",
                    ImporteSaldoInicial = 10M
                }
            };
            sut.SaldoInicialContabilidad = 9M;

            Assert.AreEqual(1, sut.DescuadreSaldoInicial);
        }

        [TestMethod]
        public void BancosViewModel_DescuadreSaldoFinal_MuestraLaDiferenciaEntreElSaldoFinalDelBancoYElDeLaCaja()
        {
            var _bancosService = A.Fake<IBancosService>();
            var _contabilidadService = A.Fake<IContabilidadService>();
            var _configuracion = A.Fake<IConfiguracion>();
            var _dialogService = A.Fake<IDialogService>();
            var _pedidoCompraService = A.Fake<IPedidoCompraService>();
            var _container = A.Fake<IUnityContainer>();
            var _recursosHumanosService = A.Fake<IRecursosHumanosService>();
            var sut = new BancosViewModel(_bancosService, _contabilidadService, _configuracion, _dialogService, _pedidoCompraService, _container, _recursosHumanosService);

            sut.ContenidoCuaderno43 = new ContenidoCuaderno43()
            {
                FinalCuenta = new RegistroFinalCuenta
                {
                    CodigoSaldoFinal = "2",
                    SaldoFinal = 10M
                }
            };
            sut.SaldoInicialContabilidad = 8M;
            sut.ApuntesContabilidad = new ObservableCollection<ContabilidadWrapper> { new ContabilidadWrapper(new ContabilidadDTO() { Debe = 1M }) };

            Assert.AreEqual(1, sut.DescuadreSaldoFinal);
        }

        [TestMethod]
        public void BancosViewModel_DescuadrePunteados_CalculaLaDiferenciaEntreLoSeleccionadoEnLaContabilidadYEnElBanco()
        {
            var _bancosService = A.Fake<IBancosService>();
            var _contabilidadService = A.Fake<IContabilidadService>();
            var _configuracion = A.Fake<IConfiguracion>();
            var _dialogService = A.Fake<IDialogService>();
            var _pedidoCompraService = A.Fake<IPedidoCompraService>();
            var _container = A.Fake<IUnityContainer>();
            var _recursosHumanosService = A.Fake<IRecursosHumanosService>();
            var sut = new BancosViewModel(_bancosService, _contabilidadService, _configuracion, _dialogService, _pedidoCompraService, _container, _recursosHumanosService);

            IList<ApunteBancarioWrapper> apuntesBancoSeleccionados = new List<ApunteBancarioWrapper> {
                new ApunteBancarioWrapper(
                    new ApunteBancarioDTO()
                    {
                        ImporteMovimiento = 100
                    }
                )
            };
            IList<ContabilidadWrapper> apuntesContabilidadSeleccionados = new List<ContabilidadWrapper>
            {
                new ContabilidadWrapper(
                    new ContabilidadDTO()
                    {
                        Debe = 70
                    }
                )
            };

            sut.SeleccionarApuntesBancoCommand.Execute(apuntesBancoSeleccionados);
            sut.SeleccionarApuntesContabilidadCommand.Execute(apuntesContabilidadSeleccionados);

            Assert.AreEqual(30, sut.DescuadrePunteo);
        }

        [TestMethod]
        public void BancosViewModel_DescuadrePunteados_CalculaLaDiferenciaEntreLoSeleccionadoEnLaContabilidadYEnElBancoPeroSoloTieneEnCuentaLoNoPunteado()
        {
            var _bancosService = A.Fake<IBancosService>();
            var _contabilidadService = A.Fake<IContabilidadService>();
            var _configuracion = A.Fake<IConfiguracion>();
            var _dialogService = A.Fake<IDialogService>();
            var _pedidoCompraService = A.Fake<IPedidoCompraService>();
            var _container = A.Fake<IUnityContainer>();
            var _recursosHumanosService = A.Fake<IRecursosHumanosService>();
            var sut = new BancosViewModel(_bancosService, _contabilidadService, _configuracion, _dialogService, _pedidoCompraService, _container, _recursosHumanosService);

            IList<ApunteBancarioWrapper> apuntesBancoSeleccionados = new List<ApunteBancarioWrapper> {
                new ApunteBancarioWrapper(
                    new ApunteBancarioDTO()
                    {
                        ImporteMovimiento = 100
                    }
                ),
                new ApunteBancarioWrapper(
                    new ApunteBancarioDTO()
                    {
                        ImporteMovimiento = 10,
                        EstadoPunteo = EstadoPunteo.CompletamentePunteado
                    }
                )
            };
            IList<ContabilidadWrapper> apuntesContabilidadSeleccionados = new List<ContabilidadWrapper>
            {
                new ContabilidadWrapper(
                    new ContabilidadDTO()
                    {
                        Debe = 70, 
                        EstadoPunteo = EstadoPunteo.CompletamentePunteado
                    }
                )
            };

            sut.SeleccionarApuntesBancoCommand.Execute(apuntesBancoSeleccionados);
            sut.SeleccionarApuntesContabilidadCommand.Execute(apuntesContabilidadSeleccionados);

            Assert.AreEqual(100, sut.DescuadrePunteo);
        }

        [TestMethod]
        public void BancosViewModel_PuntearApuntes_DespuesDePuntearAmbosMovimientosQuedanEnEstadoCompletamentePunteados()
        {
            //Assert
            var _bancosService = A.Fake<IBancosService>();
            var _contabilidadService = A.Fake<IContabilidadService>();
            var _configuracion = A.Fake<IConfiguracion>();
            var _dialogService = A.Fake<IDialogService>();
            var _pedidoCompraService = A.Fake<IPedidoCompraService>();
            var _container = A.Fake<IUnityContainer>();
            A.CallTo(() => _bancosService.CrearPunteo(1, 2, 100, "*", null)).Returns(3);
            var _recursosHumanosService = A.Fake<IRecursosHumanosService>();
            var sut = new BancosViewModel(_bancosService, _contabilidadService, _configuracion, _dialogService, _pedidoCompraService, _container, _recursosHumanosService);

            var apunteBanco = new ApunteBancarioDTO()
            {
                Id = 1,
                ImporteMovimiento = 100
            };
            var apunteContabilidad = new ContabilidadDTO()
            {
                Id = 2,
                Debe = 100
            };

            sut.ApuntesBanco = new ObservableCollection<ApunteBancarioWrapper>
            {
                new ApunteBancarioWrapper(apunteBanco)
            };
            sut.ApuntesContabilidad = new ObservableCollection<ContabilidadWrapper>
            {
                new ContabilidadWrapper(apunteContabilidad)
            };
            sut.ApuntesBancoSeleccionados = sut.ApuntesBanco.ToList();
            sut.ApuntesContabilidadSeleccionados = sut.ApuntesContabilidad.ToList();

            // Act
            sut.PuntearApuntesCommand.Execute(null);

            // Arrange
            Assert.AreEqual(EstadoPunteo.CompletamentePunteado, sut.ApuntesBanco.Single().EstadoPunteo);
            Assert.AreEqual(EstadoPunteo.CompletamentePunteado, sut.ApuntesContabilidad.Single().EstadoPunteo);
        }

        [TestMethod]
        public void BancosViewModel_PuntearApuntes_SiHayDosApuntesDelMismoTipoQuedanConElMismoGrupo()
        {
            //Assert
            var _bancosService = A.Fake<IBancosService>();
            var _contabilidadService = A.Fake<IContabilidadService>();
            var _configuracion = A.Fake<IConfiguracion>();
            var _dialogService = A.Fake<IDialogService>();
            var _pedidoCompraService = A.Fake<IPedidoCompraService>();
            A.CallTo(() => _bancosService.CrearPunteo(null, 1, 100, "*", 1)).Returns(3);
            A.CallTo(() => _bancosService.CrearPunteo(null, 2, 100, "*", 1)).Returns(4);
            var _container = A.Fake<IUnityContainer>();
            var _recursosHumanosService = A.Fake<IRecursosHumanosService>();
            var sut = new BancosViewModel(_bancosService, _contabilidadService, _configuracion, _dialogService, _pedidoCompraService, _container, _recursosHumanosService);

            var apunteContabilidad1 = new ContabilidadDTO()
            {
                Id = 1,
                Debe = 100
            };
            var apunteContabilidad2 = new ContabilidadDTO()
            {
                Id = 2,
                Haber = 100
            };
            sut.ApuntesBanco = new ObservableCollection<ApunteBancarioWrapper>();
            sut.ApuntesContabilidad = new ObservableCollection<ContabilidadWrapper>
            {
                new ContabilidadWrapper(apunteContabilidad1),
                new ContabilidadWrapper(apunteContabilidad2)
            };
            sut.ApuntesBancoSeleccionados = sut.ApuntesBanco.ToList();
            sut.ApuntesContabilidadSeleccionados = sut.ApuntesContabilidad.ToList();

            // Act
            sut.PuntearApuntesCommand.Execute(null);

            // Arrange
            A.CallTo(() => _bancosService.CrearPunteo(null, 1, 100, "*", 1)).MustHaveHappenedOnceExactly();
            A.CallTo(() => _bancosService.CrearPunteo(null, 2, -100, "*", 1)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void BancosViewModel_PuntearApuntes_SiHayTresApuntesDelMismoTipoCogenElGrupoDelMovimientoDeMayorImporte()
        {
            //Assert
            var _bancosService = A.Fake<IBancosService>();
            var _contabilidadService = A.Fake<IContabilidadService>();
            var _configuracion = A.Fake<IConfiguracion>();
            var _dialogService = A.Fake<IDialogService>();
            var _pedidoCompraService = A.Fake<IPedidoCompraService>();
            A.CallTo(() => _bancosService.CrearPunteo(null, 1, 100, "*", 2)).Returns(3);
            A.CallTo(() => _bancosService.CrearPunteo(null, 2, 250, "*", 2)).Returns(4);
            A.CallTo(() => _bancosService.CrearPunteo(null, 3, 150, "*", 2)).Returns(5);
            var _container = A.Fake<IUnityContainer>();
            var _recursosHumanosService = A.Fake<IRecursosHumanosService>();
            var sut = new BancosViewModel(_bancosService, _contabilidadService, _configuracion, _dialogService, _pedidoCompraService, _container, _recursosHumanosService);

            var apunteContabilidad1 = new ContabilidadDTO()
            {
                Id = 1,
                Haber = 100
            };
            var apunteContabilidad2 = new ContabilidadDTO()
            {
                Id = 2,
                Debe = 250
            };
            var apunteContabilidad3 = new ContabilidadDTO()
            {
                Id = 3,
                Haber = 150
            };
            sut.ApuntesBanco = new ObservableCollection<ApunteBancarioWrapper>();
            sut.ApuntesContabilidad = new ObservableCollection<ContabilidadWrapper>
            {
                new ContabilidadWrapper(apunteContabilidad1),
                new ContabilidadWrapper(apunteContabilidad2),
                new ContabilidadWrapper(apunteContabilidad3)
            };
            sut.ApuntesBancoSeleccionados = sut.ApuntesBanco.ToList();
            sut.ApuntesContabilidadSeleccionados = sut.ApuntesContabilidad.ToList();

            // Act
            sut.PuntearApuntesCommand.Execute(null);

            // Arrange
            A.CallTo(() => _bancosService.CrearPunteo(null, 1, -100, "*", 2)).MustHaveHappenedOnceExactly();
            A.CallTo(() => _bancosService.CrearPunteo(null, 2, 250, "*", 2)).MustHaveHappenedOnceExactly();
            A.CallTo(() => _bancosService.CrearPunteo(null, 3, -150, "*", 2)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void BancosViewModel_PuntearApuntes_SiHayTresApuntesHaceDosLlamadasAlServicio()
        {
            //Assert
            var _bancosService = A.Fake<IBancosService>();
            var _contabilidadService = A.Fake<IContabilidadService>();
            var _configuracion = A.Fake<IConfiguracion>();
            var _dialogService = A.Fake<IDialogService>();
            var _pedidoCompraService = A.Fake<IPedidoCompraService>();
            A.CallTo(() => _bancosService.CrearPunteo(1, 2, 70, "*", null)).Returns(4);
            A.CallTo(() => _bancosService.CrearPunteo(1, 3, 30, "*", null)).Returns(5);
            var _container = A.Fake<IUnityContainer>();
            var _recursosHumanosService = A.Fake<IRecursosHumanosService>();
            var sut = new BancosViewModel(_bancosService, _contabilidadService, _configuracion, _dialogService, _pedidoCompraService, _container, _recursosHumanosService);

            var apunteBanco = new ApunteBancarioDTO()
            {
                Id = 1,
                ImporteMovimiento = 100
            };
            var apunteContabilidad = new ContabilidadDTO()
            {
                Id = 2,
                Debe = 70
            };

            var apunteContabilidad2 = new ContabilidadDTO()
            {
                Id = 3,
                Debe = 30
            };

            sut.ApuntesBanco = new ObservableCollection<ApunteBancarioWrapper>
            {
                new ApunteBancarioWrapper(apunteBanco)
            };
            sut.ApuntesContabilidad = new ObservableCollection<ContabilidadWrapper>
            {
                new ContabilidadWrapper(apunteContabilidad),
                new ContabilidadWrapper(apunteContabilidad2)
            };
            sut.ApuntesBancoSeleccionados = sut.ApuntesBanco.ToList();
            sut.ApuntesContabilidadSeleccionados = sut.ApuntesContabilidad.ToList();

            // Act
            sut.PuntearApuntesCommand.Execute(null);

            // Arrange
            A.CallTo(() => _bancosService.CrearPunteo(1, 2, 70, "*", null)).MustHaveHappenedOnceExactly();
            A.CallTo(() => _bancosService.CrearPunteo(1, 3, 30, "*", null)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void BancosViewModel_PuntearApuntes_SiHayTresApuntesNegativosHaceDosLlamadasAlServicio()
        {
            //Assert
            var _bancosService = A.Fake<IBancosService>();
            var _contabilidadService = A.Fake<IContabilidadService>();
            var _configuracion = A.Fake<IConfiguracion>();
            var _dialogService = A.Fake<IDialogService>();
            var _pedidoCompraService = A.Fake<IPedidoCompraService>();
            A.CallTo(() => _bancosService.CrearPunteo(1, 2, -70, "*", null)).Returns(4);
            A.CallTo(() => _bancosService.CrearPunteo(1, 3, -30, "*", null)).Returns(5);
            var _container = A.Fake<IUnityContainer>();
            var _recursosHumanosService = A.Fake<IRecursosHumanosService>();
            var sut = new BancosViewModel(_bancosService, _contabilidadService, _configuracion, _dialogService, _pedidoCompraService, _container, _recursosHumanosService);

            var apunteBanco = new ApunteBancarioDTO()
            {
                Id = 1,
                ImporteMovimiento = -100
            };
            var apunteContabilidad = new ContabilidadDTO()
            {
                Id = 2,
                Debe = -70
            };

            var apunteContabilidad2 = new ContabilidadDTO()
            {
                Id = 3,
                Debe = -30
            };

            sut.ApuntesBanco = new ObservableCollection<ApunteBancarioWrapper>
            {
                new ApunteBancarioWrapper(apunteBanco)
            };
            sut.ApuntesContabilidad = new ObservableCollection<ContabilidadWrapper>
            {
                new ContabilidadWrapper(apunteContabilidad),
                new ContabilidadWrapper(apunteContabilidad2)
            };
            sut.ApuntesBancoSeleccionados = sut.ApuntesBanco.ToList();
            sut.ApuntesContabilidadSeleccionados = sut.ApuntesContabilidad.ToList();

            // Act
            sut.PuntearApuntesCommand.Execute(null);

            // Arrange
            A.CallTo(() => _bancosService.CrearPunteo(1, 2, -70, "*", null)).MustHaveHappenedOnceExactly();
            A.CallTo(() => _bancosService.CrearPunteo(1, 3, -30, "*", null)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void BancosViewModel_PuntearApuntes_SiHayTresApuntesPositivosYNegativosHaceDosLlamadasAlServicio()
        {
            //Assert
            var _bancosService = A.Fake<IBancosService>();
            var _contabilidadService = A.Fake<IContabilidadService>();
            var _configuracion = A.Fake<IConfiguracion>();
            var _dialogService = A.Fake<IDialogService>();
            var _pedidoCompraService = A.Fake<IPedidoCompraService>();
            A.CallTo(() => _bancosService.CrearPunteo(1, 2, -170, "*", null)).Returns(4);
            A.CallTo(() => _bancosService.CrearPunteo(1, 3, 70, "*", null)).Returns(5);
            var _container = A.Fake<IUnityContainer>();
            var _recursosHumanosService = A.Fake<IRecursosHumanosService>();
            var sut = new BancosViewModel(_bancosService, _contabilidadService, _configuracion, _dialogService, _pedidoCompraService, _container, _recursosHumanosService);

            var apunteBanco = new ApunteBancarioDTO()
            {
                Id = 1,
                ImporteMovimiento = -100
            };
            var apunteContabilidad = new ContabilidadDTO()
            {
                Id = 2,
                Debe = -170
            };

            var apunteContabilidad2 = new ContabilidadDTO()
            {
                Id = 3,
                Debe = 70
            };

            sut.ApuntesBanco = new ObservableCollection<ApunteBancarioWrapper>
            {
                new ApunteBancarioWrapper(apunteBanco)
            };
            sut.ApuntesContabilidad = new ObservableCollection<ContabilidadWrapper>
            {
                new ContabilidadWrapper(apunteContabilidad),
                new ContabilidadWrapper(apunteContabilidad2)
            };
            sut.ApuntesBancoSeleccionados = sut.ApuntesBanco.ToList();
            sut.ApuntesContabilidadSeleccionados = sut.ApuntesContabilidad.ToList();

            // Act
            sut.PuntearApuntesCommand.Execute(null);

            // Arrange
            A.CallTo(() => _bancosService.CrearPunteo(1, 2, -170, "*", null)).MustHaveHappenedOnceExactly();
            A.CallTo(() => _bancosService.CrearPunteo(1, 3, 70, "*", null)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void BancosViewModel_PuntearApuntes_SiElPrimerMovimientoEsNegativoLoSumaAlImporteRestante()
        {
            //Assert
            var _bancosService = A.Fake<IBancosService>();
            var _contabilidadService = A.Fake<IContabilidadService>();
            var _configuracion = A.Fake<IConfiguracion>();
            var _dialogService = A.Fake<IDialogService>();
            var _pedidoCompraService = A.Fake<IPedidoCompraService>();
            A.CallTo(() => _bancosService.CrearPunteo(1, 2, -1, "*", null)).Returns(4);
            A.CallTo(() => _bancosService.CrearPunteo(1, 3, 11, "*", null)).Returns(5);
            var _container = A.Fake<IUnityContainer>();
            var _recursosHumanosService = A.Fake<IRecursosHumanosService>();
            var sut = new BancosViewModel(_bancosService, _contabilidadService, _configuracion, _dialogService, _pedidoCompraService, _container, _recursosHumanosService);

            var apunteBanco = new ApunteBancarioDTO()
            {
                Id = 1,
                ImporteMovimiento = 10
            };
            var apunteContabilidad = new ContabilidadDTO()
            {
                Id = 2,
                Debe = -1
            };

            var apunteContabilidad2 = new ContabilidadDTO()
            {
                Id = 3,
                Debe = 11
            };
                        
            sut.ApuntesBanco = new ObservableCollection<ApunteBancarioWrapper>
            {
                new ApunteBancarioWrapper(apunteBanco)
            };
            sut.ApuntesContabilidad = new ObservableCollection<ContabilidadWrapper>
            {
                new ContabilidadWrapper(apunteContabilidad),
                new ContabilidadWrapper(apunteContabilidad2)
            };
            sut.ApuntesBancoSeleccionados = sut.ApuntesBanco.ToList();
            sut.ApuntesContabilidadSeleccionados = sut.ApuntesContabilidad.ToList();

            // Act
            sut.PuntearApuntesCommand.Execute(null);

            // Arrange
            A.CallTo(() => _bancosService.CrearPunteo(1, 2, -1, "*", null)).MustHaveHappenedOnceExactly();
            A.CallTo(() => _bancosService.CrearPunteo(1, 3, 11, "*", null)).MustHaveHappenedOnceExactly();            
        }

        [TestMethod]
        public void BancosViewModel_PuntearApuntes_SiHayDosApuntesDeCadaHaceTresLlamadasAlServicio()
        {
            //Assert
            var _bancosService = A.Fake<IBancosService>();
            var _contabilidadService = A.Fake<IContabilidadService>();
            var _configuracion = A.Fake<IConfiguracion>();
            var _dialogService = A.Fake<IDialogService>();
            var _pedidoCompraService = A.Fake<IPedidoCompraService>();
            A.CallTo(() => _bancosService.CrearPunteo(1, 3, 60, "*", null)).Returns(4);
            A.CallTo(() => _bancosService.CrearPunteo(1, 4, 10, "*", null)).Returns(5);
            A.CallTo(() => _bancosService.CrearPunteo(2, 4, 30, "*", null)).Returns(6);
            var _container = A.Fake<IUnityContainer>();
            var _recursosHumanosService = A.Fake<IRecursosHumanosService>();
            var sut = new BancosViewModel(_bancosService, _contabilidadService, _configuracion, _dialogService, _pedidoCompraService, _container, _recursosHumanosService);

            var apunteBanco = new ApunteBancarioDTO()
            {
                Id = 1,
                ImporteMovimiento = 70
            };
            var apunteBanco2 = new ApunteBancarioDTO()
            {
                Id = 2,
                ImporteMovimiento = 30
            };
            var apunteContabilidad = new ContabilidadDTO()
            {
                Id = 3,
                Debe = 60
            };

            var apunteContabilidad2 = new ContabilidadDTO()
            {
                Id = 4,
                Debe = 40
            };

            sut.ApuntesBanco = new ObservableCollection<ApunteBancarioWrapper>
            {
                new ApunteBancarioWrapper(apunteBanco),
                new ApunteBancarioWrapper(apunteBanco2)
            };
            sut.ApuntesContabilidad = new ObservableCollection<ContabilidadWrapper>
            {
                new ContabilidadWrapper(apunteContabilidad),
                new ContabilidadWrapper(apunteContabilidad2)
            };
            sut.ApuntesBancoSeleccionados = sut.ApuntesBanco.ToList();
            sut.ApuntesContabilidadSeleccionados = sut.ApuntesContabilidad.ToList();

            // Act
            sut.PuntearApuntesCommand.Execute(null);

            // Arrange
            A.CallTo(() => _bancosService.CrearPunteo(1, 3, 60, "*", null)).MustHaveHappenedOnceExactly();
            A.CallTo(() => _bancosService.CrearPunteo(1, 4, 10, "*", null)).MustHaveHappenedOnceExactly();
            A.CallTo(() => _bancosService.CrearPunteo(2, 4, 30, "*", null)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void BancosViewModel_PuntearApuntes_SiNoHayNingunoSeleccionadoNoSePuedePuntear()
        {
            //Assert
            var _bancosService = A.Fake<IBancosService>();
            var _contabilidadService = A.Fake<IContabilidadService>();
            var _configuracion = A.Fake<IConfiguracion>();
            var _dialogService = A.Fake<IDialogService>();
            var _pedidoCompraService = A.Fake<IPedidoCompraService>();
            A.CallTo(() => _bancosService.CrearPunteo(1, 2, A<decimal>._, "*", null)).Returns(3);
            var _container = A.Fake<IUnityContainer>();
            var _recursosHumanosService = A.Fake<IRecursosHumanosService>();
            var sut = new BancosViewModel(_bancosService, _contabilidadService, _configuracion, _dialogService, _pedidoCompraService, _container, _recursosHumanosService);

            sut.ApuntesBanco = new ObservableCollection<ApunteBancarioWrapper>();
            sut.ApuntesContabilidad = new ObservableCollection<ContabilidadWrapper>();
            sut.ApuntesBancoSeleccionados = sut.ApuntesBanco.ToList();
            sut.ApuntesContabilidadSeleccionados = sut.ApuntesContabilidad.ToList();

            // Act
            ((DelegateCommand)sut.PuntearApuntesCommand).RaiseCanExecuteChanged();

            // Arrange
            Assert.IsFalse(sut.PuntearApuntesCommand.CanExecute(null));
        }

        [TestMethod]
        public void BancosViewModel_PuntearApuntes_SiHaySeleccionadosApuntesQueSumanCeroSiSePuedePuntear()
        {
            //Assert
            var _bancosService = A.Fake<IBancosService>();
            var _contabilidadService = A.Fake<IContabilidadService>();
            var _configuracion = A.Fake<IConfiguracion>();
            var _dialogService = A.Fake<IDialogService>();
            var _pedidoCompraService = A.Fake<IPedidoCompraService>();
            A.CallTo(() => _bancosService.CrearPunteo(1, 2, A<decimal>._, "*", null)).Returns(3);
            var _container = A.Fake<IUnityContainer>();
            var _recursosHumanosService = A.Fake<IRecursosHumanosService>();
            var sut = new BancosViewModel(_bancosService, _contabilidadService, _configuracion, _dialogService, _pedidoCompraService, _container, _recursosHumanosService);

            sut.ApuntesBanco = new ObservableCollection<ApunteBancarioWrapper>();
            sut.ApuntesContabilidad = new ObservableCollection<ContabilidadWrapper>()
            {
                new ContabilidadWrapper(new ContabilidadDTO
                {
                    Debe = 10
                }),
                new ContabilidadWrapper(new ContabilidadDTO
                {
                    Haber = 10
                })
            };
            sut.ApuntesBancoSeleccionados = sut.ApuntesBanco.ToList();
            sut.ApuntesContabilidadSeleccionados = sut.ApuntesContabilidad.ToList();

            // Act
            ((DelegateCommand)sut.PuntearApuntesCommand).RaiseCanExecuteChanged();

            // Arrange
            Assert.IsTrue(sut.PuntearApuntesCommand.CanExecute(null));
        }

        [TestMethod]
        public void BancosViewModel_PuntearApuntes_LosApuntesCompletamentePunteadosNoSePunteanDeNuevo()
        {
            //Assert
            var _bancosService = A.Fake<IBancosService>();
            var _contabilidadService = A.Fake<IContabilidadService>();
            var _configuracion = A.Fake<IConfiguracion>();
            var _dialogService = A.Fake<IDialogService>();
            var _pedidoCompraService = A.Fake<IPedidoCompraService>();
            A.CallTo(() => _bancosService.CrearPunteo(1, 3, A<decimal>._, "*", null)).Returns(4);
            A.CallTo(() => _bancosService.CrearPunteo(1, 4, A<decimal>._, "*", null)).Returns(5);
            A.CallTo(() => _bancosService.CrearPunteo(2, 3, A<decimal>._, "*", null)).Returns(6);
            A.CallTo(() => _bancosService.CrearPunteo(2, 4, A<decimal>._, "*", null)).Returns(7);
            var _container = A.Fake<IUnityContainer>();
            var _recursosHumanosService = A.Fake<IRecursosHumanosService>();
            var sut = new BancosViewModel(_bancosService, _contabilidadService, _configuracion, _dialogService, _pedidoCompraService, _container, _recursosHumanosService);

            var apunteBanco = new ApunteBancarioDTO()
            {
                Id = 1,
                ImporteMovimiento = 70,
                EstadoPunteo = EstadoPunteo.CompletamentePunteado
            };
            var apunteBanco2 = new ApunteBancarioDTO()
            {
                Id = 2,
                ImporteMovimiento = 30
            };
            var apunteContabilidad = new ContabilidadDTO()
            {
                Id = 3,
                Debe = 70,
                EstadoPunteo = EstadoPunteo.CompletamentePunteado
            };

            var apunteContabilidad2 = new ContabilidadDTO()
            {
                Id = 4,
                Debe = 30
            };

            sut.ApuntesBanco = new ObservableCollection<ApunteBancarioWrapper>
            {
                new ApunteBancarioWrapper(apunteBanco),
                new ApunteBancarioWrapper(apunteBanco2)
            };
            sut.ApuntesContabilidad = new ObservableCollection<ContabilidadWrapper>
            {
                new ContabilidadWrapper(apunteContabilidad),
                new ContabilidadWrapper(apunteContabilidad2)
            };
            sut.ApuntesBancoSeleccionados = sut.ApuntesBanco.ToList();
            sut.ApuntesContabilidadSeleccionados = sut.ApuntesContabilidad.ToList();

            // Act
            sut.PuntearApuntesCommand.Execute(null);

            // Arrange
            A.CallTo(() => _bancosService.CrearPunteo(1, 3, A<decimal>._, "*", null)).MustNotHaveHappened();
            A.CallTo(() => _bancosService.CrearPunteo(1, 4, A<decimal>._, "*", null)).MustNotHaveHappened();
            A.CallTo(() => _bancosService.CrearPunteo(2, 3, A<decimal>._, "*", null)).MustNotHaveHappened();
            A.CallTo(() => _bancosService.CrearPunteo(2, 4, 30, "*", null)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void BancosViewModel_PuntearAutomaticamente_SiSoloHayDosApuntesDeMismaFechaYMismoImporteSePunteanEntreEllos()
        {
            //Assert
            var _bancosService = A.Fake<IBancosService>();
            var _contabilidadService = A.Fake<IContabilidadService>();
            var _configuracion = A.Fake<IConfiguracion>();
            var _dialogService = A.Fake<IDialogService>();
            var _pedidoCompraService = A.Fake<IPedidoCompraService>();
            A.CallTo(() => _bancosService.CrearPunteo(1, 2, A<decimal>._, "*", null)).Returns(3);
            A.CallTo(() => _dialogService.ShowDialog(A<string>.Ignored, A<DialogParameters>.Ignored, A<Action<IDialogResult>>.Ignored))
                .Invokes(call =>
                {
                    // Extraer el Action<IDialogResult> de los argumentos
                    Action<IDialogResult> callback = call.GetArgument<Action<IDialogResult>>(2);

                    // Simular el comportamiento del diálogo (en este caso, ButtonResult.OK)
                    callback.Invoke(new DialogResult(ButtonResult.OK));
                });
            var _container = A.Fake<IUnityContainer>();
            var _recursosHumanosService = A.Fake<IRecursosHumanosService>();
            var sut = new BancosViewModel(_bancosService, _contabilidadService, _configuracion, _dialogService, _pedidoCompraService, _container, _recursosHumanosService);

            var apunteBanco = new ApunteBancarioDTO()
            {
                Id = 1,
                FechaOperacion = new DateTime(2024, 2, 5),
                ImporteMovimiento = 100
            };
            var apunteContabilidad = new ContabilidadDTO()
            {
                Id = 2,
                Fecha = new DateTime(2024, 2, 5),
                Debe = 100
            };

            sut.ApuntesBanco = new ObservableCollection<ApunteBancarioWrapper>
            {
                new ApunteBancarioWrapper(apunteBanco)
            };
            sut.ApuntesContabilidad = new ObservableCollection<ContabilidadWrapper>
            {
                new ContabilidadWrapper(apunteContabilidad)
            };


            // Act
            sut.PuntearAutomaticamenteCommand.Execute(null);

            // Arrange
            A.CallTo(() => _bancosService.CrearPunteo(1, 2, 100, "*", null)).MustHaveHappenedOnceExactly();
            Assert.AreEqual(EstadoPunteo.CompletamentePunteado, sut.ApuntesBanco.Single().EstadoPunteo);
            Assert.AreEqual(EstadoPunteo.CompletamentePunteado, sut.ApuntesContabilidad.Single().EstadoPunteo);
        }

        [TestMethod]
        public void BancosViewModel_PuntearAutomaticamente_SiHayVariosApuntesDeMismoImportePeroDistintaFechaNoSePunteanEntreEllos()
        {
            //Assert
            var _bancosService = A.Fake<IBancosService>();
            var _contabilidadService = A.Fake<IContabilidadService>();
            var _configuracion = A.Fake<IConfiguracion>();
            var _dialogService = A.Fake<IDialogService>();
            var _pedidoCompraService = A.Fake<IPedidoCompraService>();
            A.CallTo(() => _bancosService.CrearPunteo(1, 2, A<decimal>._, "*", null)).Returns(3);
            A.CallTo(() => _dialogService.ShowDialog(A<string>.Ignored, A<DialogParameters>.Ignored, A<Action<IDialogResult>>.Ignored))
                .Invokes(call =>
                {
                    // Extraer el Action<IDialogResult> de los argumentos
                    Action<IDialogResult> callback = call.GetArgument<Action<IDialogResult>>(2);

                    // Simular el comportamiento del diálogo (en este caso, ButtonResult.OK)
                    callback.Invoke(new DialogResult(ButtonResult.OK));
                });
            var _container = A.Fake<IUnityContainer>();
            var _recursosHumanosService = A.Fake<IRecursosHumanosService>();
            var sut = new BancosViewModel(_bancosService, _contabilidadService, _configuracion, _dialogService, _pedidoCompraService, _container, _recursosHumanosService);

            var apunteBanco = new ApunteBancarioDTO()
            {
                Id = 1,
                FechaOperacion = new DateTime(2024, 2, 5),
                ImporteMovimiento = 100
            };
            var apunteContabilidad = new ContabilidadDTO()
            {
                Id = 2,
                Fecha = new DateTime(2024, 2, 6),
                Debe = 100
            };

            sut.ApuntesBanco = new ObservableCollection<ApunteBancarioWrapper>
            {
                new ApunteBancarioWrapper(apunteBanco)
            };
            sut.ApuntesContabilidad = new ObservableCollection<ContabilidadWrapper>
            {
                new ContabilidadWrapper(apunteContabilidad)
            };


            // Act
            sut.PuntearAutomaticamenteCommand.Execute(null);

            // Arrange
            A.CallTo(() => _bancosService.CrearPunteo(1, 2, A<decimal>._, "*", null)).MustNotHaveHappened();
        }

        [TestMethod]
        public void BancosViewModel_PuntearAutomaticamente_SiHayMasDeUnApunteDeMismoImporteYFechaNoSePunteanNinguno()
        {
            //Assert
            var _bancosService = A.Fake<IBancosService>();
            var _contabilidadService = A.Fake<IContabilidadService>();
            var _configuracion = A.Fake<IConfiguracion>();
            var _dialogService = A.Fake<IDialogService>();
            var _pedidoCompraService = A.Fake<IPedidoCompraService>();
            A.CallTo(() => _bancosService.CrearPunteo(1, 2, A<decimal>._, "*", null)).Returns(3);
            A.CallTo(() => _bancosService.CrearPunteo(1, 3, A<decimal>._, "*", null)).Returns(4);
            A.CallTo(() => _dialogService.ShowDialog(A<string>.Ignored, A<DialogParameters>.Ignored, A<Action<IDialogResult>>.Ignored))
                .Invokes(call =>
                {
                    // Extraer el Action<IDialogResult> de los argumentos
                    Action<IDialogResult> callback = call.GetArgument<Action<IDialogResult>>(2);

                    // Simular el comportamiento del diálogo (en este caso, ButtonResult.OK)
                    callback.Invoke(new DialogResult(ButtonResult.OK));
                });
            var _container = A.Fake<IUnityContainer>();
            var _recursosHumanosService = A.Fake<IRecursosHumanosService>();
            var sut = new BancosViewModel(_bancosService, _contabilidadService, _configuracion, _dialogService, _pedidoCompraService, _container, _recursosHumanosService);

            var apunteBanco = new ApunteBancarioDTO()
            {
                Id = 1,
                FechaOperacion = new DateTime(2024, 2, 5),
                ImporteMovimiento = 100
            };
            var apunteContabilidad = new ContabilidadDTO()
            {
                Id = 2,
                Fecha = new DateTime(2024, 2, 5),
                Debe = 100
            };
            var apunteContabilidad2 = new ContabilidadDTO()
            {
                Id = 3,
                Fecha = new DateTime(2024, 2, 5),
                Debe = 100
            };

            sut.ApuntesBanco = new ObservableCollection<ApunteBancarioWrapper>
            {
                new ApunteBancarioWrapper(apunteBanco)
            };
            sut.ApuntesContabilidad = new ObservableCollection<ContabilidadWrapper>
            {
                new ContabilidadWrapper(apunteContabilidad),
                new ContabilidadWrapper(apunteContabilidad2)
            };


            // Act
            sut.PuntearAutomaticamenteCommand.Execute(null);

            // Arrange
            A.CallTo(() => _bancosService.CrearPunteo(1, 2, A<decimal>._, "*", null)).MustNotHaveHappened();
            A.CallTo(() => _bancosService.CrearPunteo(1, 3, A<decimal>._, "*", null)).MustNotHaveHappened();
        }

        [TestMethod]
        public void BancosViewModel_PuntearAutomaticamente_SiHayVariosApuntesDeMismaFechaYMismoImporteSePunteanTodosEllos()
        {
            //Assert
            var _bancosService = A.Fake<IBancosService>();
            var _contabilidadService = A.Fake<IContabilidadService>();
            var _configuracion = A.Fake<IConfiguracion>();
            var _dialogService = A.Fake<IDialogService>();
            var _pedidoCompraService = A.Fake<IPedidoCompraService>();
            A.CallTo(() => _bancosService.CrearPunteo(1, 2, A<decimal>._, "*", null)).Returns(3);
            A.CallTo(() => _bancosService.CrearPunteo(3, 4, A<decimal>._, "*", null)).Returns(4);
            A.CallTo(() => _dialogService.ShowDialog(A<string>.Ignored, A<DialogParameters>.Ignored, A<Action<IDialogResult>>.Ignored))
                .Invokes(call =>
                {
                    // Extraer el Action<IDialogResult> de los argumentos
                    Action<IDialogResult> callback = call.GetArgument<Action<IDialogResult>>(2);

                    // Simular el comportamiento del diálogo (en este caso, ButtonResult.OK)
                    callback.Invoke(new DialogResult(ButtonResult.OK));
                });
            var _container = A.Fake<IUnityContainer>();
            var _recursosHumanosService = A.Fake<IRecursosHumanosService>();
            var sut = new BancosViewModel(_bancosService, _contabilidadService, _configuracion, _dialogService, _pedidoCompraService, _container, _recursosHumanosService);

            var apunteBanco = new ApunteBancarioDTO()
            {
                Id = 1,
                FechaOperacion = new DateTime(2024, 2, 5),
                ImporteMovimiento = 100
            };
            var apunteContabilidad = new ContabilidadDTO()
            {
                Id = 2,
                Fecha = new DateTime(2024, 2, 5),
                Debe = 100
            };
            var apunteBanco2 = new ApunteBancarioDTO()
            {
                Id = 3,
                FechaOperacion = new DateTime(2024, 2, 6),
                ImporteMovimiento = -70
            };
            var apunteContabilidad2 = new ContabilidadDTO()
            {
                Id = 4,
                Fecha = new DateTime(2024, 2, 6),
                Haber = 70
            };

            sut.ApuntesBanco = new ObservableCollection<ApunteBancarioWrapper>
            {
                new ApunteBancarioWrapper(apunteBanco),
                new ApunteBancarioWrapper(apunteBanco2)
            };
            sut.ApuntesContabilidad = new ObservableCollection<ContabilidadWrapper>
            {
                new ContabilidadWrapper(apunteContabilidad),
                new ContabilidadWrapper(apunteContabilidad2)
            };


            // Act
            sut.PuntearAutomaticamenteCommand.Execute(null);

            // Arrange
            A.CallTo(() => _bancosService.CrearPunteo(1, 2, 100, "*", null)).MustHaveHappenedOnceExactly();
            A.CallTo(() => _bancosService.CrearPunteo(3, 4, -70, "*", null)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void BancosViewModel_PuntearAutomaticamente_LosApuntesCompletamentePunteadosSeIgnoran()
        {
            //Assert
            var _bancosService = A.Fake<IBancosService>();
            var _contabilidadService = A.Fake<IContabilidadService>();
            var _configuracion = A.Fake<IConfiguracion>();
            var _dialogService = A.Fake<IDialogService>();
            var _pedidoCompraService = A.Fake<IPedidoCompraService>();
            A.CallTo(() => _bancosService.CrearPunteo(1, 2, A<decimal>._, "*", null)).Returns(3);
            A.CallTo(() => _bancosService.CrearPunteo(1, 3, A<decimal>._, "*", null)).Returns(4);
            A.CallTo(() => _dialogService.ShowDialog(A<string>.Ignored, A<DialogParameters>.Ignored, A<Action<IDialogResult>>.Ignored))
                .Invokes(call =>
                {
                    // Extraer el Action<IDialogResult> de los argumentos
                    Action<IDialogResult> callback = call.GetArgument<Action<IDialogResult>>(2);

                    // Simular el comportamiento del diálogo (en este caso, ButtonResult.OK)
                    callback.Invoke(new DialogResult(ButtonResult.OK));
                });
            var _container = A.Fake<IUnityContainer>();
            var _recursosHumanosService = A.Fake<IRecursosHumanosService>();
            var sut = new BancosViewModel(_bancosService, _contabilidadService, _configuracion, _dialogService, _pedidoCompraService, _container, _recursosHumanosService);

            var apunteBanco = new ApunteBancarioDTO()
            {
                Id = 1,
                FechaOperacion = new DateTime(2024, 2, 5),
                ImporteMovimiento = 100,
                EstadoPunteo = EstadoPunteo.CompletamentePunteado
            };
            var apunteContabilidad = new ContabilidadDTO()
            {
                Id = 2,
                Fecha = new DateTime(2024, 2, 5),
                Debe = 100
            };
            
            sut.ApuntesBanco = new ObservableCollection<ApunteBancarioWrapper>
            {
                new ApunteBancarioWrapper(apunteBanco)
            };
            sut.ApuntesContabilidad = new ObservableCollection<ContabilidadWrapper>
            {
                new ContabilidadWrapper(apunteContabilidad)
            };


            // Act
            sut.PuntearAutomaticamenteCommand.Execute(null);

            // Arrange
            A.CallTo(() => _bancosService.CrearPunteo(1, 2, A<decimal>._, "*", null)).MustNotHaveHappened();
        }
    }

}
