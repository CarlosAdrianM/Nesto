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
using Newtonsoft.Json;

namespace PlantillaVentaTests
{
    /// <summary>
    /// Incidencia 1398 (Pilar, 20/11/26): la plantilla solo encontraba cursos con evento; la API devuelve también las referencias de
    /// FORMACIÓN sin evento (p. ej. 90324) con «EsFormacion» = true y «EsCurso» = false. Se meten como una línea normal (sin evento ni
    /// modalidad), se distinguen con «Formación», se avisa si van con productos y, al guardar, la API las separa en un pedido CV.
    /// </summary>
    [TestClass]
    public class FormacionPlantillaTests
    {
        private IPlantillaVentaService _servicio;
        private IServicioDialogos _dialogos;
        private List<PedidoVentaDTO> _creados;
        private readonly List<string> _avisos = new List<string>();

        [TestInitialize]
        public void Setup()
        {
            _servicio = A.Fake<IPlantillaVentaService>();
            _dialogos = A.Fake<IServicioDialogos>();
            _creados = new List<PedidoVentaDTO>();
            A.CallTo(() => _dialogos.ShowNotification(A<string>.Ignored, A<string>.Ignored)).Invokes((string t, string m) => _avisos.Add(m));
            A.CallTo(() => _servicio.CrearPedido(A<PedidoVentaDTO>._)).ReturnsLazily((PedidoVentaDTO p) =>
            {
                _creados.Add(p);
                return Task.FromResult(new RespuestaCrearPedidoPlantilla
                {
                    Numero = "931700",
                    PedidosAdicionales = new List<PedidoAdicionalDTO> { new PedidoAdicionalDTO { Numero = 931701, Motivo = "Formación" } }
                });
            });
        }

