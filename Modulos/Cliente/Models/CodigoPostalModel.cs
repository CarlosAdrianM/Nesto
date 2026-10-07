using Newtonsoft.Json;
using System.Collections.Generic;
using CP = Nesto.Infrastructure.Shared.CodigoPostal;

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

        /// <summary>NestoAPI#596: el número en el formato canónico de la API («4480 670» → «4480-670»).</summary>
        [JsonIgnore]
        public string NumeroCanonico => CP.Normalizar(Numero, Pais);

        /// <summary>NestoAPI#596: ¿está guardado tal cual lo pide el formato canónico?</summary>
        [JsonIgnore]
        public bool EnFormatoCanonico => NumeroCanonico == Numero?.Trim();

        /// <summary>
        /// NestoAPI#596: en la tabla hay otra fila con el mismo código en el formato canónico
        /// («4430 999» y «4430-999»). Esta sobra; la fusiona el script de limpieza.
        /// </summary>
        [JsonIgnore]
        public bool DuplicadoPorFormato { get; set; }
    }

    public class VendedorGrupoProductoCodigoPostalModel
    {
        public string GrupoProducto { get; set; }
        public string Vendedor { get; set; }
    }
}
