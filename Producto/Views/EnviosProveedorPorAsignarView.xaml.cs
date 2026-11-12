using Nesto.Modules.Producto.ViewModels;
using System.Windows.Controls;

namespace Nesto.Modules.Producto.Views
{
    /// <summary>
    /// Nesto#636: la ventana «Envíos de proveedor por asignar». La lógica está en <see cref="EnviosProveedorPorAsignarViewModel"/>
    /// (lee al llegar, IReceptorNavegacion).
    /// </summary>
    public partial class EnviosProveedorPorAsignarView : UserControl
    {
        public EnviosProveedorPorAsignarView(EnviosProveedorPorAsignarViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
