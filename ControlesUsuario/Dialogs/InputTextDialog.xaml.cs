using System.Windows;
using System.Windows.Controls;

namespace ControlesUsuario.Dialogs
{
    public partial class InputTextDialog : UserControl
    {
        public InputTextDialog()
        {
            InitializeComponent();
            Loaded += InputTextDialog_Loaded;
        }

        private void InputTextDialog_Loaded(object sender, RoutedEventArgs e)
        {
            InputTextBox.Focus();
            InputTextBox.SelectAll();
        }
    }
}
