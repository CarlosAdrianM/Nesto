using FakeItEasy;
using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.Cajas;
using Nesto.Modulos.Cajas.Interfaces;
using Nesto.Modulos.Cajas.Models;
using Nesto.Modulos.Cajas.Services;
using Nesto.Modulos.Cajas.ViewModels;
using System.Net;

namespace CajasTests
{
    /// <summary>Inmovilizados (Carlos 25/11/26): buscar e imprimir la etiqueta «INMOVILIZADO · NO ES STOCK» de la máquina.</summary>
    [TestClass]
    public class InmovilizadosViewModelTests
    {
        private IInmovilizadosService _servicio = null!;
        private IServicioDialogos _dialogos = null!;
        private List<InmovilizadoModel> _lista = null!;

        [TestInitialize]
        public void Inicializar()
        {
            _servicio = A.Fake<IInmovilizadosService>();
            _dialogos = A.Fake<IServicioDialogos>();
            _lista = new List<InmovilizadoModel>
            {
                new InmovilizadoModel { Empresa = "1", Numero = "1823", Codigo = "I/1823", Descripcion = "MÁQUINA LÁSER", Delegacion = "REI", Estado = "0", EstadoDescripcion = "Vivo" },
                new InmovilizadoModel { Empresa = "1", Numero = "1824", Descripcion = "COMPRESOR", Delegacion = "ALG" }
            };
            A.CallTo(() => _servicio.Buscar(A<string>._, A<string?>._, A<string?>._, A<bool>._)).ReturnsLazily(() => Task.FromResult(_lista));
        }

        private InmovilizadosViewModel CrearViewModel() => new InmovilizadosViewModel(_servicio, _dialogos) { MilisegundosPausaBusqueda = 0 };

        [TestMethod]
        public async Task Buscar_PasaElFiltroLaDelegacionYSoloVivos()
        {
            var vm = CrearViewModel();
            vm.Delegacion = "REI";

            vm.VerAnulados = false;
            await vm.BuscarAsync();

            A.CallTo(() => _servicio.Buscar("1", null, "REI", true)).MustHaveHappenedOnceExactly();
            Assert.AreEqual(2, vm.Inmovilizados.Count);
            Assert.AreEqual("2 inmovilizados.", vm.Resumen);
        }

        [TestMethod]
        public async Task Buscar_TodasYVerAnulados_SinDelegacionYSoloVivosFalse()
        {
            var vm = CrearViewModel();
            await vm.BuscarAsync();
            Fake.ClearRecordedCalls(_servicio);

            vm.VerAnulados = true; // ya se había buscado: vuelve a buscar
            await vm.BuscarAsync();

            A.CallTo(() => _servicio.Buscar("1", A<string?>._, null, false)).MustHaveHappened();
        }

