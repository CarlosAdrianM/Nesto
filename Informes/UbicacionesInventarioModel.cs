using Nesto.Models.Nesto.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nesto.Informes
{
    public class UbicacionesInventarioModel
    {
        public string Pasillo { get; set; }
        public string Fila { get; set; }
        public string Columna { get; set; }
        public string Producto { get; set; }
        public string CodigoBarras { get; set; }
        public string Nombre { get; set; }
        public short? Tamanno { get; set; }
        public string UnidadMedida { get; set; }
        public string Familia { get; set; }

        public static async Task<List<UbicacionesInventarioModel>> CargarDatos()
        {
            List<UbicacionesInventarioModel> lista;
            using (NestoEntities db = new NestoEntities())
            {
                string consulta = "select Pasillo, Fila, Columna, rtrim(p.número) Producto, p.codbarras CodigoBarras, rtrim(p.nombre) Nombre, p.tamaño Tamanno, p.UnidadMedida, p.Familia " +
                      "from ubicaciones as u inner join productos as p " +
                      "on p.empresa = '1' and u.numero = p.numero " +
                      "where u.estado = 0 " +
                      "group by pasillo, fila, columna, p.número, p.codbarras, p.nombre, p.tamaño, p.unidadmedida, p.familia " +
                      "order by pasillo, columna, fila, p.número, p.codbarras, p.nombre, p.tamaño, p.unidadmedida, p.familia";
                lista = await db.Database.SqlQuery<UbicacionesInventarioModel>(consulta).ToListAsync();
            };
            return lista;
        }
    }
}
