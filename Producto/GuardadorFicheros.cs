using Microsoft.Win32;
using System.IO;

namespace Nesto.Modules.Producto
{
    /// <summary>
    /// Nesto#559: guardar un fichero que manda la API donde diga el usuario (el diálogo de «Guardar como»), detrás de una
    /// interfaz para probar los ViewModels sin abrir nada. A propósito NO abre el fichero: Nesto corre en el escritorio
    /// remoto, donde Office no tiene licencia; el usuario lo guarda en su carpeta (o en la unidad de su ordenador) y lo abre allí.
    /// </summary>
    public interface IGuardadorFicheros
    {
        /// <summary>La ruta elegida (con el nombre propuesto), o null si el usuario cancela.</summary>
        string ElegirDondeGuardar(string titulo, string nombrePropuesto, string filtro, string extension);

        void Guardar(string ruta, byte[] contenido);
    }

    public sealed class GuardadorFicherosSistema : IGuardadorFicheros
    {
        public string ElegirDondeGuardar(string titulo, string nombrePropuesto, string filtro, string extension)
        {
            var dialogo = new SaveFileDialog
            {
                Title = titulo,
                FileName = nombrePropuesto,
                DefaultExt = extension,
                Filter = filtro,
                AddExtension = true,
                OverwritePrompt = true
            };
            return dialogo.ShowDialog() == true ? dialogo.FileName : null;
        }

        public void Guardar(string ruta, byte[] contenido) => File.WriteAllBytes(ruta, contenido ?? System.Array.Empty<byte>());
    }
}
