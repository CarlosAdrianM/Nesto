using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ControlesUsuario.Dialogs
{
    /// <summary>
    /// Nesto#372: diálogo "Qué hay de nuevo" con el changelog de usuario.
    /// </summary>
    public partial class NovedadesDialog : UserControl
    {
        public NovedadesDialog()
        {
            InitializeComponent();
        }

        // NestoAPI#520: Ctrl+V en el cuadro de comentario pega la captura si lo copiado es una imagen;
        // si es texto, la tecla sigue su curso normal y pega el texto.
        private void CuadroComentario_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.V && Keyboard.Modifiers == ModifierKeys.Control
                && (sender as FrameworkElement)?.DataContext is NovedadItem novedad
                && novedad.PegarImagen())
            {
                e.Handled = true;
            }
        }
    }
}
