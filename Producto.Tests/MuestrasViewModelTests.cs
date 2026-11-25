using ControlesUsuario.Services;
using FakeItEasy;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Modules.Producto;
using Nesto.Modules.Producto.ViewModels;

namespace Producto.Tests
{
    /// <summary>
    /// Nesto#650 (NestoAPI#884): las muestras sin código llevan UNA etiqueta en el cajón o la gaveta con el interno del 29. Todo pasa
    /// antes por el ensayo de la API y se confirma con sus cifras («No» por defecto); se puede imprimir solo lo marcado; con la API de
    /// antes (sin la lista de impresoras) no se ofrece elegir impresora.
    /// </summary>
    [TestClass]
    public class MuestrasViewModelTests
    {
        private IServicioMuestras _servicio = null!;
        private IConfiguracion _configuracion = null!;
        private IServicioDialogos _dialogos = null!;
        private IGuardadorFicherosDescarga _guardador = null!;
        private readonly List<string> _abiertos = new();
        private readonly List<(PeticionEtiquetasMuestras Peticion, bool Ensayo)> _llamadas = new();

        private static EtiquetaMuestra Etiqueta(int orden, string producto, string? codigo = null) => new()
        {
            Orden = orden,
            Producto = producto,
            Nombre = "MUESTRA " + producto,
            Familia = "Lisap",
            Stock = 2,
            CodigoBarras = codigo
        };

        private static ResultadoEtiquetasMuestras Resultado(bool ensayo, params EtiquetaMuestra[] etiquetas) => new()
        {
            Almacen = "ALC",
            Ensayo = ensayo,
            Total = etiquetas.Length,
            Impresas = ensayo ? 0 : etiquetas.Length,
            Impresora = @"\\RDS2016\etiquetasalcobendas",
            SinCodigo = etiquetas.Count(e => e.CodigoBarras == null),
            CodigosAsignados = ensayo ? 0 : etiquetas.Count(e => e.CodigoBarras == null),
            Mensaje = ensayo ? $"Saldrían {etiquetas.Length}" : $"Impresas {etiquetas.Length}",
            Etiquetas = etiquetas.ToList()
        };

        private MuestrasViewModel Crear(bool almacen = true)
        {
            A.CallTo(() => _configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ALMACEN)).Returns(almacen);
            return new MuestrasViewModel(_servicio, _configuracion, _dialogos, _guardador, ruta => _abiertos.Add(ruta));
        }

        private void ConEtiquetas(params EtiquetaMuestra[] etiquetas)
        {
            A.CallTo(() => _servicio.Etiquetas(A<string>._, A<PeticionEtiquetasMuestras>._, A<bool>._))
                .ReturnsLazily((string _, PeticionEtiquetasMuestras p, bool ensayo) =>
                {
                    _llamadas.Add((p, ensayo));
                    EtiquetaMuestra[] salen = p.Productos == null
                        ? etiquetas
                        : etiquetas.Where(e => p.Productos.Contains(e.Producto)).ToArray();
                    return Task.FromResult(Resultado(ensayo, salen));
                });
        }

        private void Responder(bool si)
            => A.CallTo(() => _dialogos.ShowConfirmationAnswer(A<string>._, A<string>._, true)).Returns(si);

        [TestInitialize]
        public void Inicializar()
        {
            _servicio = A.Fake<IServicioMuestras>();
            _configuracion = A.Fake<IConfiguracion>();
            _dialogos = A.Fake<IServicioDialogos>();
            _guardador = A.Fake<IGuardadorFicherosDescarga>();
            ConEtiquetas(Etiqueta(1, "17404"), Etiqueta(2, "17405", "8400000000001"), Etiqueta(3, "17406"));
        }

