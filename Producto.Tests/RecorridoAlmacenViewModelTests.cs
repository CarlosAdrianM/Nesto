using FakeItEasy;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Services;
using Nesto.Infrastructure.Shared;
using Nesto.Modules.Producto;
using Nesto.Modules.Producto.ViewModels;
using Newtonsoft.Json.Linq;
using System.Net;
using System.Net.Http;
using System.Text;

namespace Producto.Tests
{
    /// <summary>
    /// Nesto#528 (corte 4 de NestoAPI#639, API en NestoAPI#642): la ventana «Recorrido del almacén». Calles en orden con sentido y
    /// pasillos, pasillos sin calle y fila a mano; vista previa (PUT ensayo=true) y guardar (PUT, con confirmación). Solo Almacén y
    /// Dirección guardan; sin tablas o con 503 no se puede tocar nada; los 400 se enseñan tal cual.
    /// </summary>
    [TestClass]
    public class RecorridoAlmacenViewModelTests
    {
        private IServicioRecorridoAlmacen _servicio = null!;
        private IConfiguracion _configuracion = null!;
        private IServicioDialogos _dialogos = null!;
        private readonly List<(PeticionRecorridoAlmacen Peticion, bool Ensayo)> _puts = new();

        [TestInitialize]
        public void Inicializar()
        {
            _servicio = A.Fake<IServicioRecorridoAlmacen>();
            _configuracion = A.Fake<IConfiguracion>();
            _dialogos = A.Fake<IServicioDialogos>();
            A.CallTo(() => _servicio.Leer(A<string>._, A<string>._)).ReturnsLazily(() => Task.FromResult(Recorrido()));
            A.CallTo(() => _servicio.Guardar(A<string>._, A<string>._, A<PeticionRecorridoAlmacen>._, A<bool>._))
                .ReturnsLazily((string _, string _, PeticionRecorridoAlmacen p, bool ensayo) =>
                {
                    _puts.Add((p, ensayo));
                    return Task.FromResult(DesdePeticion(p, ensayo));
                });
            A.CallTo(() => _dialogos.ShowConfirmationAnswer(A<string>._, A<string>._)).Returns(true);
            A.CallTo(() => _dialogos.ShowConfirmationAnswer(A<string>._, A<string>._, A<bool>._)).Returns(true);
        }

        private static RecorridoAlmacen Recorrido(bool tablas = true) => new RecorridoAlmacen
        {
            Empresa = "1",
            Almacen = "ALG",
            TablasCreadas = tablas,
            FilaAMano = 4,
            Calles = new List<CalleRecorrido>
            {
                new CalleRecorrido { Id = 1, Calle = "a", Orden = 10, Sentido = "A", Descripcion = "Entrada", Pasillos = new List<PasilloRecorrido>
                {
                    new PasilloRecorrido { Pasillo = "001", Posicion = 1, Nombre = "Esmaltes", Huecos = 30 },
                    new PasilloRecorrido { Pasillo = "002", Posicion = 2, Huecos = 20 }
                } },
                new CalleRecorrido { Id = 2, Calle = "b", Orden = 20, Sentido = "A", Pasillos = new List<PasilloRecorrido>
                {
                    new PasilloRecorrido { Pasillo = "003", Posicion = 1, Huecos = 10 }
                } },
                new CalleRecorrido { Id = 3, Calle = "c", Orden = 30, Sentido = "D", Pasillos = new List<PasilloRecorrido>() }
            },
            PasillosSinCalle = new List<PasilloRecorrido>
            {
                new PasilloRecorrido { Pasillo = "009", Huecos = 5 },
                new PasilloRecorrido { Pasillo = "012", Huecos = 2 }
            },
            Tramos = new List<TramoRecorrido>
            {
                new TramoRecorrido { Orden = 1, Calle = "a", Pasillo = "001", Columna = "001", Nivel = "AMano", Filas = new List<string> { "004", "005" } },
                new TramoRecorrido { Orden = 2, Calle = null, Pasillo = "009", Columna = "001", Nivel = null, Filas = new List<string> { "001" } }
            }
        };

