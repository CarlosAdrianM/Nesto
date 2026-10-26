using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace Nesto.Modules.Producto
{
    /// <summary>Guardar en la carpeta temporal un PDF que manda la API y abrirlo con el visor del sistema.</summary>
    internal static class AbridorFicheros
    {
        internal static void AbrirConElVisorDelSistema(string ruta)
            => _ = Process.Start(new ProcessStartInfo(ruta) { UseShellExecute = true });

        /// <summary>
        /// Lo guarda con la hora en el nombre (si el anterior sigue abierto en el visor, no se puede sobrescribir) y lo abre.
        /// Devuelve null si ha ido bien o el texto del error para enseñarlo.
        /// </summary>
        internal static string GuardarYAbrir(byte[] pdf, string nombre, Action<string> abrir)
            => GuardarYAbrir(pdf, nombre, "pdf", "el PDF", abrir);

        /// <summary>Igual que el del PDF con otra extensión (sugerencia 648: el Excel de lo facturado en tienda).</summary>
        internal static string GuardarYAbrir(byte[] contenido, string nombre, string extension, string queEs, Action<string> abrir)
        {
            try
            {
                string ruta = Path.Combine(Path.GetTempPath(), $"{nombre}_{DateTime.Now:yyyyMMdd_HHmmss}.{extension}");
                File.WriteAllBytes(ruta, contenido ?? Array.Empty<byte>());
                abrir(ruta);
                return null;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is Win32Exception || ex is InvalidOperationException)
            {
                return $"No se ha podido abrir {queEs}: {ex.Message}";
            }
        }
    }
}