        [TestMethod]
        public async Task Ver_PideElEnsayoConLosFiltrosYPintaLaListaEnOrden()
        {
            var vm = Crear();
            vm.Almacen = vm.Almacenes.Single(a => a.Almacen == "ALC");
            vm.Familia = "  Lisap ";

            await vm.VerCommand.ExecuteAsync(null);

            (PeticionEtiquetasMuestras peticion, bool ensayo) = _llamadas.Single();
            Assert.IsTrue(ensayo);
            Assert.AreEqual("ALC", peticion.Almacen);
            Assert.AreEqual("Lisap", peticion.Familia);
            Assert.IsTrue(peticion.SoloConStock, "Por defecto, solo las que tienen stock");
            Assert.IsNull(peticion.Productos);
            Assert.IsNull(peticion.Impresora, "Sin elegir, la del almacén");
            Assert.AreEqual(3, vm.Etiquetas.Count);
            Assert.AreEqual("17404", vm.Etiquetas[0].Producto);
            Assert.IsTrue(vm.Etiquetas[0].SinCodigo);
            Assert.AreEqual(FilaEtiquetaMuestra.TEXTO_SIN_CODIGO, vm.Etiquetas[0].TextoCodigo);
            Assert.IsFalse(vm.Etiquetas[1].SinCodigo);
            Assert.IsTrue(vm.Etiquetas.All(f => f.Marcada), "Al ver, todas marcadas");
            Assert.AreEqual("Saldrían 3", vm.Mensaje);
        }

        [TestMethod]
        public async Task Imprimir_HaceElEnsayoYPreguntaConLasCifrasAntesDeImprimir()
        {
            var vm = Crear();
            Responder(true);
            string? pregunta = null;
            A.CallTo(() => _dialogos.ShowConfirmationAnswer(A<string>._, A<string>._, true))
                .ReturnsLazily((string _, string texto, bool _) => { pregunta = texto; return true; });

            await vm.ImprimirCommand.ExecuteAsync(null);

            Assert.AreEqual(2, _llamadas.Count);
            Assert.IsTrue(_llamadas[0].Ensayo, "Primero el ensayo");
            Assert.IsFalse(_llamadas[1].Ensayo, "Después, de verdad");
            Assert.AreEqual(@"Van a salir 3 etiquetas en \\RDS2016\etiquetasalcobendas; a 2 sin código se les dará el interno. ¿Imprimir?", pregunta);
            StringAssert.Contains(vm.Mensaje, @"Impresas 3 etiquetas en \\RDS2016\etiquetasalcobendas.");
            StringAssert.Contains(vm.Mensaje, "Se ha dado el código interno a 2 muestras.");
        }

        [TestMethod]
        public async Task Imprimir_SiDiceQueNo_NoImprime()
        {
            var vm = Crear();
            Responder(false);

            await vm.ImprimirCommand.ExecuteAsync(null);

            Assert.IsTrue(_llamadas.Single().Ensayo, "Solo el ensayo");
            Assert.AreEqual("No se ha impreso nada.", vm.Mensaje);
        }

        [TestMethod]
        public async Task Imprimir_SinEtiquetas_NoPregunta()
        {
            ConEtiquetas();
            var vm = Crear();

            await vm.ImprimirCommand.ExecuteAsync(null);

            A.CallTo(() => _dialogos.ShowConfirmationAnswer(A<string>._, A<string>._, A<bool>._)).MustNotHaveHappened();
            Assert.IsTrue(_llamadas.Single().Ensayo);
            Assert.AreEqual("Saldrían 0", vm.Mensaje);
        }

        [TestMethod]
        public async Task Imprimir_SoloLasMarcadas_MandaSusReferencias()
        {
            var vm = Crear();
            Responder(true);
            await vm.VerCommand.ExecuteAsync(null);
            vm.Etiquetas[1].Marcada = false;
            _llamadas.Clear();

            await vm.ImprimirCommand.ExecuteAsync(null);

            CollectionAssert.AreEqual(new[] { "17404", "17406" }, _llamadas[0].Peticion.Productos);
            CollectionAssert.AreEqual(new[] { "17404", "17406" }, _llamadas[1].Peticion.Productos);
            Assert.IsFalse(_llamadas[1].Ensayo);
        }

