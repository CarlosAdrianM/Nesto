using CommunityToolkit.Mvvm.Messaging;
using ControlesUsuario.Models;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Services.ServirJunto;
using Nesto.Infrastructure.Shared;
using Nesto.Models;
using Nesto.Modulos.PedidoVenta;
using Nesto.Modulos.PlantillaVenta;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Unity;

namespace PlantillaVentaTests
{
    /// <summary>
    /// Nesto#527 (María José, REI, 15/10/26): 8 intentos seguidos de pasar a «Según vaya entrando» con las
    /// muestras 42543 y 43893 sin stock. Cada vez, ventana de error y el selector volvía a «Todo junto» sin
    /// decirlo. Ahora, como el detalle del pedido (Nesto#452), se ofrece quitarlas en el mismo paso: nunca sin
    /// confirmación y revalidando SIN ellas antes de tocar nada. Y si no se quitan, se dice que el pedido se
    /// queda en «Todo junto».
    /// </summary>
    [TestClass]
    public class QuitarMuestrasModoServicioPlantillaTests
    {
        private IServicioDialogos _dialogos;
        private IServirJuntoService _servirJunto;
        private List<List<ProductoBonificadoConCantidadRequest>> _lineasEnviadas;
        private List<string> _confirmaciones;
        private List<string> _errores;
        private Queue<bool> _respuestas;

        [TestInitialize]
        public void Inicializar()
        {
            _dialogos = A.Fake<IServicioDialogos>();
            _servirJunto = A.Fake<IServirJuntoService>();
            _lineasEnviadas = new List<List<ProductoBonificadoConCantidadRequest>>();
            _confirmaciones = new List<string>();
            _errores = new List<string>();
            _respuestas = new Queue<bool>();
            A.CallTo(() => _dialogos.ShowConfirmationAnswer(A<string>._, A<string>._))
                .ReturnsLazily((string titulo, string mensaje) =>
                {
                    _confirmaciones.Add(mensaje);
                    return _respuestas.Count > 0 ? _respuestas.Dequeue() : true;
                });
            A.CallTo(() => _dialogos.ShowError(A<string>._))
                .Invokes((string mensaje) => _errores.Add(mensaje));
        }

        private void ElServidorResponde(params ValidarServirJuntoResponse[] respuestas)
        {
            A.CallTo(() => _servirJunto.Validar(A<string>._, A<List<ProductoBonificadoConCantidadRequest>>._,
                    A<List<ProductoBonificadoConCantidadRequest>>._, A<string>._, A<string>._, A<string>._, A<string>._,
                    A<bool?>._, A<List<LineaPortesServirJuntoDTO>>._, A<int?>._, A<byte?>._))
                .Invokes(c => _lineasEnviadas.Add(c.GetArgument<List<ProductoBonificadoConCantidadRequest>>(2)))
                .ReturnsNextFromSequence(respuestas.Select(r => Task.FromResult(r)).ToArray());
        }

        private PlantillaVentaViewModel CrearViewModel(params LineaPlantillaVenta[] lineas)
        {
            IConfiguracion configuracion = A.Fake<IConfiguracion>();
            A.CallTo(() => configuracion.LeerParametroSync(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.AlmacenRuta)).Returns("ALG");
            var vm = new PlantillaVentaViewModel(A.Fake<IUnityContainer>(), A.Fake<IServicioNavegacion>(), configuracion, A.Fake<IPlantillaVentaService>(),
                new WeakReferenceMessenger(), _dialogos, A.Fake<IPedidoVentaService>(),
                A.Fake<IBorradorPlantillaVentaService>(), A.Fake<IServicioAutenticacion>());
            vm.ServicioServirJunto = _servirJunto;
            vm.ListaFiltrableProductos.ListaOriginal = new ObservableCollection<IFiltrableItem>(lineas);
            vm.direccionEntregaSeleccionada = new DireccionesEntregaCliente { contacto = "0", servirJunto = true };
            vm.ModoServicio = ModosServicio.TODO_JUNTO;
            return vm;
        }

        private static LineaPlantillaVenta Linea(string producto, string texto, int cantidad = 1)
        {
            return new LineaPlantillaVenta { producto = producto, texto = texto, cantidad = cantidad, precio = 0M };
        }

        private static ValidarServirJuntoResponse Denegada(params string[] productos)
        {
            return new ValidarServirJuntoResponse
            {
                PuedeDesmarcar = false,
                Mensaje = "No se puede pasar el pedido a «Según vaya entrando»: no hay stock en REI de estas muestras (material promocional) y no pueden quedarse pendientes. Bórrelas del pedido: "
                    + string.Join(", ", productos) + ". O elija otro modo de servicio (los que esperan a tener todo)",
                ProductosProblematicos = productos.Select(p => new ProductoSinStockDTO { ProductoId = p, ProductoNombre = "MUESTRA " + p }).ToList()
            };
        }

