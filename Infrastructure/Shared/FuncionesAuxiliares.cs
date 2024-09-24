namespace Nesto.Infrastructure.Shared
{
    public class FuncionesAuxiliares
    {
        // Método para truncar cadenas
        public static string Truncar(string value, int maxLength)
        {
            var nuevoValor = value.Length <= maxLength ? value : value.Substring(0, maxLength);
            return nuevoValor.Trim();
        }
    }
}