        [TestMethod]
        public async Task Imprimir_TodasMarcadas_NoMandaReferencias()
        {
            var vm = Crear();
            Responder(true);
            await vm.VerCommand.ExecuteAsync(null);
            _llamadas.Clear();

            await vm.ImprimirCommand.ExecuteAsync(null);

            Assert.IsNull(_llamadas[1].Peticion.Productos, "Todas: salen las de los filtros, en su orden");
        }

        [TestMethod]
        public async Task Imprimir_NingunaMarcada_NoLlamaALaApi()
        {
            var vm = Crear();
            await vm.VerCommand.ExecuteAsync(null);
            vm.DesmarcarTodasCommand.Execute(null);
            _llamadas.Clear();

            await vm.ImprimirCommand.ExecuteAsync(null);

            Assert.AreEqual(0, _llamadas.Count);
            StringAssert.Contains(vm.Mensaje, "ninguna etiqueta marcada");
        }

        [TestMethod]
        public async Task Imprimir_ActualizaElCodigoQueDaLaApi()
        {
            var vm = Crear();
            Responder(true);
            await vm.VerCommand.ExecuteAsync(null);
            A.CallTo(() => _servicio.Etiquetas(A<string>._, A<PeticionEtiquetasMuestras>._, false))
                .Returns(new ResultadoEtiquetasMuestras
                {
                    Impresas = 3,
                    Etiquetas = new List<EtiquetaMuestra> { new() { Producto = "17404", CodigoBarras = "2900000174047", CodigoInterno = true, CodigoAsignado = true } }
                });

            await vm.ImprimirCommand.ExecuteAsync(null);

            Assert.IsFalse(vm.Etiquetas[0].SinCodigo);
            Assert.AreEqual("2900000174047 (dado ahora)", vm.Etiquetas[0].TextoCodigo);
        }

        [TestMethod]
        public async Task Imprimir_ErrorDeLaApi_EnsenaSuMensaje()
        {
            var vm = Crear();
            Responder(true);
            A.CallTo(() => _servicio.Etiquetas(A<string>._, A<PeticionEtiquetasMuestras>._, false))
                .ThrowsAsync(new MuestrasException("Windows no deja imprimir en la impresora.", 502));

            await vm.ImprimirCommand.ExecuteAsync(null);

            Assert.AreEqual("Windows no deja imprimir en la impresora.", vm.Mensaje);
            A.CallTo(() => _dialogos.ShowError("Windows no deja imprimir en la impresora.")).MustHaveHappenedOnceExactly();
            Assert.IsFalse(vm.EstaOcupado);
        }

        [TestMethod]
        public async Task Impresoras_ConLaLista_SeOfreceElegirYSeMandaLaElegida()
        {
            A.CallTo(() => _servicio.LeerImpresoras()).Returns(new List<ImpresoraEtiquetasMuestras>
            {
                new() { Almacen = "ALG", Nombre = "Algete", Impresora = @"\\RDS2016\etiquetas2" },
                new() { Almacen = "ALC", Nombre = "Alcobendas", Impresora = @"\\RDS2016\etiquetasalcobendas", EsLaDelUsuario = true }
            });
            var vm = Crear();

            await vm.PrepararAsync();

            Assert.IsTrue(vm.PuedeElegirImpresora);
            Assert.AreEqual(3, vm.Impresoras.Count);
            Assert.IsNull(vm.Impresora.Impresora, "Por defecto, la del almacén");
            Assert.AreEqual(MuestrasViewModel.OPCION_IMPRESORA_DEL_ALMACEN, vm.Impresora.Texto);

            vm.Impresora = vm.Impresoras[1];
            await vm.VerCommand.ExecuteAsync(null);
            Assert.AreEqual(@"\\RDS2016\etiquetas2", _llamadas.Single().Peticion.Impresora);
        }

        [TestMethod]
        public async Task Impresoras_ApiAntigua_NoSeOfreceElegir()
        {
            A.CallTo(() => _servicio.LeerImpresoras()).Returns(Task.FromResult<List<ImpresoraEtiquetasMuestras>>(null!));
            var vm = Crear();

            await vm.PrepararAsync();

            Assert.IsFalse(vm.PuedeElegirImpresora);
            Assert.AreEqual(1, vm.Impresoras.Count);
            await vm.VerCommand.ExecuteAsync(null);
            Assert.IsNull(_llamadas.Single().Peticion.Impresora);
        }

