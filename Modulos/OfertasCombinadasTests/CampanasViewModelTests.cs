using ControlesUsuario.Dialogs;
using ControlesUsuario.Services;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.OfertasCombinadas.Interfaces;
using Nesto.Modulos.OfertasCombinadas.Models;
using Nesto.Modulos.OfertasCombinadas.ViewModels;
using Prism.Regions;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Nesto.Modulos.OfertasCombinadasTests
{
    /// <summary>
    /// NestoAPI#423: pestaña de Campañas. Hasta ahora las campañas comerciales vivían solo en las
    /// reglas de catálogo de PrestaShop —el profesional no se llevaba el descuento, y en 502
    /// productos de las rebajas de verano de 2026 acababa pagando MÁS que el público— y meterlas
    /// en Nesto era teclear INSERTs a mano.
    ///
    /// Lo que más se prueba aquí es la conversión porcentaje ↔ tanto por uno. Es la parte que
    /// puede hacer daño de verdad: la rejilla habla en porcentajes (20 = 20 %) porque es como se
    /// habla de una campaña, pero la tabla y la API trabajan en tanto por uno (0,20). Colar un 20
    /// donde va un 0,20 sería un 2.000 % de descuento.
    /// </summary>
    [TestClass]
    public class CampanasViewModelTests
    {
        private IOfertasCombinadasService _service;
        private IConfiguracion _configuracion;
        private IDialogService _dialogService;
        private IRegionManager _regionManager;
        private IServicioProducto _servicioProducto;

        [TestInitialize]
        public void Setup()
        {
            _service = A.Fake<IOfertasCombinadasService>();
            _configuracion = A.Fake<IConfiguracion>();
            _dialogService = A.Fake<IDialogService>();
            _regionManager = A.Fake<IRegionManager>();
            _servicioProducto = A.Fake<IServicioProducto>();

            A.CallTo(() => _service.GetOfertasCombinadas(A<string>._, A<bool>._))
                .Returns(Task.FromResult(new List<OfertaCombinadaModel>()));
            A.CallTo(() => _service.GetOfertasPermitidasFamilia(A<string>._))
                .Returns(Task.FromResult(new List<OfertaPermitidaFamiliaModel>()));
            A.CallTo(() => _service.GetOfertasEscalonadas(A<string>._, A<bool>._))
                .Returns(Task.FromResult(new List<OfertaEscalonadaModel>()));
            A.CallTo(() => _service.GetCampanas(A<bool>._, A<bool>._))
                .Returns(Task.FromResult(new List<CampanaModel>()));
        }

        private OfertasCombinadasViewModel CrearViewModel()
        {
            return new OfertasCombinadasViewModel(_service, _configuracion, _dialogService, _regionManager, _servicioProducto);
        }


        /// <summary>
        /// ShowConfirmationAnswer es un metodo de EXTENSION, asi que no se puede fakear: por dentro
        /// llama a ShowDialog("ConfirmationDialog", ..., callback) y se queda con el ButtonResult
        /// que le devuelva el callback. Eso si es del interfaz, y es lo que se dobla aqui.
        /// </summary>
        private void ResponderALaConfirmacion(ButtonResult respuesta)
        {
            A.CallTo(() => _dialogService.ShowDialog("ConfirmationDialog", A<IDialogParameters>._, A<Action<IDialogResult>>._))
                .Invokes((string _, IDialogParameters __, Action<IDialogResult> callback) =>
                    callback(new DialogResult(respuesta)));
        }

        #region La conversión de porcentajes

        [TestMethod]
        public void Wrapper_DesdeElModelo_ConvierteTantoPorUnoAPorcentaje()
        {
            var wrapper = new CampanaWrapper(new CampanaModel { Descuento = 0.20M, DescuentoPublico = 0.15M });

            Assert.AreEqual(20M, wrapper.DescuentoPorcentaje);
            Assert.AreEqual(15M, wrapper.DescuentoPublicoPorcentaje);
        }

        [TestMethod]
        public void Wrapper_HaciaElModelo_ConviertePorcentajeATantoPorUno()
        {
            var wrapper = new CampanaWrapper { Producto = "44166", DescuentoPorcentaje = 20M, DescuentoPublicoPorcentaje = 15M };

            CampanaModel modelo = wrapper.AModelo();

            Assert.AreEqual(0.20M, modelo.Descuento);
            Assert.AreEqual(0.15M, modelo.DescuentoPublico);
        }

        // Nulo significa "el público se lleva el mismo porcentaje que el profesional", que NO es lo
        // mismo que un 0 % (eso sería "al público no le rebajamos nada").
        [TestMethod]
        public void Wrapper_SinDescuentoPublico_ViajaNuloYNoCero()
        {
            var wrapper = new CampanaWrapper { Producto = "44166", DescuentoPorcentaje = 20M, DescuentoPublicoPorcentaje = null };

            Assert.IsNull(wrapper.AModelo().DescuentoPublico);
        }

        [TestMethod]
        public void Wrapper_IdaYVuelta_NoPierdeElValor()
        {
            var original = new CampanaModel { Descuento = 0.075M, DescuentoPublico = 0.125M, Producto = "44166" };

            CampanaModel vuelta = new CampanaWrapper(original).AModelo();

            Assert.AreEqual(0.075M, vuelta.Descuento);
            Assert.AreEqual(0.125M, vuelta.DescuentoPublico);
        }

        #endregion

        #region Alta

        // Audiencia 2 y desde hoy: lo que se quiere el 99 % de las veces, y lo que menos sorprende
        // si alguien guarda sin mirar.
        [TestMethod]
        public void NuevaCampana_TraePorDefectoProfesionalesYPublicoDesdeHoy()
        {
            var vm = CrearViewModel();

            vm.NuevaCampanaCommand.Execute(null);

            CampanaWrapper nueva = vm.Campanas.Single();
            Assert.AreEqual(2, nueva.AudienciaOferta);
            Assert.AreEqual(DateTime.Today, nueva.FechaDesde);
            Assert.AreEqual(0, nueva.Id);
        }

        [TestMethod]
        public async Task GuardarCampana_ConProductoYFamiliaALaVez_NiSiquieraLlamaAlServicio()
        {
            var vm = CrearViewModel();
            var campana = new CampanaWrapper { Producto = "44166", Familia = "Ufaes", DescuentoPorcentaje = 20M };

            vm.GuardarCampanaCommand.Execute(campana);
            await Task.Delay(50);

            A.CallTo(() => _service.CreateCampana(A<CampanaModel>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task GuardarCampana_SinDescuento_NiSiquieraLlamaAlServicio()
        {
            var vm = CrearViewModel();
            var campana = new CampanaWrapper { Producto = "44166", DescuentoPorcentaje = 0M };

            vm.GuardarCampanaCommand.Execute(campana);
            await Task.Delay(50);

            A.CallTo(() => _service.CreateCampana(A<CampanaModel>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task GuardarCampana_Nueva_CreaYSeQuedaConLoQueDevuelveElServidor()
        {
            A.CallTo(() => _service.CreateCampana(A<CampanaModel>._))
                .Returns(Task.FromResult(new CampanaModel
                {
                    Id = 777, Producto = "44166", Descuento = 0.20M, AudienciaOferta = 2, Vigente = true
                }));
            var vm = CrearViewModel();
            var campana = new CampanaWrapper { Producto = "44166", DescuentoPorcentaje = 20M, AudienciaOferta = 2 };

            vm.GuardarCampanaCommand.Execute(campana);
            await Task.Delay(50);

            A.CallTo(() => _service.CreateCampana(A<CampanaModel>.That.Matches(c => c.Descuento == 0.20M)))
                .MustHaveHappenedOnceExactly();
            Assert.AreEqual(777, campana.Id);
            Assert.IsTrue(campana.Vigente);
            Assert.IsFalse(campana.HaCambiado, "Recien guardada no puede quedar marcada como sucia");
        }

        [TestMethod]
        public async Task GuardarCampana_Existente_ActualizaEnVezDeCrear()
        {
            A.CallTo(() => _service.UpdateCampana(A<int>._, A<CampanaModel>._))
                .Returns(Task.FromResult(new CampanaModel { Id = 500, Producto = "44166", Descuento = 0.10M }));
            var vm = CrearViewModel();
            var campana = new CampanaWrapper(new CampanaModel { Id = 500, Producto = "44166", Descuento = 0.20M })
            {
                DescuentoPorcentaje = 10M
            };

            vm.GuardarCampanaCommand.Execute(campana);
            await Task.Delay(50);

            A.CallTo(() => _service.UpdateCampana(500, A<CampanaModel>._)).MustHaveHappenedOnceExactly();
            A.CallTo(() => _service.CreateCampana(A<CampanaModel>._)).MustNotHaveHappened();
        }

        #endregion

        #region Carga y borrado

        [TestMethod]
        public async Task Cargar_TraeLasCampanasDelServicio()
        {
            A.CallTo(() => _service.GetCampanas(A<bool>._, A<bool>._))
                .Returns(Task.FromResult(new List<CampanaModel>
                {
                    new CampanaModel { Id = 1, Familia = "Ufaes", Descuento = 0.15M, Vigente = true },
                    new CampanaModel { Id = 2, Producto = "44166", Descuento = 0.20M, Vigente = false }
                }));
            var vm = CrearViewModel();

            vm.CargarCommand.Execute(null);
            await Task.Delay(80);

            Assert.AreEqual(2, vm.Campanas.Count);
            Assert.AreEqual(15M, vm.Campanas.First().DescuentoPorcentaje);
        }

        // Por defecto solo las vivas: la lista es el histórico entero y las del año pasado
        // esconderían las que están corriendo.
        [TestMethod]
        public void PorDefecto_NoSePidenLasCaducadas()
        {
            var vm = CrearViewModel();

            Assert.IsFalse(vm.IncluirCampanasCaducadas);
        }

        // Una fila que nunca se guardó no existe en el servidor: quitarla es cosa de la rejilla.
        [TestMethod]
        public async Task EliminarCampana_SinGuardar_LaQuitaSinLlamarAlServicio()
        {
            var vm = CrearViewModel();
            vm.NuevaCampanaCommand.Execute(null);
            CampanaWrapper nueva = vm.Campanas.Single();

            vm.EliminarCampanaCommand.Execute(nueva);
            await Task.Delay(50);

            Assert.AreEqual(0, vm.Campanas.Count);
            A.CallTo(() => _service.DeleteCampana(A<int>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task EliminarCampana_Guardada_PideConfirmacionYBorra()
        {
            ResponderALaConfirmacion(ButtonResult.OK);
            var vm = CrearViewModel();
            var campana = new CampanaWrapper(new CampanaModel { Id = 500, Producto = "44166" });
            vm.Campanas.Add(campana);

            vm.EliminarCampanaCommand.Execute(campana);
            await Task.Delay(50);

            A.CallTo(() => _service.DeleteCampana(500)).MustHaveHappenedOnceExactly();
            Assert.AreEqual(0, vm.Campanas.Count);
        }

        [TestMethod]
        public async Task EliminarCampana_SiNoSeConfirma_NoBorraNada()
        {
            ResponderALaConfirmacion(ButtonResult.Cancel);
            var vm = CrearViewModel();
            var campana = new CampanaWrapper(new CampanaModel { Id = 500, Producto = "44166" });
            vm.Campanas.Add(campana);

            vm.EliminarCampanaCommand.Execute(campana);
            await Task.Delay(50);

            A.CallTo(() => _service.DeleteCampana(A<int>._)).MustNotHaveHappened();
            Assert.AreEqual(1, vm.Campanas.Count);
        }

        #endregion

        #region Operaciones sobre una campaña entera

        private void ConfigurarCampanas(params ResumenCampanaModel[] resumenes)
        {
            A.CallTo(() => _service.GetNombresDeCampana())
                .Returns(Task.FromResult(resumenes.ToList()));
        }

        [TestMethod]
        public async Task Cargar_TraeLosNombresDeCampanaParaElFiltro()
        {
            ConfigurarCampanas(new ResumenCampanaModel { Campana = "Rebajas verano 2026", Filas = 2017, FilasQueViajan = 0 });
            var vm = CrearViewModel();

            vm.CargarCommand.Execute(null);
            await Task.Delay(80);

            Assert.AreEqual(1, vm.ResumenCampanas.Count);
            Assert.AreEqual(2017, vm.ResumenCampanas.Single().Filas);
        }

        // El desplegable enseña los recuentos a propósito: nadie debería cerrar ni borrar 2.017
        // filas sin ver antes el número.
        [TestMethod]
        public void ResumenCampana_SeVeConSusRecuentos()
        {
            var resumen = new ResumenCampanaModel { Campana = "Rebajas verano 2026", Filas = 2017, FilasQueViajan = 0 };

            StringAssert.Contains(resumen.Descripcion, "2017");
            StringAssert.Contains(resumen.Descripcion, "Rebajas verano 2026");
        }

        [TestMethod]
        public void OperacionesEnBloque_SinCampanaElegida_EstanDeshabilitadas()
        {
            var vm = CrearViewModel();

            Assert.IsFalse(vm.CerrarCampanaCommand.CanExecute(null));
            Assert.IsFalse(vm.BorrarCampanaCommand.CanExecute(null));
        }

        [TestMethod]
        public void OperacionesEnBloque_AlElegirCampana_SeHabilitan()
        {
            var vm = CrearViewModel();

            vm.CampanaSeleccionada = new ResumenCampanaModel { Campana = "Rebajas verano 2026", Filas = 2017 };

            Assert.IsTrue(vm.CerrarCampanaCommand.CanExecute(null));
            Assert.IsTrue(vm.BorrarCampanaCommand.CanExecute(null));
        }

        [TestMethod]
        public async Task CerrarCampana_SiSeConfirma_LlamaAlServicio()
        {
            ResponderALaConfirmacion(ButtonResult.OK);
            A.CallTo(() => _service.CerrarCampana(A<string>._, A<DateTime?>._))
                .Returns(Task.FromResult(new ResultadoOperacionCampanaModel { FilasAfectadas = 2017, ProductosEncolados = 0 }));
            var vm = CrearViewModel();
            vm.CampanaSeleccionada = new ResumenCampanaModel { Campana = "Rebajas verano 2026", Filas = 2017 };

            vm.CerrarCampanaCommand.Execute(null);
            await Task.Delay(80);

            A.CallTo(() => _service.CerrarCampana("Rebajas verano 2026", A<DateTime?>._)).MustHaveHappenedOnceExactly();
        }

        // Borrar 2.017 filas no puede pasar por un despiste.
        [TestMethod]
        public async Task BorrarCampana_SiNoSeConfirma_NoBorraNada()
        {
            ResponderALaConfirmacion(ButtonResult.Cancel);
            var vm = CrearViewModel();
            vm.CampanaSeleccionada = new ResumenCampanaModel { Campana = "Rebajas verano 2026", Filas = 2017 };

            vm.BorrarCampanaCommand.Execute(null);
            await Task.Delay(80);

            A.CallTo(() => _service.DeleteCampanaPorNombre(A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task BorrarCampana_SiSeConfirma_LlamaAlServicio()
        {
            ResponderALaConfirmacion(ButtonResult.OK);
            A.CallTo(() => _service.DeleteCampanaPorNombre(A<string>._))
                .Returns(Task.FromResult(new ResultadoOperacionCampanaModel { FilasAfectadas = 2017, ProductosEncolados = 0 }));
            var vm = CrearViewModel();
            vm.CampanaSeleccionada = new ResumenCampanaModel { Campana = "Rebajas verano 2026", Filas = 2017 };

            vm.BorrarCampanaCommand.Execute(null);
            await Task.Delay(80);

            A.CallTo(() => _service.DeleteCampanaPorNombre("Rebajas verano 2026")).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void Wrapper_LlevaElNombreDeCampanaIdaYVuelta()
        {
            var wrapper = new CampanaWrapper(new CampanaModel { Producto = "44166", Campana = "Black Friday 2026" });

            Assert.AreEqual("Black Friday 2026", wrapper.Campana);
            Assert.AreEqual("Black Friday 2026", wrapper.AModelo().Campana);
        }

        // Vacío = descuento de siempre, no una campaña que se llame "".
        [TestMethod]
        public void Wrapper_SinNombreDeCampana_ViajaNulo()
        {
            var wrapper = new CampanaWrapper { Producto = "44166", Campana = "   " };

            Assert.IsNull(wrapper.AModelo().Campana);
        }

        #endregion

        #region Detalles de la rejilla

        [TestMethod]
        public void Ambito_DeUnaCampanaDeFamiliaConGrupo_LosMuestraLosDos()
        {
            var wrapper = new CampanaWrapper { Familia = "Ufaes", Grupo = "COS" };

            Assert.AreEqual("Ufaes / COS", wrapper.Ambito);
        }

        [TestMethod]
        public void Ambito_DeUnaCampanaDeProducto_EsElProducto()
        {
            Assert.AreEqual("44166", new CampanaWrapper { Producto = "44166" }.Ambito);
        }

        // El 3 ("solo público") no se puede ni elegir: lo prohíbe la base de datos porque el motor
        // de precios no mira la audiencia y le descontaría igual al profesional en el pedido.
        [TestMethod]
        public void Audiencias_NoOfrecenElSoloPublico()
        {
            var vm = CrearViewModel();

            CollectionAssert.AreEquivalent(new byte[] { 0, 1, 2 }, vm.AudienciasCampana.Select(a => a.Valor).ToList());
        }

        // Cargar una campaña del servidor no es un cambio del usuario: si quedara sucia, saldría
        // el botón de Guardar en todas las filas nada más abrir la pestaña.
        [TestMethod]
        public void Wrapper_ReciencargadoDelServidor_NoEstaSucio()
        {
            var wrapper = new CampanaWrapper(new CampanaModel { Id = 1, Producto = "44166", Descuento = 0.20M });

            Assert.IsFalse(wrapper.HaCambiado);
        }

        [TestMethod]
        public void Wrapper_AlTocarloElUsuario_QuedaSucio()
        {
            var wrapper = new CampanaWrapper(new CampanaModel { Id = 1, Producto = "44166", Descuento = 0.20M });

            wrapper.DescuentoPorcentaje = 30M;

            Assert.IsTrue(wrapper.HaCambiado);
        }

        #endregion
    }
}
