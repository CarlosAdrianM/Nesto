using System.Windows.Controls;
using System.Windows.Input;
using Xceed.Wpf.Toolkit;

namespace Nesto.Modulos.Cliente
{
    /// <summary>Nesto#618 (NestoAPI#786): diálogo «Nueva nota de cobro».</summary>
    public partial class NotaCobroDialog : UserControl
    {
        public NotaCobroDialog()
        {
            InitializeComponent();
        }

        /// <summary>El punto del teclado numérico escribe la coma decimal (como en Cajas).</summary>
        private void Importe_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (e.Text != "." || sender is not DecimalUpDown control)
            {
                return;
            }
            if (control.Template?.FindName("PART_TextBox", control) is TextBox textBox)
            {
                e.Handled = true;
                int caretIndex = textBox.CaretIndex;
                textBox.Text = textBox.Text.Insert(caretIndex, ",");
                textBox.CaretIndex = caretIndex + 1;
            }
        }
    }
}
