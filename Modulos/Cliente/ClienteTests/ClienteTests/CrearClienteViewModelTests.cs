using System;
using System.ComponentModel;
using FakeItEasy;
using Microsoft.Practices.Prism.Interactivity.InteractionRequest;
using Microsoft.Practices.Prism.Regions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Nesto.Contratos;
using Nesto.Modulos.Cliente;

namespace ClienteTests
{
    [TestClass]
    public class CrearClienteViewModelTests
    {
        private IRegionManager RegionManager { get; }
        private IConfiguracion Configuracion { get; }
        private IClienteService Servicio { get; }
        public CrearClienteViewModelTests()
        {
            RegionManager = A.Fake<IRegionManager>();
            Configuracion = A.Fake<IConfiguracion>();
            Servicio = A.Fake<IClienteService>();
        }

        [TestMethod]
        public void CrearClienteViewModel_AlCambiarElNif_BloqueaElNombreSiEsUnCif()
        {
            var vm = new CrearClienteViewModel(RegionManager, Configuracion, Servicio);

            vm.ClienteNif = "B111";

            Assert.IsFalse(vm.NombreIsEnabled);
        }

        [TestMethod]
        public void CrearClienteViewModel_AlCambiarElNif_NoBloqueaElNombreSiNoEsUnCif()
        {
            var vm = new CrearClienteViewModel(RegionManager, Configuracion, Servicio);

            vm.ClienteNif = "530021-A";

            Assert.IsTrue(vm.NombreIsEnabled);
        }

        [TestMethod]
        public void CrearClienteViewModel_AlCambiarElNif_NoBloqueaElNombreSiEsUnNie()
        {
            var vm = new CrearClienteViewModel(RegionManager, Configuracion, Servicio);

            vm.ClienteNif = "X/78787";

            Assert.IsTrue(vm.NombreIsEnabled);
        }

        [TestMethod]
        public void CrearClienteViewModel_AlCambiarElNif_SeActualizaNombreIsEnabled()
        {
            var vm = new CrearClienteViewModel(RegionManager, Configuracion, Servicio);
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
            var vm = new CrearClienteViewModel(RegionManager, Configuracion, Servicio);
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
            var vm = new CrearClienteViewModel(RegionManager, Configuracion, Servicio);
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
    }
}
