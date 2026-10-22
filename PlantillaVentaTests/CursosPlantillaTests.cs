using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using ControlesUsuario.Dialogs;
using ControlesUsuario.Models;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Models;
using Nesto.Modulos.PedidoVenta;
using Nesto.Modulos.PlantillaVenta;

namespace PlantillaVentaTests
{
    /// <summary>
    /// Nesto#543 / NestoAPI#691: vender cursos y eventos desde la plantilla. Al meter un curso se elige evento (si hay
    /// varios) y modalidad; la línea queda con el importe de la modalidad y bloqueada; el pedido va con prepago; los
    /// cursos no suman para nada del pedido de productos; y si la API separa los cursos en otro pedido, se dice.
    /// </summary>
    [TestClass]
    public class CursosPlantillaTests
    {
        private IPlantillaVentaService _servicio;
        private IServicioDialogos _dialogos;
        private ParametrosDialogo _parametrosDialogo;
        private int _dialogosAbiertos;
        private readonly List<string> _errores = new List<string>();
        private readonly List<string> _avisos = new List<string>();

        [TestInitialize]
        public void Setup()
        {
            _servicio = A.Fake<IPlantillaVentaService>();
            _dialogos = A.Fake<IServicioDialogos>();
            A.CallTo(() => _dialogos.ShowError(A<string>.Ignored)).Invokes((string m) => _errores.Add(m));
            A.CallTo(() => _dialogos.ShowNotification(A<string>.Ignored, A<string>.Ignored))
                .Invokes((string t, string m) => _avisos.Add(m));
        }

        private PlantillaVentaViewModel CrearViewModel()
        {
            IConfiguracion configuracion = A.Fake<IConfiguracion>();
            A.CallTo(() => configuracion.LeerParametroSync(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.AlmacenRuta)).Returns("ALG");
            var vm = new PlantillaVentaViewModel(A.Fake<IAbridorPedidos>(), A.Fake<IServicioNavegacion>(), configuracion,
                _servicio, new WeakReferenceMessenger(), _dialogos, A.Fake<IPedidoVentaService>(),
                A.Fake<IBorradorPlantillaVentaService>(), A.Fake<IServicioAutenticacion>());
            vm.ListaFiltrableProductos.ListaOriginal = new ObservableCollection<IFiltrableItem>();
            vm._clienteSeleccionado = new ClienteJson { empresa = "1", cliente = "15191", contacto = "0", iva = "G21", cifNif = "12345678A" };
            return vm;
        }

