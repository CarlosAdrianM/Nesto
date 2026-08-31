using Nesto.Modulos.OfertasCombinadas.Models;
using System;
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
        // NestoAPI#289: subgrupos para el combo de las filas de filtro.
        Task<List<SubgrupoComboModel>> GetSubgrupos();

        Task<List<OfertaEscalonadaModel>> GetOfertasEscalonadas(string empresa, bool soloActivas = false);
        Task<OfertaEscalonadaModel> CreateOfertaEscalonada(OfertaEscalonadaCreateModel oferta);
        Task<OfertaEscalonadaModel> UpdateOfertaEscalonada(int id, OfertaEscalonadaCreateModel oferta);
        Task<OfertaEscalonadaModel> DeleteOfertaEscalonada(int id);

        // NestoAPI#423: campañas comerciales (descuentos de tarifa con fechas y audiencia).
        Task<List<CampanaModel>> GetCampanas(bool incluirCaducadas = false, bool soloCampanas = false);
        Task<CampanaModel> CreateCampana(CampanaModel campana);
        Task<CampanaModel> UpdateCampana(int id, CampanaModel campana);
        Task DeleteCampana(int id);
        Task<List<ResumenCampanaModel>> GetNombresDeCampana();
        Task<ResultadoOperacionCampanaModel> CerrarCampana(string nombre, DateTime? fechaFin = null);
        Task<ResultadoOperacionCampanaModel> DeleteCampanaPorNombre(string nombre);

        // Ofertas "6+2" de un producto concreto (las generales; las de un cliente van en su ficha).
        Task<List<OfertaProductoModel>> GetOfertasProducto(bool incluirCaducadas = false);
        Task<OfertaProductoModel> CreateOfertaProducto(OfertaProductoModel oferta);
        Task<OfertaProductoModel> UpdateOfertaProducto(int nOrden, OfertaProductoModel oferta);
        Task DeleteOfertaProducto(int nOrden);

        Task<List<OfertaPermitidaFamiliaModel>> GetOfertasPermitidasFamilia(string empresa);
        Task<OfertaPermitidaFamiliaModel> CreateOfertaPermitidaFamilia(OfertaPermitidaFamiliaCreateModel oferta);
        Task<OfertaPermitidaFamiliaModel> UpdateOfertaPermitidaFamilia(int nOrden, OfertaPermitidaFamiliaCreateModel oferta);
        Task<OfertaPermitidaFamiliaModel> DeleteOfertaPermitidaFamilia(int nOrden);
    }
}
