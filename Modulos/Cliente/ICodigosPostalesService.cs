using Nesto.Modulos.Cliente.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nesto.Modulos.Cliente
{
    /// <summary>Nesto#442: mantenimiento de códigos postales por API (NestoAPI#378).</summary>
    public interface ICodigosPostalesService
    {
        Task<List<CodigoPostalModel>> Buscar(string filtro, string empresa = null);
        Task<CodigoPostalModel> Guardar(CodigoPostalModel codigoPostal);
    }
}
