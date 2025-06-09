using Nesto.Infrastructure.Shared;
using Nesto.Models.Nesto.Models;
using Nesto.Modulos.CanalesExternos.Interfaces;
using Nesto.Modulos.CanalesExternos.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Nesto.Modulos.CanalesExternos.Services
{
    public class CanalesExternosPagosService : ICanalesExternosPagosService
    {
        public async Task<ObservableCollection<PagoCanalExterno>> BuscarAsientos(ObservableCollection<PagoCanalExterno> pagos)
        {
            using var db = new NestoEntities();

            foreach (var pago in pagos.Where(p => p.Estado == "Closed"))
            {
                var sql = "SELECT TOP 1 Número, Fecha, Importe, Asiento FROM ExtractoProveedor WHERE Número = @p0 AND Fecha = @p1 AND Importe = @p2 and TipoApunte = 3";
                var apunte = await db.Database.SqlQuery<ExtractoProveedor>(
                    sql,
                    Constantes.Proveedores.Especiales.PROVEEDOR_AMAZON,
                    pago.FechaPago.Date,
                    pago.Importe
                ).FirstOrDefaultAsync();

                pago.Asiento = apunte?.Asiento ?? 0;
            }
            return pagos;
        }

        private class ExtractoProveedor
        {
            public string Número { get; set; }
            public DateTime Fecha { get; set; }
            public decimal Importe { get; set; }
            public int Asiento { get; set; }
        }
    }
}
