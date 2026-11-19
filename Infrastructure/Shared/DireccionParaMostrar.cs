namespace Nesto.Infrastructure.Shared
{
    /// <summary>
    /// NestoAPI#879 (novedad 1320): lo que se ENSEÑA de la dirección de un cliente. La API manda <c>direccionCompleta</c> (Dirección +
    /// «, » + Dirección 2, la regla vive allí); si no llega (API vieja), la <c>direccion</c> de siempre. Solo lectura: lo que se guarda
    /// sigue siendo <c>direccion</c>.
    /// </summary>
    public static class DireccionParaMostrar
    {
        public static string Elegir(string direccion, string direccionCompleta)
            => string.IsNullOrWhiteSpace(direccionCompleta) ? direccion : direccionCompleta;
    }
}
