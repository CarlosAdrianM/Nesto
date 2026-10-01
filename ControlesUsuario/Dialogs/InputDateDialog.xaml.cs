using System.Windows;
using System.Windows.Controls;

namespace ControlesUsuario.Dialogs
{
    /// <summary>NestoAPI#582: diálogo genérico para pedir una fecha (ver <see cref="InputDateDialogViewModel"/>).</summary>
    public partial class InputDateDialog : UserControl
    {
        public InputDateDialog()
        {
            InitializeComponent();
            Loaded += (_, __) => SelectorFecha.Focus();
        }
    }
}
