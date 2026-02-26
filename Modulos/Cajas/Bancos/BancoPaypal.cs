using Nesto.Infrastructure.Shared;
using Nesto.Modulos.Cajas.Interfaces;
using Nesto.Modulos.Cajas.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Nesto.Modulos.Cajas.Bancos
{
    public class BancoPaypal : IBancoConciliacion
    {
        private readonly IBancosService _bancoService;
        private readonly List<IConvertidorFormatoBancario> _convertidores;

        public BancoPaypal(IBancosService bancosService)
        {
            Banco = new BancoDTO
            {
                Empresa = Constantes.Empresas.EMPRESA_DEFECTO,
                Codigo = "7",
                Nombre = "Paypal",
                Entidad = "PYPL",
                Oficina = "F55F",
                NumeroCuenta = "2LRZ5CMS8",
                CuentaContable = "57200020"
            };
            _bancoService = bancosService;
            _convertidores =
            [
                new ConvertidorPaypalNuevo(),
                new ConvertidorPaypalAntiguo()
            ];
        }
        public BancoDTO Banco { get; set; }
        public string ParametroRutaFicherosMovimientos => "PathMovimientosPaypal";

        public async Task<ContenidoCuaderno43> CargarFicheroMovimientos(string contenidoFichero)
        {
            var convertidor = _convertidores.FirstOrDefault(c => c.PuedeConvertir(contenidoFichero))
                ?? throw new Exception("Formato de fichero PayPal no reconocido");
            ContenidoCuaderno43 cuaderno43 = convertidor.Convertir(contenidoFichero);
            string contenidoFicheroEnCuaderno43 = cuaderno43.ToString();
            return await _bancoService.CargarFicheroCuaderno43(contenidoFicheroEnCuaderno43);
        }

        public Task<List<MovimientoTPV>> CargarFicheroMovimientosTarjeta(string contenidoFichero)
        {
            throw new NotImplementedException();
        }

        public bool CumpleCondicionesTPV(ApunteBancarioWrapper apunteBancoSeleccionado)
        {
            return false;
        }
    }
}
