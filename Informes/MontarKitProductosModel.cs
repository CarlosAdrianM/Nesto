using Nesto.Models.Nesto.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nesto.Informes
{
    public class MontarKitProductosModel
    {
        public string Producto { get; set; }
        public string Nombre { get; set; }
        public int Tamanno { get; set; }
        public string UnidadMedida { get; set; }
        public string Familia { get; set; }
        public int Cantidad { get; set; }
        public string Pasillo { get; set; }
        public string Fila { get; set; }
        public string Columna { get; set; }

        public static async Task<List<MontarKitProductosModel>> CargarDatos(int traspaso)
        {
            List<MontarKitProductosModel> lista;
            using (NestoEntities db = new NestoEntities())
            {
                string consulta = $@"select rtrim(p.Número) Producto, rtrim(p.Nombre) Nombre, p.Tamaño, rtrim(p.UnidadMedida) UnidadMedida, rtrim(p.familia) Familia, Cantidad, rtrim(Pasillo) Pasillo, rtrim(Fila) Fila, rtrim(Columna) Columna
                                    from Ubicaciones u inner join Productos p
                                    on u.Empresa in ('1', '3') and p.Empresa = '1' and u.Número = p.Número
                                    where nºtraspaso = {traspaso} and u.Estado = -102 
                                    order by [nºorden]";
                lista = await db.Database.SqlQuery<MontarKitProductosModel>(consulta).ToListAsync();
            };
            return lista;
        }
    }
}
