using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace ControlesUsuario.BultosPedido
{
    /// <summary>
    /// Nesto#522: lo que la lista de bultos necesita del sistema (diálogo de guardar, disco, visor, portapapeles),
    /// detrás de una interfaz para poder probar el ViewModel sin abrir nada.
    /// </summary>
    internal interface IAccionesFotoBulto
    {
        string CarpetaTemporal { get; }

        /// <summary>La ruta elegida en el diálogo de guardar (con el nombre propuesto), o null si se cancela.</summary>
        string ElegirDondeGuardar(string nombrePropuesto);

        void Guardar(string ruta, byte[] datos);

        /// <summary>Abre el fichero con el programa del sistema (el visor de fotos).</summary>
        void Abrir(string ruta);

        void CopiarAlPortapapeles(string texto);
    }

    internal sealed class AccionesFotoBultoSistema : IAccionesFotoBulto
    {
        public string CarpetaTemporal => Path.GetTempPath();

        public string ElegirDondeGuardar(string nombrePropuesto)
        {
            var dialogo = new SaveFileDialog
            {
                Title = "Guardar la foto del bulto",
                FileName = nombrePropuesto,
                DefaultExt = ".jpg",
                Filter = "Imagen JPG (*.jpg)|*.jpg",
                AddExtension = true,
                OverwritePrompt = true
            };
            return dialogo.ShowDialog() == true ? dialogo.FileName : null;
        }

        public void Guardar(string ruta, byte[] datos) => File.WriteAllBytes(ruta, datos);

        public void Abrir(string ruta) => _ = Process.Start(new ProcessStartInfo(ruta) { UseShellExecute = true });

        public void CopiarAlPortapapeles(string texto) => Clipboard.SetText(texto);
    }
}