        private PlantillaVentaViewModel CrearViewModel()
        {
            IConfiguracion configuracion = A.Fake<IConfiguracion>();
            A.CallTo(() => configuracion.servidorAPI).Returns("http://127.0.0.1:9/api/");
            A.CallTo(() => configuracion.LeerParametroSync(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.AlmacenRuta)).Returns("ALG");
            var vm = new PlantillaVentaViewModel(A.Fake<IAbridorPedidos>(), A.Fake<IServicioNavegacion>(), configuracion, _servicio,
                new WeakReferenceMessenger(), _dialogos, A.Fake<IPedidoVentaService>(), A.Fake<IBorradorPlantillaVentaService>(),
                A.Fake<IServicioAutenticacion>());
            vm.ListaFiltrableProductos.ListaOriginal = new ObservableCollection<IFiltrableItem>();
            vm._clienteSeleccionado = new ClienteJson { empresa = "1", cliente = "15191", contacto = "0", iva = "G21", cifNif = "12345678A" };
            vm.FormaPagoSeleccionada = new FormaPago { formaPago = "RCB" };
            typeof(PlantillaVentaViewModel).GetField("_plazoPagoSeleccionado", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(vm, new PlazosPago { plazoPago = "1/30" });
            vm.Estado.PlazosPago = "1/30";
            vm.formaVentaSeleccionada = 1;
            vm.almacenSeleccionado = new Sede { Codigo = "ALG", Nombre = "Algete" };
            return vm;
        }

        private static LineaPlantillaVenta Formacion(int cantidad = 1) => new LineaPlantillaVenta
        {
            producto = "90324",
            texto = "FORMACIÓN EN CABINA",
            grupo = "CUR",
            EsFormacion = true,
            EsCurso = false,
            cantidad = cantidad,
            precio = 80M,
            aplicarDescuento = true,
            iva = "G21"
        };

        private static LineaPlantillaVenta Producto() => new LineaPlantillaVenta
        {
            producto = "17404", texto = "CREMA", grupo = "COS", cantidad = 2, precio = 10M, iva = "G21"
        };

        [TestMethod]
        public void Json_ConEsFormacion_LaLineaLoTrae_YSinElCampoEsFalso()
        {
            var con = JsonConvert.DeserializeObject<List<LineaPlantillaVenta>>("[{\"producto\":\"90324\",\"EsFormacion\":true,\"EsCurso\":false}]");
            var sin = JsonConvert.DeserializeObject<List<LineaPlantillaVenta>>("[{\"producto\":\"17404\"}]");

            Assert.IsTrue(con[0].EsFormacion);
            Assert.IsFalse(con[0].EsCurso);
            Assert.IsFalse(sin[0].EsFormacion, "una API anterior no lo manda");
        }

        [TestMethod]
        public void LineaDeFormacion_SeEditaComoUnaNormalYSeDistingue()
        {
            var linea = Formacion();
            linea.aplicarDescuentoFicha = true;

            Assert.IsTrue(linea.precioHabilitado, "precio editable, como un producto");
            Assert.IsTrue(linea.puedeEditarCantidadOferta, "oferta y descuento, como un producto");
            Assert.AreEqual("Formación", linea.TextoEventoOFormacion);
            Assert.IsNull(Producto().TextoEventoOFormacion, "un producto no lleva etiqueta");
        }

        [TestMethod]
        public async Task MeterFormacion_NoPideEventoNiModalidad()
        {
            var vm = CrearViewModel();
            var linea = Formacion();
            vm.ListaFiltrableProductos.ListaOriginal.Add(linea);

            bool queda = await vm.ElegirEventoCursoAsync(linea);

            Assert.IsTrue(queda);
            A.CallTo(() => _servicio.CargarEventosProducto(A<string>._, A<string>._)).MustNotHaveHappened();
            A.CallTo(() => _dialogos.ShowDialogAsync(A<string>._, A<ParametrosDialogo>._)).MustNotHaveHappened();
            Assert.IsNull(linea.EventoId);
            Assert.AreEqual(1, vm.listaProductosPedido.Count, "la línea se queda en el pedido");
        }

        [TestMethod]
        public void ProductosYFormacion_SeAvisaEnElResumen_SoloFormacionNo()
        {
            var vm = CrearViewModel();
            vm.ListaFiltrableProductos.ListaOriginal.Add(Formacion());
            Assert.IsFalse(vm.HayProductosYFormacion, "solo formación: no hay mezcla");

            vm.ListaFiltrableProductos.ListaOriginal.Add(Producto());
            Assert.IsTrue(vm.HayProductosYFormacion);
        }

        [TestMethod]
        public async Task Guardar_ConProductosYFormacion_AvisaYMandaLaFormacionComoLineaNormal()
        {
            var vm = CrearViewModel();
            vm.ListaFiltrableProductos.ListaOriginal.Add(Producto());
            vm.ListaFiltrableProductos.ListaOriginal.Add(Formacion());

            await vm.CrearPedidoAsync();

            CollectionAssert.Contains(_avisos, "La formación irá en un pedido aparte (serie CV).");
            Assert.AreEqual(1, _creados.Count);
            var lineaFormacion = _creados[0].Lineas.Single(l => l.Producto == "90324");
            Assert.IsNull(lineaFormacion.EventoId, "sin evento: la API la separa sola");
            Assert.IsNull(lineaFormacion.ModalidadEvento);
            CollectionAssert.Contains(_avisos, "Se han creado dos pedidos: 931700 (productos) y 931701 (formación). La formación se factura aparte.");
        }

        [TestMethod]
        public async Task Guardar_SoloProductos_NoAvisaDeFormacion()
        {
            A.CallTo(() => _servicio.CrearPedido(A<PedidoVentaDTO>._)).Returns(Task.FromResult(new RespuestaCrearPedidoPlantilla { Numero = "931702" }));
            var vm = CrearViewModel();
            vm.ListaFiltrableProductos.ListaOriginal.Add(Producto());

            await vm.CrearPedidoAsync();

            CollectionAssert.DoesNotContain(_avisos, PlantillaVentaViewModel.MENSAJE_FORMACION_APARTE);
        }

        [TestMethod]
        public void MensajePedidosAdicionales_CursosYFormacion()
        {
            Assert.AreEqual("Se han creado 3 pedidos: 931700 (productos), 931701 (curso) y 931702 (formación). Los cursos y la formación se facturan aparte.",
                PlantillaVentaViewModel.MensajePedidosAdicionales("931700", new[]
                {
                    new PedidoAdicionalDTO { Numero = 931701, Motivo = "Cursos" },
                    new PedidoAdicionalDTO { Numero = 931702, Motivo = "formacion" }
                }));
            Assert.AreEqual("Se han creado dos pedidos: 931700 (productos) y 931701 (curso). Los cursos se facturan aparte.",
                PlantillaVentaViewModel.MensajePedidosAdicionales("931700", new[] { new PedidoAdicionalDTO { Numero = 931701, Motivo = null } }),
                "sin Motivo, como siempre");
        }
    }
}