        private static ValidarServirJuntoResponse Permitida(string aviso = null)
        {
            return new ValidarServirJuntoResponse { PuedeDesmarcar = true, Aviso = aviso, ProductosProblematicos = new List<ProductoSinStockDTO>() };
        }

        private static string[] ProductosDelPedido(PlantillaVentaViewModel vm)
        {
            return vm.listaProductosPedido.Select(l => l.producto.Trim()).ToArray();
        }

        [TestMethod]
        public void MuestrasSinStock_SiAcepta_RevalidaSinEllasLasQuitaYSeQuedaEnElModoElegido()
        {
            ElServidorResponde(Denegada("42543", "43893"), Permitida());
            var champu = Linea("12345", "CHAMPU 1000 ML", 2);
            var muestra1 = Linea("42543 ", "MUESTRA CREMA FACIAL");
            var muestra2 = Linea("43893", "MUESTRA SERUM");
            var vm = CrearViewModel(champu, muestra1, muestra2);

            vm.ModoServicio = ModosServicio.SEGUN_VAYA_ENTRANDO;

            Assert.AreEqual(ModosServicio.SEGUN_VAYA_ENTRANDO, vm.ModoServicio, "El modo elegido se queda puesto");
            Assert.IsFalse(vm.Estado.ServirJunto);
            CollectionAssert.AreEqual(new[] { "12345" }, ProductosDelPedido(vm), "Las muestras salen del pedido");
            Assert.AreEqual(0, muestra1.cantidad);
            Assert.AreEqual(0, muestra2.cantidad);
            Assert.AreEqual(2, champu.cantidad, "Lo demás no se toca");
            Assert.AreEqual(2, _lineasEnviadas.Count, "Se valida con todas y se revalida sin las muestras");
            CollectionAssert.AreEqual(new[] { "12345" }, _lineasEnviadas[1].Select(l => l.ProductoId).ToArray(), "La revalidación va SIN las muestras");
            var oferta = _confirmaciones.Single();
            StringAssert.Contains(oferta, "· 42543 MUESTRA CREMA FACIAL");
            StringAssert.Contains(oferta, "· 43893 MUESTRA SERUM");
            StringAssert.Contains(oferta, "¿Quiere quitarlas del pedido y pasarlo a «Según vaya entrando»?");
            Assert.AreEqual(0, _errores.Count);
            Assert.IsFalse(vm.HayAvisoModoServicio);
        }

        [TestMethod]
        public void MuestrasSinStock_SiRechaza_NoQuitaNadaYAvisaDeQueSeQuedaEnTodoJunto()
        {
            ElServidorResponde(Denegada("42543"));
            _respuestas.Enqueue(false);
            var muestra = Linea("42543", "MUESTRA");
            var vm = CrearViewModel(Linea("12345", "CHAMPU"), muestra);

            vm.ModoServicio = ModosServicio.SEGUN_VAYA_ENTRANDO;

            Assert.AreEqual(ModosServicio.TODO_JUNTO, vm.ModoServicio);
            Assert.IsTrue(vm.Estado.ServirJunto);
            Assert.AreEqual(1, muestra.cantidad, "Sin confirmación no se quita nada");
            Assert.AreEqual(1, _lineasEnviadas.Count, "Si rechaza no hace falta revalidar");
            StringAssert.Contains(_confirmaciones.Single(), "Si no, el pedido se queda en «Todo junto».");
            Assert.IsTrue(vm.HayAvisoModoServicio, "Junto al selector queda dicho que ha vuelto a «Todo junto»");
            Assert.AreEqual("No se ha podido pasar a «Según vaya entrando»: el pedido se queda en «Todo junto».", vm.AvisoModoServicio);
        }

        [TestMethod]
        public void MuestrasSinStock_SiLaRevalidacionSigueDenegada_NoTocaNadaYLoExplica()
        {
            // Sin las muestras seguiría denegado por otra cosa (p. ej. un regalo sin stock): no se deja el pedido a medias.
            ElServidorResponde(Denegada("42543"), Denegada("77777"));
            _respuestas.Enqueue(true);
            var muestra = Linea("42543", "MUESTRA");
            var vm = CrearViewModel(Linea("12345", "CHAMPU"), muestra);

            vm.ModoServicio = ModosServicio.SEGUN_VAYA_ENTRANDO;

            Assert.AreEqual(ModosServicio.TODO_JUNTO, vm.ModoServicio);
            Assert.AreEqual(1, muestra.cantidad, "No se quita nada si seguiría denegado");
            Assert.AreEqual(2, _lineasEnviadas.Count);
            var error = _errores.Single();
            StringAssert.StartsWith(error, "No se ha quitado ninguna línea: aunque se quitaran, el pedido seguiría sin poder pasar a «Según vaya entrando».");
            StringAssert.Contains(error, "77777");
            StringAssert.EndsWith(error, "El pedido se queda en «Todo junto».");
            Assert.IsTrue(vm.HayAvisoModoServicio);
        }

