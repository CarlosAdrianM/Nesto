using ControlesUsuario.Models;
using Nesto.Modulos.Cajas.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nesto.Modulos.Cajas.Interfaces
{
    public interface IContabilidadService
    {
        Task<List<CuentaContableDTO>> LeerCuentas(string empresa, string grupo);
        Task<List<ContabilidadDTO>> LeerCuentasPorConcepto(string empresa, string concepto, DateTime fechaDesde, DateTime fechaHasta);
        Task<int> Contabilizar(List<PreContabilidadDTO> lineas);
        Task<int> Contabilizar(PreContabilidadDTO linea);
        Task<List<ContabilidadDTO>> LeerApuntesContabilidad(string cuenta, bool punteado);
        Task<List<ContabilidadDTO>> LeerApuntesContabilidad(string empresa, string cuenta, DateTime fechaDesde, DateTime fechaHasta);        
        Task<List<ContabilidadDTO>> LeerAsientoContable(string empresa, int asiento);
        Task<bool> PuntearPorImporte(string empresa, string cuenta, decimal importe);
        Task<decimal> SaldoCuenta(string empresa, string cuenta, DateTime fecha);
    }

}
