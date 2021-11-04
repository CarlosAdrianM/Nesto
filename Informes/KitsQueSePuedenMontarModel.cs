using Nesto.Models.Nesto.Models;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nesto.Informes
{
    public class KitsQueSePuedenMontarModel
    {
        const string FILTRO_RUTAS_DEFECTO = "(ruta='AT ' or ruta='OT ' or ruta='16 ' or ruta='FW ' or ruta='00 ')";
        public string Tipo { get; set; }
        public string Kit { get; set; }
        public string Nombre { get; set; }
        public int CantidadAMontar { get; set; }

        public static async Task<List<KitsQueSePuedenMontarModel>> CargarDatos()
        {
            List<KitsQueSePuedenMontarModel> lista;
            using (NestoEntities db = new NestoEntities())
            {
                try
                {
                    lista = await db.Database.SqlQuery<KitsQueSePuedenMontarModel>("prdInformeKitsQueSePuedenMontar @Empresa, @Fecha, @Almacen, @FiltroRutas",
                    new SqlParameter("Empresa", "1"),
                    new SqlParameter("Fecha", DateTime.Today.AddDays(4).ToString("dd/MM/yy")),
                    new SqlParameter("Almacen", "ALG"),
                    new SqlParameter("FiltroRutas", FILTRO_RUTAS_DEFECTO)
                    ).ToListAsync();
                    lista = lista.Select(p => new KitsQueSePuedenMontarModel
                    {
                        Tipo = p.Tipo?.Trim(),
                        Kit = p.Kit?.Trim(),
                        Nombre = p.Nombre?.Trim(),
                        CantidadAMontar = p.CantidadAMontar
                    }).ToList();
                }
                catch (Exception e)
                {
                    throw;
                }
            };
            return lista;
        }
    }
}
