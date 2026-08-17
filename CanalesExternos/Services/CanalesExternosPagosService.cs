using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Modulos.CanalesExternos.Interfaces;
using Nesto.Modulos.CanalesExternos.Models;
using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Nesto.Modulos.CanalesExternos.Services
{
    // Nesto#340: el asiento del pago de Amazon se resuelve por GET
    // api/ExtractoProveedores/BuscarPago (antes consulta EF directa a ExtractoProveedor).
    // Era el último acceso EF del módulo CanalesExternos junto a PagosViewModel.
    // API caída o sin coincidencia → asiento 0, el mismo comportamiento de siempre.
    public class CanalesExternosPagosService : ICanalesExternosPagosService
    {
        private readonly IServicioAutenticacion _servicioAutenticacion;
        private readonly IClienteApiFactory _clienteApiFactory;

        public CanalesExternosPagosService(IConfiguracion configuracion, IServicioAutenticacion servicioAutenticacion)
        {
            _servicioAutenticacion = servicioAutenticacion;
            _clienteApiFactory = new ClienteApiFactory(configuracion.servidorAPI, servicioAutenticacion);
        }

        public async Task<ObservableCollection<PagoCanalExterno>> BuscarAsientos(ObservableCollection<PagoCanalExterno> pagos)
        {
            using HttpClient client = _clienteApiFactory.Crear();
            if (!await _servicioAutenticacion.ConfigurarAutorizacion(client))
            {
                return pagos; // sin autorización: los asientos quedan a 0, como sin coincidencias
            }

            foreach (var pago in pagos.Where(p => p.Estado == "Closed"))
            {
                try
                {
                    string url = "ExtractoProveedores/BuscarPago" +
                        $"?proveedor={Uri.EscapeDataString(Constantes.Proveedores.Especiales.PROVEEDOR_AMAZON)}" +
                        $"&fecha={pago.FechaPago.Date:yyyy-MM-dd}" +
                        $"&importe={pago.Importe.ToString(CultureInfo.InvariantCulture)}";
                    HttpResponseMessage response = await client.GetAsync(url);
                    string body = await response.Content.ReadAsStringAsync();
                    pago.Asiento = response.IsSuccessStatusCode ? JsonConvert.DeserializeObject<int>(body) : 0;
                }
                catch (Exception)
                {
                    pago.Asiento = 0;
                }
            }
            return pagos;
        }
    }
}
