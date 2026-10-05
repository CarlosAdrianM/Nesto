using Nesto.Modules.Producto.ViewModels;
using System.Windows.Controls;

namespace Nesto.Modules.Producto.Views
{
    /// <summary>Recibir reposición (NestoAPI#553). Sin lógica: todo en <see cref="RecibirReposicionViewModel"/>.</summary>
    public partial class RecibirReposicionView : UserControl
    {
        public RecibirReposicionView(RecibirReposicionViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            Loaded += async (_, _) =>
            {
                if (DataContext is RecibirReposicionViewModel vm && vm.Pendientes.Count == 0)
                {
                    await vm.CargarAsync();
                }
            };
        }
    }
}
