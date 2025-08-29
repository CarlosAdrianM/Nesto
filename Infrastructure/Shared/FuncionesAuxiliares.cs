namespace Nesto.Infrastructure.Shared
{
    public class FuncionesAuxiliares
    {
        // Método para truncar cadenas
        public static string Truncar(string value, int maxLength)
        {
            value = value.Trim();
            var nuevoValor = value.Length <= maxLength ? value : value[..maxLength];
            return nuevoValor.Trim();
        }

        public static string TruncarSinTrim(string value, int maxLength)
        {
            var nuevoValor = value.Length <= maxLength ? value : value[..maxLength];
            return nuevoValor;
        }
    }
}
