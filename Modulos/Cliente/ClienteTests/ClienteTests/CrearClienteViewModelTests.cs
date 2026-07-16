using System;
using System.Collections.Generic;
using System.ComponentModel;
using FakeItEasy;
using Prism.Events;
using Prism.Regions;
using Nesto.Modulos.Cliente;
using Xceed.Wpf.Toolkit;
using Prism.Services.Dialogs;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;

namespace ClienteTests
{
    [TestClass]
    public class CrearClienteViewModelTests
    {
        private IRegionManager RegionManager { get; }
        private IConfiguracion Configuracion { get; }
        private IClienteService Servicio { get; }
        private IEventAggregator EventAggregator { get; }
        private IDialogService DialogService { get; }
        public CrearClienteViewModelTests()
        {
            RegionManager = A.Fake<IRegionManager>();
            Configuracion = A.Fake<IConfiguracion>();
            Servicio = A.Fake<IClienteService>();
            EventAggregator = A.Fake<IEventAggregator>();
            DialogService = A.Fake<IDialogService>();
        }

        // Nesto#409: autocompletado de direcciones (Google Places vía NestoAPI)

        [TestMethod]
        public async System.Threading.Tasks.Task AplicarSugerenciaDireccion_RellenaCalleNumeroYCodigoPostal()
        {
            A.CallTo(() => Servicio.LeerDetalleDireccion("ChIJ111", A<string>.Ignored))
                .Returns(new DireccionDetalleModel
                {
                    Calle = "Avenida de Castilla",
                    Numero = "3",
                    CodigoPostal = "28830"
                });
            var vm = new CrearClienteViewModel(RegionManager, Configuracion, Servicio, EventAggregator, DialogService);

            await vm.AplicarSugerenciaDireccionAsync(new SugerenciaDireccionModel { PlaceId = "ChIJ111" });

            Assert.AreEqual("Avenida de Castilla, 3", vm.ClienteDireccionCalleNumero);
            Assert.AreEqual("28830", vm.ClienteCodigoPostal);
            Assert.IsFalse(vm.HaySugerenciasDireccion, "Tras seleccionar, el combo se cierra");
        }

        [TestMethod]
        public async System.Threading.Tasks.Task AplicarSugerenciaDireccion_SinNumero_NoDejaComaColgando()
        {
            A.CallTo(() => Servicio.LeerDetalleDireccion("ChIJ222", A<string>.Ignored))
                .Returns(new DireccionDetalleModel { Calle = "Avenida de Castilla", CodigoPostal = "28830" });
            var vm = new CrearClienteViewModel(RegionManager, Configuracion, Servicio, EventAggregator, DialogService);

            await vm.AplicarSugerenciaDireccionAsync(new SugerenciaDireccionModel { PlaceId = "ChIJ222" });

            Assert.AreEqual("Avenida de Castilla", vm.ClienteDireccionCalleNumero);
        }

        [TestMethod]
        public async System.Threading.Tasks.Task AplicarSugerenciaDireccion_SiElDetalleFalla_NoTocaLosCampos()
        {
            A.CallTo(() => Servicio.LeerDetalleDireccion(A<string>.Ignored, A<string>.Ignored))
                .Throws(new Exception("Places no habilitado"));
            var vm = new CrearClienteViewModel(RegionManager, Configuracion, Servicio, EventAggregator, DialogService);
            vm.ClienteCodigoPostal = "28004";

            await vm.AplicarSugerenciaDireccionAsync(new SugerenciaDireccionModel { PlaceId = "ChIJ333" });

            Assert.AreEqual("28004", vm.ClienteCodigoPostal);
        }

        [TestMethod]
        public void CrearClienteViewModel_AlCambiarElNif_BloqueaElNombreSiEsUnCif()
        {
            var vm = new CrearClienteViewModel(RegionManager, Configuracion, Servicio, EventAggregator, DialogService);

            vm.ClienteNif = "B111";

            Assert.IsFalse(vm.NombreIsEnabled);
        }

        [TestMethod]
        public void CrearClienteViewModel_AlCambiarElNif_NoBloqueaElNombreSiNoEsUnCif()
        {
            var vm = new CrearClienteViewModel(RegionManager, Configuracion, Servicio, EventAggregator, DialogService);

            vm.ClienteNif = "530021-A";

            Assert.IsTrue(vm.NombreIsEnabled);
        }

