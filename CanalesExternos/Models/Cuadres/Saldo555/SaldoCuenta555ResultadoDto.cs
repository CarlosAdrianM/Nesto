using System;
using System.Collections.Generic;

namespace Nesto.Modulos.CanalesExternos.Models.Cuadres.Saldo555
{
    public class SaldoCuenta555ResultadoDto
    {
        public string Empresa { get; set; }
        public string Cuenta { get; set; }
        public DateTime FechaCorte { get; set; }
        public decimal SaldoTotal { get; set; }
        public List<GrupoAbiertoDto> GruposAbiertos { get; set; } = new List<GrupoAbiertoDto>();
    }
}