        [TestMethod]
        public async Task Buscar_FiltroConEspacios_SeMandaLimpio()
        {
            var vm = CrearViewModel();
            vm.MilisegundosPausaBusqueda = 60000; // la búsqueda por pausa no llega: se busca con Intro
            vm.Filtro = "  I/1823 ";

            await vm.BuscarAsync();

            A.CallTo(() => _servicio.Buscar("1", "I/1823", null, true)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task Buscar_ErrorDeLaApi_LoEnsenaConSuMensaje()
        {
            A.CallTo(() => _servicio.Buscar(A<string>._, A<string?>._, A<string?>._, A<bool>._))
                .ThrowsAsync(new ErrorApiCajasException("Solo Administración puede consultar los inmovilizados."));
            var vm = CrearViewModel();

            await vm.BuscarAsync();

            A.CallTo(() => _dialogos.ShowError("Solo Administración puede consultar los inmovilizados.")).MustHaveHappenedOnceExactly();
            Assert.IsFalse(vm.EstaOcupado);
        }

        [TestMethod]
        public async Task Imprimir_SinSeleccion_NoSePuede()
        {
            var vm = CrearViewModel();
            await vm.BuscarAsync();

            Assert.IsFalse(vm.ImprimirCommand.CanExecute(null));
            await vm.ImprimirAsync();
            A.CallTo(() => _servicio.ImprimirEtiquetas(A<PeticionImprimirEtiquetasInmovilizado>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task Imprimir_MandaLosElegidosLasCopiasYSinImpresora_YDiceAdondeHanIdo()
        {
            PeticionImprimirEtiquetasInmovilizado? enviada = null;
            A.CallTo(() => _servicio.ImprimirEtiquetas(A<PeticionImprimirEtiquetasInmovilizado>._))
                .ReturnsLazily((PeticionImprimirEtiquetasInmovilizado p) =>
                {
                    enviada = p;
                    return Task.FromResult(new RespuestaImprimirEtiquetasInmovilizado
                    {
                        Impresas = 2,
                        Etiquetas = new List<EtiquetaInmovilizadoImpresa>
                        {
                            new EtiquetaInmovilizadoImpresa { Numero = "1823", Codigo = "I/1823", Impresora = @"\\RDS2016\etiquetasreina", Ok = true },
                            new EtiquetaInmovilizadoImpresa { Numero = "1824", Codigo = "I/1824", Impresora = @"\\RDS2016\etiquetasreina", Ok = true }
                        }
                    });
                });
            var vm = CrearViewModel();
            await vm.BuscarAsync();
            vm.CambiarSeleccion(vm.Inmovilizados);
            vm.Copias = 1;

            Assert.IsTrue(vm.ImprimirCommand.CanExecute(null));
            await vm.ImprimirAsync();

            Assert.IsNotNull(enviada);
            Assert.AreEqual("1", enviada!.Empresa);
            CollectionAssert.AreEqual(new[] { "1823", "1824" }, enviada.Numeros);
            Assert.AreEqual(1, enviada.Copias);
            Assert.IsNull(enviada.Impresora, "Sin impresora: la API elige la de la delegación.");
            A.CallTo(() => _dialogos.ShowNotification(InmovilizadosViewModel.TITULO, @"2 etiquetas enviadas a \\RDS2016\etiquetasreina"))
                .MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task Imprimir_ErrorDeLaApi_LoEnsena()
        {
            A.CallTo(() => _servicio.ImprimirEtiquetas(A<PeticionImprimirEtiquetasInmovilizado>._))
                .ThrowsAsync(new ErrorApiCajasException("No tiene permiso para imprimir etiquetas de inmovilizado."));
            var vm = CrearViewModel();
            await vm.BuscarAsync();
            vm.CambiarSeleccion(new[] { vm.Inmovilizados[0] });

            await vm.ImprimirAsync();

            A.CallTo(() => _dialogos.ShowError("No tiene permiso para imprimir etiquetas de inmovilizado.")).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void Copias_SeQuedanEntreUnaYDiez()
        {
            var vm = CrearViewModel();
            vm.Copias = 0;
            Assert.AreEqual(1, vm.Copias);
            vm.Copias = 25;
            Assert.AreEqual(10, vm.Copias);
        }

        [TestMethod]
        public void ResumenImpresion_ConFallos_DiceCualYPorQue()
        {
            var respuesta = new RespuestaImprimirEtiquetasInmovilizado
            {
                Impresas = 1,
                Etiquetas = new List<EtiquetaInmovilizadoImpresa>
                {
                    new EtiquetaInmovilizadoImpresa { Numero = "1823", Codigo = "I/1823", Impresora = @"\\RDS2016\etiquetasreina", Ok = true },
                    new EtiquetaInmovilizadoImpresa { Numero = "1830", Descripcion = "COMPRESOR", Ok = false, Motivo = "La delegación VAR no tiene impresora." }
                }
            };

            string texto = InmovilizadosViewModel.ResumenImpresion(respuesta, 1);

            Assert.AreEqual(@"1 etiqueta enviada a \\RDS2016\etiquetasreina" + Environment.NewLine +
                "No ha salido la I/1830 (COMPRESOR): La delegación VAR no tiene impresora.", texto);
        }

        [TestMethod]
        public void ResumenImpresion_VariasCopias_LoDice()
        {
            var respuesta = new RespuestaImprimirEtiquetasInmovilizado
            {
                Etiquetas = new List<EtiquetaInmovilizadoImpresa>
                {
                    new EtiquetaInmovilizadoImpresa { Numero = "1823", Impresora = "ALG", Ok = true }
                }
            };

            Assert.AreEqual("1 etiqueta enviada (3 copias de cada una) a ALG", InmovilizadosViewModel.ResumenImpresion(respuesta, 3));
        }

        [TestMethod]
        public void CodigoParaMostrar_SinCodigoDeLaApi_LoCompone()
        {
            Assert.AreEqual("I/1824", new InmovilizadoModel { Numero = "1824" }.CodigoParaMostrar);
            Assert.AreEqual("Vivo", _lista[0].EstadoParaMostrar);
        }

        [TestMethod]
        public void Servicio_Url_ConFiltrosYSoloVivos()
        {
            Assert.AreEqual("Inmovilizados?empresa=1&filtro=I%2F1823&delegacion=REI&soloVivos=true",
                InmovilizadosService.ConstruirUrl("1", " I/1823 ", "REI", true));
            Assert.AreEqual("Inmovilizados?empresa=1&soloVivos=false", InmovilizadosService.ConstruirUrl("1", null, null, false));
        }

        [TestMethod]
        public void Servicio_Mensaje_DeLaApiOElDeSiempre()
        {
            Assert.AreEqual("Solo Administración.", InmovilizadosService.Mensaje(HttpStatusCode.Forbidden, "{\"Message\":\"Solo Administración.\"}", "x"));
            Assert.AreEqual(InmovilizadosService.MENSAJE_SIN_PERMISO, InmovilizadosService.Mensaje(HttpStatusCode.Forbidden, "", "x"));
            Assert.AreEqual(InmovilizadosService.MENSAJE_NO_EXISTE, InmovilizadosService.Mensaje(HttpStatusCode.NotFound, "", "x"));
        }

        [TestMethod]
        public void Respuesta_SeLeeSinDistinguirMayusculasYConId()
        {
            string json = "{\"$id\":\"1\",\"impresas\":1,\"etiquetas\":[{\"$id\":\"2\",\"numero\":1823,\"ok\":true,\"impresora\":\"X\"}]}";

            var respuesta = Newtonsoft.Json.JsonConvert.DeserializeObject<RespuestaImprimirEtiquetasInmovilizado>(json)!;

            Assert.AreEqual(1, respuesta.Impresas);
            Assert.AreEqual("1823", respuesta.Etiquetas[0].Numero);
            Assert.IsTrue(respuesta.Etiquetas[0].Ok);
        }

        [TestMethod]
        public void Menu_AbrirInmovilizados_AbreSuVista()
        {
            var navegacion = A.Fake<IServicioNavegacion>();
            var menu = new CajasMenuBarViewModel(navegacion, A.Fake<IConfiguracion>());

            menu.AbrirInmovilizadosCommand.Execute(null);

            A.CallTo(() => navegacion.RequestNavigate("MainRegion", Cajas.INMOVILIZADOS_VIEW)).MustHaveHappenedOnceExactly();
        }
    }
}
