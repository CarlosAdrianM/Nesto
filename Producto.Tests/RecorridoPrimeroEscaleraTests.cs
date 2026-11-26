using FakeItEasy;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Modules.Producto;
using Nesto.Modules.Producto.ViewModels;
using Newtonsoft.Json;

namespace Producto.Tests
{
    /// <summary>
    /// NestoAPI#888 (sugerencia 1418): casilla «Primero las filas de escalera» del recorrido del almacén. GET la trae; el PUT la manda solo
    /// si ha cambiado (null = no se toca); con el dato sin crear en el servidor, el 503 se enseña y lo demás sigue editable.
    /// </summary>
    [TestClass]
    public class RecorridoPrimeroEscaleraTests
    {
        private IServicioRecorridoAlmacen _servicio = null!;
        private IConfiguracion _configuracion = null!;
        private IServicioDialogos _dialogos = null!;
        private readonly List<PeticionRecorridoAlmacen> _puts = new();
        private bool _guardado;

        [TestInitialize]
        public void Inicializar()
        {
            _servicio = A.Fake<IServicioRecorridoAlmacen>();
            _configuracion = A.Fake<IConfiguracion>();
            _dialogos = A.Fake<IServicioDialogos>();
            A.CallTo(() => _configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ALMACEN)).Returns(true);
            A.CallTo(() => _servicio.Leer(A<string>._, A<string>._)).ReturnsLazily(() => Task.FromResult(Recorrido(_guardado)));
            A.CallTo(() => _servicio.Guardar(A<string>._, A<string>._, A<PeticionRecorridoAlmacen>._, A<bool>._))
                .ReturnsLazily((string _, string _, PeticionRecorridoAlmacen p, bool _) =>
                {
                    _puts.Add(p);
                    return Task.FromResult(Recorrido(p.PrimeroEscalera ?? _guardado));
                });
            A.CallTo(() => _dialogos.ShowConfirmationAnswer(A<string>._, A<string>._)).Returns(true);
        }

        private static RecorridoAlmacen Recorrido(bool primeroEscalera) => new RecorridoAlmacen
        {
            Empresa = "1",
            Almacen = "ALG",
            TablasCreadas = true,
            FilaAMano = 4,
            PrimeroEscalera = primeroEscalera,
            Calles = new List<CalleRecorrido>
            {
                new CalleRecorrido { Calle = "a", Sentido = "A", Pasillos = new List<PasilloRecorrido> { new PasilloRecorrido { Pasillo = "001" } } }
            }
        };

        private async Task<RecorridoAlmacenViewModel> Vm()
        {
            var vm = new RecorridoAlmacenViewModel(_servicio, _configuracion, _dialogos);
            await vm.CargarAsync();
            return vm;
        }

        [TestMethod]
        public async Task Cargar_TraeLaCasillaYSinCambios()
        {
            _guardado = true;
            var vm = await Vm();

            Assert.IsTrue(vm.PrimeroEscalera);
            Assert.IsFalse(vm.HayCambios);
        }

        [TestMethod]
        public async Task SinTocarla_ElPutNoLaManda()
        {
            var vm = await Vm();
            vm.FilaAMano = "5";

            await vm.GuardarAsync();

            Assert.IsNull(_puts.Single().PrimeroEscalera);
            Assert.IsFalse(JsonConvert.SerializeObject(_puts.Single()).Contains("PrimeroEscalera"), "null = no se toca: ni viaja");
        }

        [TestMethod]
        public async Task Marcarla_HayCambiosYSeManda()
        {
            var vm = await Vm();

            vm.PrimeroEscalera = true;
            Assert.IsTrue(vm.HayCambios);
            await vm.GuardarAsync();

            Assert.AreEqual(true, _puts.Single().PrimeroEscalera);
            Assert.IsTrue(vm.PrimeroEscalera);
            Assert.IsFalse(vm.HayCambios, "lo guardado es lo cargado");
        }

        [TestMethod]
        public async Task Desmarcarla_MandaFalse()
        {
            _guardado = true;
            var vm = await Vm();

            vm.PrimeroEscalera = false;
            await vm.VistaPreviaAsync();

            Assert.AreEqual(false, _puts.Single().PrimeroEscalera);
        }

        [TestMethod]
        public async Task SinElDatoEnElServidor_503ConSuMotivo_YLoDemasSigueEditable()
        {
            const string motivo = "Falta en la base de datos el dato «primero la escalera» de los almacenes (Scripts/Issue888_RecorridoPrimeroEscalera.sql). Avise a informática.";
            A.CallTo(() => _servicio.Guardar(A<string>._, A<string>._, A<PeticionRecorridoAlmacen>._, A<bool>._))
                .ThrowsAsync(new RecorridoAlmacenException(motivo, 503));
            var vm = await Vm();
            vm.PrimeroEscalera = true;

            await vm.GuardarAsync();

            StringAssert.StartsWith(vm.Mensaje, motivo);
            StringAssert.Contains(vm.Mensaje, "Desmarque «Primero las filas de escalera»");
            Assert.IsTrue(vm.Editable, "solo falta ese dato: lo demás se puede guardar");
            A.CallTo(() => _dialogos.ShowError(vm.Mensaje)).MustHaveHappened();
        }

        [TestMethod]
        public void LaAyudaExplicaElEfecto()
        {
            StringAssert.Contains(RecorridoAlmacenViewModel.AYUDA_PRIMERO_ESCALERA, "de arriba abajo");
            StringAssert.Contains(RecorridoAlmacenViewModel.AYUDA_PRIMERO_ESCALERA, "a mano");
        }
    }
}
