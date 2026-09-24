using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Events;
using Nesto.Infrastructure.Shared;
using Nesto.Modulos.PedidoVenta;
using Nesto.Modulos.PlantillaVenta;
using CommunityToolkit.Mvvm.Messaging;
using Prism.Regions;
using Prism.Services.Dialogs;
using Unity;

namespace PlantillaVentaTests
{
    /// <summary>
    /// Nesto#474: al crear un pedido que parte de un borrador cargado del disco, la plantilla
    /// pregunta si se borra el borrador y hace lo que diga el usuario. Nunca en silencio.
    /// </summary>
    [TestClass]
    public class BorrarBorradorAlCrearPedidoTests
    {
        private IBorradorPlantillaVentaService _servicioBorradores;
        private IDialogService _dialogService;
        private readonly List<string> _mensajes = new List<string>();
        private bool _respuestaUsuario = true;
        private int _preguntas;

        [TestInitialize]
        public void Setup()
        {
            _servicioBorradores = A.Fake<IBorradorPlantillaVentaService>();
            _dialogService = A.Fake<IDialogService>();
            A.CallTo(() => _dialogService.ShowDialog(
                    A<string>.Ignored, A<IDialogParameters>.Ignored, A<Action<IDialogResult>>.Ignored))
                .Invokes((string nombre, IDialogParameters parametros, Action<IDialogResult> callback) =>
                {
                    if (parametros != null && parametros.ContainsKey("message"))
                    {
                        _mensajes.Add(parametros.GetValue<string>("message"));
                    }
                    if (callback == null)
                    {
                        return; // ShowError no espera respuesta
                    }
                    _preguntas++;
                    var resultado = A.Fake<IDialogResult>();
                    A.CallTo(() => resultado.Result).Returns(_respuestaUsuario ? ButtonResult.OK : ButtonResult.Cancel);
                    callback(resultado);
                });
        }

        private PlantillaVentaViewModel CrearViewModel()
        {
            IConfiguracion configuracion = A.Fake<IConfiguracion>();
            IMessenger messenger = new WeakReferenceMessenger();
            A.CallTo(() => configuracion.LeerParametroSync(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.AlmacenRuta)).Returns("ALG");

            return new PlantillaVentaViewModel(A.Fake<IUnityContainer>(), A.Fake<IRegionManager>(), configuracion,
                A.Fake<IPlantillaVentaService>(), messenger, _dialogService, A.Fake<IPedidoVentaService>(),
                _servicioBorradores, A.Fake<IServicioAutenticacion>());
        }

        private BorradorPlantillaVenta BorradorEnDisco(string id = "borrador-1")
        {
            var borrador = new BorradorPlantillaVenta
            {
                Id = id,
                Cliente = "40445",
                NombreCliente = "LUXURY NADOR",
                FechaCreacion = new DateTime(2026, 9, 10, 12, 15, 0)
            };
            A.CallTo(() => _servicioBorradores.ObtenerBorradores()).Returns(new List<BorradorPlantillaVenta> { borrador });
            A.CallTo(() => _servicioBorradores.EliminarBorrador(id)).Returns(true);
            return borrador;
        }

        [TestMethod]
        public void PedidoDesdeBorrador_UsuarioDiceSi_SeBorraElBorradorYSaleDeLaLista()
        {
            var borrador = BorradorEnDisco();
            var vm = CrearViewModel();
            vm.ListaBorradores = new ObservableCollection<BorradorPlantillaVenta> { borrador };
            _respuestaUsuario = true;

            bool borrado = vm.OfrecerBorrarBorradorDeOrigen("925123", borrador);

            Assert.IsTrue(borrado);
            A.CallTo(() => _servicioBorradores.EliminarBorrador("borrador-1")).MustHaveHappenedOnceExactly();
            Assert.AreEqual(0, vm.ListaBorradores.Count);
            StringAssert.Contains(_mensajes[0], "925123");
            StringAssert.Contains(_mensajes[0], "40445 - LUXURY NADOR");
        }

        [TestMethod]
        public void PedidoDesdeBorrador_UsuarioDiceNo_ElBorradorSeQueda()
        {
            var borrador = BorradorEnDisco();
            var vm = CrearViewModel();
            _respuestaUsuario = false;

            bool borrado = vm.OfrecerBorrarBorradorDeOrigen("925123", borrador);

            Assert.IsFalse(borrado);
            Assert.AreEqual(1, _preguntas, "Se pregunta una vez");
            A.CallTo(() => _servicioBorradores.EliminarBorrador(A<string>.Ignored)).MustNotHaveHappened();
        }

        [TestMethod]
        public void PedidoSinBorradorCargado_NoSePregunta()
        {
            var vm = CrearViewModel();

            bool borrado = vm.OfrecerBorrarBorradorDeOrigen("925123", null);

            Assert.IsFalse(borrado);
            Assert.AreEqual(0, _preguntas);
            A.CallTo(() => _servicioBorradores.EliminarBorrador(A<string>.Ignored)).MustNotHaveHappened();
        }

        [TestMethod]
        public void BorradorSoloEnMemoria_ModificarConPlantillaOJsonPegado_NoSePregunta()
        {
            // Nesto#397: el borrador se construye en memoria y no tiene fichero en disco.
            var vm = CrearViewModel();
            A.CallTo(() => _servicioBorradores.ObtenerBorradores()).Returns(new List<BorradorPlantillaVenta>());
            var enMemoria = new BorradorPlantillaVenta { Id = Guid.NewGuid().ToString(), NumeroPedidoEnEdicion = 921838 };

            bool borrado = vm.OfrecerBorrarBorradorDeOrigen("921838", enMemoria);

            Assert.IsFalse(borrado);
            Assert.AreEqual(0, _preguntas);
            A.CallTo(() => _servicioBorradores.EliminarBorrador(A<string>.Ignored)).MustNotHaveHappened();
        }
    }
}
