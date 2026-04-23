using Nesto.Infrastructure.Contracts;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Nesto.Infrastructure.Services.ServirJunto
{
    /// <summary>
    /// Implementación HTTP del servicio de validación de "Servir junto".
    /// Llama a POST api/PedidosVenta/ValidarServirJunto (NestoAPI#161). Si el servidor
    /// responde con error, devuelve PuedeDesmarcar=true (fail-safe: no bloqueamos al
    /// vendedor por un fallo de red).
    /// </summary>
    public class ServirJuntoService : IServirJuntoService
    {
        private readonly IConfiguracion _configuracion;
        private readonly IServicioAutenticacion _servicioAutenticacion;

        public ServirJuntoService(IConfiguracion configuracion, IServicioAutenticacion servicioAutenticacion)
        {
            _configuracion = configuracion;
            _servicioAutenticacion = servicioAutenticacion;
        }

        public async Task<ValidarServirJuntoResponse> Validar(
            string almacen,
            List<ProductoBonificadoConCantidadRequest> productosBonificados,
            List<ProductoBonificadoConCantidadRequest> lineasPedido,
            string formaPago = null,
            string plazosPago = null,
            string ccc = null,
            string periodoFacturacion = null,
            bool? notaEntrega = null)
        {
            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(_configuracion.servidorAPI);
                try
                {
                    if (!await _servicioAutenticacion.ConfigurarAutorizacion(client).ConfigureAwait(false))
                    {
                        return PuedeDesmarcar();
                    }

                    var request = new ValidarServirJuntoRequest
                    {
                        Almacen = almacen,
                        ProductosBonificadosConCantidad = productosBonificados,
                        LineasPedido = lineasPedido,
                        FormaPago = formaPago,
                        PlazosPago = plazosPago,
                        CCC = ccc,
                        PeriodoFacturacion = periodoFacturacion,
                        NotaEntrega = notaEntrega
                    };

                    var content = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");
                    var response = await client.PostAsync("PedidosVenta/ValidarServirJunto", content).ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode)
                    {
                        return PuedeDesmarcar();
                    }

                    string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    return JsonConvert.DeserializeObject<ValidarServirJuntoResponse>(body);
                }
                catch
                {
                    return PuedeDesmarcar();
                }
            }
        }

        private static ValidarServirJuntoResponse PuedeDesmarcar() =>
            new ValidarServirJuntoResponse
            {
                PuedeDesmarcar = true,
                ProductosProblematicos = new List<ProductoSinStockDTO>(),
                Mensaje = null
            };
    }
}
