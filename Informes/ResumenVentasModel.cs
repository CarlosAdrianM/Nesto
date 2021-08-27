using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Nesto.Models.Nesto.Models;

namespace Nesto.Informes
{
    public class ResumenVentasModel
    {
        public string Grupo { get; set; }
        public string Vendedor { get; set; }
        public string NombreVendedor { get; set; }
        public decimal VtaNV { get; set; }
        public decimal VtaCV { get; set; }
        public decimal VtaVC { get; set; }
        public decimal VtaUL { get; set; }
        public decimal VtaTotal { get; set; }

        public static async Task<List<ResumenVentasModel>> CargarDatos(DateTime fechaDesde, DateTime fechaHasta, bool soloFacturas)
        {
            List<ResumenVentasModel> lista;
            using (NestoEntities db = new NestoEntities())
            {
                SqlParameter fechaDesdeParam = new SqlParameter("@FechaDesde", System.Data.SqlDbType.DateTime)
                {
                    Value = fechaDesde
                };
                SqlParameter fechaHastaParam = new SqlParameter("@FechaHasta", System.Data.SqlDbType.DateTime)
                {
                    Value = fechaHasta
                };
                SqlParameter soloFacturasParam = new SqlParameter("@soloFacturas", System.Data.SqlDbType.Bit)
                {
                    Value = soloFacturas
                };
                lista = await db.Database.SqlQuery<ResumenVentasModel>("prdInformeResumenVentas @FechaDesde, @FechaHasta, @soloFacturas", fechaDesdeParam, fechaHastaParam, soloFacturasParam).ToListAsync();
            };
            return lista;
        }
    }
}
