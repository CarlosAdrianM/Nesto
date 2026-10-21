using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Services;
using Nesto.Models;
using Nesto.Modulos.PedidoVenta;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ControlesUsuario.Dialogs;

namespace PedidoVentaTests
{
    /// <summary>
    /// NestoAPI#684 (pedido 928623): una fecha de entrega anterior al 01/01/2000 la rechaza la API con un 400. Nesto tiene
    /// que enseñar ese mensaje tal cual, venga como BadRequest de Web API ({"Message": ...}, PUT/POST de pedidos) o como
    /// excepción de negocio ({"error": {"code": "FECHA_ENTREGA_NO_VALIDA", ...}}, red de seguridad al guardar).
    /// </summary>
    [TestClass]
    public class FechaEntregaDesde2000DetalleTests
    {
        private const string MENSAJE = "La fecha de entrega del producto 12345 (19/10/1926) no es válida: no puede ser anterior al 01/01/2000. Revise el año.";

        [TestMethod]
        public void InterpretarRespuestaError_BadRequestDeWebApi_LlevaElMensajeTalCual()
        {
            Exception ex = PedidoVentaService.InterpretarRespuestaError("{\"Message\":\"" + MENSAJE + "\"}");

            Assert.AreEqual(MENSAJE, ex.Message);
        }

        [TestMethod]
        public void InterpretarRespuestaError_ExcepcionDeNegocio_LlevaElMensajeTalCual()
        {
            Exception ex = PedidoVentaService.InterpretarRespuestaError(
                "{\"error\":{\"code\":\"FECHA_ENTREGA_NO_VALIDA\",\"message\":\"" + MENSAJE + "\"}}");

            // HttpErrorHelper antepone el código entre corchetes, como en el resto de errores con código
            StringAssert.Contains(ex.Message, MENSAJE);
            Assert.IsNotInstanceOfType(ex, typeof(System.ComponentModel.DataAnnotations.ValidationException),
                "No es una validación que se pueda saltar: no se pregunta «¿crear sin pasar validación?»");
        }

        [TestMethod]
        public async Task Guardar_ConFechaDe1926_EnsenaElMensajeDeLaApi()
        {
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            IServicioDialogos dialogos = A.Fake<IServicioDialogos>();
            var vm = new DetallePedidoViewModel(A.Fake<IServicioNavegacion>(), A.Fake<IConfiguracion>(), servicio, new WeakReferenceMessenger(),
                dialogos, A.Fake<IAbridorPedidos>(), A.Fake<IServicioAutenticacion>());
            vm.ServicioFechaEntregaAgencia = A.Fake<IServicioFechaEntregaAgencia>();
            vm.pedido = new PedidoVentaWrapper(new PedidoVentaDTO
            {
                empresa = "1",
                numero = 928623,
                cliente = "15191",
                contacto = "0",
                Lineas = new List<LineaPedidoVentaDTO>
                {
                    new LineaPedidoVentaDTO
                    {
                        id = 1, tipoLinea = 1, Producto = "12345", Cantidad = 1, PrecioUnitario = 10M, almacen = "ALG",
                        formaVenta = "DIR", iva = "G21", estado = -1, fechaEntrega = new DateTime(1926, 10, 19)
                    }
                }
            });
            A.CallTo(() => servicio.modificarPedido(A<PedidoVentaDTO>._))
                .ThrowsAsync(PedidoVentaService.InterpretarRespuestaError("{\"Message\":\"" + MENSAJE + "\"}"));

            await vm.ModificarPedidoAsync();

            A.CallTo(() => dialogos.ShowError(A<string>.That.Contains(MENSAJE))).MustHaveHappenedOnceExactly();
        }
    }
}
