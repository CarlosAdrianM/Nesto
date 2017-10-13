using Nesto.Contratos;
using Nesto.Modulos.CanalesExternos.ApisExternas;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using static Nesto.Models.PedidoVenta;
using Nesto.Models;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace Nesto.Modulos.CanalesExternos
{
    public class CanalExternoPedidosGuapalia : ICanalExternoPedidos
    {
        private IConfiguracion configuracion;

        public CanalExternoPedidosGuapalia(IConfiguracion configuracion)
        {
            this.configuracion = configuracion;
        }
        public async Task<ObservableCollection<PedidoVentaDTO>> GetAllPedidosAsync()
        {
            var servicio = new GuapaliaService();
            var listaNesto = new ObservableCollection<PedidoVentaDTO>();

            var listaPedidosEntrada = await servicio.CargarListaPedidosAsync();

            foreach (var pedidoEntrada in listaPedidosEntrada)
            {
                listaNesto.Add(TransformarPedido(pedidoEntrada));
            }

            return listaNesto;
        }

        private PedidoVentaDTO TransformarPedido(GuapaliaOrder pedidoEntrada)
        {
            PedidoVentaDTO pedidoSalida = new PedidoVentaDTO();

            pedidoSalida.empresa = "1";
            pedidoSalida.origen = "1";
            pedidoSalida.cliente = "31335"; // cambiar por el cliente de Guapalia
            pedidoSalida.contacto = "0";
            pedidoSalida.contactoCobro = "0";
            pedidoSalida.vendedor = "NV";

            pedidoSalida.iva = "G21";
            pedidoSalida.comentarios = pedidoEntrada.orderId.ToString() + " \r\n";
            pedidoSalida.comentarios += (pedidoEntrada.customerFirstName + " " + pedidoEntrada.customerLastName).ToUpper() + "\r\n";
            pedidoSalida.comentarios += pedidoEntrada.customerAddress.ToUpper() + "\r\n";
            pedidoSalida.comentarios += pedidoEntrada.customerPc.ToUpper() + " ";
            pedidoSalida.comentarios += pedidoEntrada.customerCity.ToUpper() + " (";
            pedidoSalida.comentarios += pedidoEntrada.customerState.ToUpper() + ")\r\n";
            pedidoSalida.comentarios += pedidoEntrada.customerPhone != null ? pedidoEntrada.customerPhone.ToUpper() + "\r\n" : "";
            pedidoSalida.comentarios += "TOTAL PEDIDO: " + pedidoEntrada.total.ToString();
            pedidoSalida.comentarios += pedidoEntrada.comment != null ? "\r\n" + pedidoEntrada.comment : "";


            pedidoSalida.fecha = pedidoEntrada.date;
            pedidoSalida.formaPago = "TRN";
            pedidoSalida.plazosPago = "PRE";
            pedidoSalida.ruta = "00";
            pedidoSalida.serie = "NV";
            pedidoSalida.periodoFacturacion = "NRM";

            pedidoSalida.usuario = configuracion.usuario;

            pedidoSalida.LineasPedido = TrasformarLineas(pedidoEntrada.items);

            // Añadir portes
            if (Convert.ToDecimal(pedidoEntrada.shipping) != 0)
            {
                LineaPedidoVentaDTO lineaPortes = new LineaPedidoVentaDTO
                {
                    almacen = "ALG",
                    aplicarDescuento = false,
                    cantidad = (short)1,
                    delegacion = "ALG",
                    formaVenta = "BLT",
                    estado = 1,
                    fechaEntrega = DateTime.Today,
                    iva = "G21",
                    precio = Convert.ToDecimal(pedidoEntrada.shipping) / 10000 / (decimal)1.21, //comprobar,
                    producto = "62400003",
                    texto = "GASTOS DE TRANSPORTE",
                    tipoLinea = 2, // cuenta contable
                    usuario = configuracion.usuario
                };
                pedidoSalida.LineasPedido.Add(lineaPortes);
            }

            return pedidoSalida;
        }

        private ObservableCollection<LineaPedidoVentaDTO> TrasformarLineas(List<GuapaliaOrderItem> items)
        {
            ObservableCollection<LineaPedidoVentaDTO> lineasNesto = new ObservableCollection<LineaPedidoVentaDTO>();
            foreach (var orderItem in items)
            {
                LineaPedidoVentaDTO lineaNesto = new LineaPedidoVentaDTO
                {
                    almacen = "ALG",
                    aplicarDescuento = false,
                    cantidad = (short)orderItem.quantity,
                    delegacion = "ALG",
                    formaVenta = "BLT",
                    estado = 1,
                    fechaEntrega = DateTime.Today,
                    iva = "G21", // TODO: LEER DEL PRODUCTO
                    precio = Math.Round(Convert.ToDecimal(orderItem.unitPrice) / (decimal)1.21, 4),//comprobar
                    producto = orderItem.itemId.ToString(), // comprobar
                    texto = orderItem.description.ToUpper(),
                    tipoLinea = 1, // producto
                    usuario = configuracion.usuario
                };
                lineasNesto.Add(lineaNesto);
            }

            return lineasNesto;
        }

        public PedidoVentaDTO GetPedido(int Id)
        {
            throw new NotImplementedException();
        }
    }
}