        [TestMethod]
        public async Task Preparar_ProponeElAlmacenDelUsuario()
        {
            A.CallTo(() => _configuracion.leerParametro(A<string>._, Parametros.Claves.AlmacenPedidoVta)).Returns("REI ");
            var vm = Crear();

            await vm.PrepararAsync();

            Assert.AreEqual("REI", vm.Almacen.Almacen);
        }

        [TestMethod]
        public async Task DarCodigo_EnsenaCualesYSoloConElSiLosDa()
        {
            var vm = Crear();
            A.CallTo(() => _servicio.DarCodigoInterno(A<string>._, true)).Returns(new ResultadoCodigoInternoMuestras
            {
                Ensayo = true,
                Total = 2,
                Mensaje = "Hay 2 muestras sin código.",
                Muestras = new List<MuestraCodigoInterno>
                {
                    new() { Producto = "17404", Nombre = "CREMA", Familia = "Lisap", Codigo = "2900000174047" },
                    new() { Producto = "AB12", Nombre = "CHAMPU", Familia = "Lisap" }
                }
            });
            A.CallTo(() => _servicio.DarCodigoInterno(A<string>._, false)).Returns(new ResultadoCodigoInternoMuestras
            {
                Total = 2,
                Asignados = 1,
                Errores = 1,
                Mensaje = "Se ha dado el código a 1 de 2.",
                Muestras = new List<MuestraCodigoInterno>
                {
                    new() { Producto = "17404", Nombre = "CREMA", Asignado = true, Codigo = "2900000174047" },
                    new() { Producto = "AB12", Nombre = "CHAMPU", Error = "Otro producto ya tiene ese código" }
                }
            });
            string? pregunta = null;
            A.CallTo(() => _dialogos.ShowConfirmationAnswer(A<string>._, A<string>._, true))
                .ReturnsLazily((string _, string texto, bool _) => { pregunta = texto; return true; });

            await vm.DarCodigoCommand.ExecuteAsync(null);

            StringAssert.Contains(pregunta, "17404 CREMA (Lisap) → 2900000174047");
            StringAssert.Contains(pregunta, "AB12 CHAMPU (Lisap)");
            StringAssert.Contains(pregunta, "¿Les doy el código?");
            A.CallTo(() => _servicio.DarCodigoInterno(A<string>._, false)).MustHaveHappenedOnceExactly();
            Assert.AreEqual("Se ha dado el código a 1 de 2.", vm.Mensaje);
            Assert.AreEqual("AB12 CHAMPU: Otro producto ya tiene ese código", vm.Errores.Single());
            A.CallTo(() => _dialogos.ShowError(A<string>.That.Contains("AB12 CHAMPU: Otro producto ya tiene ese código"))).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task DarCodigo_SiDiceQueNo_NoLosDa()
        {
            var vm = Crear();
            A.CallTo(() => _servicio.DarCodigoInterno(A<string>._, true)).Returns(new ResultadoCodigoInternoMuestras
            {
                Total = 1,
                Muestras = new List<MuestraCodigoInterno> { new() { Producto = "17404" } }
            });
            Responder(false);

            await vm.DarCodigoCommand.ExecuteAsync(null);

            A.CallTo(() => _servicio.DarCodigoInterno(A<string>._, false)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task DarCodigo_NingunaSinCodigo_SoloElMensaje()
        {
            var vm = Crear();
            A.CallTo(() => _servicio.DarCodigoInterno(A<string>._, true))
                .Returns(new ResultadoCodigoInternoMuestras { Total = 0, Mensaje = "Todas las muestras tienen código." });

            await vm.DarCodigoCommand.ExecuteAsync(null);

            A.CallTo(() => _dialogos.ShowConfirmationAnswer(A<string>._, A<string>._, A<bool>._)).MustNotHaveHappened();
            Assert.AreEqual("Todas las muestras tienen código.", vm.Mensaje);
        }

        [TestMethod]
        public void DarCodigo_SoloAlmacenComprasDireccionEInformatica()
        {
            Assert.IsTrue(Crear(almacen: true).PuedeDarCodigo);
            var tienda = Crear(almacen: false);
            A.CallTo(() => _configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.TIENDAS)).Returns(true);
            Assert.IsFalse(tienda.PuedeDarCodigo);
            Assert.IsFalse(tienda.DarCodigoCommand.CanExecute(null));
            Assert.IsTrue(MuestrasViewModel.PuedeUsarMuestras(_configuracion), "Las tiendas sí abren la ventana");
        }

        [TestMethod]
        public async Task Hoja_GuardaEnDescargasConElNombreDeLaApiYLaAbre()
        {
            var vm = Crear();
            vm.Almacen = vm.Almacenes.Single(a => a.Almacen == "REI");
            vm.Familia = "Lisap";
            vm.SoloConStock = false;
            byte[] pdf = { 1, 2, 3 };
            A.CallTo(() => _servicio.Hoja(A<string>._, "REI", "Lisap", false, true))
                .Returns(new FicheroMuestras { Nombre = "Muestras Lisap REI.pdf", Contenido = pdf });
            A.CallTo(() => _guardador.ElegirDondeGuardar(A<string>._, "Muestras Lisap REI.pdf", A<string>.That.EndsWith("Downloads"), A<string>._, ".pdf"))
                .Returns(@"C:\Users\x\Downloads\Muestras Lisap REI.pdf");

            await vm.HojaCommand.ExecuteAsync(null);

            A.CallTo(() => _guardador.Guardar(@"C:\Users\x\Downloads\Muestras Lisap REI.pdf", pdf)).MustHaveHappenedOnceExactly();
            Assert.AreEqual(@"C:\Users\x\Downloads\Muestras Lisap REI.pdf", _abiertos.Single());
        }

        [TestMethod]
        public async Task Hoja_SiCancelaNoGuardaNiAbre()
        {
            var vm = Crear();
            A.CallTo(() => _servicio.Hoja(A<string>._, A<string>._, A<string>._, A<bool>._, A<bool>._))
                .Returns(new FicheroMuestras { Contenido = new byte[] { 1 } });
            A.CallTo(() => _guardador.ElegirDondeGuardar(A<string>._, A<string>._, A<string>._, A<string>._, A<string>._)).Returns(null!);

            await vm.HojaCommand.ExecuteAsync(null);

            A.CallTo(() => _guardador.Guardar(A<string>._, A<byte[]>._)).MustNotHaveHappened();
            Assert.AreEqual(0, _abiertos.Count);
        }

        [TestMethod]
        public async Task Hoja_SinMuestras_EnsenaElMensajeDeLaApi()
        {
            var vm = Crear();
            A.CallTo(() => _servicio.Hoja(A<string>._, A<string>._, A<string>._, A<bool>._, A<bool>._))
                .ThrowsAsync(new MuestrasException("No hay ninguna muestra activa con esos filtros.", 404));

            await vm.HojaCommand.ExecuteAsync(null);

            Assert.AreEqual("No hay ninguna muestra activa con esos filtros.", vm.Mensaje);
            A.CallTo(() => _guardador.ElegirDondeGuardar(A<string>._, A<string>._, A<string>._, A<string>._, A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public void NombreHoja_SinElDeLaApi_LoCompone()
        {
            Assert.AreEqual("Muestras Lisap ALC.pdf", MuestrasViewModel.NombreHoja(null, "Lisap", "ALC"));
            Assert.AreEqual("Muestras ALG.pdf", MuestrasViewModel.NombreHoja("", null, "ALG"));
        }

        [TestMethod]
        public void Confirmacion_UnaSolaYSinImpresoraConocida()
        {
            string texto = MuestrasViewModel.TextoConfirmarImpresion(new ResultadoEtiquetasMuestras { Total = 1, SinCodigo = 1 }, null);
            Assert.AreEqual("Va a salir 1 etiqueta en la impresora del almacén; a 1 sin código se le dará el interno. ¿Imprimir?", texto);
        }
    }
}