        /// <summary>Lo que devolvería la API: las calles tal como se mandan, con un tramo por pasillo.</summary>
        private static RecorridoAlmacen DesdePeticion(PeticionRecorridoAlmacen p, bool ensayo)
        {
            int orden = 0;
            return new RecorridoAlmacen
            {
                TablasCreadas = true,
                Ensayo = ensayo,
                FilaAMano = p.FilaAMano ?? 4,
                Calles = p.Calles.Select(c => new CalleRecorrido
                {
                    Calle = c.Calle,
                    Sentido = c.Sentido,
                    Descripcion = c.Descripcion,
                    Pasillos = c.Pasillos.Select(x => new PasilloRecorrido { Pasillo = x.Pasillo.PadLeft(3, '0'), Nombre = x.Nombre }).ToList()
                }).ToList(),
                PasillosSinCalle = p.PasillosSinCalle.Select(x => new PasilloRecorrido { Pasillo = x.Pasillo, Nombre = x.Nombre }).ToList(),
                Tramos = p.Calles.SelectMany(c => c.Pasillos.Select(x => new TramoRecorrido
                {
                    Orden = ++orden,
                    Calle = c.Calle,
                    Pasillo = x.Pasillo,
                    Columna = "001",
                    Nivel = "AMano",
                    Filas = new List<string> { "004" }
                })).ToList()
            };
        }

        private async Task<RecorridoAlmacenViewModel> Vm(params string[] grupos)
        {
            foreach (string grupo in grupos)
            {
                A.CallTo(() => _configuracion.UsuarioEnGrupo(grupo)).Returns(true);
            }
            var vm = new RecorridoAlmacenViewModel(_servicio, _configuracion, _dialogos);
            await vm.CargarAsync();
            return vm;
        }

        private static string[] Orden(RecorridoAlmacenViewModel vm) => vm.Calles.Select(c => c.Calle).ToArray();

        // ---------------------------------------------------------------- leer

        [TestMethod]
        public async Task Cargar_PideEmpresa1YAlgete_YPintaCallesPasillosSinCalleFilaYTramos()
        {
            var vm = await Vm(Constantes.GruposSeguridad.ALMACEN);

            A.CallTo(() => _servicio.Leer("1", "ALG")).MustHaveHappenedOnceExactly();
            CollectionAssert.AreEqual(new[] { "a", "b", "c" }, Orden(vm));
            Assert.AreEqual("D", vm.Calles[2].Sentido);
            CollectionAssert.AreEqual(new[] { "001", "002" }, vm.Calles[0].Pasillos.Select(p => p.Pasillo).ToArray());
            Assert.AreEqual("Esmaltes", vm.Calles[0].Pasillos[0].Nombre);
            Assert.AreEqual("001, 002", vm.Calles[0].ResumenPasillos);
            CollectionAssert.AreEqual(new[] { "009", "012" }, vm.PasillosSinCalle.Select(p => p.Pasillo).ToArray());
            Assert.AreEqual("4", vm.FilaAMano);
            Assert.AreEqual(2, vm.Tramos.Count);
            Assert.AreEqual("A mano", vm.Tramos[0].Nivel);
            Assert.AreEqual("004, 005", vm.Tramos[0].Filas);
            Assert.AreEqual("(sin calle)", vm.Tramos[1].Calle);
            Assert.AreSame(vm.Calles[0], vm.CalleSeleccionada);
            Assert.IsTrue(vm.Editable);
            Assert.IsFalse(vm.HayCambios);
            Assert.IsFalse(vm.HayAviso);
        }

