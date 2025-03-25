using Nesto.Infrastructure.Shared;
using Nesto.Modulos.Cajas.Interfaces;
using Nesto.Modulos.Cajas.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nesto.Modulos.Cajas.Bancos
{
    public class BancoCaixabank : IBancoConciliacion
    {
        private readonly IBancosService _bancoService;

        public BancoCaixabank(IBancosService bancoService)
        {
            _bancoService = bancoService;
            Banco = new BancoDTO
            {
                Empresa = Constantes.Empresas.EMPRESA_DEFECTO,
                Codigo = "5",
                Nombre = "Caixabank",
                CuentaContable = "57200013",
                Entidad = "2100",
                Oficina = "6273",
                NumeroCuenta = "0200063554"
            };
        }
        public BancoDTO Banco { get; set; }

        public string ParametroRutaFicherosMovimientos => Parametros.Claves.PathNorma43;

        public async Task<ContenidoCuaderno43> CargarFicheroMovimientos(string contenidoFichero)
        {
            return await _bancoService.CargarFicheroCuaderno43(contenidoFichero);
        }

        public async Task<List<MovimientoTPV>> CargarFicheroMovimientosTarjeta(string contenidoFichero)
        {
            return await _bancoService.CargarFicheroTarjetas(contenidoFichero);
        }

        public bool CumpleCondicionesTPV(ApunteBancarioWrapper apunteBancoSeleccionado)
        {
            if (apunteBancoSeleccionado is null)
            {
                return false;
            }
            string concepto2_0 = apunteBancoSeleccionado.RegistrosConcepto[0]?.Concepto2;
            string concepto2_1 = apunteBancoSeleccionado.RegistrosConcepto[1]?.Concepto2;

            return (concepto2_0.StartsWith("WEB") || concepto2_0.StartsWith("ON ")) &&
                   concepto2_1.StartsWith("FACTURAC.COMERCIOS");
        }
    }
}
