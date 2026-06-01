namespace Nesto.Infrastructure.Services.ServirJunto
{
    /// <summary>
    /// Línea de producto con los datos que el servidor necesita para calcular la base de portes
    /// que quedaría al desmarcar "servir junto" (NestoAPI#211 / Nesto#365).
    /// </summary>
    public class LineaPortesServirJuntoDTO
    {
        public string ProductoId { get; set; }
        public string Almacen { get; set; }
        public short Estado { get; set; }
        public int Cantidad { get; set; }
        public decimal BaseImponible { get; set; }
    }
}
