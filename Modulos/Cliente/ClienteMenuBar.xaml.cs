using System.Windows.Controls;

namespace Nesto.Modulos.Cliente
{
    /// <summary>
    /// Lógica de interacción para ClienteMenuBar.xaml
    /// </summary>
    public partial class ClienteMenuBar : UserControl
    {
        public ClienteMenuBar()
        {
            InitializeComponent();
        }

        public ClienteMenuBar(CrearClienteViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
