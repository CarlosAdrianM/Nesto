using Nesto.Models.Nesto.Models;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nesto.Informes
{
    public class DetalleRapportsModel
    {
        public string Usuario { get; set; }
        public string Empresa { get; set; }
        public string NombreEmpresa { get; set; }
        public string Cliente { get; set; }
        public string Direccion { get; set; }
        public string Comentarios { get; set; }
        public DateTime? HoraLlamada { get; set; }
        public short? EstadoCliente { get; set; }
        public int? AcumuladoMes { get; set; }
        public string Tipo { get; set; }
        public bool? Pedido { get; set; }
        public string Vendedor { get; set; }
        public string CodigoPostal { get; set; }
        public string Poblacion { get; set; }
        public short? EstadoRapport { get; set; }

        public static async Task<List<DetalleRapportsModel>> CargarDatos(DateTime fechaDesde, DateTime fechaHasta, string cadenaVendedores)
        {
            List<DetalleRapportsModel> lista;
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
                SqlParameter listaVendedores = new SqlParameter("@ListaVendedores", System.Data.SqlDbType.NVarChar)
                {
                    Value = cadenaVendedores
                };
                lista = await db.Database.SqlQuery<DetalleRapportsModel>("prdInformeRapportEstado9 @FechaDesde, @FechaHasta, @ListaVendedores", fechaDesdeParam, fechaHastaParam, listaVendedores).ToListAsync();
            };
            return lista;
        }
    }
}