        [TestMethod]
        public void MuestrasSinStock_SiCancelaElAvisoPosterior_NoQuitaLasLineas()
        {
            ElServidorResponde(Denegada("42543"), Permitida(aviso: "Se cobra comisión contra reembolso en cada envío."));
            _respuestas.Enqueue(true);   // acepta quitarlas
            _respuestas.Enqueue(false);  // pero cancela ante el aviso
            var muestra = Linea("42543", "MUESTRA");
            var vm = CrearViewModel(Linea("12345", "CHAMPU"), muestra);

            vm.ModoServicio = ModosServicio.SEGUN_VAYA_ENTRANDO;

            Assert.AreEqual(ModosServicio.TODO_JUNTO, vm.ModoServicio);
            Assert.AreEqual(1, muestra.cantidad, "Solo se quitan cuando todo queda aceptado");
        }

        [TestMethod]
        public void ProblematicoQueNoEstaEnElPedido_NoOfreceQuitarYElErrorDiceQueSeQuedaEnTodoJunto()
        {
            // 99999 es, p. ej., un regalo de la otra lista: quitar las líneas no resolvería nada.
            ElServidorResponde(Denegada("42543", "99999"));
            var muestra = Linea("42543", "MUESTRA");
            var vm = CrearViewModel(Linea("12345", "CHAMPU"), muestra);

            vm.ModoServicio = ModosServicio.SEGUN_VAYA_ENTRANDO;

            Assert.AreEqual(ModosServicio.TODO_JUNTO, vm.ModoServicio);
            Assert.AreEqual(1, muestra.cantidad);
            Assert.AreEqual(0, _confirmaciones.Count, "No se ofrece quitar nada");
            var error = _errores.Single();
            StringAssert.StartsWith(error, "No se puede pasar el pedido a «Según vaya entrando»");
            StringAssert.EndsWith(error, "(los que esperan a tener todo). El pedido se queda en «Todo junto».");
            Assert.IsTrue(vm.HayAvisoModoServicio);
        }

        [TestMethod]
        public void Permitido_NoPreguntaNiQuitaNada()
        {
            ElServidorResponde(Permitida());
            var muestra = Linea("42543", "MUESTRA");
            var vm = CrearViewModel(Linea("12345", "CHAMPU"), muestra);

            vm.ModoServicio = ModosServicio.SEGUN_VAYA_ENTRANDO;

            Assert.AreEqual(ModosServicio.SEGUN_VAYA_ENTRANDO, vm.ModoServicio);
            Assert.AreEqual(1, muestra.cantidad);
            Assert.AreEqual(0, _confirmaciones.Count + _errores.Count);
        }

        [TestMethod]
        public void AnadirVueltaATodoJunto_TerminaSiempreConLaFrase()
        {
            Assert.AreEqual("No hay stock. El pedido se queda en «Todo junto».", PlantillaVentaViewModel.AnadirVueltaATodoJunto("No hay stock."));
            Assert.AreEqual("No hay stock. El pedido se queda en «Todo junto».", PlantillaVentaViewModel.AnadirVueltaATodoJunto("No hay stock "));
            Assert.AreEqual("El pedido se queda en «Todo junto».", PlantillaVentaViewModel.AnadirVueltaATodoJunto(null));
        }

        [TestMethod]
        public void ConstruirOfertaQuitarYCambiarModo_NombraElModoElegidoYTrataDeUsted()
        {
            var texto = PlantillaVentaViewModel.ConstruirOfertaQuitarYCambiarModo(
                new[] { Linea("42543 ", "MUESTRA CREMA FACIAL ") }, ModosServicio.TRAS_REPONER_DE_TIENDAS);

            StringAssert.StartsWith(texto, "No se puede pasar el pedido a «Tras reponer de tiendas»");
            StringAssert.Contains(texto, "· 42543 MUESTRA CREMA FACIAL");
            StringAssert.Contains(texto, "¿Quiere quitarlas del pedido y pasarlo a «Tras reponer de tiendas»?");
            Assert.IsFalse(texto.Contains("Servir junto"));
        }
    }
}
