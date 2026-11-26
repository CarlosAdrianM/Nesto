using ControlesUsuario.Models;
using ControlesUsuario.Services;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Modulos.PedidoCompra;
using Nesto.Modulos.PedidoCompra.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PedidoCompraTests
{
    /// <summary>
    /// Nesto#647 (Carlos 26/11/26: «la problemática es la misma y debería ir igual»): en la casilla de producto de las líneas del pedido
    /// de compra, un código de barras carga su producto y, si es de varios, se pregunta cuál con el selector común.
    /// </summary>
    [TestClass]
    public class LineaPedidoCompraCodigoBarrasTests
    {
        private const string EAN = "8436538394380";

        private IPedidoCompraService _servicio = null!;
        private IServicioDialogos _dialogos = null!;

        [TestInitialize]
        public void Preparar()
        {
            _servicio = A.Fake<IPedidoCompraService>();
            _dialogos = A.Fake<IServicioDialogos>();
            A.CallTo(() => _servicio.LeerProducto(A<string>._, A<string>._, A<string>._, A<string>._)).Returns(Task.FromResult<LineaPedidoCompraDTO>(null));
            A.CallTo(() => _servicio.LeerProducto(A<string>._, "45931", A<string>._, A<string>._))
                .Returns(new LineaPedidoCompraDTO { Producto = "45931", Texto = "PACK VIVO", TipoLinea = Constantes.LineasPedido.TiposLinea.PRODUCTO });
            A.CallTo(() => _servicio.LeerProducto(A<string>._, "44967", A<string>._, A<string>._))
                .Returns(new LineaPedidoCompraDTO { Producto = "44967", Texto = "PACK ANULADO", TipoLinea = Constantes.LineasPedido.TiposLinea.PRODUCTO });
        }

        private LineaPedidoCompraWrapper Linea()
        {
            var pedido = new PedidoCompraWrapper(new PedidoCompraDTO { Empresa = "1", Proveedor = "1043" }, _servicio);
            var linea = new LineaPedidoCompraWrapper(new LineaPedidoCompraDTO { TipoLinea = Constantes.LineasPedido.TiposLinea.PRODUCTO, Cantidad = 1 }, _servicio, _dialogos);
            pedido.Lineas.Add(linea);
            linea.Pedido = pedido;
            return linea;
        }

        private void Candidatos(params string[] productos)
            => A.CallTo(() => _servicio.BuscarProductosPorCodigoBarras("1", EAN))
                .Returns(productos.Select(p => new ProductoCodigoBarrasDuplicado { Producto = p, Nombre = "PACK " + p }).ToList());

        [TestMethod]
        public async Task CodigoDeVarios_PreguntaYCargaElElegido()
        {
            Candidatos("44967", "45931");
            A.CallTo(() => _dialogos.ShowDialogAsync(ElectorProductoCodigoBarras.NOMBRE_DIALOGO, A<ParametrosDialogo>._))
                .Returns(new ResultadoDialogo(ResultadoBoton.OK, new ParametrosDialogo { { "producto", "45931" } }));
            LineaPedidoCompraWrapper linea = Linea();

            linea.Producto = EAN;
            await linea.UltimaCarga;

            Assert.AreEqual("45931", linea.Producto);
            Assert.AreEqual("PACK VIVO", linea.Texto);
            A.CallTo(() => _dialogos.ShowDialogAsync(ElectorProductoCodigoBarras.NOMBRE_DIALOGO,
                A<ParametrosDialogo>.That.Matches(p => p.GetValue<string>("title") == $"El código de barras {EAN} es de varios productos")))
                .MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task CodigoDeVarios_Cancela_LaLineaSeQuedaComoEstaba()
        {
            Candidatos("44967", "45931");
            A.CallTo(() => _dialogos.ShowDialogAsync(A<string>._, A<ParametrosDialogo>._)).Returns(new ResultadoDialogo(ResultadoBoton.Cancel));
            LineaPedidoCompraWrapper linea = Linea();

            linea.Producto = EAN;
            await linea.UltimaCarga;

            Assert.AreEqual(EAN, linea.Producto);
            A.CallTo(() => _servicio.LeerProducto(A<string>._, "45931", A<string>._, A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task CodigoDeUno_LoCargaSinPreguntar()
        {
            Candidatos("44967");
            LineaPedidoCompraWrapper linea = Linea();

            linea.Producto = EAN;
            await linea.UltimaCarga;

            Assert.AreEqual("44967", linea.Producto);
            A.CallTo(() => _dialogos.ShowDialogAsync(A<string>._, A<ParametrosDialogo>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task UnaReferencia_NoBuscaCodigos()
        {
            LineaPedidoCompraWrapper linea = Linea();

            linea.Producto = "45931";
            await linea.UltimaCarga;

            Assert.AreEqual("PACK VIVO", linea.Texto);
            A.CallTo(() => _servicio.BuscarProductosPorCodigoBarras(A<string>._, A<string>._)).MustNotHaveHappened();
        }
    }
}