        [TestMethod]
        public async Task AlLlegar_LeeUnaSolaVez()
        {
            var vm = await Vm(Constantes.GruposSeguridad.ALMACEN);

            vm.AlLlegar(new ParametrosNavegacion());

            A.CallTo(() => _servicio.Leer(A<string>._, A<string>._)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task SinTablas_AvisaYNadaEsEditable()
        {
            A.CallTo(() => _servicio.Leer(A<string>._, A<string>._)).ReturnsLazily(() => Task.FromResult(Recorrido(tablas: false)));
            var vm = await Vm(Constantes.GruposSeguridad.DIRECCION);

            Assert.IsFalse(vm.Disponible);
            Assert.IsFalse(vm.Editable);
            Assert.AreEqual(RecorridoAlmacenViewModel.MENSAJE_SIN_TABLAS, vm.Aviso);
            Assert.IsFalse(vm.SubirCalleCommand.CanExecute(null));
            Assert.IsFalse(vm.VistaPreviaCommand.CanExecute(null));
            Assert.IsFalse(vm.SerpentinaCommand.CanExecute(null));
        }

        [TestMethod]
        public async Task Leer503_AvisaConElMotivoYNadaEsEditable()
        {
            A.CallTo(() => _servicio.Leer(A<string>._, A<string>._))
                .ThrowsAsync(new RecorridoAlmacenException("El mantenimiento del recorrido no está configurado en el servidor.", 503));
            var vm = await Vm(Constantes.GruposSeguridad.ALMACEN);

            Assert.IsFalse(vm.Editable);
            StringAssert.StartsWith(vm.Aviso, "El mantenimiento del recorrido no está configurado en el servidor.");
            Assert.IsFalse(vm.NuevaCalleCommand.CanExecute(null));
        }

        [TestMethod]
        public async Task SinSerDeAlmacenNiDireccion_SoloLectura()
        {
            var vm = await Vm(Constantes.GruposSeguridad.TIENDAS);

            Assert.IsTrue(vm.SoloLectura);
            Assert.IsTrue(vm.Disponible, "Se ve el recorrido");
            Assert.IsFalse(vm.Editable);
            Assert.IsTrue(vm.NoEditable);
            Assert.AreEqual(RecorridoAlmacenViewModel.MENSAJE_SOLO_LECTURA, vm.Aviso);
            Assert.IsFalse(vm.BajarCalleCommand.CanExecute(null));
            Assert.IsFalse(vm.GuardarCommand.CanExecute(null));
            vm.BajarCalle();
            CollectionAssert.AreEqual(new[] { "a", "b", "c" }, Orden(vm), "Ni llamando al método");
        }

        // ---------------------------------------------------------------- calles

        [TestMethod]
        public async Task SubirYBajarCalle_CambiaElOrdenYMarcaCambios()
        {
            var vm = await Vm(Constantes.GruposSeguridad.ALMACEN);
            vm.CalleSeleccionada = vm.Calles[1];

            Assert.IsTrue(vm.SubirCalleCommand.CanExecute(null));
            vm.SubirCalleCommand.Execute(null);

            CollectionAssert.AreEqual(new[] { "b", "a", "c" }, Orden(vm));
            Assert.AreEqual("b", vm.CalleSeleccionada.Calle, "Sigue elegida la que se mueve");
            Assert.IsTrue(vm.HayCambios);
            Assert.IsFalse(vm.SubirCalleCommand.CanExecute(null), "Ya es la primera");

            vm.BajarCalleCommand.Execute(null);

            CollectionAssert.AreEqual(new[] { "a", "b", "c" }, Orden(vm));
            Assert.IsFalse(vm.HayCambios, "Vuelve a estar como se leyó");
        }

        [TestMethod]
        public async Task NuevaCalle_VaDetrasDeLaElegidaConElNombreQueDiga()
        {
            A.CallTo(() => _dialogos.GetText(A<string>._, A<string>._, A<string>._)).Returns("x");
            var vm = await Vm(Constantes.GruposSeguridad.ALMACEN);
            vm.CalleSeleccionada = vm.Calles[0];

            vm.NuevaCalleCommand.Execute(null);

            CollectionAssert.AreEqual(new[] { "a", "x", "b", "c" }, Orden(vm));
            Assert.AreEqual("A", vm.Calles[1].Sentido);
            Assert.AreSame(vm.Calles[1], vm.CalleSeleccionada);
            A.CallTo(() => _dialogos.GetText(A<string>._, A<string>._, "d")).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task NuevaCalle_Repetida_LoDiceYNoLaCrea()
        {
            A.CallTo(() => _dialogos.GetText(A<string>._, A<string>._, A<string>._)).Returns("B");
            var vm = await Vm(Constantes.GruposSeguridad.ALMACEN);

            vm.NuevaCalle();

            Assert.AreEqual(3, vm.Calles.Count);
            Assert.AreEqual("Ya hay una calle «B».", vm.Mensaje);
        }

        [TestMethod]
        public async Task RenombrarCalle_MarcaCambiosYSeManda()
        {
            var vm = await Vm(Constantes.GruposSeguridad.ALMACEN);

            vm.Calles[0].Calle = "z";
            vm.Calles[0].Descripcion = "Nueva entrada";

            Assert.IsTrue(vm.HayCambios);
            PeticionRecorridoAlmacen peticion = vm.Peticion(out _)!;
            Assert.AreEqual("z", peticion.Calles[0].Calle);
            Assert.AreEqual("Nueva entrada", peticion.Calles[0].Descripcion);
        }

        [TestMethod]
        public async Task BorrarCalle_SusPasillosPasanASinCalleEnOrden()
        {
            var vm = await Vm(Constantes.GruposSeguridad.ALMACEN);
            vm.CalleSeleccionada = vm.Calles[0];

            vm.BorrarCalleCommand.Execute(null);

            CollectionAssert.AreEqual(new[] { "b", "c" }, Orden(vm));
            CollectionAssert.AreEqual(new[] { "001", "002", "009", "012" }, vm.PasillosSinCalle.Select(p => p.Pasillo).ToArray());
            Assert.AreEqual("b", vm.CalleSeleccionada.Calle);
            A.CallTo(() => _dialogos.ShowConfirmationAnswer("Borrar calle", A<string>._)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task BorrarCalle_SiNoConfirma_NoBorra()
        {
            A.CallTo(() => _dialogos.ShowConfirmationAnswer("Borrar calle", A<string>._)).Returns(false);
            var vm = await Vm(Constantes.GruposSeguridad.ALMACEN);

            vm.BorrarCalle();

            Assert.AreEqual(3, vm.Calles.Count);
            Assert.IsFalse(vm.HayCambios);
        }

        [TestMethod]
        public async Task Serpentina_AlternaEmpezandoPorElSentidoDeLaPrimera()
        {
            var vm = await Vm(Constantes.GruposSeguridad.ALMACEN);

            vm.SerpentinaCommand.Execute(null);
            CollectionAssert.AreEqual(new[] { "A", "D", "A" }, vm.Calles.Select(c => c.Sentido).ToArray());

            vm.Calles[0].Sentido = "D";
            vm.Serpentina();
            CollectionAssert.AreEqual(new[] { "D", "A", "D" }, vm.Calles.Select(c => c.Sentido).ToArray());
            Assert.IsTrue(vm.HayCambios);
        }

        // ---------------------------------------------------------------- pasillos

        [TestMethod]
        public async Task SubirYBajarPasillo_DentroDeSuCalle()
        {
            var vm = await Vm(Constantes.GruposSeguridad.ALMACEN);
            vm.CalleSeleccionada = vm.Calles[0];
            vm.PasilloSeleccionado = vm.Calles[0].Pasillos[1];

            Assert.IsFalse(vm.BajarPasilloCommand.CanExecute(null), "Ya es el último");
            vm.SubirPasilloCommand.Execute(null);

            CollectionAssert.AreEqual(new[] { "002", "001" }, vm.Calles[0].Pasillos.Select(p => p.Pasillo).ToArray());
            Assert.AreEqual("002, 001", vm.Calles[0].ResumenPasillos);
            Assert.IsTrue(vm.HayCambios);
        }

        [TestMethod]
        public async Task MoverPasillo_AOtraCalle_VaAlFinal()
        {
            var vm = await Vm(Constantes.GruposSeguridad.ALMACEN);
            vm.CalleSeleccionada = vm.Calles[0];
            vm.PasilloSeleccionado = vm.Calles[0].Pasillos[0];
            vm.CalleDestino = vm.Calles[1];

            vm.MoverPasilloCommand.Execute(null);

            CollectionAssert.AreEqual(new[] { "002" }, vm.Calles[0].Pasillos.Select(p => p.Pasillo).ToArray());
            CollectionAssert.AreEqual(new[] { "003", "001" }, vm.Calles[1].Pasillos.Select(p => p.Pasillo).ToArray());
            Assert.AreSame(vm.Calles[1], vm.CalleSeleccionada);
            Assert.AreEqual("001", vm.PasilloSeleccionado.Pasillo);
            Assert.AreEqual("Esmaltes", vm.Calles[1].Pasillos[1].Nombre, "Se lleva su nombre");
        }

        [TestMethod]
        public async Task MoverPasillo_ALaMismaCalle_NoSePuede()
        {
            var vm = await Vm(Constantes.GruposSeguridad.ALMACEN);
            vm.CalleSeleccionada = vm.Calles[0];
            vm.PasilloSeleccionado = vm.Calles[0].Pasillos[0];
            vm.CalleDestino = vm.Calles[0];

            Assert.IsFalse(vm.MoverPasilloCommand.CanExecute(null));
        }

        [TestMethod]
        public async Task QuitarPasillo_PasaASinCalleEnSuSitio()
        {
            var vm = await Vm(Constantes.GruposSeguridad.ALMACEN);
            vm.CalleSeleccionada = vm.Calles[1];
            vm.PasilloSeleccionado = vm.Calles[1].Pasillos[0];

            vm.QuitarPasilloCommand.Execute(null);

            Assert.AreEqual(0, vm.Calles[1].Pasillos.Count);
            CollectionAssert.AreEqual(new[] { "003", "009", "012" }, vm.PasillosSinCalle.Select(p => p.Pasillo).ToArray());
            Assert.AreEqual("003", vm.PasilloSinCalleSeleccionado.Pasillo);
        }

        [TestMethod]
        public async Task AnadirPasilloSinCalle_ALaCalleElegida()
        {
            var vm = await Vm(Constantes.GruposSeguridad.ALMACEN);
            vm.CalleSeleccionada = vm.Calles[2];
            vm.PasilloSinCalleSeleccionado = vm.PasillosSinCalle[1];

            vm.AnadirPasilloSinCalleCommand.Execute(null);

            CollectionAssert.AreEqual(new[] { "012" }, vm.Calles[2].Pasillos.Select(p => p.Pasillo).ToArray());
            CollectionAssert.AreEqual(new[] { "009" }, vm.PasillosSinCalle.Select(p => p.Pasillo).ToArray());
            Assert.IsTrue(vm.HayCambios);
        }

        [TestMethod]
        public async Task AnadirPasillo_Tecleado_SeNormalizaYSiEstaSinCalleSeMueve()
        {
            A.CallTo(() => _dialogos.GetText(A<string>._, A<string>._)).Returns("9");
            var vm = await Vm(Constantes.GruposSeguridad.ALMACEN);
            vm.CalleSeleccionada = vm.Calles[2];

            vm.AnadirPasilloCommand.Execute(null);

            CollectionAssert.AreEqual(new[] { "009" }, vm.Calles[2].Pasillos.Select(p => p.Pasillo).ToArray());
            Assert.AreEqual(5, vm.Calles[2].Pasillos[0].Huecos, "Es el mismo pasillo, con sus huecos");
            CollectionAssert.AreEqual(new[] { "012" }, vm.PasillosSinCalle.Select(p => p.Pasillo).ToArray());
        }

        [TestMethod]
        public async Task AnadirPasillo_QueYaEstaEnOtraCalle_LoDiceYNoLoToca()
        {
            A.CallTo(() => _dialogos.GetText(A<string>._, A<string>._)).Returns("3");
            var vm = await Vm(Constantes.GruposSeguridad.ALMACEN);
            vm.CalleSeleccionada = vm.Calles[0];

            vm.AnadirPasillo();

            Assert.AreEqual("El pasillo 003 ya está en la calle «b»: muévalo desde allí.", vm.Mensaje);
            Assert.AreEqual(2, vm.Calles[0].Pasillos.Count);
            Assert.IsFalse(vm.HayCambios);
        }

        [TestMethod]
        public void NormalizarPasillo_ComoLaApi()
        {
            Assert.AreEqual("004", RecorridoAlmacenViewModel.NormalizarPasillo(" 4 "));
            Assert.AreEqual("012", RecorridoAlmacenViewModel.NormalizarPasillo("12"));
            Assert.AreEqual("123", RecorridoAlmacenViewModel.NormalizarPasillo("123"));
            Assert.AreEqual("A1", RecorridoAlmacenViewModel.NormalizarPasillo("a1"));
            Assert.AreEqual(string.Empty, RecorridoAlmacenViewModel.NormalizarPasillo(null!));
        }

        // ---------------------------------------------------------------- vista previa y guardar

        [TestMethod]
        public async Task VistaPrevia_PutConEnsayo_EnsenaLosTramosSinTocarLaPantalla()
        {
            var vm = await Vm(Constantes.GruposSeguridad.ALMACEN);
            vm.CalleSeleccionada = vm.Calles[1];
            vm.SubirCalle();
            vm.FilaAMano = "3";

            await vm.VistaPreviaAsync();

            (PeticionRecorridoAlmacen peticion, bool ensayo) = _puts.Single();
            Assert.IsTrue(ensayo);
            A.CallTo(() => _servicio.Guardar("1", "ALG", A<PeticionRecorridoAlmacen>._, true)).MustHaveHappenedOnceExactly();
            Assert.AreEqual(3, peticion.FilaAMano);
            CollectionAssert.AreEqual(new[] { "b", "a", "c" }, peticion.Calles.Select(c => c.Calle).ToArray());
            CollectionAssert.AreEqual(new[] { "001", "002" }, peticion.Calles[1].Pasillos.Select(p => p.Pasillo).ToArray());
            Assert.AreEqual("Esmaltes", peticion.Calles[1].Pasillos[0].Nombre);
            Assert.AreEqual("D", peticion.Calles[2].Sentido);
            CollectionAssert.AreEqual(new[] { "009", "012" }, peticion.PasillosSinCalle.Select(p => p.Pasillo).ToArray());
            CollectionAssert.AreEqual(new[] { "003", "001", "002" }, vm.Tramos.Select(t => t.Pasillo).ToArray());
            StringAssert.StartsWith(vm.TextoTramos, "Vista previa (sin guardar)");
            Assert.IsTrue(vm.HayCambios, "La vista previa no guarda: los cambios siguen pendientes");
            A.CallTo(() => _dialogos.ShowConfirmationAnswer(A<string>._, A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task VistaPrevia_FilaAManoQueNoEsNumero_NoLlamaALaApi()
        {
            var vm = await Vm(Constantes.GruposSeguridad.ALMACEN);
            vm.FilaAMano = "cuatro";

            await vm.VistaPreviaAsync();

            Assert.AreEqual(RecorridoAlmacenViewModel.MENSAJE_FILA_A_MANO, vm.Mensaje);
            Assert.AreEqual(0, _puts.Count);
        }

        [TestMethod]
        public async Task FilaAManoVacia_VaNull_NoSeToca()
        {
            var vm = await Vm(Constantes.GruposSeguridad.ALMACEN);
            vm.FilaAMano = " ";

            Assert.IsNull(vm.Peticion(out string error)!.FilaAMano);
            Assert.IsNull(error);
        }

        [TestMethod]
        public async Task Guardar_ConConfirmacion_PutDeVerdadYPintaLoGuardado()
        {
            var vm = await Vm(Constantes.GruposSeguridad.DIRECCION);
            vm.Serpentina();
            Assert.IsTrue(vm.GuardarCommand.CanExecute(null));

            await vm.GuardarAsync();

            A.CallTo(() => _dialogos.ShowConfirmationAnswer("Guardar el recorrido", A<string>._)).MustHaveHappenedOnceExactly();
            (PeticionRecorridoAlmacen peticion, bool ensayo) = _puts.Single();
            Assert.IsFalse(ensayo);
            CollectionAssert.AreEqual(new[] { "A", "D", "A" }, peticion.Calles.Select(c => c.Sentido).ToArray());
            Assert.IsFalse(vm.HayCambios, "Lo de la pantalla es lo guardado");
            Assert.AreEqual("Recorrido guardado.", vm.Mensaje);
            StringAssert.StartsWith(vm.TextoTramos, "Recorrido guardado");
            CollectionAssert.AreEqual(new[] { "A", "D", "A" }, vm.Calles.Select(c => c.Sentido).ToArray());
            Assert.IsFalse(vm.GuardarCommand.CanExecute(null));
        }

        [TestMethod]
        public async Task Guardar_SinCambios_NoSePuede()
        {
            var vm = await Vm(Constantes.GruposSeguridad.ALMACEN);

            Assert.IsFalse(vm.GuardarCommand.CanExecute(null));
        }

        [TestMethod]
        public async Task Guardar_SiNoConfirma_NoLlamaALaApi()
        {
            A.CallTo(() => _dialogos.ShowConfirmationAnswer("Guardar el recorrido", A<string>._)).Returns(false);
            var vm = await Vm(Constantes.GruposSeguridad.ALMACEN);
            vm.Serpentina();

            await vm.GuardarAsync();

            Assert.AreEqual(0, _puts.Count);
            Assert.IsTrue(vm.HayCambios);
        }

        [TestMethod]
        public async Task Guardar_400_EnsenaElMensajeTalCualYSigueEditable()
        {
            A.CallTo(() => _servicio.Guardar(A<string>._, A<string>._, A<PeticionRecorridoAlmacen>._, A<bool>._))
                .ThrowsAsync(new RecorridoAlmacenException("La calle «abcdefghijk» tiene más de 10 caracteres.", 400));
            var vm = await Vm(Constantes.GruposSeguridad.ALMACEN);
            vm.Calles[0].Calle = "abcdefghijk";

            await vm.GuardarAsync();

            Assert.AreEqual("La calle «abcdefghijk» tiene más de 10 caracteres.", vm.Mensaje);
            A.CallTo(() => _dialogos.ShowError("La calle «abcdefghijk» tiene más de 10 caracteres.")).MustHaveHappenedOnceExactly();
            Assert.IsTrue(vm.Editable);
            Assert.IsTrue(vm.HayCambios);
            Assert.IsFalse(vm.EstaOcupado);
        }

        [TestMethod]
        public async Task Guardar_403_EnsenaElMensajeYPasaASoloLectura()
        {
            A.CallTo(() => _servicio.Guardar(A<string>._, A<string>._, A<PeticionRecorridoAlmacen>._, A<bool>._))
                .ThrowsAsync(new RecorridoAlmacenException("Solo Almacén y Dirección pueden escribir en el almacén.", 403));
            var vm = await Vm(Constantes.GruposSeguridad.ALMACEN);
            vm.Serpentina();

            await vm.GuardarAsync();

            Assert.AreEqual("Solo Almacén y Dirección pueden escribir en el almacén.", vm.Mensaje);
            Assert.IsTrue(vm.SoloLectura);
            Assert.IsFalse(vm.Editable);
            Assert.AreEqual("Solo Almacén y Dirección pueden escribir en el almacén.", vm.Aviso);
        }

        [TestMethod]
        public async Task VistaPrevia_503_NadaEditable()
        {
            A.CallTo(() => _servicio.Guardar(A<string>._, A<string>._, A<PeticionRecorridoAlmacen>._, A<bool>._))
                .ThrowsAsync(new RecorridoAlmacenException("Faltan las tablas del recorrido (Scripts/Issue641_RecorridoCalles.sql).", 503));
            var vm = await Vm(Constantes.GruposSeguridad.ALMACEN);

            await vm.VistaPreviaAsync();

            Assert.IsFalse(vm.Disponible);
            Assert.IsFalse(vm.Editable);
            StringAssert.StartsWith(vm.Aviso, "Faltan las tablas del recorrido");
        }

        // ---------------------------------------------------------------- cerrar y recargar

        [TestMethod]
        public async Task Cerrar_SinCambios_NoPregunta()
        {
            var vm = await Vm(Constantes.GruposSeguridad.ALMACEN);

            Assert.IsTrue(vm.ConfirmTabClose());
            A.CallTo(() => _dialogos.ShowConfirmationAnswer(A<string>._, A<string>._, A<bool>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task Cerrar_ConCambios_PreguntaConNoPorDefecto()
        {
            A.CallTo(() => _dialogos.ShowConfirmationAnswer("Cambios sin guardar", A<string>._, true)).Returns(false);
            var vm = await Vm(Constantes.GruposSeguridad.ALMACEN);
            vm.PasillosSinCalle[0].Nombre = "Palés";

            Assert.IsTrue(vm.HayCambios, "Cambiar el nombre de un pasillo sin calle también es un cambio");
            Assert.IsFalse(vm.ConfirmTabClose());
        }

        [TestMethod]
        public async Task Recargar_ConCambios_PreguntaAntesDePerderlos()
        {
            A.CallTo(() => _dialogos.ShowConfirmationAnswer("Cambios sin guardar", A<string>._, true)).Returns(false);
            var vm = await Vm(Constantes.GruposSeguridad.ALMACEN);
            vm.Serpentina();

            await vm.RecargarAsync();

            A.CallTo(() => _servicio.Leer(A<string>._, A<string>._)).MustHaveHappenedOnceExactly();
            Assert.IsTrue(vm.HayCambios);
        }

        // ---------------------------------------------------------------- el botón de la cinta

        [TestMethod]
        public void MenuBar_CualquieraAbreLaVentana()
        {
            var navegacion = A.Fake<IServicioNavegacion>();
            var menu = new ProductoMenuBarViewModel(navegacion, _configuracion, A.Fake<IServicioDiferenciasReposicion>());

            Assert.IsTrue(menu.AbrirRecorridoAlmacenCommand.CanExecute(null));
            menu.AbrirRecorridoAlmacenCommand.Execute(null);

            A.CallTo(() => navegacion.RequestNavigate("MainRegion", ProductoMenuBarViewModel.VISTA_RECORRIDO_ALMACEN)).MustHaveHappenedOnceExactly();
        }

        // ---------------------------------------------------------------- el servicio

        private sealed class HandlerFalso : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
            public List<(string Metodo, string Url, string? Cuerpo)> Peticiones { get; } = new();

            public HandlerFalso(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                string? cuerpo = request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
                Peticiones.Add((request.Method.Method, request.RequestUri!.PathAndQuery, cuerpo));
                return _responder(request);
            }
        }

        private static ServicioRecorridoAlmacen Servicio(HandlerFalso handler)
        {
            var factoria = A.Fake<IClienteApiFactory>();
            A.CallTo(() => factoria.Crear()).ReturnsLazily(() =>
                new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("http://api.local/api/") });
            return new ServicioRecorridoAlmacen(factoria);
        }

        private static HttpResponseMessage Json(HttpStatusCode codigo, string json)
            => new HttpResponseMessage(codigo) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

        [TestMethod]
        public async Task Servicio_Leer_GetConAlmacenYEmpresa()
        {
            var handler = new HandlerFalso(_ => Json(HttpStatusCode.OK,
                "{\"Empresa\":\"1\",\"Almacen\":\"ALG\",\"TablasCreadas\":true,\"FilaAMano\":4,\"Calles\":[{\"Id\":7,\"Calle\":\"a\",\"Orden\":10,\"Sentido\":\"D\",\"Pasillos\":[{\"Pasillo\":\"001\",\"Posicion\":1,\"Huecos\":3}]}],\"PasillosSinCalle\":[],\"Tramos\":[{\"Orden\":1,\"Calle\":\"a\",\"Pasillo\":\"001\",\"Columna\":\"002\",\"Nivel\":\"Escalera\",\"Filas\":[\"001\"]}]}"));

            RecorridoAlmacen recorrido = await Servicio(handler).Leer("1", "ALG");

            Assert.AreEqual(("GET", "/api/Almacen/Recorrido?almacen=ALG&empresa=1"), (handler.Peticiones.Single().Metodo, handler.Peticiones.Single().Url));
            Assert.IsTrue(recorrido.TablasCreadas);
            Assert.AreEqual("D", recorrido.Calles[0].Sentido);
            Assert.AreEqual(3, recorrido.Calles[0].Pasillos[0].Huecos);
            Assert.AreEqual("Escalera", recorrido.Tramos[0].Nivel);
        }

        [TestMethod]
        public async Task Servicio_Guardar_PutConEnsayoYElCuerpoDelContrato()
        {
            var handler = new HandlerFalso(_ => Json(HttpStatusCode.OK, "{\"TablasCreadas\":true,\"Ensayo\":true}"));

            RecorridoAlmacen recorrido = await Servicio(handler).Guardar("1", "ALG", new PeticionRecorridoAlmacen
            {
                FilaAMano = 4,
                Calles = new List<PeticionCalleRecorrido>
                {
                    new PeticionCalleRecorrido { Calle = "a", Sentido = "A", Descripcion = "Entrada", Pasillos = new List<PeticionPasilloRecorrido> { new PeticionPasilloRecorrido { Pasillo = "001", Nombre = "Esmaltes" } } }
                },
                PasillosSinCalle = new List<PeticionPasilloRecorrido> { new PeticionPasilloRecorrido { Pasillo = "009" } }
            }, ensayo: true);

            var peticion = handler.Peticiones.Single();
            Assert.AreEqual("PUT", peticion.Metodo);
            Assert.AreEqual("/api/Almacen/Recorrido?almacen=ALG&empresa=1&ensayo=true", peticion.Url);
            JObject cuerpo = JObject.Parse(peticion.Cuerpo!);
            Assert.AreEqual(4, (int)cuerpo["FilaAMano"]!);
            Assert.AreEqual("a", (string?)cuerpo["Calles"]![0]!["Calle"]);
            Assert.AreEqual("A", (string?)cuerpo["Calles"]![0]!["Sentido"]);
            Assert.AreEqual("Esmaltes", (string?)cuerpo["Calles"]![0]!["Pasillos"]![0]!["Nombre"]);
            Assert.AreEqual("009", (string?)cuerpo["PasillosSinCalle"]![0]!["Pasillo"]);
            Assert.IsTrue(recorrido.Ensayo);
        }

        [TestMethod]
        public async Task Servicio_Guardar400_LanzaConElMensajeDeLaApi()
        {
            var handler = new HandlerFalso(_ => Json(HttpStatusCode.BadRequest, "{\"Message\":\"El pasillo 004 está en dos sitios.\"}"));

            RecorridoAlmacenException ex = await Assert.ThrowsExceptionAsync<RecorridoAlmacenException>(
                () => Servicio(handler).Guardar("1", "ALG", new PeticionRecorridoAlmacen(), false));

            Assert.AreEqual(400, ex.Codigo);
            Assert.AreEqual("El pasillo 004 está en dos sitios.", ex.Message);
        }

        [TestMethod]
        public async Task Servicio_503EnCadena_LanzaConElMotivo()
        {
            var handler = new HandlerFalso(_ => Json(HttpStatusCode.ServiceUnavailable, "\"El mantenimiento del recorrido no está configurado en el servidor.\""));

            RecorridoAlmacenException ex = await Assert.ThrowsExceptionAsync<RecorridoAlmacenException>(() => Servicio(handler).Leer("1", "ALG"));

            Assert.AreEqual(503, ex.Codigo);
            Assert.AreEqual("El mantenimiento del recorrido no está configurado en el servidor.", ex.Message);
        }

        [TestMethod]
        public async Task Servicio_403SinCuerpo_MensajePropio()
        {
            var handler = new HandlerFalso(_ => new HttpResponseMessage(HttpStatusCode.Forbidden) { Content = new StringContent(string.Empty) });

            RecorridoAlmacenException ex = await Assert.ThrowsExceptionAsync<RecorridoAlmacenException>(
                () => Servicio(handler).Guardar("1", "ALG", new PeticionRecorridoAlmacen(), false));

            Assert.AreEqual(403, ex.Codigo);
            Assert.AreEqual(ServicioRecorridoAlmacen.MENSAJE_SIN_PERMISO, ex.Message);
        }
    }
}
