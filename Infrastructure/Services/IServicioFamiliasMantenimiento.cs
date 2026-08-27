using Nesto.Infrastructure.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nesto.Infrastructure.Services
{
    /// <summary>
    /// Cliente de api/Familias (NestoAPI#406). Solo leer y marcar/desmarcar
    /// "público igual que profesional": no hay alta ni baja de familias, y los campos de
    /// comisiones no se tocan desde aquí.
    /// </summary>
    public interface IServicioFamiliasMantenimiento
    {
        Task<List<FamiliaMantenimiento>> LeerFamilias(string empresa);
        Task GuardarFamilia(FamiliaMantenimiento familia);
    }
}
