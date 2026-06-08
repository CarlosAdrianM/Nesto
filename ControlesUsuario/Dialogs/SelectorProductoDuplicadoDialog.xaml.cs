using System.Windows.Controls;
using System.Windows.Input;

namespace ControlesUsuario.Dialogs
{
    /// <summary>
    /// Interaction logic for SelectorProductoDuplicadoDialog. Nesto#368.
    /// </summary>
    public partial class SelectorProductoDuplicadoDialog : UserControl
    {
        public SelectorProductoDuplicadoDialog()
        {
            InitializeComponent();
        }

        private void ListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is SelectorProductoDuplicadoDialogViewModel vm &&
                vm.AceptarCommand.CanExecute())
            {
                vm.AceptarCommand.Execute();
            }
        }
    }
}
