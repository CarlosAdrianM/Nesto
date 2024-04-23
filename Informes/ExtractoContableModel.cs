using Nesto.Models.Nesto.Models;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading.Tasks;

namespace Nesto.Informes
{
    public class ExtractoContableModel
    {
        public int Id { get; set; }
        public string Empresa { get; set; }
        public DateTime Fecha { get; set; }
        public string Documento { get; set; }
        public string Concepto { get; set; }
        public decimal Debe { get; set; }
        public decimal Haber { get; set; }
        public decimal Saldo { get; set; }
        public string Delegacion { get;set; }
        public string FormaVenta { get;set; }

        public static async Task<List<ExtractoContableModel>> CargarDatos(string empresa, string cuenta, DateTime fechaDesde, DateTime fechaHasta)
        {
            List<ExtractoContableModel> lista;
            using (NestoEntities db = new NestoEntities())
            {
                string consultaSQL = @"
                    SELECT 
                        [Nº Orden] Id, 
                        Empresa, 
                        Delegación Delegacion, 
                        FormaVenta, 
                        Fecha, 
                        [Nº Documento] Documento, 
                        Concepto, 
                        Debe, 
                        Haber,
                        ISNULL((
                            SELECT SUM(Debe - Haber) 
                            FROM Contabilidad 
                            WHERE [Nº Cuenta] = @Cuenta 
                            AND Fecha < @FechaDesde
                            AND Empresa = @Empresa
                        ), 0) 
                        + SUM(Debe - Haber) OVER (ORDER BY Fecha, [Nº Orden]) AS Saldo
                    FROM Contabilidad
                    WHERE [Nº Cuenta] = @Cuenta 
                    AND Fecha >= @FechaDesde AND Fecha < DATEADD(dd, 1, @FechaHasta)
                    AND Empresa = @Empresa 
                ";

                SqlParameter empresaParam = new SqlParameter("@Empresa", System.Data.SqlDbType.NVarChar)
                {
                    Value = empresa
                };
                SqlParameter cuentaParam = new SqlParameter("@Cuenta", System.Data.SqlDbType.NVarChar)
                {
                    Value = cuenta
                };
                SqlParameter fechaDesdeParam = new SqlParameter("@FechaDesde", System.Data.SqlDbType.DateTime)
                {
                    Value = fechaDesde
                };
                SqlParameter fechaHastaParam = new SqlParameter("@FechaHasta", System.Data.SqlDbType.DateTime)
                {
                    Value = fechaHasta
                };

                lista = await db.Database.SqlQuery<ExtractoContableModel>(consultaSQL, empresaParam, cuentaParam, fechaDesdeParam, fechaHastaParam).ToListAsync();
            }
            return lista;
        }
    }
}
