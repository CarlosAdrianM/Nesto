using Nesto.Modules.Producto.ViewModels;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace Nesto.Modules.Producto.Views
{
    /// <summary>
    /// Enviar reposición a Algete (NestoAPI#553). La lógica está en <see cref="EnviarReposicionViewModel"/>; aquí solo
    /// se pone el foco en la cantidad de la línea que ha leído el lector y, al terminar de escribirla con Intro (o si lo
    /// leído no entra), se vuelve al cuadro del lector para la siguiente lectura.
    /// </summary>
    public partial class EnviarReposicionView : UserControl
    {
        public EnviarReposicionView(EnviarReposicionViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            viewModel.PedirFocoEnCantidad += EnfocarCantidad;
            viewModel.PedirFocoEnLector += EnfocarLector;
            Loaded += async (_, _) =>
            {
                if (DataContext is EnviarReposicionViewModel vm && vm.Lineas.Count == 0)
                {
                    await vm.CargarAsync();
                }
            };
        }

        private void EnfocarLector()
        {
            _ = Dispatcher.InvokeAsync(() =>
            {
                if (txtLectura?.IsVisible == true)
                {
                    _ = txtLectura.Focus();
                    txtLectura.SelectAll();
                }
            }, DispatcherPriority.Background);
        }

        /// <summary>Intro en la cantidad: se guarda (como siempre) y el cursor vuelve al lector para la siguiente lectura.</summary>
        private void GrdLineas_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e?.Key != Key.Enter || grdLineas == null || grdLineas.CurrentColumn != colCantidad)
            {
                return;
            }
            _ = grdLineas.CommitEdit(DataGridEditingUnit.Row, true);
            e.Handled = true;
            EnfocarLector();
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
