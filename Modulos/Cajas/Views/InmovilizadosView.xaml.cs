using Nesto.Modulos.Cajas.Models;
using Nesto.Modulos.Cajas.ViewModels;
using System.Linq;
using System.Windows.Controls;

namespace Nesto.Modulos.Cajas.Views
{
    /// <summary>Inmovilizados (Carlos 25/11/26): buscar e imprimir la etiqueta de la máquina.</summary>
    public partial class InmovilizadosView : UserControl
    {
        public InmovilizadosView(InmovilizadosViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        // SelectedItems de la rejilla no se puede enlazar: la vista le pasa al ViewModel lo elegido.
        private void grdInmovilizados_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is InmovilizadosViewModel vm && sender is DataGrid rejilla)
            {
                vm.CambiarSeleccion(rejilla.SelectedItems?.OfType<InmovilizadoModel>().ToList());
            }
        }
    }
}
