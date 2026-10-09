using Nesto.Modules.Producto.ViewModels;
using System.Windows.Controls;

namespace Nesto.Modules.Producto.Views
{
    /// <summary>
    /// Calendario de reposiciones (NestoAPI#577). La lógica está en <see cref="CalendarioReposicionesViewModel"/>; aquí solo
    /// se carga al abrirla.
    /// </summary>
    public partial class CalendarioReposicionesView : UserControl
    {
        public CalendarioReposicionesView(CalendarioReposicionesViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            Loaded += async (_, _) =>
            {
                // Al quitar la vista de la región el DataContext pasa a null: guarda para no reventar en el teardown
                if (DataContext is CalendarioReposicionesViewModel vm && vm.Filas.Count == 0 && !vm.EstaOcupado)
                {
                    await vm.CargarAsync();
                }
            };
        }
    }
}
