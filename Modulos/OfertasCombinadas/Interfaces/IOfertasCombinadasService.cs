using Nesto.Modulos.OfertasCombinadas.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nesto.Modulos.OfertasCombinadas.Interfaces
{
    public interface IOfertasCombinadasService
    {
        Task<List<OfertaCombinadaModel>> GetOfertasCombinadas(string empresa, bool soloActivas = false);
        Task<OfertaCombinadaModel> CreateOfertaCombinada(OfertaCombinadaCreateModel oferta);
        Task<OfertaCombinadaModel> UpdateOfertaCombinada(int id, OfertaCombinadaCreateModel oferta);
        Task<OfertaCombinadaModel> DeleteOfertaCombinada(int id);

        Task<List<OfertaPermitidaFamiliaModel>> GetOfertasPermitidasFamilia(string empresa);
        Task<OfertaPermitidaFamiliaModel> CreateOfertaPermitidaFamilia(OfertaPermitidaFamiliaCreateModel oferta);
        Task<OfertaPermitidaFamiliaModel> UpdateOfertaPermitidaFamilia(int nOrden, OfertaPermitidaFamiliaCreateModel oferta);
        Task<OfertaPermitidaFamiliaModel> DeleteOfertaPermitidaFamilia(int nOrden);
    }
}
