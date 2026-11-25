using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Modulos.PedidoVenta;
using Nesto.Modulos.PlantillaVenta;
using CommunityToolkit.Mvvm.Messaging;
using ControlesUsuario.Dialogs;

namespace PlantillaVentaTests
{
    /// <summary>
    /// Nesto#654 (sugerencia 1432, María José; decisión de Carlos 24/11/26): por parámetro de usuario
    /// («PlantillaBorradorActualizarCargado» = "1"), guardar un borrador CARGADO lo actualiza en vez de crear otro. Sin el parámetro,
    /// o si no se puede leer, como siempre.
    /// </summary>
    [TestClass]
    public class ActualizarBorradorCargadoTests
    {
        private IBorradorPlantillaVentaService _servicioBorradores;
        private IServicioDialogos _dialogService;
        private IConfiguracion _configuracion;
        private BorradorPlantillaVenta _guardado;

        [TestInitialize]
        public void Setup()
        {
            _servicioBorradores = A.Fake<IBorradorPlantillaVentaService>();
            _dialogService = A.Fake<IServicioDialogos>();
            _configuracion = A.Fake<IConfiguracion>();
            A.CallTo(() => _configuracion.LeerParametroSync(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.AlmacenRuta)).Returns("ALG");
            A.CallTo(() => _servicioBorradores.GuardarBorrador(A<BorradorPlantillaVenta>._))
                .ReturnsLazily((BorradorPlantillaVenta b) =>
                {
                    _guardado = b;
                    if (string.IsNullOrEmpty(b.Id))
                    {
                        b.Id = "nuevo";
                    }
                    return b;
                });
        }

        private PlantillaVentaViewModel CrearViewModel()
        {
            return new PlantillaVentaViewModel(A.Fake<IAbridorPedidos>(), A.Fake<IServicioNavegacion>(), _configuracion,
                A.Fake<IPlantillaVentaService>(), new WeakReferenceMessenger(), _dialogService, A.Fake<IPedidoVentaService>(),
                _servicioBorradores, A.Fake<IServicioAutenticacion>());
        }

        private BorradorPlantillaVenta BorradorEnDisco(string id = "borrador-1")
        {
            var borrador = new BorradorPlantillaVenta { Id = id, Cliente = "40445", NombreCliente = "LUXURY NADOR", FechaCreacion = new DateTime(2026, 11, 20, 10, 0, 0) };
            A.CallTo(() => _servicioBorradores.ObtenerBorradores()).Returns(new List<BorradorPlantillaVenta> { borrador });
            return borrador;
        }

        private void Parametro(string valor)
            => A.CallTo(() => _configuracion.leerParametro(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.PlantillaBorradorActualizarCargado))
                .Returns(Task.FromResult(valor));

        [DataTestMethod]
        [DataRow("1", true)]
        [DataRow(" 1 ", true)]
        [DataRow("0", false)]
        [DataRow("", false)]
        [DataRow(null, false)]
        [DataRow("si", false)]
        public void ActualizaElBorradorCargado_SoloConUno(string valor, bool esperado)
        {
            Assert.AreEqual(esperado, PlantillaVentaViewModel.ActualizaElBorradorCargado(valor));
        }

        [TestMethod]
        public async Task ParametroApagado_NoHayBorradorQueActualizar_ComoSiempre()
        {
            Parametro("0");
            var vm = CrearViewModel();
            vm.BorradorCargado = BorradorEnDisco();

            await vm.CargarActualizarBorradorCargadoAsync();

            Assert.IsNull(vm.BorradorCargadoParaActualizar());
        }

        [TestMethod]
        public async Task SinPoderLeerElParametro_ComoSiempre()
        {
            A.CallTo(() => _configuracion.leerParametro(A<string>._, Parametros.Claves.PlantillaBorradorActualizarCargado))
                .ThrowsAsync(new Exception("No se puede leer el parámetro"));
            var vm = CrearViewModel();
            vm.BorradorCargado = BorradorEnDisco();

            await vm.CargarActualizarBorradorCargadoAsync();

            Assert.IsNull(vm.BorradorCargadoParaActualizar());
        }

        [TestMethod]
        public async Task ParametroEncendido_ConBorradorCargadoDelDisco_EsEseElQueSeActualiza()
        {
            Parametro("1");
            var vm = CrearViewModel();
            vm.BorradorCargado = BorradorEnDisco("borrador-1");

            await vm.CargarActualizarBorradorCargadoAsync();

            Assert.AreEqual("borrador-1", vm.BorradorCargadoParaActualizar()?.Id);
        }

        [TestMethod]
        public async Task ParametroEncendido_BorradorQueNoEstaEnElDisco_NoSeActualiza()
        {
            // «Modificar con plantilla» construye el borrador en memoria: no hay fichero que actualizar
            Parametro("1");
            BorradorEnDisco("otro");
            var vm = CrearViewModel();
            vm.BorradorCargado = new BorradorPlantillaVenta { Id = "en-memoria", Cliente = "40445" };

            await vm.CargarActualizarBorradorCargadoAsync();

            Assert.IsNull(vm.BorradorCargadoParaActualizar());
        }

