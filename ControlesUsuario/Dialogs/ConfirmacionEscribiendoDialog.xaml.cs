using System.Windows;
using System.Windows.Controls;

namespace ControlesUsuario.Dialogs
{
    /// <summary>Confirmación que obliga a escribir lo que se pide (ver <see cref="ConfirmacionEscribiendoDialogViewModel"/>).</summary>
    public partial class ConfirmacionEscribiendoDialog : UserControl
    {
        public ConfirmacionEscribiendoDialog()
        {
            InitializeComponent();
            Loaded += AlCargar;
            Unloaded += AlDescargar;
        }

        private void AlCargar(object sender, RoutedEventArgs e)
        {
            TextoConfirmacion?.Focus();
        }

        private void AlDescargar(object sender, RoutedEventArgs e)
        {
            Loaded -= AlCargar;
            Unloaded -= AlDescargar;
        }
    }
}
