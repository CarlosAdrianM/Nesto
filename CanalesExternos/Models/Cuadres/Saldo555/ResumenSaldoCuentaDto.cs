using System.Linq;

namespace Nesto.Modulos.CanalesExternos.Models.Cuadres.Saldo555
{
    /// <summary>
    /// Fila de la tabla resumen de la UI: combina los metadatos del marketplace
    /// (nombre, concepto Pago/Comisión) con el resultado del endpoint para esa cuenta.
    /// </summary>
    public class ResumenSaldoCuentaDto
    {
        public string Cuenta { get; set; }
        public string NombreMarket { get; set; }
        public string Concepto { get; set; }
        public SaldoCuenta555ResultadoDto Resultado { get; set; }
        public string Error { get; set; }

        public decimal Saldo => Resultado?.SaldoTotal ?? 0M;
        public int NumeroAbiertos => Resultado?.GruposAbiertos?.Count ?? 0;
        public int DiasMasAntiguo => Resultado?.GruposAbiertos == null || Resultado.GruposAbiertos.Count == 0
            ? 0
            : Resultado.GruposAbiertos.Max(g => g.DiasAntiguedad);
    }
}
