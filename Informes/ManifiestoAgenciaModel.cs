using Nesto.Models.Nesto.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nesto.Informes
{
    public class ManifiestoAgenciaModel
    {
        public string Cliente { get; set; }
        public string Contacto { get; set; }
        public string Nombre { get; set; }
        public string Direccion { get; set; }
        public string CodigoPostal { get; set; }
        public string Poblacion { get; set; }
        public string Provincia { get; set; }
        public short Bultos { get; set; }
        public decimal Reembolso { get; set; }
        public string TelefonoFijo { get; set; }
        public string TelefonoMovil { get; set; }
        public string Observaciones { get; set; }


        public static async Task<List<ManifiestoAgenciaModel>> CargarDatos(string empresa, int agencia, DateTime fecha)
        {
            List<ManifiestoAgenciaModel> lista;
            using (NestoEntities db = new NestoEntities())
            {
                string consulta = "select rtrim(Cliente) Cliente, rtrim(Contacto) Contacto, rtrim(Nombre) Nombre, rtrim(Direccion) Direccion, rtrim(CodPostal) CodigoPostal, rtrim(Poblacion) Poblacion, rtrim(Provincia) Provincia, Bultos, Reembolso, rtrim(Telefono) TelefonoFijo, rtrim(Movil) TelefonoMovil, rtrim(Observaciones) Observaciones " +
                    "from enviosagencia e " +
                    "where e.empresa = '" + empresa + "' and e.estado = 1 and e.agencia = " + agencia.ToString() + "  and Fecha = '" + fecha.ToString("dd/MM/yy") + "' " +
                    "group by nombre, Direccion, CodPostal, Poblacion, Provincia, Telefono,  observaciones, bultos, reembolso,cliente, contacto, Movil";
                lista = await db.Database.SqlQuery<ManifiestoAgenciaModel>(consulta).ToListAsync();
            };
            return lista;
        }
    }
}
