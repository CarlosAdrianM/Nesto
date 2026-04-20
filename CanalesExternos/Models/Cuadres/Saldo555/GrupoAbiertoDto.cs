using System;
using System.Collections.Generic;

namespace Nesto.Modulos.CanalesExternos.Models.Cuadres.Saldo555
{
    public class GrupoAbiertoDto
    {
        public string Clave { get; set; }
        public TipoClaveGrupo TipoClave { get; set; }
        public decimal Saldo { get; set; }
        public DateTime FechaPrimerApunte { get; set; }
        public int DiasAntiguedad { get; set; }
        public List<ApunteCuentaDto> Apuntes { get; set; } = new List<ApunteCuentaDto>();
    }
}
