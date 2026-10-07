using System.Windows.Controls;

namespace Nesto.Modulos.Cliente
{
    /// <summary>NestoAPI#591: mantenimiento de eventos con señal reembolsable.</summary>
    public partial class MantenimientoEventosView : UserControl
    {
        public MantenimientoEventosView(MantenimientoEventosViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
