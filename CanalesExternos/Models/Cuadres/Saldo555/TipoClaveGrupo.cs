namespace Nesto.Modulos.CanalesExternos.Models.Cuadres.Saldo555
{
    /// <summary>
    /// Pasada del motor que generó el grupo abierto. Mismo orden y valores que el enum
    /// servidor (NestoAPI.Models.Informes.SaldoCuenta555.TipoClaveGrupo) para que la
    /// deserialización por valor numérico funcione.
    /// </summary>
    public enum TipoClaveGrupo
    {
        AmazonOrderId = 0,
        NumeroDocumento = 1,
        Fifo = 2,
        SinMatch = 3
    }
}
