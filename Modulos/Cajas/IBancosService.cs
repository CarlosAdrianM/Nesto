using Nesto.Modulos.Cajas.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nesto.Modulos.Cajas
{
    public interface IBancosService
    {
        Task<ContenidoCuaderno43> CargarFicheroCuaderno43(string contenidoFichero);
        Task<List<MovimientoTPV>> CargarFicheroTarjetas(string contenidoFichero);
        Task<int> CrearPunteo(int? apunteBancoId, int? apunteContabilidadId, decimal importePunteo, string simboloPunteo, int? grupoPunteo = null);
        Task<List<ApunteBancarioDTO>> LeerApuntesBanco(string empresa, string codigo, DateTime fechaDesde, DateTime fechaHasta);
        Task<BancoDTO> LeerBanco(string empresa, string codigo);
        Task<BancoDTO> LeerBanco(string entidad, string oficina, string numeroCuenta);
        Task<List<MovimientoTPV>> LeerMovimientosTPV(DateTime fechaCaptura, string tipoDatafono);
        Task<int> NumeroRecibosRemesa(string remesa);
        Task<decimal> SaldoBancoFinal(string entidad, string oficina, string cuenta, DateTime fecha);
        Task<decimal> SaldoBancoInicial(string entidad, string oficina, string cuenta, DateTime fecha);
        
    }
}
