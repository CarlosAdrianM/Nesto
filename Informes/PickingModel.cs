using Nesto.Models.Nesto.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nesto.Informes
{
    public class PickingModel
    {
        public string Proveedor { get; set; }
        public string Producto { get; set; }
        public string CodigoBarras { get; set; }
        public string Descripcion { get; set; }
        public short? Tamanno { get; set; }
        public string UnidadMedida { get; set; }
        public string Subgrupo { get; set; }
        public int Cantidad { get; set; }
        public int CantidadCajas { get; set; }
        public string Pasillo { get; set; }
        public string Fila { get; set; }
        public string Columna { get; set; }
        public short? Estado { get; set; }

        public static async Task<List<PickingModel>> CargarDatos(int numeroPicking)
        {
            List<PickingModel> lista;
            using (NestoEntities db = new NestoEntities())
            {
                try
                {
                    var lista2 = await db.Database.SqlQuery<PickingAntiguoModel>("prdInformePickingAgrupado @empresa, @picking, @personas",
                    new SqlParameter("empresa", "1"),
                    new SqlParameter("picking", numeroPicking),
                    new SqlParameter("personas", 1)
                    ).ToListAsync(); ;
                    lista = lista2.Select(p => new PickingModel
                    {
                        Proveedor = p.ProveedorProducto?.Trim(),
                        Producto = p.NºProducto?.Trim(),
                        CodigoBarras = p.CodBarras?.Trim(),
                        Descripcion = p.Descripcion?.Trim(),
                        Tamanno = p.Tamaño,
                        UnidadMedida = p.UnidadMedida?.Trim(),
                        Subgrupo = p.NombreSubGrupo?.Trim(),
                        Cantidad = p.Cantidad,
                        CantidadCajas = p.CantidadCajas,
                        Pasillo = p.Pasillo,
                        Fila = p.Fila,
                        Columna = p.Columna
                    }).ToList();
                } catch (Exception e)
                {
                    throw;
                }
            };
            return lista;
        }


        public static async Task<int> UltimoPicking()
        {
            using (NestoEntities db = new NestoEntities())
            {
                return (int)db.LinPedidoVta.Max(p => p.Picking);
            }
        }

        // Necesitamos esta clase porque Database.SqlQuery no admite mapping de campos
        // en cuanto esté permitido, hay que eliminar esta clase
        private class PickingAntiguoModel
        {
            public string ProveedorProducto { get; set; }
            public string NºProducto { get; set; }
            public string CodBarras { get; set; }
            public string Descripcion { get; set; }
            public short? Tamaño { get; set; }
            public string UnidadMedida { get; set; }
            public string NombreSubGrupo { get; set; }
            public int Cantidad { get; set; }
            public int CantidadCajas { get; set; }
            public string Pasillo { get; set; }
            public string Fila { get; set; }
            public string Columna { get; set; }
        }
    }
}
