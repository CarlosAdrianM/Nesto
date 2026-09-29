using ControlesUsuario.Services;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.OfertasCombinadas.Interfaces;
using Nesto.Modulos.OfertasCombinadas.Models;
using Nesto.Modulos.OfertasCombinadas.ViewModels;
using Prism.Regions;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nesto.Modulos.OfertasCombinadasTests
{
    /// <summary>
    /// NestoAPI#233 (Carlos, 29/09/26): guardar una oferta en cualquiera de las tres pestañas NO avisa a
    /// los vendedores. Tras un guardado correcto se pregunta «¿Desea informar a los vendedores…?» y solo
    /// si se contesta que sí se llama a la API para mandar la push.
    /// </summary>
    [TestClass]
    public class InformarVendedoresViewModelTests
    {
        private IOfertasCombinadasService _service;
        private IServicioDialogos _dialogService;

        [TestInitialize]
        public void Setup()
        {
            _service = A.Fake<IOfertasCombinadasService>();
            _dialogService = A.Fake<IServicioDialogos>();

            A.CallTo(() => _service.GetOfertasCombinadas(A<string>._, A<bool>._))
                .Returns(Task.FromResult(new List<OfertaCombinadaModel>()));
            A.CallTo(() => _service.GetOfertasPermitidasFamilia(A<string>._))
                .Returns(Task.FromResult(new List<OfertaPermitidaFamiliaModel>()));
            A.CallTo(() => _service.GetOfertasEscalonadas(A<string>._, A<bool>._))
                .Returns(Task.FromResult(new List<OfertaEscalonadaModel>()));
            A.CallTo(() => _service.InformarVendedores(A<string>._, A<int>._, A<bool>._))
                .Returns(Task.FromResult(new ResultadoInformarVendedoresModel { DispositivosNotificados = 5, Cuerpo = "Texto" }));
        }

        private OfertasCombinadasViewModel CrearViewModel()
        {
            return new OfertasCombinadasViewModel(_service, A.Fake<IConfiguracion>(), _dialogService, A.Fake<IRegionManager>(), A.Fake<IServicioProducto>());
        }

        private void ContestarALaPregunta(bool respuesta)
        {
            A.CallTo(() => _dialogService.ShowConfirmationAnswer(A<string>._, A<string>.That.Contains(OfertasCombinadasViewModel.PREGUNTA_INFORMAR_VENDEDORES)))
                .Returns(respuesta);
        }

        private static OfertaCombinadaWrapper OfertaCombinadaNueva()
        {
            var oferta = new OfertaCombinadaWrapper { Nombre = "Kit Roseta" };
            oferta.Detalles.Add(new DetalleOfertaCombinadaWrapper { Producto = "PROD1", Cantidad = 1, Precio = 10 });
            oferta.Detalles.Add(new DetalleOfertaCombinadaWrapper { Producto = "PROD2", Cantidad = 1, Precio = 0 });
            return oferta;
        }

        private void CrearCombinadaDevuelve(int id)
        {
            A.CallTo(() => _service.CreateOfertaCombinada(A<OfertaCombinadaCreateModel>._))
                .Returns(Task.FromResult(new OfertaCombinadaModel { Id = id, Nombre = "Kit Roseta", Detalles = new List<OfertaCombinadaDetalleModel>() }));
        }

        [TestMethod]
        public async Task GuardarCombinada_ContestaSi_InformaALosVendedoresDeEsaOferta()
        {
            CrearCombinadaDevuelve(12);
            ContestarALaPregunta(true);
            var vm = CrearViewModel();

            vm.GuardarOfertaCombinadaCommand.Execute(OfertaCombinadaNueva());
            await Task.Delay(50);

            A.CallTo(() => _service.InformarVendedores("combinada", 12, true)).MustHaveHappenedOnceExactly();
            A.CallTo(() => _dialogService.ShowNotification(A<string>.That.Contains("Se ha informado a los vendedores (5 dispositivos)"))).MustHaveHappened();
        }

        [TestMethod]
        public async Task GuardarCombinada_ContestaNo_NoInformaANadie()
        {
            CrearCombinadaDevuelve(12);
            ContestarALaPregunta(false);
            var vm = CrearViewModel();

            vm.GuardarOfertaCombinadaCommand.Execute(OfertaCombinadaNueva());
            await Task.Delay(50);

            A.CallTo(() => _dialogService.ShowConfirmationAnswer(A<string>._, A<string>.That.Contains("Oferta combinada 'Kit Roseta' creada."))).MustHaveHappenedOnceExactly();
            A.CallTo(() => _service.InformarVendedores(A<string>._, A<int>._, A<bool>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task GuardarCombinada_FallaElGuardado_NiPreguntaNiInforma()
        {
            A.CallTo(() => _service.CreateOfertaCombinada(A<OfertaCombinadaCreateModel>._)).ThrowsAsync(new Exception("Error: el producto no existe"));
            ContestarALaPregunta(true);
            var vm = CrearViewModel();

            vm.GuardarOfertaCombinadaCommand.Execute(OfertaCombinadaNueva());
            await Task.Delay(50);

            A.CallTo(() => _dialogService.ShowConfirmationAnswer(A<string>._, A<string>._)).MustNotHaveHappened();
            A.CallTo(() => _service.InformarVendedores(A<string>._, A<int>._, A<bool>._)).MustNotHaveHappened();
            A.CallTo(() => _dialogService.ShowError("Error: el producto no existe")).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task GuardarCombinada_FallaElAviso_DiceQueLaOfertaSiEstaGuardada()
        {
            CrearCombinadaDevuelve(12);
            ContestarALaPregunta(true);
            A.CallTo(() => _service.InformarVendedores(A<string>._, A<int>._, A<bool>._)).ThrowsAsync(new Exception("sin conexión"));
            var vm = CrearViewModel();

            vm.GuardarOfertaCombinadaCommand.Execute(OfertaCombinadaNueva());
            await Task.Delay(50);

            A.CallTo(() => _dialogService.ShowError(A<string>.That.StartsWith("La oferta se ha guardado, pero no se ha podido informar a los vendedores"))).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task GuardarEscalonadaExistente_ContestaSi_InformaComoActualizada()
        {
            A.CallTo(() => _service.UpdateOfertaEscalonada(A<int>._, A<OfertaEscalonadaCreateModel>._))
                .Returns(Task.FromResult(new OfertaEscalonadaModel { Id = 4, Nombre = "Tintes" }));
            ContestarALaPregunta(true);
            var vm = CrearViewModel();
            var oferta = new OfertaEscalonadaWrapper { Id = 4, Nombre = "Tintes" };
            oferta.AnadirProducto(new OfertaEscalonadaProductoWrapper { Producto = "44707" });
            oferta.AnadirTramo(new OfertaEscalonadaTramoWrapper { CantidadMinima = 6, DescuentoPorcentaje = 25 });

            vm.GuardarOfertaEscalonadaCommand.Execute(oferta);
            await Task.Delay(50);

            A.CallTo(() => _service.InformarVendedores("escalonada", 4, false)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task GuardarFamilia_ContestaSi_InformaConElNOrden()
        {
            A.CallTo(() => _service.CreateOfertaPermitidaFamilia(A<OfertaPermitidaFamiliaCreateModel>._))
                .Returns(Task.FromResult(new OfertaPermitidaFamiliaModel { NOrden = 55, Familia = "Roseta", CantidadConPrecio = 6, CantidadRegalo = 2 }));
            ContestarALaPregunta(true);
            var vm = CrearViewModel();
            var oferta = new OfertaPermitidaFamiliaWrapper(new OfertaPermitidaFamiliaModel { Familia = "Roseta", CantidadConPrecio = 6, CantidadRegalo = 2 });

            vm.GuardarOfertaFamiliaCommand.Execute(oferta);
            await Task.Delay(50);

            A.CallTo(() => _service.InformarVendedores("familia", 55, true)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task GuardarFamiliaDenegacion_NiPregunta_PorqueNoEsUnaOferta()
        {
            A.CallTo(() => _service.CreateOfertaPermitidaFamilia(A<OfertaPermitidaFamiliaCreateModel>._))
                .Returns(Task.FromResult(new OfertaPermitidaFamiliaModel { NOrden = 56, Familia = "Genéricos", Denegar = true }));
            ContestarALaPregunta(true);
            var vm = CrearViewModel();
            var oferta = new OfertaPermitidaFamiliaWrapper(new OfertaPermitidaFamiliaModel { Familia = "Genéricos", CantidadConPrecio = 6, CantidadRegalo = 1 });
            oferta.Denegar = true;

            vm.GuardarOfertaFamiliaCommand.Execute(oferta);
            await Task.Delay(50);

            A.CallTo(() => _dialogService.ShowConfirmationAnswer(A<string>._, A<string>._)).MustNotHaveHappened();
            A.CallTo(() => _service.InformarVendedores(A<string>._, A<int>._, A<bool>._)).MustNotHaveHappened();
            A.CallTo(() => _dialogService.ShowNotification("Oferta por familia 'Genéricos' creada.")).MustHaveHappenedOnceExactly();
        }
    }
}