        [TestMethod]
        public async Task AlSalir_DiceQueSi_SeGuardaConElMismoId()
        {
            Parametro("1");
            var vm = CrearViewModel();
            var cargado = BorradorEnDisco("borrador-1");
            vm.BorradorCargado = cargado;
            await vm.CargarActualizarBorradorCargadoAsync();
            A.CallTo(() => _dialogService.ShowConfirmationAnswer(PlantillaVentaViewModel.TITULO_BORRADOR_CARGADO, A<string>._)).Returns(true);

            bool cierra = vm.GuardarAlSalirConBorradorCargado(cargado);

            Assert.IsTrue(cierra);
            Assert.IsNotNull(_guardado, "Se ha guardado");
            Assert.AreEqual("borrador-1", _guardado.Id);
            A.CallTo(() => _dialogService.ShowNotification("Borrador guardado", A<string>.That.Contains("actualizado"))).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task AlSalir_DiceQueNo_SeGuardaComoUnoNuevo()
        {
            Parametro("1");
            var vm = CrearViewModel();
            var cargado = BorradorEnDisco("borrador-1");
            vm.BorradorCargado = cargado;
            await vm.CargarActualizarBorradorCargadoAsync();
            A.CallTo(() => _dialogService.ShowConfirmationAnswer(PlantillaVentaViewModel.TITULO_BORRADOR_CARGADO, A<string>._)).Returns(false);

            bool cierra = vm.GuardarAlSalirConBorradorCargado(cargado);

            Assert.IsTrue(cierra);
            Assert.AreEqual("nuevo", _guardado.Id, "Sin Id: el servicio le da uno nuevo");
            A.CallTo(() => _dialogService.ShowNotification("Borrador guardado", A<string>.That.Contains("nuevo"))).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void TextoPregunta_NombraElBorradorYExplicaLasDosOpciones()
        {
            string texto = PlantillaVentaViewModel.TextoPreguntaBorradorCargado(new BorradorPlantillaVenta { Cliente = "40445", NombreCliente = "LUXURY NADOR" });
            StringAssert.Contains(texto, "40445 - LUXURY NADOR");
            StringAssert.Contains(texto, "Sí: se actualiza ese borrador.");
            StringAssert.Contains(texto, "No: se guarda como un borrador nuevo");
        }
    
        // ---------------- La casilla del panel de borradores (Carlos 25/11/26) ----------------

        [TestMethod]
        public async Task LaCasilla_EnsenaElValorDelUsuarioDesdeElPrincipio()
        {
            Parametro("1");
            var vm = CrearViewModel();
            await vm.CargarActualizarBorradorCargadoAsync();

            Assert.IsTrue(vm.ActualizarBorradorCargado);
        }

        [TestMethod]
        public async Task LaCasilla_SinParametro_Desmarcada()
        {
            Parametro(null);
            var vm = CrearViewModel();
            await vm.CargarActualizarBorradorCargadoAsync();

            Assert.IsFalse(vm.ActualizarBorradorCargado);
        }

        [TestMethod]
        public async Task MarcarLaCasilla_GuardaUnoComoParametroDelUsuario_YValeYa()
        {
            Parametro("0");
            var vm = CrearViewModel();
            await vm.CargarActualizarBorradorCargadoAsync();
            vm.BorradorCargado = BorradorEnDisco("borrador-1");
            var cambios = new List<string>();
            vm.PropertyChanged += (s, e) => cambios.Add(e.PropertyName);

            vm.ActualizarBorradorCargado = true;
            await vm.GuardadoPreferenciaBorrador;

            A.CallTo(() => _configuracion.GuardarParametro(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.PlantillaBorradorActualizarCargado, "1"))
                .MustHaveHappenedOnceExactly();
            Assert.IsTrue(vm.ActualizarBorradorCargado);
            CollectionAssert.Contains(cambios, nameof(PlantillaVentaViewModel.ActualizarBorradorCargado));
            Assert.AreEqual("borrador-1", vm.BorradorCargadoParaActualizar()?.Id, "Sin esperar a cargar otro borrador");
        }

        [TestMethod]
        public async Task DesmarcarLaCasilla_GuardaCero_YDejaDeActualizar()
        {
            Parametro("1");
            var vm = CrearViewModel();
            await vm.CargarActualizarBorradorCargadoAsync();
            vm.BorradorCargado = BorradorEnDisco("borrador-1");

            vm.ActualizarBorradorCargado = false;
            await vm.GuardadoPreferenciaBorrador;

            A.CallTo(() => _configuracion.GuardarParametro(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.PlantillaBorradorActualizarCargado, "0"))
                .MustHaveHappenedOnceExactly();
            Assert.IsNull(vm.BorradorCargadoParaActualizar());
        }

        [TestMethod]
        public async Task SiNoSePuedeGuardar_LaCasillaVuelveASuValorYSeEnsenaElMensaje()
        {
            Parametro("0");
            A.CallTo(() => _configuracion.GuardarParametro(A<string>._, Parametros.Claves.PlantillaBorradorActualizarCargado, A<string>._))
                .ThrowsAsync(new Exception("El servidor no responde"));
            var vm = CrearViewModel();
            await vm.CargarActualizarBorradorCargadoAsync();

            vm.ActualizarBorradorCargado = true;
            await vm.GuardadoPreferenciaBorrador;

            Assert.IsFalse(vm.ActualizarBorradorCargado);
            A.CallTo(() => _dialogService.ShowError(A<string>.That.Contains("El servidor no responde"))).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task PonerElMismoValor_NoGuardaNada()
        {
            Parametro("1");
            var vm = CrearViewModel();
            await vm.CargarActualizarBorradorCargadoAsync();

            vm.ActualizarBorradorCargado = true;
            await vm.GuardadoPreferenciaBorrador;

            A.CallTo(() => _configuracion.GuardarParametro(A<string>._, A<string>._, A<string>._)).MustNotHaveHappened();
        }
}
}
