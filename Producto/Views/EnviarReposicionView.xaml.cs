using Nesto.Modules.Producto.ViewModels;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Nesto.Modules.Producto.Views
{
    /// <summary>
    /// Enviar reposición a Algete (NestoAPI#553). La lógica está en <see cref="EnviarReposicionViewModel"/>; aquí solo
    /// se pone el foco en la cantidad de la línea que ha leído el lector.
    /// </summary>
    public partial class EnviarReposicionView : UserControl
    {
        public EnviarReposicionView(EnviarReposicionViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            viewModel.PedirFocoEnCantidad += EnfocarCantidad;
            Loaded += async (_, _) =>
            {
                if (DataContext is EnviarReposicionViewModel vm && vm.Lineas.Count == 0)
                {
                    await vm.CargarAsync();
                }
            };
        }

        private void EnfocarCantidad(LineaEnviarReposicion linea)
        {
            if (linea == null)
            {
                return;
            }
            grdLineas.ScrollIntoView(linea, colCantidad);
            _ = Dispatcher.InvokeAsync(() =>
            {
                grdLineas.Focus();
                grdLineas.CurrentCell = new DataGridCellInfo(linea, colCantidad);
                grdLineas.BeginEdit();
            }, DispatcherPriority.Background);
        }
    }
}
