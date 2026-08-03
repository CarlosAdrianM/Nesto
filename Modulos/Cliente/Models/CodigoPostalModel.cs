using System.Collections.Generic;

namespace Nesto.Modulos.Cliente.Models
{
    /// <summary>
    /// Nesto#442: fila del mantenimiento de códigos postales (espejo del
    /// CodigoPostalMantenimientoDTO de NestoAPI#378).
    /// </summary>
    public class CodigoPostalModel
    {
        public string Empresa { get; set; }
        public string Numero { get; set; }
        public string Poblacion { get; set; }
        public string Provincia { get; set; }
        public string Ruta { get; set; }
        public string Vendedor { get; set; }
        public string Pais { get; set; }
        public List<VendedorGrupoProductoCodigoPostalModel> VendedoresGrupoProducto { get; set; } = new();
    }

    public class VendedorGrupoProductoCodigoPostalModel
    {
        public string GrupoProducto { get; set; }
        public string Vendedor { get; set; }
    }
}