        private static void PonerPlazo(PlantillaVentaViewModel vm, string plazo)
        {
            // Sin vista no hay selector de plazos: se pone el plazo elegido directamente
            typeof(PlantillaVentaViewModel).GetField("_plazoPagoSeleccionado", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(vm, new PlazosPago { plazoPago = plazo });
        }

        private static LineaPlantillaVenta Curso(int cantidad = 1) => new LineaPlantillaVenta
        {
            producto = "90077",
            texto = "CURSO DE MICROPIGMENTACIÓN",
            grupo = "CUR",
            EsCurso = true,
            cantidad = cantidad,
            precio = 100M,
            aplicarDescuento = true
        };

        private static EventoCursoDTO Evento(int id, string almacen = "REI", string formato = "Presencial", int plazas = 8) => new EventoCursoDTO
        {
            Id = id,
            Titulo = "Micropigmentación nivel 1",
            Fecha = new DateTime(2026, 11, 10),
            HoraInicio = "10:00",
            Almacen = almacen,
            Formato = formato,
            PlazasLibres = plazas,
            Modalidades = new List<ModalidadEventoDTO>
            {
                new ModalidadEventoDTO { Modalidad = "Reembolsable", Nombre = "Con cupón de productos", Importe = 100M },
                new ModalidadEventoDTO { Modalidad = "NoReembolsable", Nombre = "Sin cupón", Importe = 60M }
            }
        };

        private void EventosDelCurso(params EventoCursoDTO[] eventos)
            => A.CallTo(() => _servicio.CargarEventosProducto("1", "90077")).Returns(Task.FromResult(eventos.ToList()));

        /// <summary>El diálogo contesta con el evento elegido (o el único) y la modalidad indicada (null = sin elegir).</summary>
        private void DialogoElige(int? indiceEvento, int? indiceModalidad, ResultadoBoton boton = ResultadoBoton.OK)
        {
            A.CallTo(() => _dialogos.ShowDialogAsync(ElegirEventoCursoDialogViewModel.NOMBRE, A<ParametrosDialogo>.Ignored))
                .ReturnsLazily((string nombre, ParametrosDialogo parametros) =>
                {
                    _dialogosAbiertos++;
                    _parametrosDialogo = parametros;
                    var eventos = parametros.GetValue<List<EventoCursoDTO>>(ElegirEventoCursoDialogViewModel.PARAMETRO_EVENTOS);
                    var dialogo = new ElegirEventoCursoDialogViewModel();
                    dialogo.OnDialogOpened(parametros);
                    if (indiceEvento.HasValue)
                    {
                        dialogo.EventoSeleccionado = eventos[indiceEvento.Value];
                    }
                    if (indiceModalidad.HasValue)
                    {
                        dialogo.ModalidadSeleccionada = dialogo.EventoSeleccionado.Modalidades[indiceModalidad.Value];
                    }
                    var salida = new ParametrosDialogo();
                    if (boton == ResultadoBoton.OK && dialogo.AceptarCommand.CanExecute(null))
                    {
                        salida.Add(ElegirEventoCursoDialogViewModel.PARAMETRO_EVENTO, dialogo.EventoSeleccionado);
                        salida.Add(ElegirEventoCursoDialogViewModel.PARAMETRO_MODALIDAD, dialogo.ModalidadSeleccionada);
                        return Task.FromResult(new ResultadoDialogo(ResultadoBoton.OK, salida));
                    }
                    return Task.FromResult(new ResultadoDialogo(ResultadoBoton.Cancel));
                });
        }

        #region Diálogo de evento y modalidad

        [TestMethod]
        public void Dialogo_ConUnSoloEvento_SaleElegidoYLaModalidadNo()
        {
            var dialogo = new ElegirEventoCursoDialogViewModel();
            dialogo.OnDialogOpened(new ParametrosDialogo { { ElegirEventoCursoDialogViewModel.PARAMETRO_EVENTOS, new List<EventoCursoDTO> { Evento(12) } } });

            Assert.AreEqual(12, dialogo.EventoSeleccionado?.Id, "Con un solo evento sale elegido");
            Assert.IsFalse(dialogo.HayVariosEventos);
            Assert.IsNull(dialogo.ModalidadSeleccionada, "La modalidad se elige siempre a mano");
            Assert.IsFalse(dialogo.AceptarCommand.CanExecute(null), "Sin modalidad no se puede aceptar");
            Assert.AreEqual(2, dialogo.Modalidades.Count);
        }

        [TestMethod]
        public void Dialogo_ConVariosEventos_NoHayNingunoElegidoHastaQueSeElige()
        {
            var dialogo = new ElegirEventoCursoDialogViewModel();
            dialogo.OnDialogOpened(new ParametrosDialogo { { ElegirEventoCursoDialogViewModel.PARAMETRO_EVENTOS, new List<EventoCursoDTO> { Evento(12), Evento(13, "ALC", "Online") } } });

            Assert.IsTrue(dialogo.HayVariosEventos);
            Assert.IsNull(dialogo.EventoSeleccionado);
            Assert.IsFalse(dialogo.AceptarCommand.CanExecute(null));

            dialogo.EventoSeleccionado = dialogo.Eventos[1];
            dialogo.ModalidadSeleccionada = dialogo.Modalidades[1];

            Assert.IsTrue(dialogo.AceptarCommand.CanExecute(null));
            Assert.AreEqual("10/11/2026 10:00 · Alcobendas · Online", dialogo.EventoSeleccionado.TextoEvento);
            StringAssert.Contains(dialogo.ModalidadSeleccionada.Texto, "Sin cupón: 60,00");
        }

        [TestMethod]
        public void Dialogo_AlCambiarDeEvento_HayQueVolverAElegirLaModalidad()
        {
            var dialogo = new ElegirEventoCursoDialogViewModel();
            dialogo.OnDialogOpened(new ParametrosDialogo { { ElegirEventoCursoDialogViewModel.PARAMETRO_EVENTOS, new List<EventoCursoDTO> { Evento(12), Evento(13) } } });
            dialogo.EventoSeleccionado = dialogo.Eventos[0];
            dialogo.ModalidadSeleccionada = dialogo.Modalidades[0];

            dialogo.EventoSeleccionado = dialogo.Eventos[1];

            Assert.IsNull(dialogo.ModalidadSeleccionada);
            Assert.IsFalse(dialogo.AceptarCommand.CanExecute(null));
        }

        #endregion

        #region Meter un curso en la plantilla

        [TestMethod]
        public async Task MeterCurso_ConUnEvento_LaLineaQuedaConElImporteDeLaModalidadYBloqueada()
        {
            var vm = CrearViewModel();
            var curso = Curso();
            vm.ListaFiltrableProductos.ListaOriginal.Add(curso);
            EventosDelCurso(Evento(12));
            DialogoElige(null, 1); // el único evento sale elegido; modalidad «Sin cupón»

            bool queda = await vm.ElegirEventoCursoAsync(curso);

            Assert.IsTrue(queda);
            Assert.AreEqual(1, _dialogosAbiertos);
            Assert.AreEqual(12, curso.EventoId);
            Assert.AreEqual("NoReembolsable", curso.ModalidadEvento);
            Assert.AreEqual(60M, curso.precio, "El precio es el importe de la modalidad");
            Assert.AreEqual(0M, curso.descuento);
            Assert.IsFalse(curso.aplicarDescuento, "El descuento queda bloqueado");
            Assert.IsFalse(curso.precioHabilitado, "El precio queda bloqueado");
            Assert.IsFalse(curso.puedeEditarCantidadOferta, "La oferta queda bloqueada");
            Assert.AreEqual("10/11/2026 10:00 · Reina · Presencial · Sin cupón", curso.TextoEvento);
        }

        [TestMethod]
        public async Task MeterCurso_ConVariosEventos_SeEligeUnoYSeMandanSoloLosQueTienenPlazas()
        {
            var vm = CrearViewModel();
            var curso = Curso();
            vm.ListaFiltrableProductos.ListaOriginal.Add(curso);
            EventosDelCurso(Evento(12), Evento(13, "ALC"), Evento(14, plazas: 0));
            DialogoElige(1, 0);

            bool queda = await vm.ElegirEventoCursoAsync(curso);

            Assert.IsTrue(queda);
            var mandados = _parametrosDialogo.GetValue<List<EventoCursoDTO>>(ElegirEventoCursoDialogViewModel.PARAMETRO_EVENTOS);
            CollectionAssert.AreEqual(new[] { 12, 13 }, mandados.Select(e => e.Id).ToArray(), "El evento sin plazas no se ofrece");
            Assert.AreEqual(13, curso.EventoId);
            Assert.AreEqual("Reembolsable", curso.ModalidadEvento);
            Assert.AreEqual(100M, curso.precio);
            StringAssert.Contains(curso.TextoEvento, "Alcobendas");
        }

        [TestMethod]
        public async Task MeterCurso_SinEventosConPlazas_AvisaYNoAnadeLaLinea()
        {
            var vm = CrearViewModel();
            var curso = Curso();
            vm.ListaFiltrableProductos.ListaOriginal.Add(curso);
            EventosDelCurso(Evento(14, plazas: 0));
            DialogoElige(null, 0);

            bool queda = await vm.ElegirEventoCursoAsync(curso);

            Assert.IsFalse(queda);
            Assert.AreEqual(0, _dialogosAbiertos, "Sin plazas no se abre el diálogo");
            Assert.AreEqual(0, curso.cantidad, "La línea no se añade");
            Assert.IsNull(curso.EventoId);
            Assert.AreEqual(1, _errores.Count);
            StringAssert.Contains(_errores[0], "no tiene ningún evento con plazas libres");
            Assert.AreEqual(0, vm.listaProductosPedido.Count);
        }

        [TestMethod]
        public async Task MeterCurso_SinElegirModalidad_NoSeAnadeLaLinea()
        {
            var vm = CrearViewModel();
            var curso = Curso();
            vm.ListaFiltrableProductos.ListaOriginal.Add(curso);
            EventosDelCurso(Evento(12));
            DialogoElige(null, null); // acepta sin modalidad: el botón no se habilita

            bool queda = await vm.ElegirEventoCursoAsync(curso);

            Assert.IsFalse(queda);
            Assert.AreEqual(0, curso.cantidad);
            Assert.IsNull(curso.EventoId);
            Assert.IsNull(curso.ModalidadEvento);
        }

        [TestMethod]
        public async Task MeterCurso_CancelandoElDialogo_NoSeAnadeLaLinea()
        {
            var vm = CrearViewModel();
            var curso = Curso();
            vm.ListaFiltrableProductos.ListaOriginal.Add(curso);
            EventosDelCurso(Evento(12));
            DialogoElige(null, 0, ResultadoBoton.Cancel);

            Assert.IsFalse(await vm.ElegirEventoCursoAsync(curso));
            Assert.AreEqual(0, curso.cantidad);
        }

        [TestMethod]
        public async Task MeterCurso_YaConEvento_NoSeVuelveAPreguntar()
        {
            var vm = CrearViewModel();
            var curso = Curso();
            vm.ListaFiltrableProductos.ListaOriginal.Add(curso);
            EventosDelCurso(Evento(12));
            DialogoElige(null, 0);
            await vm.ElegirEventoCursoAsync(curso);

            curso.cantidad = 2;
            await vm.ElegirEventoCursoAsync(curso);

            Assert.AreEqual(1, _dialogosAbiertos);
            Assert.AreEqual(12, curso.EventoId);
        }

        [TestMethod]
        public void PedidoConCurso_LaLineaLlevaEventoYModalidad()
        {
            var vm = CrearViewModel();
            var curso = Curso();
            PlantillaVentaViewModel.AplicarEventoALinea(curso, Evento(12), Evento(12).Modalidades[0]);
            vm.ListaFiltrableProductos.ListaOriginal.Add(curso);
            vm.ListaFiltrableProductos.ListaOriginal.Add(new LineaPlantillaVenta { producto = "17404", grupo = "COS", cantidad = 1, precio = 10M });

            vm.SincronizarListasAlEstado();
            var pedido = vm.Estado.ToPedidoVentaDTO("DIR", () => 0, () => "NV");

            var lineaCurso = pedido.Lineas.Single(l => l.Producto == "90077");
            Assert.AreEqual(12, lineaCurso.EventoId);
            Assert.AreEqual("Reembolsable", lineaCurso.ModalidadEvento);
            Assert.AreEqual(100M, lineaCurso.PrecioUnitario);
            var lineaProducto = pedido.Lineas.Single(l => l.Producto == "17404");
            Assert.IsNull(lineaProducto.EventoId);
            Assert.IsNull(lineaProducto.ModalidadEvento);
        }

        #endregion

        #region Prepago

        [TestMethod]
        public async Task MeterCurso_SinPrepago_PidePrepagoAlSelectorYAvisa()
        {
            var vm = CrearViewModel();
            PonerPlazo(vm, "CONTADO");
            var curso = Curso();
            vm.ListaFiltrableProductos.ListaOriginal.Add(curso);
            EventosDelCurso(Evento(12));
            DialogoElige(null, 0);

            await vm.ElegirEventoCursoAsync(curso);

            Assert.AreEqual(Constantes.PlazosPago.PREPAGO, vm.PlazoPagoCliente, "Se pide «Prepago» al selector de plazos");
            CollectionAssert.Contains(_avisos, PlantillaVentaViewModel.MENSAJE_PREPAGO_FORZADO);
        }

        [TestMethod]
        public void Guardar_ConCursoSinPrepago_SeBloqueaConMensaje()
        {
            var vm = CrearViewModel();
            PonerPlazo(vm, "CONTADO");
            var curso = Curso();
            PlantillaVentaViewModel.AplicarEventoALinea(curso, Evento(12), Evento(12).Modalidades[0]);
            vm.ListaFiltrableProductos.ListaOriginal.Add(curso);

            Assert.AreEqual(PlantillaVentaViewModel.MENSAJE_PREPAGO_OBLIGATORIO, vm.ErrorCursosAlGuardar());

            PonerPlazo(vm, Constantes.PlazosPago.PREPAGO);
            Assert.IsNull(vm.ErrorCursosAlGuardar(), "Con prepago se puede guardar");
        }

        [TestMethod]
        public void Guardar_ConCursoSinPrepago_ElComandoNoLlamaALaApi()
        {
            var vm = CrearViewModel();
            PonerPlazo(vm, "CONTADO");
            vm.FormaPagoSeleccionada = new FormaPago { formaPago = "EFC" };
            var curso = Curso();
            PlantillaVentaViewModel.AplicarEventoALinea(curso, Evento(12), Evento(12).Modalidades[0]);
            vm.ListaFiltrableProductos.ListaOriginal.Add(curso);

            vm.cmdCrearPedido.Execute(null);

            CollectionAssert.Contains(_errores, PlantillaVentaViewModel.MENSAJE_PREPAGO_OBLIGATORIO);
            A.CallTo(() => _servicio.CrearPedido(A<PedidoVentaDTO>.Ignored)).MustNotHaveHappened();
        }

        [TestMethod]
        public void Guardar_SinCursos_NoSeExigePrepago()
        {
            var vm = CrearViewModel();
            PonerPlazo(vm, "CONTADO");
            vm.ListaFiltrableProductos.ListaOriginal.Add(new LineaPlantillaVenta { producto = "17404", cantidad = 1, precio = 10M });

            Assert.IsNull(vm.ErrorCursosAlGuardar());
        }

        [TestMethod]
        public void Guardar_CursoAmpliandoUnPedidoPendiente_SeBloquea()
        {
            var vm = CrearViewModel();
            PonerPlazo(vm, Constantes.PlazosPago.PREPAGO);
            var curso = Curso();
            PlantillaVentaViewModel.AplicarEventoALinea(curso, Evento(12), Evento(12).Modalidades[0]);
            vm.ListaFiltrableProductos.ListaOriginal.Add(curso);
            vm.PedidoPendienteSeleccionado = 927001;

            Assert.AreEqual(PlantillaVentaViewModel.MENSAJE_CURSO_EN_AMPLIACION, vm.ErrorCursosAlGuardar());
        }

        #endregion

        #region Los cursos no suman al pedido de productos

        [TestMethod]
        public void CienEurosDeProductosYCientoCincuentaDeCurso_PortesYGanavisionesSobreCien()
        {
            // Regresión: los cursos van en un pedido aparte; su importe no cuenta para portes ni Ganavisiones
            var vm = CrearViewModel();
            vm._resultadoPortes = new ResultadoPortesDTO { ImportePortes = 7M, ImporteMinimoPedidoSinPortes = 120M };
            vm.ListaFiltrableProductos.ListaOriginal.Add(new LineaPlantillaVenta { producto = "17404", grupo = "COS", cantidad = 1, precio = 100M, estado = 0 });
            var curso = Curso();
            PlantillaVentaViewModel.AplicarEventoALinea(curso, Evento(12), new ModalidadEventoDTO { Modalidad = "Reembolsable", Nombre = "Con cupón", Importe = 150M });
            curso.grupo = "COS"; // aunque el curso fuera de un grupo bonificable, no cuenta
            vm.ListaFiltrableProductos.ListaOriginal.Add(curso);

            Assert.AreEqual(100M, vm.baseImponibleParaPortes, "Portes sobre 100 €");
            Assert.IsFalse(vm.PortesGratis, "Con 100 € no se llega a los 120 € de portes pagados");
            Assert.AreEqual(7M, vm.ImportePortesMostrar);
            Assert.AreEqual(100M, vm.BaseImponibleBonificable, "Ganavisiones sobre 100 €");
            Assert.AreEqual(100M, vm.baseImponiblePedido, "El pedido de productos es de 100 €");
            Assert.AreEqual(150M, vm.BaseImponibleCursos, "Los cursos se enseñan aparte");
            Assert.IsTrue(vm.HayCursosEnElPedido);
        }

        [TestMethod]
        public void SoloCursos_NoHayPortesNiReembolso()
        {
            var vm = CrearViewModel();
            vm._resultadoPortes = new ResultadoPortesDTO { ImportePortes = 7M, ImporteMinimoPedidoSinPortes = 120M, EsContraReembolso = true, ComisionReembolso = 3M };
            var curso = Curso();
            PlantillaVentaViewModel.AplicarEventoALinea(curso, Evento(12), Evento(12).Modalidades[0]);
            vm.ListaFiltrableProductos.ListaOriginal.Add(curso);

            Assert.AreEqual(0M, vm.ImportePortesMostrar);
            Assert.AreEqual(0M, vm.ImporteReembolsoMostrar);
            Assert.IsFalse(vm.listaProductosPedidoConPortes.Any(l => l.esLineaPortes));
        }

        [TestMethod]
        public void PreguntasALaApiSobreElPedido_SinLineasDeCurso()
        {
            var vm = CrearViewModel();
            var curso = Curso();
            PlantillaVentaViewModel.AplicarEventoALinea(curso, Evento(12), Evento(12).Modalidades[0]);
            vm.ListaFiltrableProductos.ListaOriginal.Add(curso);
            vm.ListaFiltrableProductos.ListaOriginal.Add(new LineaPlantillaVenta { producto = "17404", cantidad = 2, precio = 10M });

            var pedido = vm.PrepararPedidoParaSugerencias();
            var carrito = vm.CarritoSugerenciasProductos();

            Assert.IsNotNull(pedido);
            Assert.IsTrue(pedido.Lineas.All(l => l.Producto == "17404"), "Modos, ofertas sugeridas… se preguntan sin cursos");
            Assert.IsTrue(carrito.All(c => c.Producto == "17404"));
        }

        #endregion

        #region Pedidos adicionales

        [TestMethod]
        public void Respuesta_ConPedidosAdicionales_SeLeen()
        {
            var adicionales = RespuestaCrearPedidoPlantilla.LeerPedidosAdicionales(
                "{\"numero\":928122,\"PedidosAdicionales\":[{\"Numero\":928123,\"Motivo\":\"Cursos\"}]}");

            Assert.AreEqual(1, adicionales.Count);
            Assert.AreEqual(928123, adicionales[0].Numero);
            Assert.AreEqual("Cursos", adicionales[0].Motivo);
        }

        [TestMethod]
        public void Respuesta_SinPedidosAdicionalesOVacia_ListaVacia()
        {
            Assert.AreEqual(0, RespuestaCrearPedidoPlantilla.LeerPedidosAdicionales("{\"numero\":928122}").Count);
            Assert.AreEqual(0, RespuestaCrearPedidoPlantilla.LeerPedidosAdicionales("").Count);
            Assert.AreEqual(0, RespuestaCrearPedidoPlantilla.LeerPedidosAdicionales("no es json").Count);
            Assert.AreEqual(0, RespuestaCrearPedidoPlantilla.LeerPedidosAdicionales("{\"pedidosAdicionales\":null}").Count);
        }

        [TestMethod]
        public void AlGuardar_ConPedidoDeCursoAparte_SeDiceQueSonDosPedidos()
        {
            var vm = CrearViewModel();
            var respuesta = new RespuestaCrearPedidoPlantilla
            {
                Numero = "928122",
                PedidosAdicionales = new List<PedidoAdicionalDTO> { new PedidoAdicionalDTO { Numero = 928123, Motivo = "Cursos" } }
            };

            Assert.IsTrue(vm.AvisarPedidosAdicionales(respuesta));

            CollectionAssert.Contains(_avisos, "Se han creado dos pedidos: 928122 (productos) y 928123 (curso). Los cursos se facturan aparte.");
        }

        [TestMethod]
        public void AlGuardar_SinPedidosAdicionales_NoSeAvisa()
        {
            var vm = CrearViewModel();

            Assert.IsFalse(vm.AvisarPedidosAdicionales(new RespuestaCrearPedidoPlantilla { Numero = "928122" }));
            Assert.IsFalse(vm.AvisarPedidosAdicionales(null));
            Assert.AreEqual(0, _avisos.Count);
        }

        #endregion
    }
}
