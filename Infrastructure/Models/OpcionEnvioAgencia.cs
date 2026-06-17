namespace Nesto.Infrastructure.Models
{
    /// <summary>
    /// Resultado del comparador de agencias de NestoAPI (api/Agencias/MasEconomica): la agencia +
    /// servicio más barato para un pedido, con el coste total ya calculado (fuel incluido).
    /// </summary>
    public class OpcionEnvioAgencia
    {
        public int AgenciaId { get; set; }
        public byte ServicioId { get; set; }
        public string NombreServicio { get; set; }
        public decimal Coste { get; set; }
    }
}
