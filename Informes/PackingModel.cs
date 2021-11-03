using Nesto.Models.Nesto.Models;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Nesto.Informes
{
    public class PackingModel
    {
        public int Número { get; set; }
        public string Ampliacion { get; set; }
        public string Aviso { get; set; }
        public string NºCliente { get; set; }
        public string Contacto { get; set; }
        public string Direccion { get; set; }
        public string CodPostal { get; set; }
        public string Poblacion { get; set; }
        public string Telefono { get; set; }
        public string ComentarioPicking { get; set; }
        public string Usuario { get; set; }
        public string Ruta { get; set; }


        // Líneas
        public string ProveedorProducto { get; set; }
        public string NºProducto { get; set; }
        public string CodBarras { get; set; }
        public string NombreSubGrupo { get; set; }
        public string Descripcion { get; set; }
        public short? Tamaño { get; set; }
        public string UnidadMedida { get; set; }
        public int Cantidad { get; set; }
        public int CantidadCajas { get; set; }
        public short? Estado { get; set; }
        public string Pasillo { get; set; }
        public string Fila { get; set; }
        public string Columna { get; set; }
        public static async Task<List<PackingModel>> CargarDatos(int numeroPicking)
        {
            List<PackingModel> lista = new List<PackingModel>();
            using (NestoEntities db = new NestoEntities())
            {
                try
                {
                    lista = await db.Database.SqlQuery<PackingModel>("prdInformePicking @Picking, @Personas",
                    new SqlParameter("picking", numeroPicking),
                    new SqlParameter("personas", 1)
                    ).ToListAsync();
                    foreach (var item in lista)
                    {
                        item.ProveedorProducto = item.ProveedorProducto?.Trim();
                        item.NºProducto = item.NºProducto?.Trim();
                        item.CodBarras = item.CodBarras?.Trim();
                        item.UnidadMedida = item.UnidadMedida?.Trim();
                        item.Ampliacion = item.Ampliacion?.Trim();
                        item.Aviso = item.Aviso?.Trim();
                        item.ComentarioPicking = item.ComentarioPicking?.Trim();
                        item.CodPostal = item.CodPostal?.Trim();
                        item.Descripcion = item.Descripcion?.Trim();
                        item.Direccion = item.Direccion?.Trim();
                        item.NombreSubGrupo = item.NombreSubGrupo?.Trim();
                        item.NºCliente = item.NºCliente?.Trim();
                        item.Poblacion = item.Poblacion?.Trim();
                        item.Telefono = item.Telefono?.Trim();
                    }
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
