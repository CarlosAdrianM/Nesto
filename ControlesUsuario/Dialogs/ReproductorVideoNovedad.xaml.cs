using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace ControlesUsuario.Dialogs
{
    /// <summary>
    /// NestoAPI#711: ventana con el vídeo de una novedad: pausa/seguir, desde el principio y cerrar. Al cerrar suelta el
    /// fichero (MediaElement.Close) para que quien la abrió lo pueda borrar.
    /// </summary>
    public partial class ReproductorVideoNovedad : Window
    {
        private bool _reproduciendo;
        private bool _terminado;

        public ReproductorVideoNovedad(string rutaFichero, string titulo)
        {
            InitializeComponent();
            Title = string.IsNullOrWhiteSpace(titulo) ? "Vídeo de la novedad" : $"Vídeo: {titulo.Trim()}";
            Loaded += (s, e) =>
            {
                if (Reproductor == null)
                {
                    return;
                }
                Reproductor.Source = new Uri(rutaFichero, UriKind.Absolute);
                Reproducir();
            };
            Closed += (s, e) => SoltarFichero();
            PreviewKeyDown += AlPulsarTecla;
        }

        private void Reproducir()
        {
            if (Reproductor == null)
            {
                return;
            }
            if (_terminado)
            {
                Reproductor.Position = TimeSpan.Zero;
                _terminado = false;
            }
            Reproductor.Play();
            _reproduciendo = true;
            ActualizarBoton();
        }

        private void Pausar()
        {
            Reproductor?.Pause();
            _reproduciendo = false;
            ActualizarBoton();
        }

        private void ActualizarBoton()
        {
            if (BotonPausa == null)
            {
                return;
            }
            BotonPausa.Content = _reproduciendo ? "⏸ Pausa" : _terminado ? "▶ Ver otra vez" : "▶ Seguir";
        }

        private void BotonPausa_Click(object sender, RoutedEventArgs e)
        {
            if (_reproduciendo)
            {
                Pausar();
            }
            else
            {
                Reproducir();
            }
        }

        private void BotonPrincipio_Click(object sender, RoutedEventArgs e)
        {
            if (Reproductor == null)
            {
                return;
            }
            _terminado = false;
            Reproductor.Position = TimeSpan.Zero;
            Reproducir();
        }

        private void BotonCerrar_Click(object sender, RoutedEventArgs e) => Close();

        private void AlPulsarTecla(object sender, KeyEventArgs e)
        {
            if (e?.Key == Key.Space)
            {
                BotonPausa_Click(sender, e);
                e.Handled = true;
            }
        }

        private void Reproductor_MediaEnded(object sender, RoutedEventArgs e)
        {
            Reproductor?.Pause();
            _reproduciendo = false;
            _terminado = true;
            ActualizarBoton();
        }

        private void Reproductor_MediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            _reproduciendo = false;
            ActualizarBoton();
            if (TextoError != null)
            {
                TextoError.Text = "No se puede reproducir el vídeo en este equipo: " + (e?.ErrorException?.Message ?? "error desconocido");
                TextoError.Visibility = Visibility.Visible;
            }
        }

        private void SoltarFichero()
        {
            try
            {
                Reproductor?.Stop();
                Reproductor?.Close();
                if (Reproductor != null)
                {
                    Reproductor.Source = null;
                }
            }
            catch (Exception)
            {
                // Cerrando: si el reproductor ya no está, no hay nada que soltar
            }
        }
    }

    /// <summary>NestoAPI#711: la implementación de la aplicación: la ventana de arriba, modal sobre la ventana activa.</summary>
    public class ReproductorVideoNovedadWpf : IReproductorVideoNovedad
    {
        public void Reproducir(string rutaFichero, string titulo)
        {
            var ventana = new ReproductorVideoNovedad(rutaFichero, titulo);
            Window propietaria = Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive && w != ventana)
                ?? Application.Current?.MainWindow;
            if (propietaria != null && propietaria != ventana && propietaria.IsLoaded)
            {
                ventana.Owner = propietaria;
            }
            else
            {
                ventana.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
            _ = ventana.ShowDialog();
        }
    }
}
