using Nesto.Models.Nesto.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nesto.Informes
{
    public class ControlPedidosModel
    {
        public int Pedido { get; set; }
        public string Producto { get; set; }
        public string Ruta { get; set; }
        public string Cliente { get; set; }
        public string Vendedor { get; set; }
        public string Nombre { get; set; }
        public string Familia { get; set; }
        public int CantidadPedido { get; set; }
        public int CantidadTotal { get; set; }

        public static async Task<List<ControlPedidosModel>> CargarDatos()
        {
            List<ControlPedidosModel> lista;
            using (NestoEntities db = new NestoEntities())
            {
                lista = await db.Database.SqlQuery<ControlPedidosModel>("prdInformeControlPedidos").ToListAsync();
            };
            return lista;
        }

    }
}
