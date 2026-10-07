using System.Windows.Controls;
using System.Windows.Input;

namespace Nesto.Modulos.Cliente
{
    /// <summary>NestoAPI#591: diálogo para elegir el evento de una señal (doble clic = aceptar).</summary>
    public partial class ElegirEventoDialog : UserControl
    {
        public ElegirEventoDialog()
        {
            InitializeComponent();
        }

        private void ListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is ElegirEventoDialogViewModel vm && vm.AceptarCommand.CanExecute(null))
            {
                vm.AceptarCommand.Execute(null);
            }
        }
    }
}
