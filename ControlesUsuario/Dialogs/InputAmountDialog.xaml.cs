using System.Text.RegularExpressions;
using System.Windows.Controls;
using System.Windows.Input;

namespace ControlesUsuario.Dialogs
{
    public partial class InputAmountDialog : UserControl
    {
        public InputAmountDialog()
        {
            InitializeComponent();
        }

        private void TextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Solo permite números y coma/punto decimal
            Regex regex = new Regex(@"^[0-9,\.]+$");
            e.Handled = !regex.IsMatch(e.Text);
        }
    }
}