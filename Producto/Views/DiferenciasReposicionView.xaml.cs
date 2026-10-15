using Nesto.Modules.Producto.ViewModels;
using System.Windows.Controls;

namespace Nesto.Modules.Producto.Views
{
    /// <summary>
    /// Diferencias de reposición (Nesto#526, NestoAPI#634). La lógica está en <see cref="DiferenciasReposicionViewModel"/>,
    /// que se lee a sí mismo al llegar (IReceptorNavegacion).
    /// </summary>
    public partial class DiferenciasReposicionView : UserControl
    {
        public DiferenciasReposicionView(DiferenciasReposicionViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
