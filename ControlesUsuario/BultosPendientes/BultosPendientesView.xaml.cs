using System.Windows.Controls;

namespace ControlesUsuario.BultosPendientes
{
    /// <summary>NestoAPI#880 (corte 2): la ventana «Bultos pendientes del reparto». Lee al llegar (IReceptorNavegacion del ViewModel).</summary>
    public partial class BultosPendientesView : UserControl
    {
        public BultosPendientesView(BultosPendientesViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