        [TestMethod]
        public void CrearClienteViewModel_AlCambiarElNif_NoBloqueaElNombreSiEsUnNie()
        {
            var vm = new CrearClienteViewModel(RegionManager, Configuracion, Servicio, EventAggregator, DialogService);

            vm.ClienteNif = "X/78787";

            Assert.IsTrue(vm.NombreIsEnabled);
        }

        [TestMethod]
        public void CrearClienteViewModel_AlCambiarElNif_SeActualizaNombreIsEnabled()
        {
            var vm = new CrearClienteViewModel(RegionManager, Configuracion, Servicio, EventAggregator, DialogService);
            int vecesSeHaLlamado = 0;
            vm.PropertyChanged += delegate (object sender, PropertyChangedEventArgs e)
            {
                if (e.PropertyName == "NombreIsEnabled")
                {
                    vecesSeHaLlamado++;
                }
            };

            vm.ClienteNif = "X";

            Assert.AreEqual(1, vecesSeHaLlamado);
        }

        [TestMethod]
        public void CrearClienteViewModel_AlCambiarElNif_SeActualizaSePuedeAvanzarADatosGenerales()
        {
            var vm = new CrearClienteViewModel(RegionManager, Configuracion, Servicio, EventAggregator, DialogService);
            int vecesSeHaLlamado = 0;
            vm.PropertyChanged += delegate (object sender, PropertyChangedEventArgs e)
            {
                if (e.PropertyName == "SePuedeAvanzarADatosGenerales")
                {
                    vecesSeHaLlamado++;
                }
            };

            vm.ClienteNif = "X";

            Assert.AreEqual(1, vecesSeHaLlamado);
        }

        [TestMethod]
        public void CrearClienteViewModel_AlCambiarElNombre_SeActualizaSePuedeAvanzarADatosGenerales()
        {
            var vm = new CrearClienteViewModel(RegionManager, Configuracion, Servicio, EventAggregator, DialogService);
            int vecesSeHaLlamado = 0;
            vm.PropertyChanged += delegate (object sender, PropertyChangedEventArgs e)
            {
                if (e.PropertyName == "SePuedeAvanzarADatosGenerales")
                {
                    vecesSeHaLlamado++;
                }
            };

            vm.ClienteNombre = "X";

            Assert.AreEqual(1, vecesSeHaLlamado);
        }

        /*
        // Comento el test porque todos los tests con WizardPage dan error
        [TestMethod]
        public void CrearClienteViewModel_PasarADatosComision_SiOtroClienteTieneEseMovilSeNotifica()
        {
            var vm = new CrearClienteViewModel(RegionManager, Configuracion, Servicio, EventAggregator, DialogService);
            RespuestaDatosGeneralesClientes respuestaFake = A.Fake<RespuestaDatosGeneralesClientes>();
            ClienteTelefonoLookup clienteFake = new ClienteTelefonoLookup
            {
                Empresa = "1",
                Cliente = "12345",
                Contacto = "1",
                Nombre = "Prueba"
            };
            respuestaFake.ClientesMismoTelefono = new List<ClienteTelefonoLookup> { clienteFake };
            A.CallTo(() => Servicio.ValidarDatosGenerales(A<string>.Ignored, A<string>.Ignored, A<string>.Ignored)).Returns(respuestaFake);
            WizardPage paginaActual = A.Fake<WizardPage>(); // NO FUNCIONA EL TEST PORQUE NO SÉ CÓMO CREAR EL MOCK DE WizardPage
            paginaActual.Name = CrearClienteViewModel.DATOS_GENERALES;
            WizardPage paginaSiguiente = A.Fake<WizardPage>();
            paginaSiguiente.Name = CrearClienteViewModel.DATOS_COMISIONES;
            vm.PaginaActual = paginaActual;
            // ACT
            vm.PaginaActual = paginaSiguiente;

            //Assert
            A.CallTo(() => DialogService.ShowDialog(A<string>._, A<IDialogParameters>._, A<Action<IDialogResult>>._)).MustHaveHappenedOnceExactly();
        }
        */
    }
}
