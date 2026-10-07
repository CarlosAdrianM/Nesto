using System.Windows.Controls;

namespace Nesto.Modulos.Cliente
{
    /// <summary>NestoAPI#591: lista de señales de eventos para Administración.</summary>
    public partial class SenalesEventosView : UserControl
    {
        public SenalesEventosView(SenalesEventosViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
