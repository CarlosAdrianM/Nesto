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
        /// <summary>NestoAPI#392: POST api/Verifactu/DeclararSimplificada (solo Administración y Dirección, motivo obligatorio).</summary>
        Task<ResultadoReintentoVerifactuModel> DeclararSimplificada(string empresa, string numero, string motivo);
        /// <summary>
        /// «No declarar»: POST api/Verifactu/NoDeclarar (motivo obligatorio): la factura deja de enviarse a Hacienda y de salir en
        /// la lista. Con una API que aún no lo tiene, lanza «necesita la versión nueva del servidor».
        /// </summary>
        Task<ResultadoReintentoVerifactuModel> NoDeclarar(string empresa, string numero, string motivo);
    }
}
