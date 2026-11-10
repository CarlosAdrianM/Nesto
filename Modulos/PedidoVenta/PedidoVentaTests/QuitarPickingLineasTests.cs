using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Services;
using Nesto.Modulos.PedidoVenta;
using Nesto.Informes;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PedidoVentaTests
{
    /// <summary>
    /// Novedad 1146 (Alfredo, 10/11/26: «lo más común es quitar alguna línea, por ejemplo, los portes»): al quitar el picking se
    /// eligen las líneas. Todas marcadas = el pedido entero (el POST de siempre); si no, solo las marcadas.
    /// </summary>
    [TestClass]
    public class QuitarPickingLineasTests
    {
        private const int PEDIDO = 930110;
        private IPedidoVentaService _servicio;
        private IServicioDialogos _dialogos;
        private IConfiguracion _configuracion;

        [TestInitialize]
        public void Initialize()
        {
            _servicio = A.Fake<IPedidoVentaService>();
            _dialogos = A.Fake<IServicioDialogos>();
            _configuracion = A.Fake<IConfiguracion>();
            A.CallTo(() => _configuracion.UsuarioEnGrupo(A<string>.Ignored)).Returns(true);
        }

        private PickingPopupViewModel Vm()
        {
            var vm = new PickingPopupViewModel(_servicio, new WeakReferenceMessenger(), _dialogos, _configuracion, A.Fake<IInformesService>())
            {
                esPickingPedido = true,
                numeroPedidoPicking = PEDIDO
            };
            return vm;
        }

        private static List<LineaConPicking> Lineas() => new List<LineaConPicking>
        {
            new LineaConPicking { Linea = 1, Picking = 100033, Producto = "12485", Texto = "Agujas Miliun", Cantidad = 20, TipoLinea = 1, SePuedeQuitar = true },
            new LineaConPicking { Linea = 4, Picking = 100033, Producto = "62400003", Texto = "Portes", Cantidad = 1, TipoLinea = 2, SePuedeQuitar = true },
            new LineaConPicking { Linea = 7, Picking = 100030, Producto = "17404", Texto = "Laca", Cantidad = 1, TipoLinea = 1, SePuedeQuitar = false, Motivo = "Ya está en albarán." }
        };

        private void ElUsuarioElige(List<int> lineas, bool todas, ResultadoBoton boton = ResultadoBoton.OK)
        {
            A.CallTo(() => _servicio.LeerLineasConPicking("1", PEDIDO)).Returns(Task.FromResult(Lineas()));
            A.CallTo(() => _dialogos.ShowDialogAsync(QuitarPickingLineasPopupViewModel.NOMBRE, A<ParametrosDialogo>.Ignored))
                .Returns(Task.FromResult(new ResultadoDialogo(boton, new ParametrosDialogo { { "lineas", lineas }, { "todas", todas } })));
        }

        [TestMethod]
        public async Task SoloLosPortes_QuitaSoloEsaLinea()
        {
            ElUsuarioElige(new List<int> { 4 }, todas: false);
            A.CallTo(() => _servicio.QuitarPickingLineas("1", PEDIDO, false, A<List<int>>._))
                .Returns(Task.FromResult(new ResultadoQuitarPicking { Message = "Quitado el picking 100033 de 1 línea del pedido 930110 (Portes)" }));

            await Vm().QuitarPickingAsync();

            A.CallTo(() => _servicio.QuitarPickingLineas("1", PEDIDO, false, A<List<int>>.That.Matches(l => l.Count == 1 && l[0] == 4))).MustHaveHappenedOnceExactly();
            A.CallTo(() => _servicio.QuitarPicking(A<string>._, A<int>._, A<bool>._)).MustNotHaveHappened();
            A.CallTo(() => _dialogos.ShowNotification("Quitar picking", A<string>.That.Contains("Portes"))).MustHaveHappenedOnceExactly();
            // El diálogo de las líneas ya es la pregunta: no se pregunta otra vez
            A.CallTo(() => _dialogos.ShowConfirmationAnswer(A<string>._, A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task TodasMarcadas_EsElPedidoEnteroComoAntes()
        {
            ElUsuarioElige(new List<int> { 1, 4 }, todas: true);

            await Vm().QuitarPickingAsync();

            A.CallTo(() => _servicio.QuitarPicking("1", PEDIDO, false)).MustHaveHappenedOnceExactly();
            A.CallTo(() => _servicio.QuitarPickingLineas(A<string>._, A<int>._, A<bool>._, A<List<int>>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task Cancela_NoQuitaNada()
        {
            ElUsuarioElige(new List<int> { 4 }, todas: false, boton: ResultadoBoton.Cancel);

            await Vm().QuitarPickingAsync();

            A.CallTo(() => _servicio.QuitarPicking(A<string>._, A<int>._, A<bool>._)).MustNotHaveHappened();
            A.CallTo(() => _servicio.QuitarPickingLineas(A<string>._, A<int>._, A<bool>._, A<List<int>>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task ConLecturasDeEsasLineas_PreguntaYConfirmaConLasMismasLineas()
        {
            ElUsuarioElige(new List<int> { 1 }, todas: false);
            A.CallTo(() => _dialogos.ShowConfirmationAnswer(A<string>._, A<string>._)).Returns(true);
            var conLecturas = new ResultadoQuitarPicking { Message = "Pedro está recogiendo", NecesitaConfirmar = true };
            conLecturas.Lecturas.Add(new LecturaQuitarPicking { Picking = 100033, Usuario = "Pedro", Producto = "12485", Nombre = "Agujas Miliun", Cantidad = 20 });
            A.CallTo(() => _servicio.QuitarPickingLineas("1", PEDIDO, false, A<List<int>>._)).Returns(Task.FromResult(conLecturas));
            A.CallTo(() => _servicio.QuitarPickingLineas("1", PEDIDO, true, A<List<int>>._)).Returns(Task.FromResult(new ResultadoQuitarPicking { Message = "Quitado" }));

            await Vm().QuitarPickingAsync();

            A.CallTo(() => _dialogos.ShowConfirmationAnswer("Quitar picking: hay lecturas en la PDA", A<string>.That.Contains("20 Agujas Miliun (12485)"))).MustHaveHappenedOnceExactly();
            A.CallTo(() => _servicio.QuitarPickingLineas("1", PEDIDO, true, A<List<int>>.That.Matches(l => l.Single() == 1))).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task UnaSolaLineaQuitable_LaPreguntaDeSiempre()
        {
            A.CallTo(() => _servicio.LeerLineasConPicking("1", PEDIDO)).Returns(Task.FromResult(Lineas().Where(l => l.Linea != 1).ToList()));
            A.CallTo(() => _dialogos.ShowConfirmationAnswer(A<string>._, A<string>._)).Returns(true);

            await Vm().QuitarPickingAsync();

            A.CallTo(() => _dialogos.ShowDialogAsync(A<string>._, A<ParametrosDialogo>._)).MustNotHaveHappened();
            A.CallTo(() => _servicio.QuitarPicking("1", PEDIDO, false)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task SiNoSePuedenLeerLasLineas_LaPreguntaDeSiempre()
        {
            A.CallTo(() => _servicio.LeerLineasConPicking("1", PEDIDO)).ThrowsAsync(new System.Exception("404"));
            A.CallTo(() => _dialogos.ShowConfirmationAnswer(A<string>._, A<string>._)).Returns(true);

            await Vm().QuitarPickingAsync();

            A.CallTo(() => _servicio.QuitarPicking("1", PEDIDO, false)).MustHaveHappenedOnceExactly();
        }

        // ===== El diálogo

        private static QuitarPickingLineasPopupViewModel Popup()
        {
            var vm = new QuitarPickingLineasPopupViewModel();
            vm.OnDialogOpened(new ParametrosDialogo { { "lineas", Lineas() }, { "pedido", PEDIDO } });
            return vm;
        }

        [TestMethod]
        public void Popup_TodasLasQueSePuedenSalenMarcadas_LasDemasNo()
        {
            QuitarPickingLineasPopupViewModel vm = Popup();

            Assert.IsTrue(vm.Lineas.Single(l => l.Linea == 1).Marcado);
            Assert.IsTrue(vm.Lineas.Single(l => l.Linea == 4).Marcado);
            Assert.IsFalse(vm.Lineas.Single(l => l.Linea == 7).Marcado);
            Assert.IsTrue(vm.EstanTodas);
            StringAssert.Contains(vm.Cabecera, "930110");

            vm.Lineas.Single(l => l.Linea == 7).Marcado = true;
            Assert.IsFalse(vm.Lineas.Single(l => l.Linea == 7).Marcado, "La que no se puede quitar no se marca");
        }

        [TestMethod]
        public void Popup_DesmarcarDevuelveSoloLasMarcadas_SinNingunaNoDeja()
        {
            QuitarPickingLineasPopupViewModel vm = Popup();
            ResultadoDialogo cerrado = null;
            vm.RequestClose += r => cerrado = r;

            vm.Lineas.Single(l => l.Linea == 1).Marcado = false;
            Assert.IsFalse(vm.EstanTodas);
            vm.QuitarCommand.Execute(null);

            Assert.AreEqual(ResultadoBoton.OK, cerrado.Result);
            CollectionAssert.AreEqual(new List<int> { 4 }, cerrado.Parameters.GetValue<List<int>>("lineas"));
            Assert.IsFalse(cerrado.Parameters.GetValue<bool>("todas"));

            vm.Lineas.Single(l => l.Linea == 4).Marcado = false;
            Assert.IsFalse(vm.QuitarCommand.CanExecute(null));
            vm.OnDialogClosed();
        }
    }
}
