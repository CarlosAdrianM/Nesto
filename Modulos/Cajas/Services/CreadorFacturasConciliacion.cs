using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.PedidoCompra;
using Nesto.Modulos.PedidoCompra.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nesto.Modulos.Cajas.Services
{
    /// <summary>Una factura de compra que crea una regla de conciliación (con su documento, para los avisos).</summary>
    public record FacturaConciliacion(PedidoCompraDTO Pedido, string Documento);

    public class ResultadoFacturasConciliacion
    {
        public int Creadas { get; set; }
        public int YaExistian { get; set; } // NestoAPI#384: idempotencia al reintentar
        public CrearFacturaCmpResponse? Ultima { get; set; }
        /// <summary>Nesto#536: fecha de factura que el usuario aceptó (null si no hizo falta).</summary>
        public DateTime? FechaFacturaAceptada { get; set; }
    }

    /// <summary>
    /// Crea las facturas de compra (con su pago) de una regla de conciliación con CrearFacturas.
    /// Nesto#536 / NestoAPI#668 (caso real 19/10/26, «Remesa recibos» con el IVA del 3T presentado):
    /// si las compras están cerradas en la fecha del movimiento, la API lo dice (409) con la primera
    /// fecha permitida. Se pregunta UNA vez por regla; si se acepta, esa factura y las siguientes que
    /// lo necesiten se reenvían con FechaFactura, y el pago se queda con la fecha del banco.
    /// </summary>
    public class CreadorFacturasConciliacion
    {
        private readonly IPedidoCompraService _pedidoCompraService;
        private readonly IServicioDialogos _dialogos;

        public CreadorFacturasConciliacion(IPedidoCompraService pedidoCompraService, IServicioDialogos dialogos)
        {
            _pedidoCompraService = pedidoCompraService;
            _dialogos = dialogos;
        }

        public async Task<ResultadoFacturasConciliacion> CrearFacturas(IEnumerable<FacturaConciliacion> facturas, string contrapartidaPago, string documento)
        {
            var resultado = new ResultadoFacturasConciliacion();
            foreach (FacturaConciliacion factura in facturas)
            {
                var request = new CrearFacturaCmpRequest
                {
                    Pedido = factura.Pedido,
                    CrearPago = true,
                    ContraPartidaPago = contrapartidaPago,
                    Documento = documento
                };

                CrearFacturaCmpResponse response;
                try
                {
                    response = await _pedidoCompraService.CrearAlbaranYFactura(request);
                }
                catch (FechaCompraNoPermitidaException ex)
                {
                    // Solo se pregunta si la fecha propuesta es nueva (con varias facturas, una sola vez)
                    if (resultado.FechaFacturaAceptada != ex.FechaPropuesta)
                    {
                        if (!_dialogos.ShowConfirmationAnswer("Compras cerradas", Pregunta(ex)))
                        {
                            throw new Exception($"No se ha contabilizado la factura del documento {factura.Documento}: " +
                                $"las compras están cerradas el {ex.Fecha:dd/MM/yyyy}." + TextoAnteriores(resultado));
                        }
                        resultado.FechaFacturaAceptada = ex.FechaPropuesta;
                    }
                    request.FechaFactura = ex.FechaPropuesta;
                    response = await _pedidoCompraService.CrearAlbaranYFactura(request);
                }

                if (!response.Exito)
                {
                    // NestoAPI#384: el servidor es idempotente: al volver a pulsar el
                    // botón, las ya contabilizadas se saltan y solo se crean las que faltan.
                    throw new Exception($"No se ha podido crear la factura del documento {factura.Documento}. " +
                        "Las anteriores quedaron contabilizadas: vuelva a pulsar el botón y se crearán " +
                        "solo las que faltan (no se duplican).");
                }
                if (response.YaExistia)
                {
                    resultado.YaExistian++;
                }
                else
                {
                    resultado.Creadas++;
                }
                resultado.Ultima = response;
            }
            return resultado;
        }

        internal static string Pregunta(FechaCompraNoPermitidaException ex)
        {
            return $"Las compras están cerradas hasta el {ex.FechaPropuesta.AddDays(-1):dd/MM/yyyy}.\n\n" +
                $"¿Desea contabilizar la factura con fecha {ex.FechaPropuesta:dd/MM/yyyy}? " +
                $"El pago se queda con la fecha del banco ({ex.Fecha:dd/MM/yyyy}).";
        }

        private static string TextoAnteriores(ResultadoFacturasConciliacion resultado)
        {
            int anteriores = resultado.Creadas + resultado.YaExistian;
            return anteriores switch
            {
                0 => " No se ha contabilizado nada.",
                1 => " La anterior quedó contabilizada: al volver a pulsar el botón no se duplica.",
                _ => $" Las {anteriores} anteriores quedaron contabilizadas: al volver a pulsar el botón no se duplican."
            };
        }
    }
}
