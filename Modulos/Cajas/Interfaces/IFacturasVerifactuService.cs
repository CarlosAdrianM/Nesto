using Nesto.Modulos.Cajas.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nesto.Modulos.Cajas.Interfaces
{
    /// <summary>
    /// NestoAPI#522: facturas pendientes de Verifactu para administración. Los dos lanzan con el mensaje de la API.
    /// </summary>
    public interface IFacturasVerifactuService
    {
        Task<List<FacturaPendienteVerifactuModel>> LeerFacturasPendientes();
        Task<ResultadoReintentoVerifactuModel> ReintentarFactura(string empresa, string numero);
    }
}
